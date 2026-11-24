using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Pieces;
using RimChronicles.Game.Crisis.Strikes;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Siege;

/// <summary>The camp's own buildings, looked up once the defs are loaded (they need Royalty, so they may be missing).</summary>
[StaticConstructorOnStartup]
public static class SiegeDefs
{
    public static readonly ThingDef? Uplink = DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_ImperialUplink");
    public static readonly ThingDef? Shield = DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_ImperialShield");
    public static readonly ThingDef? Relay = DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_PsychicRelay");
    public static readonly ThingDef? Beacon = DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_ImperialDropBeacon");
    public static readonly HediffDef? RelayLink = DefDatabase<HediffDef>.GetNamedSilentFail("RimChronicles_RelayLink");

    static SiegeDefs()
    {
        if (Uplink is not null)
        {
            MapComponent_RcStrikes.Uplinks.Add(Uplink);
        }
    }
}

/// <summary>
/// One siege's camp (crisis C8, spec §5.5): where it is and which way it faces, its goal, the stage it's on and that
/// stage's blueprints, who its builders are, and the beacon's waves. Saved with the operation's lord. The camp's shape
/// and the stage rules are Core's (<see cref="CampLayout"/>, <see cref="BuildStages"/>, <see cref="SiegeRules"/>).
/// Main thread only.
/// </summary>
public sealed class SiegeCamp : IExposable
{
    private const int RelayRange = 30;
    private const int SupplyRadius = 20;

    public IntVec3 spot;
    public Side facing;
    public SiegeGoal goal;

    /// <summary>The stage being built (0 before the camp is started).</summary>
    public int stage;

    /// <summary>The highest stage announced as standing.</summary>
    public int announced;

    public List<Blueprint> blueprints = [];
    public List<Pawn> builders = [];
    public int startBuilders;
    public int wavesSent;

    /// <summary>When the beacon was lit or the last wave came (-1: not yet).</summary>
    public int lastWaveTick = -1;

    public SiegeCamp()
    {
    }

    public SiegeCamp(IntVec3 spot, Side facing, SiegeGoal goal)
    {
        this.spot = spot;
        this.facing = facing;
        this.goal = goal;
    }

    /// <summary>The siege camp on a map, if one is running.</summary>
    public static SiegeCamp? On(Map map) =>
        map.lordManager.lords.Select(l => l.LordJob).OfType<LordJob_RcOperation>().FirstOrDefault(j => j.siege is not null)?.siege;

    public bool Started => stage >= BuildStages.First;

    /// <summary>The camp's pieces in map cells: each placement's south-west corner on this map.</summary>
    public IReadOnlyList<(CampPlacement Placement, PrefabDef Prefab, IntVec3 Min)> Plan()
    {
        var prefabs = PieceLibrary.All.ToDictionary(p => p.Source, p => p.Prefab);
        return CampLayout.Plan(prefabs.Keys, facing)
            .Select(p => (p, prefabs[p.Piece], new IntVec3(spot.x + p.Min.X, 0, spot.z + p.Min.Z)))
            .ToList();
    }

    /// <summary>Places a stage's pieces as the Empire's blueprints (each nudged up to two cells if its place is taken),
    /// and drops in what they take to build, plus shells for mortars.</summary>
    public int PlaceStage(Lord lord, int number)
    {
        var map = lord.Map;
        var placed = new List<Blueprint_Build>();
        foreach (var (placement, prefab, min) in Plan().Where(p => p.Placement.Stage == number))
        {
            var rot = new Rot4((int)placement.Rotation);
            for (var i = 0; i < GenRadial.NumCellsInRadius(2.9f); i++)
            {
                if (PieceStamper.Blueprints(prefab, map, min + GenRadial.RadialPattern[i], rot, lord.faction, StuffPalette.Imperial, placed))
                {
                    break;
                }
            }
        }

        blueprints.AddRange(placed);
        var supplies = new List<Thing>();
        foreach (var cost in placed.SelectMany(b => b.TotalMaterialCost()))
        {
            if (supplies.FirstOrDefault(t => t.def == cost.thingDef) is { } same)
            {
                same.stackCount += cost.count;
            }
            else
            {
                var thing = ThingMaker.MakeThing(cost.thingDef);
                thing.stackCount = cost.count;
                supplies.Add(thing);
            }
        }

        foreach (var mortar in placed.Where(b => b.def.entityDefToBuild is ThingDef { building.IsMortar: true }))
        {
            if (ShellFor(lord.faction) is { } shell)
            {
                var shells = ThingMaker.MakeThing(shell);
                shells.stackCount = 5;
                supplies.Add(shells);
            }
        }

        Drop(map, supplies);
        return placed.Count;
    }

    /// <summary>Blueprints and frames of the current stage still waiting to be built.</summary>
    public int Pending(Lord lord)
    {
        blueprints.RemoveAll(b => b is null || b.Destroyed);
        var frames = lord.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
            .Count(f => f.Faction == lord.faction && f.Position.InHorDistOf(spot, CampLayout.Radius + 6));
        return blueprints.Count + frames;
    }

    /// <summary>Every 500 ticks of the siege phase: moves to the next stage when this one is done, says what's
    /// standing, and keeps the mortars and the troops supplied as vanilla's siege does.</summary>
    public void SiegeTick(Lord lord)
    {
        var pending = Pending(lord);
        var next = BuildStages.Next(stage, pending);
        if (next != stage)
        {
            Announce(lord, stage);
            stage = next;
            PlaceStage(lord, stage);
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] Siege camp: stage {stage} ({BuildStages.Get(stage).Name}) placed.");
            }
        }
        else if (BuildStages.Done(stage, pending))
        {
            Announce(lord, stage);
        }

        Resupply(lord);
    }

    /// <summary>Tells the player a stage stands, once, and only if what it unlocks was actually built.</summary>
    private void Announce(Lord lord, int number)
    {
        if (number < BuildStages.First || number <= announced)
        {
            return;
        }

        announced = number;
        var spec = BuildStages.Get(number);
        if (number == BuildStages.First || Standing(lord, spec.Unlock).Any())
        {
            Messages.Message(spec.Built, new TargetInfo(spot, lord.Map), number == BuildStages.First ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.ThreatBig);
        }
    }

    /// <summary>The camp's standing buildings that give an unlock.</summary>
    public IEnumerable<Building> Standing(Lord lord, SiegeUnlock unlock)
    {
        var defs = unlock switch
        {
            SiegeUnlock.OrbitalStrikes => new[] { SiegeDefs.Uplink },
            SiegeUnlock.ShieldAndRelay => new[] { SiegeDefs.Shield, SiegeDefs.Relay },
            SiegeUnlock.Reinforcements => new[] { SiegeDefs.Beacon },
            _ => Array.Empty<ThingDef?>(),
        };
        return lord.Map.listerBuildings.allBuildingsNonColonist.Where(b => b.Faction == lord.faction && !b.Destroyed
            && b.Position.InHorDistOf(spot, CampLayout.Radius + 6)
            && (unlock == SiegeUnlock.Bombardment ? b.def.building?.IsMortar == true : defs.Contains(b.def)));
    }

    /// <summary>As <c>LordToil_Siege</c>: shells when the camp's mortars run low, meals, and barrels for mortars that
    /// need them.</summary>
    private void Resupply(Lord lord)
    {
        var map = lord.Map;
        var mortars = Standing(lord, SiegeUnlock.Bombardment).ToList();
        int shells = 0, meals = 0, barrels = 0, needBarrels = 0;
        foreach (var cell in GenRadial.RadialCellsAround(spot, SupplyRadius, useCenter: true).Where(c => c.InBounds(map)))
        {
            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.IsShell && thing.def.projectileWhenLoaded?.projectile?.damageDef?.harmsHealth == true)
                {
                    shells += thing.stackCount;
                }
                else if (thing.def == ThingDefOf.MealSurvivalPack)
                {
                    meals += thing.stackCount;
                }
                else if (thing.def == ThingDefOf.ReinforcedBarrel)
                {
                    barrels += thing.stackCount;
                }
            }
        }

        if (!Find.Storyteller.difficulty.classicMortars)
        {
            foreach (var mortar in mortars)
            {
                if (mortar.TryGetComp<CompRefuelable>() is { } fuel)
                {
                    needBarrels++;
                    if (fuel.Props.fuelFilter.Allows(ThingDefOf.ReinforcedBarrel) && fuel.HasFuel)
                    {
                        barrels++;
                    }
                }
            }
        }

        var supplies = new List<Thing>();
        if (mortars.Count > 0 && shells < 4 && ShellFor(lord.faction) is { } shell)
        {
            supplies.Add(Stack(shell, 6));
        }

        if (meals < 5)
        {
            supplies.Add(Stack(ThingDefOf.MealSurvivalPack, 12));
        }

        if (barrels < needBarrels)
        {
            supplies.Add(Stack(ThingDefOf.ReinforcedBarrel, needBarrels - barrels));
        }

        Drop(map, supplies);
    }

    /// <summary>Every 250 ticks while the siege lasts (not while it retreats): the relay links nearby psykers, and the
    /// beacon brings in waves from the operation's reserve.</summary>
    public void JobTick(Lord lord, LordJob_RcOperation job)
    {
        if (!Started)
        {
            return;
        }

        if (SiegeDefs.RelayLink is { } link && Standing(lord, SiegeUnlock.ShieldAndRelay).Where(b => b.def == SiegeDefs.Relay).ToList() is { Count: > 0 } relays)
        {
            foreach (var pawn in lord.ownedPawns.Where(p => p.Spawned && !p.Dead && p.HasPsylink && relays.Any(r => r.Position.InHorDistOf(p.Position, RelayRange))))
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(link) is HediffWithComps had)
                {
                    had.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                }
                else
                {
                    pawn.health.AddHediff(link);
                }
            }
        }

        var beacon = Standing(lord, SiegeUnlock.Reinforcements).FirstOrDefault();
        var now = Find.TickManager.TicksGame;
        if (beacon is not null && lastWaveTick < 0)
        {
            lastWaveTick = now; // lit: the first wave comes one interval later
        }

        if (beacon is null || !SiegeRules.WaveDue(true, wavesSent, job.reserve, now - lastWaveTick))
        {
            return;
        }

        var points = SiegeRules.WavePoints(job.reserve, wavesSent);
        var pawns = OperationSpawner.Force(job.operation, job.stage, points, lord.faction);
        wavesSent++;
        lastWaveTick = now;
        job.reserve = Math.Max(0, job.reserve - points);
        if (pawns.Count == 0)
        {
            return;
        }

        lord.AddPawns(pawns);
        DropPodUtility.DropThingsNear(beacon.Position, lord.Map, pawns, canRoofPunch: false, forbid: false, allowFogged: false, faction: lord.faction);
        Messages.Message($"Imperial reinforcements are dropping in at the siege beacon: {pawns.Count} more.", new TargetInfo(beacon.Position, lord.Map), MessageTypeDefOf.ThreatBig);
    }

    /// <summary>What the siege does now about its builders: hold, attack or leave (<see cref="SiegeRules.AfterBuildersFall"/>).</summary>
    public AfterBuilders AfterBuilders(Lord lord)
    {
        var up = builders.Count(p => p is { Dead: false, Downed: false, Spawned: true } && p.GetLord() == lord);
        var lost = lord.numPawnsEverGained == 0 ? 0f : lord.numPawnsLostViolently / (float)lord.numPawnsEverGained;
        return SiegeRules.AfterBuildersFall(startBuilders, up, lost);
    }

    /// <summary>The siege is over: unbuilt blueprints and frames are cancelled, and what was built is abandoned (no
    /// faction), as vanilla's siege leaves its camp. An abandoned uplink calls no strikes.</summary>
    public void Teardown(Lord lord)
    {
        foreach (var blueprint in blueprints.Where(b => b is { Destroyed: false }).ToList())
        {
            blueprint.Destroy(DestroyMode.Cancel);
        }

        blueprints.Clear();
        if (lord.Map is not { } map)
        {
            return;
        }

        foreach (var frame in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                     .Where(f => f.Faction == lord.faction && f.Position.InHorDistOf(spot, CampLayout.Radius + 6)).ToList())
        {
            frame.Destroy(DestroyMode.Cancel);
        }

        foreach (var building in map.listerBuildings.allBuildingsNonColonist
                     .Where(b => b.Faction == lord.faction && b.Position.InHorDistOf(spot, CampLayout.Radius + 6)).ToList())
        {
            building.SetFaction(null);
        }
    }

    private static ThingDef? ShellFor(Faction faction) =>
        TurretGunUtility.TryFindRandomShellDef(ThingDefOf.Turret_Mortar, allowEMP: false, allowToxGas: false, mustHarmHealth: true,
            faction.def.techLevel, allowAntigrainWarhead: false, 250f, faction);

    private static Thing Stack(ThingDef def, int count)
    {
        var thing = ThingMaker.MakeThing(def);
        thing.stackCount = count;
        return thing;
    }

    /// <summary>Drops supplies by pod next to the camp, two stacks a pod, as vanilla's siege does.</summary>
    private void Drop(Map map, List<Thing> things)
    {
        var stacks = new List<Thing>();
        foreach (var thing in things)
        {
            while (thing.stackCount > thing.def.stackLimit)
            {
                stacks.Add(thing.SplitOff(thing.def.stackLimit));
            }

            stacks.Add(thing);
        }

        var groups = stacks.Select((t, i) => (t, i)).GroupBy(x => x.i / 2).Select(g => g.Select(x => x.t).ToList()).ToList();
        if (groups.Count > 0)
        {
            DropPodUtility.DropThingGroupsNear(spot, map, groups, canRoofPunch: false, allowFogged: true);
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref spot, "spot");
        Scribe_Values.Look(ref facing, "facing");
        Scribe_Values.Look(ref goal, "goal");
        Scribe_Values.Look(ref stage, "stage");
        Scribe_Values.Look(ref announced, "announced");
        Scribe_Collections.Look(ref blueprints, "blueprints", LookMode.Reference);
        Scribe_Collections.Look(ref builders, "builders", LookMode.Reference);
        Scribe_Values.Look(ref startBuilders, "startBuilders");
        Scribe_Values.Look(ref wavesSent, "wavesSent");
        Scribe_Values.Look(ref lastWaveTick, "lastWaveTick", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            blueprints ??= [];
            builders ??= [];
            blueprints.RemoveAll(b => b is null);
            builders.RemoveAll(p => p is null);
        }
    }
}
