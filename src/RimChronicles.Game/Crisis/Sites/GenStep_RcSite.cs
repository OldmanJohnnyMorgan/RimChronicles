using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Pieces;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>
/// Generates an Imperial site's base (crisis C9, tech spec §3.3): plans it with Core's <see cref="SitePlanner"/>,
/// seeded from the site's world-object id so the same site always looks the same, stamps it on the map, records its
/// objective, and spawns its garrison. Linked to the archetype's site part in XML.
/// </summary>
public sealed class GenStep_RcSite : GenStep
{
    public SiteArchetypeDef? archetype;

    /// <summary>Dev Mode: the next site generated uses this id for its seed instead of its own.</summary>
    public static long? NextSeed;

    public override int SeedPart => 61270139;

    public override void Generate(Map map, GenStepParams parms)
    {
        if (archetype is null || Powers.PowerFactions.Of(PowerOf(archetype)) is not { } empire)
        {
            Log.Error($"[Rim Chronicles] Crisis site without an archetype or its power's faction ({archetype?.power}); nothing generated.");
            return;
        }

        var id = NextSeed ?? (long?)parms.sitePart?.site?.ID ?? map.Tile.tileId;
        NextSeed = null;
        var points = parms.sitePart?.parms.threatPoints ?? archetype.budget.min;
        var plan = SitePlanner.PlanValid(archetype.ToCore(), PieceLibrary.SourcesFor(archetype.power), points, id, out var attempts);
        if (plan.Fallback)
        {
            Log.Warning($"[Rim Chronicles] {archetype.label}: {SitePlanner.MaxAttempts} seeded plans failed the validators; using the fixed layout.");
        }

        SiteStamper.Generate(map, plan, archetype, empire, id, attempts);
    }

    /// <summary>The power whose site this is (War in Heaven W8).</summary>
    public static Core.Powers.Power PowerOf(SiteArchetypeDef archetype) =>
        archetype.power == Core.Powers.Power.Choir.Id ? Core.Powers.Power.Choir : Core.Powers.Power.Empire;
}

/// <summary>Turns a <see cref="SitePlan"/> into things on a map. Main thread only.</summary>
public static class SiteStamper
{
    private const int RoadWidth = 1;

    /// <summary>Stamps the plan at the map's centre, records the site, and spawns the garrison.</summary>
    public static void Generate(Map map, SitePlan plan, SiteArchetypeDef archetype, Faction empire, long seed, int attempts)
    {
        var origin = new IntVec3(
            Math.Clamp(map.Center.x - (plan.Grid.Width / 2), 0, Math.Max(0, map.Size.x - plan.Grid.Width)), 0,
            Math.Clamp(map.Center.z - (plan.Grid.Height / 2), 0, Math.Max(0, map.Size.z - plan.Grid.Height)));
        var objectives = Stamp(map, plan, origin, archetype.objectiveThing, archetype.label, empire, roads: true, StuffPalette.ForPower(archetype.power));
        var centre = At(origin, plan.Ring.Centre);
        if (MapComponent_RcSite.For(map) is { } site)
        {
            site.Set(archetype, objectives, seed);
        }

        if (plan.Objective is { } goal && !Reachable(map, origin, goal))
        {
            Log.Warning($"[Rim Chronicles] {archetype.label}: the objective can't be reached from the map edge on this map (rock or water outside the site).");
        }

        Garrison(map, plan, origin, archetype, empire, centre);
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] {archetype.label} generated: seed {seed}, attempt {attempts}{(plan.Fallback ? " (fixed layout)" : "")}, "
                + $"{plan.Pieces.Count} pieces, {plan.Gates.Count} gates, budget {plan.Budget:0} ({plan.PieceCost:0} pieces, {plan.DefenderPoints:0} garrison).");
        }
    }

    public static IntVec3 At(IntVec3 origin, Cell c) => new(origin.x + c.X, 0, origin.z + c.Z);

    /// <summary>Clears the ground, then spawns the walls and gates, the pieces and the conduits, all owned by the
    /// Empire, and returns the objective buildings.</summary>
    /// <summary>With <paramref name="roads"/>, also clears a road from each gate to the map edge (not for a ring
    /// inside another: the road would cut through the outer ring).</summary>
    public static List<Thing> Stamp(Map map, SitePlan plan, IntVec3 origin, ThingDef? objectiveThing, string label, Faction empire, bool roads,
        StuffPalette? stuff = null)
    {
        var palette = stuff ?? StuffPalette.Imperial;
        var rect = new CellRect(origin.x, origin.z, plan.Grid.Width, plan.Grid.Height).ClipInsideMap(map);
        MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects").Add(rect); // later gen steps (ruins, scatter) keep out, as for vanilla outposts
        foreach (var cell in rect)
        {
            Clear(map, cell, clearPlants: true);
        }

        if (roads)
        {
            ClearRoads(map, plan, origin);
        }
        foreach (var wall in plan.Walls)
        {
            Spawn(map, ThingDefOf.Wall, At(origin, wall), empire, palette);
        }

        foreach (var gate in plan.Gates)
        {
            Spawn(map, ThingDefOf.Door, At(origin, gate), empire, palette);
        }

        var prefabs = PieceLibrary.All.ToDictionary(p => p.Source.Name, p => p.Prefab);
        var objectives = new List<Thing>();
        foreach (var placement in plan.Pieces)
        {
            if (!prefabs.TryGetValue(placement.Piece.Name, out var prefab))
            {
                continue;
            }

            var spawned = new List<Thing>();
            if (!PieceStamper.Instant(prefab, map, At(origin, placement.Min), new Rot4((int)placement.Rotation), empire, palette, spawned))
            {
                Log.Warning($"[Rim Chronicles] {label}: {placement.Piece.Name} didn't fit at {At(origin, placement.Min)}.");
                continue;
            }

            if (placement.Role == SiteRole.Objective)
            {
                objectives.AddRange(spawned.Where(t => t.def == objectiveThing));
            }
        }

        foreach (var conduit in plan.Conduits)
        {
            Spawn(map, ThingDefOf.PowerConduit, At(origin, conduit), empire, palette);
        }

        return objectives;
    }

    private static void Spawn(Map map, ThingDef def, IntVec3 cell, Faction faction, StuffPalette palette)
    {
        if (!cell.InBounds(map))
        {
            return;
        }

        var thing = ThingMaker.MakeThing(def, palette.For(def));
        thing.SetFaction(faction);
        GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
    }

    /// <summary>Takes away rock, buildings, items and (inside the site) plants, and gives the cell ground that holds
    /// heavy buildings.</summary>
    private static void Clear(Map map, IntVec3 cell, bool clearPlants)
    {
        foreach (var thing in cell.GetThingList(map).ToList())
        {
            if (thing is Pawn || (!clearPlants && thing.def.category == ThingCategory.Plant))
            {
                continue;
            }

            thing.Destroy(DestroyMode.Vanish);
        }

        map.roofGrid.SetRoof(cell, null);
        if (!cell.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy) || cell.GetTerrain(map).passability == Traversability.Impassable)
        {
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Gravel);
        }
    }

    /// <summary>A path from each gate straight out to the map edge, through rock and water, so the way in is open
    /// whatever surrounds the site (the plan's reachability check only covers the site itself).</summary>
    private static void ClearRoads(Map map, SitePlan plan, IntVec3 origin)
    {
        var step = plan.Entry switch
        {
            Side.South => new IntVec3(0, 0, -1),
            Side.North => new IntVec3(0, 0, 1),
            Side.West => new IntVec3(-1, 0, 0),
            _ => new IntVec3(1, 0, 0),
        };
        var across = step.x != 0 ? new IntVec3(0, 0, 1) : new IntVec3(1, 0, 0);
        foreach (var gate in plan.Gates)
        {
            for (var at = At(origin, gate) + step; at.InBounds(map); at += step)
            {
                for (var w = -RoadWidth; w <= RoadWidth; w++)
                {
                    var c = at + (across * w);
                    if (c.InBounds(map) && (c.GetEdifice(map) is { def.mineable: true } || !c.Walkable(map)))
                    {
                        Clear(map, c, clearPlants: false);
                    }
                }
            }
        }
    }

    /// <summary>Fairness rule 4 on the real map: a cell inside the objective's room reaches the map edge.</summary>
    private static bool Reachable(Map map, IntVec3 origin, SitePlacement objective)
    {
        var inside = objective.Cells().Select(c => At(origin, c)).Where(c => c.InBounds(map) && c.Standable(map)).ToList();
        return inside.Count > 0 && inside.Any(c => map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)));
    }

    /// <summary>The garrison: the plan's defender points of the archetype's mix at the crisis stage, spread over the
    /// plan's defender spots, defending the base.</summary>
    private static void Garrison(Map map, SitePlan plan, IntVec3 origin, SiteArchetypeDef archetype, Faction empire, IntVec3 centre)
    {
        if (archetype.power == Core.Powers.Power.Choir.Id)
        {
            ChoirGarrison(map, plan, origin, archetype, empire, centre);
            return;
        }

        var stage = CrisisStage.Probing;
        if (RimChroniclesGameComponent.Current is { } component)
        {
            var now = CrisisDriver.Stage(component, empire).Stage;
            stage = now > stage ? now : stage;
        }

        stage = archetype.minStage > stage ? archetype.minStage : stage;
        var pawns = OperationSpawner.Force(ForceMix.ForSite(archetype.archetype), stage, plan.DefenderPoints, empire);
        if (pawns.Count == 0)
        {
            return;
        }

        var spots = plan.DefenderSpots.Select(c => At(origin, c)).Where(c => c.InBounds(map) && c.Standable(map)).ToList();
        for (var i = 0; i < pawns.Count; i++)
        {
            var spot = spots.Count > 0 ? spots[i % spots.Count] : centre;
            GenSpawn.Spawn(pawns[i], CellFinder.RandomClosewalkCellNear(spot, map, 2), map);
        }

        LordMaker.MakeNewLord(empire, new LordJob_DefendBase(empire, centre, 25000), map, pawns);
    }

    /// <summary>A Choir site's garrison (War in Heaven W8): the archetype's Choir mix at the Choir's crisis stage, its
    /// mechs under its mechanitors (the site's record shuts a fallen mechanitor's mechs down).</summary>
    private static void ChoirGarrison(Map map, SitePlan plan, IntVec3 origin, SiteArchetypeDef archetype, Faction choir, IntVec3 centre)
    {
        var stage = RimChroniclesGameComponent.Current is { } component ? Choir.ChoirCrisisDriver.StageNow(component) : CrisisStage.Probing;
        stage = stage < CrisisStage.Probing ? CrisisStage.Probing : stage;
        stage = archetype.minStage > stage ? archetype.minStage : stage;
        var parts = Choir.ChoirForce.Build(Core.Choir.ChoirMixes.ForSite(archetype.archetype), stage, plan.DefenderPoints, choir);
        if (parts.Count == 0)
        {
            return;
        }

        var pawns = parts.All;
        var spots = plan.DefenderSpots.Select(c => At(origin, c)).Where(c => c.InBounds(map) && c.Standable(map)).ToList();
        for (var i = 0; i < pawns.Count; i++)
        {
            var spot = spots.Count > 0 ? spots[i % spots.Count] : centre;
            GenSpawn.Spawn(pawns[i], CellFinder.RandomClosewalkCellNear(spot, map, 2), map);
        }

        LordMaker.MakeNewLord(choir, new LordJob_DefendBase(choir, centre, 25000), map, pawns);
        MapComponent_RcSite.For(map)?.Command(parts.Links);
    }
}

/// <summary>
/// An Imperial site on its map (crisis C9): its archetype and objective buildings. When the objectives are all
/// destroyed it knocks out the archetype's capability for its days (crisis spec §6: a relay weakens Imperial psykers,
/// an uplink stops orbital strikes) and tells the player. Saved with the map.
/// </summary>
public sealed class MapComponent_RcSite(Map map) : MapComponent(map)
{
    private const int CheckTicks = 250;

    private SiteArchetypeDef? _archetype;
    private List<Thing> _objectives = [];

    /// <summary>A Choir garrison's chain of command (War in Heaven W8): <c>_mechs[i]</c> is commanded by <c>_commanders[i]</c>.</summary>
    private List<Pawn?> _mechs = [];

    private List<Pawn?> _commanders = [];

    /// <summary>Records who commands which of the garrison's mechs.</summary>
    public void Command(IEnumerable<(Pawn Mech, Pawn Commander)> links)
    {
        foreach (var (mech, commander) in links)
        {
            _mechs.Add(mech);
            _commanders.Add(commander);
        }
    }

    /// <summary>A Choir mechanitor on the site is dead, downed or captured: its mechs shut down where they stand.</summary>
    private void SilenceFallenCommanders()
    {
        var silenced = new List<Pawn>();
        for (var i = _mechs.Count - 1; i >= 0; i--)
        {
            if (_commanders[i] is { Dead: false, Downed: false, IsPrisoner: false, Spawned: true })
            {
                continue;
            }

            if (_mechs[i] is { Dead: false, Spawned: true } mech && mech.Map == map)
            {
                mech.TryGetComp<CompMechanoid>()?.Deactivate();
                silenced.Add(mech);
            }

            _mechs.RemoveAt(i);
            _commanders.RemoveAt(i);
        }

        if (silenced.Count > 0)
        {
            Messages.Message($"With their mechanitor down, {silenced.Count} of the Choir's mechs fall silent. Any colonist can dismantle them.",
                new LookTargets(silenced), MessageTypeDefOf.PositiveEvent);
        }
    }
    private long _seed;
    private bool _done;

    /// <summary>How many objectives the site had: a destroyed one doesn't come back from a save, so the list alone
    /// can't tell "all destroyed" from "none".</summary>
    private int _objectiveCount;

    public SiteArchetypeDef? Archetype => _archetype;
    public IReadOnlyList<Thing> Objectives => _objectives;
    public long Seed => _seed;
    public bool Done => _done;

    public static MapComponent_RcSite? For(Map map) => map.GetComponent<MapComponent_RcSite>();

    public void Set(SiteArchetypeDef archetype, List<Thing> objectives, long seed)
    {
        _archetype = archetype;
        _objectives = objectives;
        _objectiveCount = objectives.Count;
        _seed = seed;
        _done = false;
    }

    public override void MapComponentTick()
    {
        if (_mechs.Count > 0 && Find.TickManager.TicksGame % 60 == 0)
        {
            SilenceFallenCommanders();
        }

        if (_done || _archetype is null || _objectiveCount == 0 || Find.TickManager.TicksGame % CheckTicks != 0)
        {
            return;
        }

        if (_objectives.Any(t => t is { Destroyed: false }))
        {
            return;
        }

        _done = true;
        ObjectiveDestroyed(_archetype);
    }

    /// <summary>The objective is gone: the capability goes down, and the player hears what it bought them.</summary>
    public static void ObjectiveDestroyed(SiteArchetypeDef archetype)
    {
        var today = GenDate.DaysPassed;
        if (archetype.power == Core.Powers.Power.Choir.Id)
        {
            ChoirObjectiveDestroyed(archetype, today);
            return;
        }

        if (archetype.capability is { Length: > 0 } capability && RimChroniclesGameComponent.Current is { } component)
        {
            component.Crisis.Hold(capability, today + archetype.capabilityDays);
            if (capability == CrisisCapability.Psykers)
            {
                component.Crisis.RelaySitesDestroyed++; // one relay fewer in the Citadel's courtyard (C12)
            }
            else if (capability == CrisisCapability.Strikes)
            {
                component.Crisis.UplinkSitesDestroyed++;
            }
        }

        if (archetype.needsHolding)
        {
            return; // a Reclaimed Holding's quest frees the settlement and sends its own letter (Liberation)
        }

        var (label, text) = archetype.capability switch
        {
            CrisisCapability.Psykers => ("Psychic relay destroyed",
                $"The Imperial psychic relay is gone. For the next {archetype.capabilityDays} days, the Empire's psykers fight without it: "
                + "they're less sensitive and shed heat slower."),
            CrisisCapability.Strikes => ("Orbital uplink destroyed",
                $"The Imperial orbital uplink is gone. For the next {archetype.capabilityDays} days, the Empire can't call orbital strikes "
                + "on the colony, Herald or not."),
            _ => ("Outpost comms destroyed", "The Imperial outpost's comms console is destroyed: the Empire has lost its eyes on this stretch of the rim."),
        };
        StoryLetters.Crisis(label, text, LetterDefOf.PositiveEvent);
    }

    /// <summary>A Choir site's objective is gone (W8): the resonator silences the hum, the spire grounds the cluster drops,
    /// for the archetype's days; an outpost blinds the Choir. A held town's quest sends its own letter.</summary>
    private static void ChoirObjectiveDestroyed(SiteArchetypeDef archetype, int today)
    {
        if (archetype.capability is { Length: > 0 } capability && RimChroniclesGameComponent.Current is { } component)
        {
            component.ChoirCrisis.Hold(capability, today + archetype.capabilityDays);
            if (capability == CrisisCapability.Psykers)
            {
                component.ChoirCrisis.RelaySitesDestroyed++; // one resonator fewer guarding the Cathedral (W9)
            }
            else if (capability == CrisisCapability.Strikes)
            {
                component.ChoirCrisis.UplinkSitesDestroyed++;
            }
        }

        if (archetype.needsHolding)
        {
            return;
        }

        var (label, text) = archetype.capability switch
        {
            CrisisCapability.Psykers => ("Choir resonator destroyed",
                $"The Choir's resonator is gone, and the hum with it. For the next {archetype.capabilityDays} days the Choir can't bring the hum "
                + "against the colony."),
            CrisisCapability.Strikes => ("Choir signal spire destroyed",
                $"The Choir's signal spire is gone. For the next {archetype.capabilityDays} days its cantors can't call cluster drops on the colony, "
                + "and no cluster siege can come."),
            _ => ("Choir outpost comms destroyed", "The Choir outpost's comms console is destroyed: the Choir has lost its eyes on this stretch of the rim."),
        };
        StoryLetters.ChoirCrisis(label, text, LetterDefOf.PositiveEvent);
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref _mechs, "rcSiteMechs", LookMode.Reference);
        Scribe_Collections.Look(ref _commanders, "rcSiteCommanders", LookMode.Reference);
        Scribe_Defs.Look(ref _archetype, "rcSiteArchetype");
        Scribe_Collections.Look(ref _objectives, "rcSiteObjectives", LookMode.Reference);
        Scribe_Values.Look(ref _seed, "rcSiteSeed");
        Scribe_Values.Look(ref _done, "rcSiteDone");
        Scribe_Values.Look(ref _objectiveCount, "rcSiteObjectiveCount");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _objectives ??= [];
            _objectives.RemoveAll(t => t is null);
            _mechs ??= [];
            _commanders ??= [];
            if (_mechs.Count != _commanders.Count)
            {
                _mechs.Clear();
                _commanders.Clear();
            }
        }
    }
}
