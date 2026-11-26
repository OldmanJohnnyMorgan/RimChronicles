using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Strikes;
using Power = RimChronicles.Core.Powers.Power;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Pieces;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>The cluster siege's own buildings, looked up once the defs are loaded (they need Biotech).</summary>
[StaticConstructorOnStartup]
public static class ChoirDefs
{
    public static readonly ThingDef? Spire = DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_SignalSpire");
    public static readonly ThingDef? Beacon = DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_ChoirBeacon");

    static ChoirDefs()
    {
        if (Spire is not null)
        {
            MapComponent_RcClusterDrops.Spires.Add(Spire);
        }
    }
}

/// <summary>
/// One cluster siege's camp (War in Heaven W8, tech spec §4.3 row 4): where it is, which way it faces, its goal, the
/// stage it has grown to, its mechanitors, and the beacon's waves. Saved with the operation's lord. Its shape is the
/// Imperial camp's (<see cref="CampLayout"/>, with the Choir's pieces); unlike it, each stage drops in by pod on a timer
/// while a mechanitor holds the camp (<see cref="ClusterSiegeRules"/>). Main thread only.
/// </summary>
public sealed class ClusterCamp : IExposable
{
    public IntVec3 spot;
    public Side facing;
    public SiegeGoal goal;

    /// <summary>The stage it has grown to (0 before it lands).</summary>
    public int stage;

    public int lastStageTick = -1;
    public List<Pawn> mechanitors = [];
    public int startMechanitors;
    public int wavesSent;
    public int lastWaveTick = -1;

    public ClusterCamp()
    {
    }

    public ClusterCamp(IntVec3 spot, Side facing, SiegeGoal goal)
    {
        this.spot = spot;
        this.facing = facing;
        this.goal = goal;
    }

    /// <summary>The cluster camp on a map, if one is running.</summary>
    public static ClusterCamp? On(Map map) =>
        map.lordManager.lords.Select(l => l.LordJob).OfType<LordJob_RcOperation>().FirstOrDefault(j => j.cluster is not null)?.cluster;

    public bool Started => stage >= BuildStages.First;

    /// <summary>The camp's pieces in map cells, with the Choir's pieces.</summary>
    public IReadOnlyList<(CampPlacement Placement, PrefabDef Prefab, IntVec3 Min)> Plan()
    {
        var prefabs = PieceLibrary.For(Power.Choir.Id).ToDictionary(p => p.Source, p => p.Prefab);
        return CampLayout.Plan(prefabs.Keys, facing)
            .Select(p => (p, prefabs[p.Piece], new IntVec3(spot.x + p.Min.X, 0, spot.z + p.Min.Z)))
            .ToList();
    }

    /// <summary>The siege phase begins: the first stage lands, and the mechanitors who hold the camp are counted.</summary>
    public void Land(Lord lord)
    {
        if (Started)
        {
            return;
        }

        mechanitors = lord.ownedPawns.Where(p => ChoirRoster.IsMechanitor(UnitKit.Unit(p.kindDef)?.role)).ToList();
        startMechanitors = mechanitors.Count;
        Grow(lord, BuildStages.First);
    }

    /// <summary>Drops a stage's pieces in by pod (each nudged up to two cells if its place is taken) and says so.</summary>
    public void Grow(Lord lord, int number)
    {
        stage = number;
        lastStageTick = Find.TickManager.TicksGame;
        var map = lord.Map;
        var landed = 0;
        foreach (var (placement, prefab, min) in Plan().Where(p => p.Placement.Stage == number))
        {
            var rot = new Rot4((int)placement.Rotation);
            for (var i = 0; i < GenRadial.NumCellsInRadius(2.9f); i++)
            {
                if (PieceStamper.Pods(prefab, map, min + GenRadial.RadialPattern[i], rot, lord.faction, StuffPalette.Choir))
                {
                    landed++;
                    break;
                }
            }
        }

        if (landed > 0)
        {
            Messages.Message(ClusterSiegeRules.Landed(number), new TargetInfo(spot, map), number == BuildStages.First ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.ThreatBig);
        }

        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Cluster siege: stage {number} ({BuildStages.Get(number).Name}) dropped in, {landed} pieces.");
        }
    }

    /// <summary>True while a mechanitor of the siege stands within reach of the camp.</summary>
    public bool Held(Lord lord) => lord.ownedPawns.Any(p => p is { Dead: false, Downed: false, Spawned: true }
        && ChoirRoster.IsMechanitor(UnitKit.Unit(p.kindDef)?.role) && p.Position.InHorDistOf(spot, ClusterSiegeRules.CampReach));

    /// <summary>Every 250 ticks while the siege lasts (not while it retreats): the camp grows in its siege phase, and the
    /// beacon brings in waves from the reserve, under the siege's mechanitors.</summary>
    public void JobTick(Lord lord, LordJob_RcOperation job, bool growing)
    {
        if (!Started)
        {
            return;
        }

        var now = Find.TickManager.TicksGame;
        if (growing)
        {
            var next = ClusterSiegeRules.Next(stage, now - lastStageTick, Held(lord));
            if (next != stage)
            {
                Grow(lord, next);
            }
        }

        var beacon = Standing(lord, ChoirDefs.Beacon).FirstOrDefault();
        if (beacon is not null && lastWaveTick < 0)
        {
            lastWaveTick = now;
        }

        if (beacon is null || !SiegeRules.WaveDue(true, wavesSent, job.reserve, now - lastWaveTick))
        {
            return;
        }

        var points = SiegeRules.WavePoints(job.reserve, wavesSent);
        wavesSent++;
        lastWaveTick = now;
        job.reserve = Math.Max(0, job.reserve - points);
        var parts = ChoirForce.Build(ChoirMixes.ClusterSiege, job.stage, points, lord.faction);
        if (parts.Count == 0)
        {
            return;
        }

        var commanders = new ChoirForceParts();
        commanders.Mechanitors.AddRange(lord.ownedPawns.Where(p => !p.Downed && ChoirRoster.IsMechanitor(UnitKit.Unit(p.kindDef)?.role)).Concat(parts.Mechanitors));
        ChoirForce.Link(commanders, parts.Mechs);
        lord.AddPawns(parts.All);
        foreach (var (mech, commander) in commanders.Links)
        {
            job.Command(mech, commander);
        }

        DropPodUtility.DropThingsNear(beacon.Position, lord.Map, parts.All, canRoofPunch: false, forbid: false, allowFogged: false, faction: lord.faction);
        Messages.Message($"More of the Choir are dropping in at the cluster's beacon: {parts.Count}.", new TargetInfo(beacon.Position, lord.Map), MessageTypeDefOf.ThreatBig);
    }

    /// <summary>The camp's standing buildings of a def.</summary>
    public IEnumerable<Building> Standing(Lord lord, ThingDef? def) =>
        def is null
            ? []
            : lord.Map.listerBuildings.allBuildingsNonColonist.Where(b => b.Faction == lord.faction && !b.Destroyed && b.def == def
                && b.Position.InHorDistOf(spot, CampLayout.Radius + 6));

    /// <summary>What the siege does now about its mechanitors: hold, attack or leave, as the Imperial siege does about its
    /// builders (<see cref="SiegeRules.AfterBuildersFall"/>).</summary>
    public AfterBuilders AfterMechanitors(Lord lord)
    {
        var up = mechanitors.Count(p => p is { Dead: false, Downed: false, Spawned: true } && p.GetLord() == lord);
        var lost = lord.numPawnsEverGained == 0 ? 0f : lord.numPawnsLostViolently / (float)lord.numPawnsEverGained;
        return SiegeRules.AfterBuildersFall(startMechanitors, up, lost);
    }

    /// <summary>The siege is over: what it dropped in is abandoned (no faction), as the Imperial camp is, so an abandoned
    /// spire calls no drops and an abandoned resonator falls silent.</summary>
    public void Teardown(Lord lord)
    {
        if (lord.Map is not { } map)
        {
            return;
        }

        foreach (var building in map.listerBuildings.allBuildingsNonColonist
                     .Where(b => b.Faction == lord.faction && b.Position.InHorDistOf(spot, CampLayout.Radius + 6)).ToList())
        {
            building.SetFaction(null);
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref spot, "spot");
        Scribe_Values.Look(ref facing, "facing");
        Scribe_Values.Look(ref goal, "goal");
        Scribe_Values.Look(ref stage, "stage");
        Scribe_Values.Look(ref lastStageTick, "lastStageTick", -1);
        Scribe_Collections.Look(ref mechanitors, "mechanitors", LookMode.Reference);
        Scribe_Values.Look(ref startMechanitors, "startMechanitors");
        Scribe_Values.Look(ref wavesSent, "wavesSent");
        Scribe_Values.Look(ref lastWaveTick, "lastWaveTick", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            mechanitors ??= [];
            mechanitors.RemoveAll(p => p is null);
        }
    }
}

/// <summary>
/// The cluster siege's dig-in phase (W8): the first stage lands on arrival; mechanitors hold and command near the camp,
/// mechs defend it. The camp grows from <see cref="LordJob_RcOperation.LordJobTick"/>.
/// </summary>
public sealed class LordToil_RcClusterSiege : LordToil
{
    private const float MechRadius = 16f;

    private ClusterCamp? Camp => (lord.LordJob as LordJob_RcOperation)?.cluster;

    public override IntVec3 FlagLoc => Camp?.spot ?? IntVec3.Invalid;

    public override bool ForceHighStoryDanger => true;

    public override void Init()
    {
        base.Init();
        Camp?.Land(lord);
    }

    public override void UpdateAllDuties()
    {
        if (Camp is not { } camp)
        {
            return;
        }

        foreach (var pawn in lord.ownedPawns.Where(p => p.mindState is not null))
        {
            pawn.mindState.duty = ChoirRoster.IsMechanitor(UnitKit.Unit(pawn.kindDef)?.role)
                ? new PawnDuty(CrisisDefs.RimChronicles_Command, camp.spot, ChoirCommand.HoldRadius * 3)
                : new PawnDuty(DutyDefOf.Defend, camp.spot, MechRadius);
        }
    }

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % 250 == 0)
        {
            UpdateAllDuties(); // reinforcements and drop-ins get their duties
        }
    }
}
