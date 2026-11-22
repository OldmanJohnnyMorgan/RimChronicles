using LudeonTK;
using RimChronicles.Core.Empire;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Game.Plots;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Empire;

/// <summary>The Awakened Empire in game (Stage 5 M1): reads the awakening signs, moves the Empire through its states,
/// and spreads its rumours as news. Needs Royalty and the Empire faction; without them, or with the major plot off in
/// settings, it does nothing. Main thread only.</summary>
public static class EmpireDriver
{
    public static Faction? EmpireFaction => ModsConfig.RoyaltyActive && Faction.OfEmpire is { defeated: false } f ? f : null;

    /// <summary>True when the major plot can run in this game.</summary>
    public static bool Available => RimChroniclesMod.Settings.MajorPlot && EmpireFaction is not null;

    private static string Colony => Faction.OfPlayer.Name;

    public static void Daily(RimChroniclesGameComponent component)
    {
        if (!Available || EmpireFaction is not { } empire)
        {
            return;
        }

        var saga = component.Empire;
        var today = GenDate.DaysPassed;
        switch (saga.State)
        {
            case EmpireState.Dormant:
                var signs = ReadSigns();
                var options = saga.Triggers(signs, today);
                if (options.Count > 0 && BeatBlocked(component, today) is null)
                {
                    var pick = Director.PickWeighted(options, Rand.Value);
                    Awaken(component, pick.Step, saga.DetailFor(pick.Step, signs), today, options.Count == 1 ? "Only" : "Random",
                        string.Join(", ", options.Select(o => o.Id)));
                }

                break;
            case EmpireState.Stirring when saga.StirringOver(today):
                if (BeatBlocked(component, today) is null)
                {
                    Show(component, saga.Wake(today, empire.Name), today, empire);
                    Record(component, today, "Empire:Awaken", "the stirring has run its course", "Only");
                    component.Plots.LastBeatDay = today;
                }

                break;
            case EmpireState.Stirring:
                if (saga.Rumour(today, empire.Name, Neighbours(empire), n => Rand.Range(0, n), Rand.RangeInclusive(EmpireSaga.MinRumourGapDays, EmpireSaga.MaxRumourGapDays)) is { } rumour)
                {
                    Show(component, rumour, today, empire);
                }

                break;
        }
    }

    /// <summary>Dormant → Stirring, recorded in the director's log. Counts as a beat, so minor plots space around it.</summary>
    private static void Awaken(RimChroniclesGameComponent component, string trigger, string detail, int today, string source, string options)
    {
        var empire = EmpireFaction!;
        var ev = component.Empire.Awaken(trigger, detail, today,
            Rand.RangeInclusive(EmpireSaga.MinStirringDays, EmpireSaga.MaxStirringDays), Colony);
        Show(component, ev, today, empire);
        Record(component, today, $"Awaken:{trigger}", EmpireText.TriggerWhy(trigger, detail, Colony), source, options);
        component.Plots.LastBeatDay = today;
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Empire stirring: {EmpireText.TriggerWhy(trigger, detail, Colony)}.");
        }
    }

    private static void Record(RimChroniclesGameComponent component, int today, string choice, string reason, string source, string? options = null) =>
        component.Plots.Record(new DirectorDecision
        {
            Day = today, PlotId = EmpireSaga.PlotId, Options = options ?? choice, Choice = choice, Reason = reason, Source = source,
        });

    /// <summary>Null if a major-plot beat may fire today, else why not: the same spacing and gate as minor beats.</summary>
    private static string? BeatBlocked(RimChroniclesGameComponent component, int today) =>
        component.Plots.LastBeatDay >= 0 && today - component.Plots.LastBeatDay < PlotBook.BeatSpacingDays
            ? $"a beat fired on day {component.Plots.LastBeatDay}"
            : ColonyPacingGate.Instance.BlockReason();

    private static void Show(RimChroniclesGameComponent component, EmpireEvent ev, int today, Faction empire)
    {
        component.News.Add(new NewsEvent(today, EmpireSaga.NewsKind, ev.NewsText, empire.Name));
        if (ev.LetterLabel is { } label && ev.LetterText is { } text)
        {
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig);
        }
    }

    /// <summary>Today's colony wealth and any colonist with an Imperial backstory.</summary>
    private static EmpireSigns ReadSigns()
    {
        var wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal);
        var imperial = PawnsFinder.AllMaps_FreeColonists
            .Where(p => IsImperial(p.story?.Childhood) || IsImperial(p.story?.Adulthood))
            .OrderBy(p => p.ThingID, StringComparer.Ordinal)
            .FirstOrDefault();
        return new EmpireSigns(wealth, imperial?.LabelShort);
    }

    private static bool IsImperial(BackstoryDef? backstory) =>
        backstory?.spawnCategories?.Any(c => c.Contains("Imperial", StringComparison.OrdinalIgnoreCase)) ?? false;

    /// <summary>Factions the Empire might send envoys to or make demands of.</summary>
    private static List<string> Neighbours(Faction empire)
    {
        var settled = Find.WorldObjects.Settlements.Select(s => s.Faction).ToHashSet();
        return Find.FactionManager.AllFactionsVisible
            .Where(f => f != empire && !f.IsPlayer && !f.defeated && f.def.humanlikeFaction && settled.Contains(f))
            .Select(f => f.Name)
            .ToList();
    }

    /// <summary>Called when the player defeats an Imperial settlement.</summary>
    public static void Notify_SiteDestroyed(string label)
    {
        if (RimChroniclesGameComponent.Current is { } component && !component.Empire.IsAwake && component.Empire.SiteDestroyed.Length == 0)
        {
            component.Empire.SiteDestroyed = label;
            component.Empire.Note(GenDate.DaysPassed, $"sign: {Colony} destroyed {label}");
        }
    }

    /// <summary>Called when a colonist renounces an Imperial title.</summary>
    public static void Notify_TitleRefused(string colonist)
    {
        if (RimChroniclesGameComponent.Current is { } component && !component.Empire.IsAwake && component.Empire.TitleRefusedBy.Length == 0)
        {
            component.Empire.TitleRefusedBy = colonist;
            component.Empire.Note(GenDate.DaysPassed, $"sign: {colonist} renounced an Imperial title");
        }
    }

    [DebugAction("Rim Chronicles", "Empire: show state", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevShow()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var saga = component.Empire;
        var signs = EmpireFaction is null ? EmpireSigns.None : ReadSigns();
        var lines = new List<string>
        {
            Available ? $"Empire: {EmpireFaction!.Name}" : "The major plot is off: it needs Royalty, the Empire faction, and the setting on.",
            $"State: {saga.State} since day {saga.StateDay}; next change day {saga.NextStateDay}; next rumour day {saga.NextRumourDay}",
            $"Woken by: {(saga.Trigger.Length > 0 ? EmpireText.TriggerWhy(saga.Trigger, saga.TriggerDetail, Colony) : "not yet")}",
            $"Signs today: wealth {(int)signs.ColonyWealth} (threshold {(int)EmpireSaga.WealthThreshold}); Imperial colonist {signs.ImperialColonist ?? "none"}; "
            + $"site destroyed {(saga.SiteDestroyed.Length > 0 ? saga.SiteDestroyed : "no")}; title renounced by {(saga.TitleRefusedBy.Length > 0 ? saga.TitleRefusedBy : "no one")}",
            $"Triggers that would fire today: {string.Join(", ", saga.Triggers(signs, GenDate.DaysPassed).Select(o => o.Id).DefaultIfEmpty("none"))}",
            "",
            "Log:",
        };
        lines.AddRange(saga.Log.TakeLast(20));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    [DebugAction("Rim Chronicles", "Empire: trigger awakening", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevAwaken()
    {
        if (RimChroniclesGameComponent.Current is not { } component || !Available)
        {
            Messages.Message("Rim Chronicles: the major plot needs Royalty, the Empire faction, and the setting on.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (component.Empire.State != EmpireState.Dormant)
        {
            Messages.Message($"Rim Chronicles: the Empire is already {component.Empire.State}.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Awaken(component, AwakeningTrigger.Dev, "", GenDate.DaysPassed, "Dev", AwakeningTrigger.Dev);
    }

    [DebugAction("Rim Chronicles", "Empire: end stirring now", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevWake()
    {
        if (RimChroniclesGameComponent.Current is not { } component || EmpireFaction is not { } empire || component.Empire.State != EmpireState.Stirring)
        {
            Messages.Message("Rim Chronicles: the Empire isn't stirring.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var today = GenDate.DaysPassed;
        Show(component, component.Empire.Wake(today, empire.Name), today, empire);
        Record(component, today, "Empire:Awaken", "forced from Dev Mode", "Dev");
    }
}
