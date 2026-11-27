using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>
/// The Hollow Choir's operations (War in Heaven W7, tech spec §4.3): each builds its force from the Choir's mix, brings
/// it in its own way (walking in, burrowing up, resonators in drop pods) and hands it to a <see cref="LordJob_RcOperation"/>
/// marked as the Choir's, which runs the profile its Imperial mirror runs. The cluster siege comes with W8's pieces.
/// Main thread only.
/// </summary>
public static class ChoirOperations
{
    public static void Register()
    {
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.ProbingSwarm, (map, op, stage, budget, _) => ProbingSwarm(map, stage, budget, dev: false));
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.TunnelBreach, (map, op, stage, budget, _) => TunnelBreach(map, stage, budget, dev: false));
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.TheHum, (map, op, stage, budget, _) => TheHum(map, stage, budget, dev: false));
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.Retrieval, (map, op, stage, budget, _) => Retrieval(map, stage, budget, dev: false));
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.Burrowing, (map, op, stage, budget, _) => Burrowing(map, stage, budget, dev: false));
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.FinalChorus, (map, op, stage, budget, _) => FinalChorus(map, stage, budget, dev: false));
        ChoirCrisisDriver.RegisterOperation(CrisisOperations.ClusterSiege, (map, op, stage, budget, detail) =>
            ClusterSiege(map, stage, budget, SiegeGoals.Parse(detail) is var goal && goal != SiegeGoal.None ? goal : null, dev: false));
    }

    /// <summary>The Choir's people the colony holds prisoner (what a retrieval comes for), highest rank first.</summary>
    public static IEnumerable<Pawn> Held() =>
        Find.Maps.Where(m => m.IsPlayerHome)
            .SelectMany(m => m.mapPawns.AllPawnsSpawned)
            .Where(p => !p.Dead && p.IsPrisonerOfColony && ChoirRoster.IsMechanitor(UnitKit.Unit(p.kindDef)?.role))
            .OrderByDescending(p => ChoirRoster.Command(UnitKit.Unit(p.kindDef)!.role)).ThenBy(p => p.thingIDNumber);

    private static Faction? Choir => ChoirCrisisDriver.Faction;

    private static LordJob_RcOperation Job(string operation, CrisisStage stage, float reserve, IntVec3 staging, Pawn? target, bool dev) =>
        new(operation, stage, reserve, staging, target, dev) { power = Power.Choir.Id };

    /// <summary>Makes the lord and records who commands which mech.</summary>
    private static Lord MakeLord(Faction faction, LordJob_RcOperation job, Map map, ChoirForceParts parts)
    {
        var lord = LordMaker.MakeNewLord(faction, job, map, parts.All);
        foreach (var (mech, commander) in parts.Links)
        {
            job.Command(mech, commander);
        }

        return lord;
    }

    private static void WalkIn(IEnumerable<Pawn> pawns, IntVec3 entry, Map map)
    {
        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 8), map);
        }
    }

    /// <summary>A light swarm walks in, tests the defenses for about two hours, and pulls back to report the doors.</summary>
    public static bool ProbingSwarm(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (Choir is not { } choir || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.ProbingSwarm);
        var parts = ChoirForce.Build(ChoirMixes.ProbingSwarm, stage, budget * profile.ForceShare, choir);
        if (parts.Count == 0)
        {
            return false;
        }

        WalkIn(parts.All, entry, map);
        MakeLord(choir, Job(profile.Operation, stage, budget * profile.ReserveShare, entry, null, dev), map, parts);
        StoryLetters.ChoirCrisis("Choir swarm",
            $"A swarm of {parts.Mechs.Count} Choir mechs is testing {Faction.OfPlayer.Name}'s defenses, with {Mechanitors(parts)} behind it. "
            + "It won't stay long: it means to learn the colony's layout and report back.\n\nThe mechanitors command the mechs: bring one "
            + "down and its mechs fall silent.",
            LetterDefOf.ThreatBig, new LookTargets(parts.All));
        return true;
    }

    /// <summary>Tunnelers come up beside the colony's turrets (or its walls); the rest of the force walks in after them.</summary>
    public static bool TunnelBreach(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (Choir is not { } choir || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.TunnelBreach);
        var parts = ChoirForce.Build(ChoirMixes.TunnelBreach, stage, budget * profile.ForceShare, choir);
        if (parts.Count == 0)
        {
            return false;
        }

        var tunnelers = parts.Mechs.Where(m => ChoirRoster.Mechs.Any(k => k.Def == m.kindDef.defName && k.Class == MechClass.Tunneler)).ToList();
        var targets = DefenseTargets(map, entry).ToList();
        var tunnels = new List<Thing>();
        foreach (var (mech, i) in tunnelers.Select((m, i) => (m, i)))
        {
            var near = targets.Count > 0 ? targets[i % targets.Count] : entry;
            if (BurrowSpot(map, near, 2, 6) is { } spot)
            {
                tunnels.Add(Burrow(mech, spot, map));
            }
            else
            {
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(entry, map, 8), map);
            }
        }

        WalkIn(parts.All.Except(tunnelers), entry, map);
        MakeLord(choir, Job(profile.Operation, stage, budget * profile.ReserveShare, entry, null, dev), map, parts);
        StoryLetters.ChoirCrisis("Choir tunnel breach",
            $"The ground is rumbling under {Faction.OfPlayer.Name}'s defenses: {tunnels.Count} tunnelling mechs will come up beside them in about half a "
            + $"minute. {parts.Count - tunnels.Count} more of the Choir are walking in behind them, with {Mechanitors(parts)} in command.",
            LetterDefOf.ThreatBig, new LookTargets(tunnels.Count > 0 ? tunnels : parts.All.Where(p => p.Spawned)));
        return true;
    }

    /// <summary>Resonators drop near the colony and start the hum; mechanitors push in behind a mixed line while it lasts.</summary>
    public static bool TheHum(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (Choir is not { } choir || ChoirHum.ResonatorDef is not { } def
            || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var centre = ColonyCentre(map);
        var spots = new List<IntVec3>();
        for (var i = 0; i < HumRules.Resonators(stage); i++)
        {
            var cells = DropCells(map, centre, HumRules.MaxDrop).Where(c => spots.All(s => s.DistanceTo(new IntVec3(c.Cell.X, 0, c.Cell.Z)) >= 10));
            if (Tactics.DropSpot(new Cell(centre.x, centre.z), cells, HumRules.MinDrop, HumRules.MaxDrop) is { } spot)
            {
                spots.Add(new IntVec3(spot.X, 0, spot.Z));
            }
        }

        if (spots.Count == 0)
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.TheHum);
        var parts = ChoirForce.Build(ChoirMixes.TheHum, stage, budget * profile.ForceShare, choir);
        if (parts.Count == 0)
        {
            return false;
        }

        foreach (var spot in spots)
        {
            ChoirHum.Drop(def, spot, map, choir);
        }

        WalkIn(parts.All, entry, map);
        MakeLord(choir, Job(profile.Operation, stage, budget * profile.ReserveShare, entry, null, dev), map, parts);
        StoryLetters.ChoirCrisis("The Hum",
            $"{spots.Count} Choir {(spots.Count == 1 ? "resonator is" : "resonators are")} dropping near {Faction.OfPlayer.Name}. While one stands, "
            + "everyone who can feel it will hear the hum: slower at work and worse in mood. Destroy the resonators to end it.\n\n"
            + $"{parts.Count} of the Choir are coming in behind the hum, with {Mechanitors(parts)} in command.",
            LetterDefOf.ThreatBig, new LookTargets(spots.Select(s => new TargetInfo(s, map))));
        return true;
    }

    /// <summary>A strike force burrows up beside a Choir captive the colony holds, frees them and leaves with them.</summary>
    public static bool Retrieval(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (Choir is not { } choir || Held().FirstOrDefault(p => p.Map == map) is not { } held)
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.Retrieval);
        var parts = ChoirForce.Build(ChoirMixes.Retrieval, stage, budget * profile.ForceShare, choir);
        if (parts.Count == 0 || !BurrowAll(parts.All, held.Position, map, 3, 10, out var tunnels, out var first))
        {
            return false;
        }

        MakeLord(choir, Job(profile.Operation, stage, 0, first, held, dev), map, parts);
        StoryLetters.ChoirCrisis("Choir retrieval",
            $"The ground is rumbling near {held.LabelShort}. {parts.Count} of the Choir are burrowing up to free them, and will come up in about half a minute.",
            LetterDefOf.ThreatBig, new LookTargets(tunnels));
        return true;
    }

    /// <summary>Mechs burrow up beside the colony's leader (never under overhead mountain) and go for them.</summary>
    public static bool Burrowing(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (Choir is not { } choir || OperationSpawner.Target(map) is not { } target)
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.Burrowing);
        var parts = ChoirForce.Build(ChoirMixes.Burrowing, stage, budget * profile.ForceShare, choir);
        if (parts.Count == 0 || !BurrowAll(parts.All, target.Position, map, 3, 12, out var tunnels, out var first))
        {
            return false;
        }

        MakeLord(choir, Job(profile.Operation, stage, 0, first, target, dev), map, parts);
        StoryLetters.ChoirCrisis("Choir mechs burrowing",
            $"The ground is rumbling near {target.LabelShort}. {parts.Count} of the Choir are burrowing up to bring them down, and will break the surface "
            + $"in about half a minute. They'll pull out once {target.LabelShort} is down.\n\nGet {target.LabelShort} behind the guns, or under the "
            + "mountain: they can't dig up through rock.",
            LetterDefOf.ThreatBig, new LookTargets(tunnels));
        return true;
    }

    /// <summary>
    /// The cluster siege (W8, tech spec §4.3 row 4): the force walks to the best camp spot (as the Imperial siege does),
    /// lands its cluster there and grows it stage by stage while its mechanitors hold it, then attacks. Its goal (the
    /// director's pick, else a weighted one) sets what its cluster drops aim at. The reserve comes through its beacon.
    /// </summary>
    public static bool ClusterSiege(Map map, CrisisStage stage, float budget, SiegeGoal? goal, bool dev)
    {
        if (Choir is not { } choir || Siege.SiegeSpotSampler.Best(map, Power.Choir) is not { } best)
        {
            return false;
        }

        var spot = best.Spot;
        var edge = CellRect.WholeMap(map).EdgeCells.Where(c => c.Standable(map)).Select(c => c.DistanceTo(spot)).DefaultIfEmpty(0).Min();
        if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && c.DistanceTo(spot) <= edge + 25
                && map.reachability.CanReach(c, spot, Verse.AI.PathEndMode.OnCell, TraverseMode.PassDoors), map, CellFinder.EdgeRoadChance_Ignore, out var entry))
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.ClusterSiege);
        var parts = ChoirForce.Build(ChoirMixes.ClusterSiege, stage, budget * profile.ForceShare, choir);
        if (parts.Count == 0)
        {
            return false;
        }

        WalkIn(parts.All, entry, map);
        var picked = goal ?? OperationSpawner.PickGoal(map);
        var facing = Core.Crisis.Siege.SpotScorer.Facing(best.Score.Spot.Cell, new Cell(best.Centre.x, best.Centre.z));
        var job = Job(profile.Operation, stage, budget * profile.ReserveShare, entry, null, dev);
        job.cluster = new ClusterCamp(spot, facing, picked);
        MakeLord(choir, job, map, parts);
        var aim = picked switch
        {
            SiegeGoal.BreakWalls => "Its cluster drops will go for the walls and turrets.",
            SiegeGoal.Starve => "Its cluster drops will go for the fields, the food stores and the power.",
            SiegeGoal.KillLeader => "Its cluster drops will go for the colony's leader whenever they're in the open.",
            _ => "",
        };
        StoryLetters.ChoirCrisis("Choir cluster siege",
            $"{parts.Count} of the Choir are marching on {Faction.OfPlayer.Name}, with {Mechanitors(parts)}, to land a mech cluster near it. Once "
            + "they reach their spot, the cluster grows a stage every few hours while a mechanitor holds it: sandbags and shelters, then "
            + $"auto-mortars, a signal spire, a shield and a resonator, and last a drop beacon. {aim}\n\nStrike before it grows. Kill its "
            + "mechanitors and it stops growing; kill most of them and the siege must attack or give up.",
            LetterDefOf.ThreatBig, new LookTargets(new TargetInfo(spot, map)));
        return true;
    }

    /// <summary>The final chorus: every class of mech, led by the First Voice in person. It doesn't retreat.</summary>
    public static bool FinalChorus(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (Choir is not { } choir || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var profile = OperationProfiles.For(CrisisOperations.FinalChorus);
        var parts = ChoirForce.Build(ChoirMixes.FinalChorus, stage, budget * profile.ForceShare, choir);
        var voice = FirstVoice.Ready(choir); // the one First Voice, kept between appearances (W9); null once it has fallen
        if (voice is not null)
        {
            parts.Mechanitors.Insert(0, voice);
            parts.Links.Clear();
            ChoirForce.Link(parts, parts.Mechs);
        }

        if (parts.Count == 0)
        {
            return false;
        }

        WalkIn(parts.All, entry, map);
        MakeLord(choir, Job(profile.Operation, stage, 0, entry, null, dev), map, parts);
        StoryLetters.ChoirCrisis("The final chorus", voice is not null
                ? $"The First Voice has come for {Faction.OfPlayer.Name} in person, with {parts.Mechs.Count} mechs and {parts.Mechanitors.Count - 1} mechanitors. "
                  + "They won't retreat.\n\nThe First Voice commands the most mechs of all: bring the First Voice down and they fall silent."
                : $"The last of the Chorus have come for {Faction.OfPlayer.Name}: {parts.Mechs.Count} mechs and {parts.Mechanitors.Count} mechanitors, "
                  + "with no First Voice to lead them. They won't retreat.",
            LetterDefOf.ThreatBig, new LookTargets(parts.All));
        return true;
    }

    /// <summary>A regrouped Choir force's reserve: more mechs (and a mechanitor if the mix has one) drop in near its staging
    /// point, under its commanders.</summary>
    public static void Reinforce(Lord lord, LordJob_RcOperation job, float points, IntVec3 staging)
    {
        if (lord.Map is not { } map || lord.faction is not { } faction)
        {
            return;
        }

        var parts = ChoirForce.Build(ChoirMixes.For(job.operation), job.stage, points, faction);
        if (parts.Count == 0)
        {
            return;
        }

        var commanders = lord.ownedPawns.Where(p => !p.Downed && ChoirRoster.IsMechanitor(UnitKit.Unit(p.kindDef)?.role)).Concat(parts.Mechanitors).ToList();
        var links = new ChoirForceParts();
        links.Mechanitors.AddRange(commanders);
        ChoirForce.Link(links, parts.Mechs);
        lord.AddPawns(parts.All);
        foreach (var (mech, commander) in links.Links)
        {
            job.Command(mech, commander);
        }

        var spot = Tactics.DropSpot(new Cell(staging.x, staging.z), DropCells(map, staging, 12), 0, 12) is { } s ? new IntVec3(s.X, 0, s.Z) : staging;
        DropPodUtility.DropThingsNear(spot, map, parts.All, canRoofPunch: false, forbid: false, allowFogged: false, faction: faction);
        Messages.Message($"Choir reinforcements are dropping in: {parts.Count} more.", new TargetInfo(spot, map), MessageTypeDefOf.ThreatBig);
    }

    // ---- arrival helpers

    /// <summary>The colony's turrets nearest the entry, else its walls nearest the entry: where tunnelers come up.</summary>
    private static IEnumerable<IntVec3> DefenseTargets(Map map, IntVec3 entry)
    {
        var turrets = map.listerBuildings.allBuildingsColonist.OfType<Building_Turret>().Select(t => t.Position).OrderBy(c => c.DistanceToSquared(entry)).Take(4).ToList();
        return turrets.Count > 0
            ? turrets
            : map.listerBuildings.allBuildingsColonist.Where(b => b.def.IsWall).Select(b => b.Position).OrderBy(c => c.DistanceToSquared(entry)).Take(4);
    }

    /// <summary>A pawn in a vanilla tunnel: it breaks the surface after 26–30 seconds, with a rumble and dust.</summary>
    private static Thing Burrow(Pawn pawn, IntVec3 cell, Map map)
    {
        var spawner = (PawnGroundSpawner)ThingMaker.MakeThing(ThingDefOf.PawnGroundSpawner);
        spawner.Init(pawn, new IntRange(Core.Choir.Burrowing.MinDelayTicks, Core.Choir.Burrowing.MaxDelayTicks));
        return GenSpawn.Spawn(spawner, cell, map);
    }

    /// <summary>Burrows every pawn up near <paramref name="near"/>, in spread-out tunnels; false if there's no cell for any.</summary>
    private static bool BurrowAll(IReadOnlyList<Pawn> pawns, IntVec3 near, Map map, float min, float max, out List<Thing> tunnels, out IntVec3 first)
    {
        tunnels = [];
        first = near;
        var spots = Core.Choir.Burrowing.Spots(new Cell(near.x, near.z), DropCells(map, near, max, pods: false).ToList(), Math.Min(pawns.Count, 6), min, max);
        if (spots.Count == 0)
        {
            return false;
        }

        first = new IntVec3(spots[0].X, 0, spots[0].Z);
        for (var i = 0; i < pawns.Count; i++)
        {
            tunnels.Add(Burrow(pawns[i], new IntVec3(spots[i % spots.Count].X, 0, spots[i % spots.Count].Z), map));
        }

        return true;
    }

    private static IntVec3? BurrowSpot(Map map, IntVec3 near, float min, float max) =>
        Core.Choir.Burrowing.Spot(new Cell(near.x, near.z), DropCells(map, near, max, pods: false), min, max) is { } c ? new IntVec3(c.X, 0, c.Z) : null;

    /// <summary>The cells around <paramref name="near"/>, as Core's arrival rules read them. For drop pods a cell must
    /// take a pod; for a tunnel, only be standable (Core keeps tunnels out from under overhead mountain).</summary>
    public static IEnumerable<DropCell> DropCells(Map map, IntVec3 near, float radius, bool pods = true) =>
        GenRadial.RadialCellsAround(near, Math.Min(radius, GenRadial.MaxRadialPatternRadius - 1), useCenter: true)
            .Where(c => c.InBounds(map))
            .Select(c => new DropCell(new Cell(c.x, c.z),
                c.GetRoof(map) is not { } roof ? RoofKind.None : roof.isThickRoof ? RoofKind.Thick : RoofKind.Constructed,
                c.Standable(map) && c.GetFirstPawn(map) is null && (!pods || DropCellFinder.CanPhysicallyDropInto(c, map, canRoofPunch: false)), c.Fogged(map)));

    /// <summary>The middle of the colony: its colonists, else its buildings, else the map.</summary>
    public static IntVec3 ColonyCentre(Map map)
    {
        var cells = map.mapPawns.FreeColonistsSpawned.Select(p => p.Position).ToList();
        if (cells.Count == 0)
        {
            cells = map.listerBuildings.allBuildingsColonist.Select(b => b.Position).ToList();
        }

        return cells.Count == 0 ? map.Center : new IntVec3((int)cells.Average(c => c.x), 0, (int)cells.Average(c => c.z));
    }

    private static string Mechanitors(ChoirForceParts parts) =>
        parts.Mechanitors.Count == 1 ? $"the mechanitor {parts.Mechanitors[0].LabelShort}" : $"{parts.Mechanitors.Count} mechanitors";
}
