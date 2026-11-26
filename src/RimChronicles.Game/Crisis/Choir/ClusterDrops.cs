using LudeonTK;
using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Strikes;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>A cluster drop that has been called and is being telegraphed. Saved with the map.</summary>
public sealed class PendingClusterDrop : IExposable
{
    public IntVec3 cell;
    public Pawn? cantor;
    public int fireTick;
    public float points;

    /// <summary>Called from Dev Mode: no Cantor needed.</summary>
    public bool dev;

    public void ExposeData()
    {
        Scribe_Values.Look(ref cell, "cell");
        Scribe_References.Look(ref cantor, "cantor");
        Scribe_Values.Look(ref fireTick, "fireTick");
        Scribe_Values.Look(ref points, "points");
        Scribe_Values.Look(ref dev, "dev");
    }
}

/// <summary>
/// The Cantors' cluster drops on one map (War in Heaven W7, tech spec §4.3), the orbital strike's mirror: while a Choir
/// raid is on the map, a Cantor who is up and sees a good target (or a standing signal spire, W8) calls a few mechs down
/// near it. The landing spot is marked and a letter warns the player; the pods land 6–10 seconds later unless the
/// Cantor falls or loses sight first (fairness rule 1). Rules in Core's <see cref="ClusterDropRules"/>. Main thread only.
/// </summary>
public sealed class MapComponent_RcClusterDrops(Map map) : MapComponent(map)
{
    private const int CheckTicks = 60;
    private const int SourceCheckTicks = 30;

    /// <summary>ThingDefs that count as a standing signal spire (W8 adds the Choir's).</summary>
    public static readonly HashSet<ThingDef> Spires = [];

    private StrikeState _state = new();
    private List<PendingClusterDrop> _pending = [];

    public StrikeState State => _state;

    public static MapComponent_RcClusterDrops? For(Map map) => map.GetComponent<MapComponent_RcClusterDrops>();

    public override void MapComponentTick()
    {
        var tick = Find.TickManager.TicksGame;
        if (_pending.Count > 0 && tick % SourceCheckTicks == 0)
        {
            Telegraph(tick);
        }

        if (tick % CheckTicks != 0 || !ChoirCrisisDriver.Available || ChoirCrisisDriver.Faction is not { } choir)
        {
            return;
        }

        if (ChoirLord(map) is null)
        {
            _state.Reset();
            return;
        }

        var cantors = Cantors(choir);
        var spire = SpireStanding(choir);
        if (_state.Cap == 0 && (cantors.Count > 0 || spire))
        {
            _state.Cap = ClusterDropRules.CapFor(RaidBudget());
        }

        if (_pending.Count > 0 || ClusterDropRules.Blocked(_state, cantors.Count > 0, spire, DropsDown(), tick) is not null
            || MapComponent_RcStrikes.For(map) is not { } strikes)
        {
            return;
        }

        if (ClusterDropRules.Target(strikes.Candidates(choir, cantors, spire), _state) is not { } target)
        {
            return;
        }

        var aim = new IntVec3(target.Cell.X, 0, target.Cell.Z);
        if (ClusterDropRules.Landing(target.Cell, ChoirOperations.DropCells(map, aim, ClusterDropRules.MaxLanding)) is not { } landing)
        {
            return;
        }

        var cell = new IntVec3(landing.X, 0, landing.Z);
        Call(cell, cantors.Where(c => Sees(c, cell)).OrderBy(c => c.Position.DistanceTo(cell)).FirstOrDefault(), ClusterDropRules.Points(RaidBudget()), dev: false);
        _state.Struck(target.Cell, tick);
    }

    /// <summary>Calls a drop on <paramref name="cell"/>: marks it, warns the player, and lands it after the delay.</summary>
    public void Call(IntVec3 cell, Pawn? cantor, float points, bool dev)
    {
        var delay = StrikeRules.DelayTicks(RimChroniclesMod.Settings.StrikeDelaySeconds);
        _pending.Add(new PendingClusterDrop { cell = cell, cantor = cantor, fireTick = Find.TickManager.TicksGame + delay, points = points, dev = dev });
        FleckMaker.Static(cell, map, FleckDefOf.PsycastAreaEffect, 6f);
        SoundDefOf.OrbitalStrike_Ordered.PlayOneShotOnCamera(map);
        var who = cantor is null ? "A Choir signal" : $"The Choir cantor {cantor.LabelShort}";
        StoryLetters.ChoirCrisis("Cluster drop called",
            $"{who} has called a cluster of mechs down on the marked spot. The pods land in about {delay / StrikeRules.TicksPerSecond} seconds.\n\n"
            + (cantor is null ? "Get ready for them." : $"Take {cantor.LabelShort} down before they land, or break their line of sight: without the cantor, the drop is called off."),
            LetterDefOf.ThreatBig, new LookTargets(new TargetInfo(cell, map)), record: false);
    }

    public override void ExposeData()
    {
        ScribeArchive.Instance.Deep(ref _state, "clusterDropState");
        Scribe_Collections.Look(ref _pending, "pendingClusterDrops", LookMode.Deep);
        _state ??= new StrikeState();
        _pending ??= [];
        _pending.RemoveAll(p => p is null);
    }

    private void Telegraph(int tick)
    {
        foreach (var drop in _pending.ToList())
        {
            if (!drop.dev && !SourceHolds(drop))
            {
                _pending.Remove(drop);
                Messages.Message("Rim Chronicles: the cluster drop was called off: its cantor is down or lost sight.",
                    new TargetInfo(drop.cell, map), MessageTypeDefOf.PositiveEvent, historical: false);
                continue;
            }

            if (tick >= drop.fireTick)
            {
                _pending.Remove(drop);
                Land(drop);
                continue;
            }

            FleckMaker.Static(drop.cell, map, FleckDefOf.PsycastAreaEffect, 6f);
        }
    }

    private bool SourceHolds(PendingClusterDrop drop) =>
        (drop.cantor is { } c && Up(c) && c.Map == map && Sees(c, drop.cell))
        || (ChoirCrisisDriver.Faction is { } choir && SpireStanding(choir));

    /// <summary>The pods land: a few mechs of the stage join the Choir's operation on the map, under the cantor if it
    /// still stands, else the operation's other mechanitors; with no operation left, they assault on their own.</summary>
    private void Land(PendingClusterDrop drop)
    {
        if (!drop.cell.InBounds(map) || ChoirCrisisDriver.Faction is not { } choir || RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var stage = ChoirCrisisDriver.StageNow(component);
        var parts = ChoirForce.Build(ChoirMixes.ClusterDrop, stage == CrisisStage.None ? CrisisStage.Reclamation : stage, drop.points, choir);
        if (parts.Mechs.Count == 0)
        {
            return;
        }

        if (ChoirLord(map) is { LordJob: LordJob_RcOperation job } lord)
        {
            lord.AddPawns(parts.Mechs);
            var commanders = new ChoirForceParts();
            commanders.Mechanitors.AddRange(drop.cantor is { Dead: false, Downed: false } c && lord.ownedPawns.Contains(c)
                ? [c]
                : lord.ownedPawns.Where(p => !p.Downed && ChoirRoster.IsMechanitor(UnitKit.Unit(p.kindDef)?.role)));
            ChoirForce.Link(commanders, parts.Mechs);
            foreach (var (mech, commander) in commanders.Links)
            {
                job.Command(mech, commander);
            }
        }
        else
        {
            LordMaker.MakeNewLord(choir, new LordJob_AssaultColony(choir, canKidnap: false, canTimeoutOrFlee: true), map, parts.Mechs);
        }

        DropPodUtility.DropThingsNear(drop.cell, map, parts.Mechs, canRoofPunch: false, forbid: false, allowFogged: false, faction: choir);
    }

    /// <summary>The Choir's crisis operation on this map, if one is still standing.</summary>
    public static Lord? ChoirLord(Map map) =>
        map.lordManager.lords.FirstOrDefault(l => l.LordJob is LordJob_RcOperation { IsChoir: true } && l.ownedPawns.Any(p => !p.Downed));

    private List<Pawn> Cantors(Faction choir) =>
        map.mapPawns.SpawnedPawnsInFaction(choir).Where(p => UnitKit.Unit(p.kindDef)?.role == UnitRole.Cantor && Up(p)).ToList();

    private static bool Up(Pawn pawn) => pawn is { Dead: false, Spawned: true, Downed: false } && pawn.Awake() && !pawn.InMentalState;

    private bool Sees(Pawn cantor, IntVec3 cell) =>
        cantor.Position.DistanceTo(cell) <= ClusterDropRules.Sight && GenSight.LineOfSight(cantor.Position, cell, map);

    public bool SpireStanding(Faction choir) =>
        Spires.Count > 0 && map.listerBuildings.allBuildingsNonColonist.Any(b => b.Faction == choir && Spires.Contains(b.def) && !b.Destroyed);

    private static bool DropsDown() =>
        RimChroniclesGameComponent.Current is { } c && c.ChoirCrisis.IsDown(CrisisCapability.Strikes, GenDate.DaysPassed);

    private static float RaidBudget()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return ThreatBudget.Base(CrisisStage.Probing);
        }

        var stage = ChoirCrisisDriver.StageNow(component);
        return ChoirCrisisDriver.Budget(component, stage == CrisisStage.None ? CrisisStage.Probing : stage, ColonyStrengthReader.Read());
    }

    /// <summary>Calls a cluster drop on a clicked cell, with no cantor needed.</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: call cluster drop", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DevCall()
    {
        if (Find.CurrentMap is not { } map || For(map) is not { } drops || !UI.MouseCell().InBounds(map))
        {
            return;
        }

        if (ChoirCrisisDriver.Faction is null)
        {
            Messages.Message("Rim Chronicles: no Hollow Choir yet (Hollow Choir: wake now).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        drops.Call(UI.MouseCell(), null, ClusterDropRules.Points(RaidBudget()), dev: true);
    }
}
