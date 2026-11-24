using LudeonTK;
using RimChronicles.Core.Empire;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Game.Plots;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
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
        if (saga.IsAwake)
        {
            EmpireBeats.EnsurePlot(component, today); // a save from before M4 woke the Empire without its plot
        }

        EmpirePromotions.Daily(component);
        if (saga.IsAwake)
        {
            saga.HighTitleHolder = EmpireBeats.HighTitleHolder(empire);
            ShipRaids(component, empire, today);
        }

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
            case EmpireState.Awakened or EmpireState.Ascendant when saga.ExpansionDue(today):
                ExpandOnce(component, empire, today);
                break;
            case EmpireState.Stirring:
                if (saga.Rumour(today, empire.Name, Neighbours(empire), n => Rand.Range(0, n), Rand.RangeInclusive(EmpireSaga.MinRumourGapDays, EmpireSaga.MaxRumourGapDays)) is { } rumour)
                {
                    Show(component, rumour, today, empire);
                }

                break;
        }
    }

    /// <summary>One expansion attack (M2): pick a target, apply the result to the world map, and report it. Pure C#, no
    /// model. With no target, it tries again after the usual gap.</summary>
    private static void ExpandOnce(RimChroniclesGameComponent component, Faction empire, int today)
    {
        var gap = StoryIntensity.Scale(Rand.RangeInclusive(EmpireExpansion.MinGapDays, EmpireExpansion.MaxGapDays), RimChroniclesMod.Settings.StoryIntensity);
        var settlements = Targets(empire);
        var target = EmpireExpansion.PickTarget(settlements.Keys.ToList(), Rand.Value);
        if (target is null)
        {
            component.Empire.NextExpansionDay = today + gap;
            component.Empire.Note(today, "expansion: no settlement within reach");
            return;
        }

        var result = EmpireExpansion.PickResult(target, Rand.Value);
        var settlement = settlements[target];
        switch (result)
        {
            case ExpansionResult.Conquered:
                settlement.SetFaction(empire);
                Repaint(settlement);
                EmpirePromotions.OnConquest(component, target.Faction, target.Name, empire, today);
                break;
            case ExpansionResult.Razed:
                Raze(settlement);
                break;
        }

        Show(component, component.Empire.Expanded(target, result, today, gap, empire.Name), today, empire);
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Empire expansion: {EmpireExpansion.News(result, empire.Name, target)}.");
        }
    }

    /// <summary>Settlements the Empire may attack: other humanlike factions' settlements on the same layer as an
    /// Imperial one, never the player's, never one with a map open or a quest on it, and never a faction's last.</summary>
    internal static Dictionary<ExpansionTarget, Settlement> Targets(Faction empire)
    {
        var all = Find.WorldObjects.Settlements;
        var imperial = all.Where(s => s.Faction == empire).ToList();
        var counts = all.Where(s => s.Faction is not null).GroupBy(s => s.Faction).ToDictionary(g => g.Key, g => g.Count());
        var questTargets = Find.QuestManager.QuestsListForReading
            .Where(q => q.State is QuestState.Ongoing or QuestState.NotYetAccepted)
            .SelectMany(q => q.QuestLookTargets)
            .Where(t => t.HasWorldObject)
            .Select(t => t.WorldObject)
            .ToHashSet();
        var targets = new Dictionary<ExpansionTarget, Settlement>();
        foreach (var s in all)
        {
            if (s.Faction is not { } owner || owner == empire || owner.IsPlayer || owner.Hidden || owner.defeated
                || !owner.def.humanlikeFaction || s.HasMap || questTargets.Contains(s) || counts[owner] < 2)
            {
                continue;
            }

            var near = imperial.Where(e => e.Tile.Layer == s.Tile.Layer).ToList();
            if (near.Count == 0)
            {
                continue;
            }

            var distance = near.Min(e => Find.WorldGrid.ApproxDistanceInTiles(e.Tile, s.Tile));
            targets[new ExpansionTarget(s.ID.ToString(System.Globalization.CultureInfo.InvariantCulture), s.Label, owner.Name, distance, counts[owner])] = s;
        }

        return targets;
    }

    private static readonly System.Reflection.FieldInfo? CachedMat = HarmonyLib.AccessTools.Field(typeof(Settlement), "cachedMat");
    private static readonly System.Reflection.FieldInfo? ExpandingMat = HarmonyLib.AccessTools.Field(typeof(WorldObject), "expandingMaterial");

    /// <summary>A settlement caches its world-map materials in its first owner's colour, and <c>SetFaction</c> doesn't
    /// reset them. Clears both caches and redraws the layer, so a conquered settlement shows in Imperial colours.</summary>
    /// <summary>A settlement changes hands outside the expansion timer (a job's aftermath, Dev Mode): recorded as the
    /// colony's doing, repainted, and checked for promotions.</summary>
    public static void Conquer(RimChroniclesGameComponent component, Settlement settlement, Faction empire, int today, string why, bool news = true)
    {
        var owner = settlement.Faction.Name;
        settlement.SetFaction(empire);
        Repaint(settlement);
        component.Empire.RecordAssisted(settlement.Label, owner, ExpansionResult.Conquered, today, why);
        if (news)
        {
            component.News.Add(new NewsEvent(today, EmpireSaga.NewsKind, $"{empire.Name} took {settlement.Label} from {owner}", empire.Name));
        }

        EmpirePromotions.OnConquest(component, owner, settlement.Label, empire, today);
    }

    internal static void Repaint(Settlement settlement)
    {
        CachedMat?.SetValue(settlement, null);
        ExpandingMat?.SetValue(settlement, null);
        Find.World.renderer.SetDirty<WorldDrawLayer_WorldObjects>(settlement.Tile.Layer);
    }

    /// <summary>Destroys a settlement and leaves ruins, as vanilla does when the player defeats one
    /// (<c>SettlementDefeatUtility.CheckDefeated</c>), without its letter or raids.</summary>
    private static void Raze(Settlement settlement)
    {
        var ruins = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(settlement.Tile.LayerDef.DestroyedSettlementWorldObjectDef);
        ruins.Tile = settlement.Tile;
        ruins.SetFaction(settlement.Faction);
        Find.WorldObjects.Add(ruins);
        settlement.Destroy();
    }

    /// <summary>Dormant → Stirring, recorded in the director's log. Counts as a beat, so minor plots space around it.</summary>
    private static void Awaken(RimChroniclesGameComponent component, string trigger, string detail, int today, string source, string options)
    {
        var empire = EmpireFaction!;
        var ev = component.Empire.Awaken(trigger, detail, today,
            Rand.RangeInclusive(EmpireSaga.MinStirringDays, EmpireSaga.MaxStirringDays), Colony);
        Show(component, ev, today, empire);
        EmpireBeats.EnsurePlot(component, today);
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
            StoryLetters.Send(label, text, LetterDefOf.ThreatBig, faction: empire);
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

    /// <summary>Days between Imperial raids while a ship reactor starts up.</summary>
    public const int ShipRaidGapDays = 4;

    /// <summary>The ship-launch reaction (design spec, Stage 5): an awake Empire that the colony hasn't allied with
    /// won't let it leave. Starting a ship reactor brings war and an Imperial raid at once, then another every few days
    /// while the reactor starts up.</summary>
    public static void Notify_ShipStartup()
    {
        if (RimChroniclesGameComponent.Current is not { } component || !Available || EmpireFaction is not { } empire)
        {
            return;
        }

        var saga = component.Empire;
        var stance = EmpireBeats.Current(component) is { } plot ? EmpireArc.StanceOf(plot) : EmpireStance.Neutral;
        if (saga.State is not (EmpireState.Awakened or EmpireState.Ascendant) || saga.Ending.Length > 0 || stance == EmpireStance.Ally
            || saga.NextShipRaidDay >= 0)
        {
            return;
        }

        if (!empire.HostileTo(Faction.OfPlayer))
        {
            empire.TryAffectGoodwillWith(Faction.OfPlayer, -100 - empire.PlayerGoodwill, canSendMessage: true, canSendHostilityLetter: true);
        }

        StoryLetters.Send("The Empire will not let you leave",
            $"Word of the ship has reached {empire.Name}. A colony that could carry its secrets off the rim is not one the Empire can allow.\n\n"
            + "Imperial troops are on their way, and more will follow while the reactor starts up.", LetterDefOf.ThreatBig, faction: empire);
        var today = GenDate.DaysPassed;
        saga.Note(today, "the colony started a ship reactor: the Empire moves to stop it");
        saga.NextShipRaidDay = today;
        ShipRaids(component, empire, today);
    }

    private static void ShipRaids(RimChroniclesGameComponent component, Faction empire, int today)
    {
        var saga = component.Empire;
        if (saga.NextShipRaidDay < 0 || today < saga.NextShipRaidDay)
        {
            return;
        }

        var reactorMap = Find.Maps.FirstOrDefault(m => m.IsPlayerHome && m.listerThings.ThingsOfDef(ThingDefOf.Ship_Reactor)
            .Any(t => t.TryGetComp<CompHibernatable>() is { } h && h.State == HibernatableStateDefOf.Starting));
        if (reactorMap is null || saga.Ending.Length > 0 || saga.State == EmpireState.Broken)
        {
            saga.NextShipRaidDay = -1; // the reactor is running, gone, or the story is over
            return;
        }

        saga.NextShipRaidDay = today + ShipRaidGapDays;
        if (!ImperialRaid(reactorMap, empire, 1.5f))
        {
            Log.Warning("[Rim Chronicles] The Empire's raid against the ship could not start.");
        }
    }

    /// <summary>A raid by the Empire on a home map at <paramref name="factor"/> times the storyteller's big-threat
    /// points, lifted to the Empire's minimum group (see ENGINE_NOTES: a forced faction skips that check).</summary>
    public static bool ImperialRaid(Map map, Faction empire, float factor)
    {
        var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.faction = empire;
        parms.forced = true;
        parms.points = Math.Max(parms.points * factor, empire.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat) * 1.05f);
        return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
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
        if (saga.State == EmpireState.Awakened)
        {
            lines.Insert(3, $"Expansion: next attack day {saga.NextExpansionDay}; {saga.Conquests} taken or razed, {saga.Defeats} repelled");
        }

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

    [DebugAction("Rim Chronicles", "Empire: expansion tick", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevExpand()
    {
        if (RimChroniclesGameComponent.Current is not { } component || EmpireFaction is not { } empire || component.Empire.State is not (EmpireState.Awakened or EmpireState.Ascendant))
        {
            Messages.Message("Rim Chronicles: the Empire must be awake to expand (Empire: trigger awakening, then end stirring now).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        ExpandOnce(component, empire, GenDate.DaysPassed);
    }
}
