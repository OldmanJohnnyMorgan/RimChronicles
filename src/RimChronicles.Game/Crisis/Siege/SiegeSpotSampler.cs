using LudeonTK;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Powers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimChronicles.Game.Crisis.Siege;

/// <summary>
/// Samples candidate siege camp spots around the colony and measures each for Core's <see cref="SpotScorer"/> (tech
/// spec §4.6): its distance, the colony turrets' and mortars' reach, cover on the line in, the line to the doors a
/// probe found, open flanks, and ground to build on. Replaces vanilla's <c>SiegeBlueprintPlacer</c> for the Empire's
/// sieges and the Choir's cluster sieges, each scored against its own power's probe report.
/// Main thread only.
/// </summary>
public static class SiegeSpotSampler
{
    private const float RingStep = 4f;
    private const int AngleStep = 10;
    private const float FlankReach = 14f;
    private const int CampHalf = 10;

    private sealed record Turret(IntVec3 Position, float Range, float MinRange, bool Mortar);

    /// <summary>The middle of the colony: its buildings' centre, else its colonists'.</summary>
    public static IntVec3? ColonyCentre(Map map)
    {
        var cells = map.listerBuildings.allBuildingsColonist.Select(b => b.Position).ToList();
        if (cells.Count == 0)
        {
            cells = map.mapPawns.FreeColonistsSpawned.Select(p => p.Position).ToList();
        }

        return cells.Count == 0 ? null : new IntVec3((int)cells.Average(c => c.x), 0, (int)cells.Average(c => c.z));
    }

    /// <summary>Every candidate spot, measured. Spots too near the map edge for the camp to fit are skipped.</summary>
    public static List<SpotFeatures> Sample(Map map, IntVec3 centre, IReadOnlyList<IntVec3> entrances, SpotWeights? weights = null)
    {
        var w = weights ?? SpotWeights.Default;
        var turrets = map.listerBuildings.allBuildingsColonist.OfType<Building_TurretGun>()
            .Where(t => t.AttackVerb?.verbProps is not null)
            .Select(t => new Turret(t.Position, t.AttackVerb.verbProps.range, t.AttackVerb.verbProps.minRange, t.def.building?.IsMortar == true))
            .ToList();
        var inner = map.BoundsRect().ContractedBy(CampLayout.Radius + 1);
        var spots = new List<SpotFeatures>();
        var seen = new HashSet<IntVec3>();
        for (var d = w.MinDistance; d <= w.MaxDistance; d += RingStep)
        {
            for (var a = 0; a < 360; a += AngleStep)
            {
                var rad = a * Math.PI / 180;
                var cell = new IntVec3(centre.x + (int)Math.Round(Math.Cos(rad) * d), 0, centre.z + (int)Math.Round(Math.Sin(rad) * d));
                if (inner.Contains(cell) && seen.Add(cell))
                {
                    spots.Add(Measure(map, cell, centre, turrets, entrances));
                }
            }
        }

        return spots;
    }

    private static SpotFeatures Measure(Map map, IntVec3 cell, IntVec3 centre, List<Turret> turrets, IReadOnlyList<IntVec3> entrances)
    {
        var distance = cell.DistanceTo(centre);
        var guns = turrets.Where(t => !t.Mortar).ToList();
        var margin = guns.Count == 0 ? 999f : guns.Min(t => cell.DistanceTo(t.Position) - CampLayout.Radius - t.Range);
        var mortars = turrets.Count(t => t.Mortar && cell.DistanceTo(t.Position) > t.MinRange && cell.DistanceTo(t.Position) <= t.Range);

        // Cover on the way in: the stretch between the camp's edge and the colony's outskirts.
        var line = GenSight.PointsOnLineOfSight(cell, centre)
            .Where(c => c.InBounds(map) && c.DistanceTo(cell) > CampLayout.Radius / 2f && c.DistanceTo(centre) > 8)
            .ToList();
        var cover = line.Count == 0 ? 0f : line.Count(c => GivesCover(map, c)) / (float)line.Count;

        float? entranceAngle = entrances.Count == 0 ? null : entrances.Min(e => Angle(centre, cell, e));

        // Flanks: open ground on either side of the camp that the colony can walk to.
        float dx = centre.x - cell.x, dz = centre.z - cell.z;
        var len = Math.Max(1f, MathF.Sqrt((dx * dx) + (dz * dz)));
        var flanks = 0;
        foreach (var sign in new[] { -1, 1 })
        {
            var flank = new IntVec3(cell.x + (int)Math.Round(-dz / len * FlankReach * sign), 0, cell.z + (int)Math.Round(dx / len * FlankReach * sign));
            if (flank.InBounds(map) && flank.Standable(map) && map.reachability.CanReachColony(flank))
            {
                flanks++;
            }
        }

        var ground = 0;
        var total = 0;
        for (var x = -CampHalf; x <= CampHalf; x += 2)
        {
            for (var z = -CampHalf; z <= CampHalf; z += 2)
            {
                var c = new IntVec3(cell.x + x, 0, cell.z + z);
                total++;
                if (c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) is null && c.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
                {
                    ground++;
                }
            }
        }

        var reachable = cell.Standable(map) && map.reachability.CanReachColony(cell)
            && map.reachability.CanReachMapEdge(cell, TraverseParms.For(TraverseMode.PassDoors));
        return new SpotFeatures
        {
            Cell = new Cell(cell.x, cell.z),
            DistanceToColony = distance,
            TurretMargin = margin,
            ColonyMortarsInReach = mortars,
            CoverShare = cover,
            EntranceAngle = entranceAngle,
            OpenFlanks = flanks,
            BuildableShare = total == 0 ? 0 : ground / (float)total,
            Reachable = reachable,
            InColony = map.areaManager.Home[cell] || cell.Roofed(map),
        };
    }

    /// <summary>Rock, walls, trees, sandbags: anything that stops some shots.</summary>
    private static bool GivesCover(Map map, IntVec3 c) =>
        c.GetEdifice(map) is { def.passability: Traversability.Impassable } || (c.GetCover(map) is { } cover && cover.def.fillPercent >= 0.2f);

    /// <summary>The angle at <paramref name="centre"/> between the camp and an entrance, in degrees.</summary>
    private static float Angle(IntVec3 centre, IntVec3 camp, IntVec3 entrance)
    {
        var a = Math.Atan2(camp.z - centre.z, camp.x - centre.x);
        var b = Math.Atan2(entrance.z - centre.z, entrance.x - centre.x);
        var diff = Math.Abs(a - b) * 180 / Math.PI;
        return (float)(diff > 180 ? 360 - diff : diff);
    }

    /// <summary>The entrances <paramref name="power"/>'s last probing raid reported, as map cells. Each power has its
    /// own: the Choir's probing swarms report to the Choir's crisis, and its cluster sieges use only that.</summary>
    public static List<IntVec3> KnownEntrances(Power power) =>
        RimChroniclesGameComponent.Current?.CrisisOf(power).Entrances.Select(e => new IntVec3(e.X, 0, e.Z)).ToList() ?? [];

    /// <summary>The best spot on the map for <paramref name="power"/>'s siege camp, or null when none qualifies.</summary>
    public static (IntVec3 Spot, IntVec3 Centre, SpotScore Score)? Best(Map map, Power power)
    {
        if (ColonyCentre(map) is not { } centre)
        {
            return null;
        }

        var entrances = KnownEntrances(power).Where(e => e.InBounds(map)).ToList();
        return SpotScorer.Best(Sample(map, centre, entrances)) is { } best
            ? (new IntVec3(best.Spot.Cell.X, 0, best.Spot.Cell.Z), centre, best)
            : null;
    }

    /// <summary>Draws every candidate spot's score on the current map (crisis spec §9, "RC: Show siege scoring"):
    /// brighter is better, x is ruled out, and the best spot says so. A window lists the top five and why the others
    /// were ruled out.</summary>
    [DebugAction("Rim Chronicles", "Crisis: show siege scoring", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ShowScoring() => ShowScoring(Power.Empire);

    /// <summary>The same, for the Choir's cluster siege: scored against the doors the Choir's own probes found.</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: show cluster siege scoring", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ShowChoirScoring() => ShowScoring(Power.Choir);

    private static void ShowScoring(Power power)
    {
        if (Find.CurrentMap is not { } map || ColonyCentre(map) is not { } centre)
        {
            Messages.Message("Rim Chronicles: no colony on this map to besiege.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var entrances = KnownEntrances(power).Where(e => e.InBounds(map)).ToList();
        var ranked = SpotScorer.Rank(Sample(map, centre, entrances));
        var ok = ranked.Where(s => s.Ok).ToList();
        var top = ok.Count == 0 ? 1f : Math.Max(0.01f, ok[0].Total);
        var bottom = ok.Count == 0 ? 0f : ok.Min(s => s.Total);
        const int Frames = 1200;
        foreach (var score in ranked)
        {
            var cell = new IntVec3(score.Spot.Cell.X, 0, score.Spot.Cell.Z);
            if (!score.Ok)
            {
                map.debugDrawer.FlashCell(cell, 0f, "x", Frames);
                continue;
            }

            var pct = top - bottom < 0.01f ? 1f : (score.Total - bottom) / (top - bottom);
            map.debugDrawer.FlashCell(cell, 0.25f + (0.75f * pct), score == ok[0] ? "BEST" : score.Total.ToString("0.0"), Frames);
        }

        map.debugDrawer.FlashCell(centre, 0.5f, "colony", Frames);
        foreach (var entrance in entrances)
        {
            map.debugDrawer.FlashCell(entrance, 0.5f, "door", Frames);
        }

        var lines = new List<string>
        {
            $"Colony centre {centre}; {ranked.Count} spots sampled, {ok.Count} qualify; {entrances.Count} entrances known from a probe.",
            "",
            "Best spots:",
        };
        lines.AddRange(ok.Take(5).Select(s => $"  {s}"));
        if (ok.Count == 0)
        {
            lines.Add("  none: a siege can't start on this map");
        }

        lines.Add("");
        lines.Add("Ruled out:");
        lines.AddRange(ranked.Where(s => !s.Ok)
            .GroupBy(s => s.Rejected!.Split('(')[0].Trim())
            .OrderByDescending(g => g.Count())
            .Select(g => $"  {g.Count()}: {g.Key}"));
        if (ok.Count > 0)
        {
            var facing = SpotScorer.Facing(ok[0].Spot.Cell, new Cell(centre.x, centre.z));
            lines.Add("");
            lines.Add($"The camp at the best spot would face {facing}.");
        }

        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }
}
