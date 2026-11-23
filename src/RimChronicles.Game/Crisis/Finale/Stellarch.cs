using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Empire;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Finale;

/// <summary>
/// The High Stellarch in game (crisis C13): one pawn, made once and kept between its appearances, leading the final
/// assault and the Citadel's honor guard. It skips away once when badly hurt (Core's <see cref="StellarchRules"/>), and
/// its death or capture is announced and carries the colony's assault on the capital. Main thread only.
/// </summary>
public static class Stellarch
{
    private const float FarskipPsyfocus = 0.25f;
    private const int KeepEscapeSearch = 40;

    private static CrisisState? State => RimChroniclesGameComponent.Current?.Crisis;

    public static bool Is(Pawn? pawn) => pawn is not null && State is { StellarchPawnId.Length: > 0 } s && pawn.ThingID == s.StellarchPawnId;

    /// <summary>The Stellarch, ready to spawn: the one met before if it's off the maps, a new one the first time; null
    /// once it has fallen, or while it's already on a map.</summary>
    public static Pawn? Ready(Faction empire)
    {
        if (State is not { } state || RimChroniclesGameComponent.Current is not { } component || !StellarchRules.CanLead(state))
        {
            return null;
        }

        if (state.StellarchPawnId.Length > 0 && component.TrackedOrFind(state.StellarchPawnId) is { } known)
        {
            return known.Dead || known.Spawned || known.IsPrisoner ? null : known;
        }

        if (UnitKit.Kinds.FirstOrDefault(k => UnitKit.Unit(k)?.role == UnitRole.Stellarch) is not { } kind)
        {
            return null;
        }

        var pawn = UnitKit.Generate(kind, empire, CrisisStage.Finale);
        state.StellarchPawnId = pawn.ThingID;
        state.StellarchFate = StellarchFate.Alive;
        component.Track(pawn); // kept between appearances
        return pawn;
    }

    /// <summary>After damage: once, when badly hurt or downed, the Stellarch skips away. In the Citadel it skips deep
    /// into the keep, as far from the colonists as it can; anywhere else it leaves the map, to be met again.</summary>
    public static void Hurt(Pawn pawn)
    {
        if (!Is(pawn) || State is not { } state || !pawn.Spawned
            || !StellarchRules.ShouldEscape(state, pawn.health.summaryHealth.SummaryHealthPercent, pawn.Downed, pawn.Dead))
        {
            return;
        }

        StellarchRules.Escaped(state);
        var map = pawn.Map;
        var from = pawn.Position;
        map.effecterMaintainer.AddEffecterToMaintain(EffecterDefOf.Skip_Entry.Spawn(from, map), from, 60);
        pawn.psychicEntropy?.OffsetPsyfocusDirectly(-FarskipPsyfocus);
        var keep = Sites.MapComponent_RcCitadel.For(map) is { Active: true } citadel && citadel.Rings.Count > 2 ? citadel.Rings[2] : (CellRect?)null;
        if (keep is { } rect && Refuge(map, rect) is { } refuge)
        {
            pawn.Position = refuge; // as vanilla's Skip moves a pawn: it keeps its lord and job state
            pawn.Notify_Teleported();
            map.effecterMaintainer.AddEffecterToMaintain(EffecterDefOf.Skip_Exit.Spawn(refuge, map), refuge, 60);
            Find.LetterStack.ReceiveLetter("The High Stellarch skips away",
                $"{pawn.LabelShort}, badly hurt, has farskipped deep into the keep. It won't be able to do that again.",
                LetterDefOf.ThreatBig, pawn, pawn.Faction);
            return;
        }

        pawn.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.Invalid);
        Find.LetterStack.ReceiveLetter("The High Stellarch skips away",
            $"{pawn.LabelShort}, badly hurt, has farskipped off the field. It will be waiting in the Citadel, and it can't escape that way again.",
            LetterDefOf.ThreatBig, new LookTargets(new TargetInfo(from, map)), EmpireDriver.EmpireFaction);
    }

    /// <summary>The standable cell in the keep farthest from any colonist.</summary>
    private static IntVec3? Refuge(Map map, CellRect keep)
    {
        var colonists = map.mapPawns.FreeColonistsSpawned.Select(p => p.Position).ToList();
        return keep.Cells.Where(c => c.Standable(map) && c.GetRoom(map) is { PsychologicallyOutdoors: false })
            .OrderByDescending(c => colonists.Count == 0 ? 0 : colonists.Min(p => p.DistanceToSquared(c)))
            .Take(KeepEscapeSearch).Cast<IntVec3?>().FirstOrDefault()
            ?? keep.Cells.Where(c => c.Standable(map)).Cast<IntVec3?>().FirstOrDefault();
    }

    /// <summary>The Stellarch died or was taken: once, a letter and the news, and if the colony is storming the
    /// capital, that assault has won (the Resistance ending).</summary>
    public static void Fell(Pawn pawn, string fate)
    {
        if (!Is(pawn) || State is not { } state || RimChroniclesGameComponent.Current is not { } component
            || !StellarchRules.Fell(state, fate, GenDate.DaysPassed))
        {
            return;
        }

        var empire = EmpireDriver.EmpireFaction;
        var how = fate == StellarchFate.Captured ? "taken prisoner" : "killed";
        var carried = CarryTheAssault(component);
        Find.LetterStack.ReceiveLetter($"The High Stellarch is {(fate == StellarchFate.Captured ? "captured" : "dead")}",
            $"{pawn.LabelShort}, the Empire's High Stellarch, has been {how}. The Empire has lost its last commander."
            + (carried ? "\n\nWith the Stellarch fallen, the assault on the capital has won." : ""),
            LetterDefOf.PositiveEvent, pawn, empire);
        component.News.Add(new NewsEvent(GenDate.DaysPassed, EmpireSaga.NewsKind,
            $"The Imperial High Stellarch was {how} by {Faction.OfPlayer.Name}", empire?.Name ?? ""));
    }

    /// <summary>Ends the Empire plot's pending capital assault in success, if there is one.</summary>
    private static bool CarryTheAssault(RimChroniclesGameComponent component)
    {
        var plot = component.Plots.Active.FirstOrDefault(p => p.Fact(EmpireArc.Job) == EmpireStep.StormCapital);
        if (plot is null)
        {
            return false;
        }

        var link = component.Plots.Quests.LastOrDefault(q => q.PlotId == plot.Id && q.Pending);
        if (link is null || Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == link.QuestId) is not { State: QuestState.Ongoing } quest)
        {
            return false;
        }

        quest.End(QuestEndOutcome.Success);
        return true;
    }

    // ---- the final assault

    /// <summary>
    /// The final assault (crisis spec §5.2, C13): every arm of the Empire walks in from one edge, led by the High
    /// Stellarch while it hasn't fallen. It fights to the end; it only pulls out if the crisis itself ends.
    /// </summary>
    public static bool FinalAssault(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (EmpireDriver.EmpireFaction is not { } empire
            || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var profile = OperationProfiles.FinalAssault;
        var pawns = OperationSpawner.Force(profile.Operation, CrisisStage.Finale, budget * profile.ForceShare, empire);
        if (pawns.Count == 0)
        {
            return false;
        }

        var leader = Ready(empire);
        if (leader is not null)
        {
            pawns.Insert(0, leader);
        }

        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 10), map);
        }

        LordMaker.MakeNewLord(empire, new LordJob_RcOperation(profile.Operation, CrisisStage.Finale, 0, entry, null, dev), map, pawns);
        Find.LetterStack.ReceiveLetter("The Imperial final assault",
            $"{empire.Name} is throwing everything it has left at {Faction.OfPlayer.Name}: {pawns.Count} troops"
            + (leader is null ? ", without a Stellarch to lead them." : $", led by the High Stellarch {leader.LabelShort}.")
            + " They won't retreat this time.",
            LetterDefOf.ThreatBig, new LookTargets(pawns), empire);
        return true;
    }

    // ---- Dev Mode

    [DebugAction("Rim Chronicles", "Crisis: Stellarch state", allowedGameStates = AllowedGameStates.Playing)]
    public static void Show()
    {
        if (State is not { } state || RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var pawn = state.StellarchPawnId.Length > 0 ? component.TrackedOrFind(state.StellarchPawnId) : null;
        Find.WindowStack.Add(new Dialog_MessageBox(
            $"High Stellarch: {(pawn is null ? "not met yet" : pawn.LabelShort)}; fate {(state.StellarchFate.Length == 0 ? "none" : state.StellarchFate)}"
            + $"{(state.StellarchFellDay >= 0 ? $" (day {state.StellarchFellDay})" : "")}; escape used: {(state.StellarchEscaped ? "yes" : "no")}; "
            + $"{(pawn is null ? "" : pawn.Spawned ? $"on {pawn.Map}" : pawn.Dead ? "dead" : "off the maps")}."));
    }

    /// <summary>Spawns the Stellarch (the kept one, or a new one) at the click.</summary>
    [DebugAction("Rim Chronicles", "Crisis: spawn High Stellarch", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Spawn()
    {
        if (EmpireDriver.EmpireFaction is not { } empire || Find.CurrentMap is not { } map || !UI.MouseCell().InBounds(map))
        {
            return;
        }

        if (Ready(empire) is not { } pawn)
        {
            Messages.Message("Rim Chronicles: the Stellarch has fallen, or is already on a map.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        GenSpawn.Spawn(pawn, UI.MouseCell(), map);
    }
}

/// <summary>Watches for the High Stellarch on this map every 30 ticks and lets it skip away once when badly hurt or
/// downed (checked here rather than inside the damage call, so the pawn never vanishes mid-hit).</summary>
public sealed class MapComponent_RcStellarch(Map map) : MapComponent(map)
{
    private const int CheckTicks = 30;

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % CheckTicks != 0 || RimChroniclesGameComponent.Current is not { } component
            || component.Crisis.StellarchPawnId.Length == 0 || component.Crisis.StellarchEscaped)
        {
            return;
        }

        if (map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.ThingID == component.Crisis.StellarchPawnId) is { } stellarch && !stellarch.IsPrisoner)
        {
            Stellarch.Hurt(stellarch);
        }
    }
}
