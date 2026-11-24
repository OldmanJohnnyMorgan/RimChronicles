using LudeonTK;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Plots;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Powers;

/// <summary>
/// The Hollow Choir's saga, day by day (War in Heaven W4a; spec §3.1, §4): dormant until a trigger (colony wealth, a
/// mechanitor colonist, day 300 at the latest), then stirring (rumours and the hum), then awake: its faction appears
/// near the colony (<see cref="ChoirFaction.Create"/>), takes the colony's stored allegiance, and starts taking
/// settlements by the same rules as the Empire. Its plot is <see cref="ChoirBeats"/>; cross-waking with the Empire, the
/// ship launch and the Royal Ascent come with W5. Main thread only.
/// </summary>
public static class ChoirDriver
{
    private static Power Choir => Power.Choir;

    public static bool Available => PowerFactions.Runs(Choir);

    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>The Choir's name before its faction exists (for rumours and the waking letter).</summary>
    private static string Name => ChoirFaction.Existing?.Name ?? ChoirFaction.Def?.fixedName ?? "The Hollow Choir";

    public static void Daily(RimChroniclesGameComponent component)
    {
        if (!Available)
        {
            return;
        }

        var saga = component.Choir;
        var today = GenDate.DaysPassed;
        if (saga.IsAwake)
        {
            ChoirBeats.EnsurePlot(component, today);
            saga.HighTitleHolder = ChoirBeats.HighRankHolder(component);
        }

        switch (saga.State)
        {
            case PowerState.Dormant:
                var signs = ReadSigns();
                var options = saga.Triggers(signs, today);
                if (options.Count > 0 && BeatBlocked(component, today) is null)
                {
                    var pick = Director.PickWeighted(options, Rand.Value);
                    Awaken(component, pick.Step, saga.DetailFor(pick.Step, signs), today, options.Count == 1 ? "Only" : "Random",
                        string.Join(", ", options.Select(o => o.Id)));
                }

                break;
            case PowerState.Stirring when saga.StirringOver(today):
                if (BeatBlocked(component, today) is null)
                {
                    Wake(component, today, "the stirring has run its course", "Only");
                }

                break;
            case PowerState.Stirring:
                if (saga.Rumour(today, Name, Neighbours(), n => Rand.Range(0, n), Rand.RangeInclusive(PowerSaga.MinRumourGapDays, PowerSaga.MaxRumourGapDays)) is { } rumour)
                {
                    Show(component, rumour, today);
                }

                break;
            case PowerState.Awakened or PowerState.Ascendant when saga.ExpansionDue(today):
                if (ChoirFaction.Existing is { } choir)
                {
                    ExpandOnce(component, choir, today);
                }

                break;
        }
    }

    /// <summary>Dormant → Stirring, recorded in the director's log. Counts as a beat, so minor plots space around it.</summary>
    private static void Awaken(RimChroniclesGameComponent component, string trigger, string detail, int today, string source, string options)
    {
        var ev = component.Choir.Awaken(trigger, detail, today, Rand.RangeInclusive(PowerSaga.MinStirringDays, PowerSaga.MaxStirringDays), Colony);
        Show(component, ev, today);
        ChoirBeats.EnsurePlot(component, today);
        Record(component, today, $"Awaken:{trigger}", Choir.Text.TriggerWhy(trigger, detail, Colony), source, options);
        component.Plots.LastBeatDay = today;
    }

    /// <summary>Stirring → Awakened: the faction appears with its settlements, takes the colony's stored allegiance
    /// (an Imperial ally is its enemy from the start), and its expansion begins.</summary>
    private static void Wake(RimChroniclesGameComponent component, int today, string reason, string source)
    {
        if (ChoirFaction.Create(Find.AnyPlayerHomeMap?.Tile ?? PlanetTile.Invalid) is not { } choir)
        {
            return;
        }

        var stance = component.Allegiance.StanceToward(Choir);
        if (stance != PowerStance.Neutral)
        {
            PowerFactions.ApplyStance(choir, stance);
        }

        Show(component, component.Choir.Wake(today, choir.Name), today, choir);
        Record(component, today, $"{Choir.OptionPrefix}:Awaken", reason, source);
        component.Plots.LastBeatDay = today;
    }

    /// <summary>One expansion attack, by the Empire's rules (<see cref="PowerExpansion"/>), against any other humanlike
    /// faction's settlement in reach. The War in Heaven (W10) weights the Empire's settlements up.</summary>
    private static void ExpandOnce(RimChroniclesGameComponent component, Faction choir, int today)
    {
        var saga = component.Choir;
        var gap = StoryIntensity.Scale(Rand.RangeInclusive(PowerExpansion.MinGapDays, PowerExpansion.MaxGapDays), RimChroniclesMod.Settings.StoryIntensity);
        var settlements = EmpireDriver.Targets(choir);
        var target = PowerExpansion.PickTarget(settlements.Keys.ToList(), Rand.Value);
        if (target is null)
        {
            saga.NextExpansionDay = today + gap;
            saga.Note(today, "expansion: no settlement within reach");
            return;
        }

        var result = PowerExpansion.PickResult(target, Rand.Value);
        var settlement = settlements[target];
        switch (result)
        {
            case ExpansionResult.Conquered:
                settlement.SetFaction(choir);
                EmpireDriver.Repaint(settlement);
                break;
            case ExpansionResult.Razed:
                EmpireDriver.Raze(settlement);
                break;
        }

        Show(component, saga.Expanded(target, result, today, gap, choir.Name), today, choir);
    }

    private static void Record(RimChroniclesGameComponent component, int today, string choice, string reason, string source, string? options = null) =>
        component.Plots.Record(new DirectorDecision
        {
            Day = today, PlotId = Choir.Id, Options = options ?? choice, Choice = choice, Reason = reason, Source = source,
        });

    /// <summary>Null if a major-plot beat may fire today, else why not: the same spacing and gate as the Empire's.</summary>
    private static string? BeatBlocked(RimChroniclesGameComponent component, int today) =>
        component.Plots.LastBeatDay >= 0 && today - component.Plots.LastBeatDay < PlotBook.BeatSpacingDays
            ? $"a beat fired on day {component.Plots.LastBeatDay}"
            : ColonyPacingGate.Instance.BlockReason();

    private static void Show(RimChroniclesGameComponent component, PowerEvent ev, int today, Faction? choir = null)
    {
        component.News.Add(new NewsEvent(today, Choir.NewsKind, ev.NewsText, choir?.Name ?? Name));
        if (ev.LetterLabel is { } label && ev.LetterText is { } text)
        {
            ChoirBeats.Current(component)?.Tell(today, label, text);
            StoryLetters.Send(label, text, LetterDefOf.ThreatBig, faction: choir);
        }
    }

    /// <summary>Today's colony wealth and any mechanitor among the colonists.</summary>
    private static PowerSigns ReadSigns()
    {
        var wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal);
        var mechanitor = PawnsFinder.AllMaps_FreeColonists
            .Where(MechanitorUtility.IsMechanitor)
            .OrderBy(p => p.ThingID, StringComparer.Ordinal)
            .FirstOrDefault();
        return new PowerSigns(wealth, mechanitor?.LabelShort);
    }

    /// <summary>Settled humanlike factions the rumours can name.</summary>
    private static List<string> Neighbours()
    {
        var settled = Find.WorldObjects.Settlements.Select(s => s.Faction).ToHashSet();
        return Find.FactionManager.AllFactionsVisible
            .Where(f => !f.IsPlayer && !f.defeated && f.def.humanlikeFaction && settled.Contains(f) && f != ChoirFaction.Existing)
            .Select(f => f.Name)
            .ToList();
    }

    [DebugAction("Rim Chronicles", "Hollow Choir: show", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevShow()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var saga = component.Choir;
        var lines = new List<string>
        {
            $"Running: {(Available ? "yes" : "no (needs the major plot, the Hollow Choir setting, Royalty and Biotech)")}",
            $"State: {saga.State} since day {saga.StateDay}",
            $"Faction: {ChoirFaction.Existing?.Name ?? "not yet"}",
            $"Woken by: {(saga.Trigger.Length > 0 ? Choir.Text.TriggerWhy(saga.Trigger, saga.TriggerDetail, Colony) : "nothing yet")}",
            $"Next expansion: {(saga.NextExpansionDay >= 0 ? $"day {saga.NextExpansionDay}" : "none")}",
            $"Stance: {component.Allegiance.StanceToward(Choir)}",
            "",
        };
        lines.AddRange(saga.Log.Count > 0 ? saga.Log : ["(nothing has happened yet)"]);
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    [DebugAction("Rim Chronicles", "Hollow Choir: trigger stirring", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevAwaken()
    {
        if (RimChroniclesGameComponent.Current is not { } component || !Available || component.Choir.State != PowerState.Dormant)
        {
            Messages.Message("Rim Chronicles: the Choir isn't dormant, or isn't running in this game.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Awaken(component, AwakeningTrigger.Dev, "", GenDate.DaysPassed, "Dev", AwakeningTrigger.Dev);
    }

    [DebugAction("Rim Chronicles", "Hollow Choir: wake now", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevWake()
    {
        if (RimChroniclesGameComponent.Current is not { } component || !Available || component.Choir.IsAwake && component.Choir.State != PowerState.Stirring)
        {
            Messages.Message("Rim Chronicles: the Choir is already awake, or isn't running in this game.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var today = GenDate.DaysPassed;
        if (component.Choir.State == PowerState.Dormant)
        {
            Awaken(component, AwakeningTrigger.Dev, "", today, "Dev", AwakeningTrigger.Dev);
        }

        Wake(component, today, "woken from Dev Mode", "Dev");
    }

    [DebugAction("Rim Chronicles", "Hollow Choir: expand now", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevExpand()
    {
        if (RimChroniclesGameComponent.Current is not { } component || ChoirFaction.Existing is not { } choir)
        {
            Messages.Message("Rim Chronicles: the Choir isn't awake yet (Hollow Choir: wake now).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        ExpandOnce(component, choir, GenDate.DaysPassed);
    }
}
