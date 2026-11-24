using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Core.Crisis.Siege;

/// <summary>What the game measured about one candidate camp spot (tech spec §4.6). Plain data, so the scoring is
/// tested without a map.</summary>
public sealed record SpotFeatures
{
    public required Cell Cell { get; init; }

    /// <summary>Cells from the spot to the colony's centre.</summary>
    public required float DistanceToColony { get; init; }

    /// <summary>How far the camp's edge stays outside the reach of the nearest colony turret (not counting mortars),
    /// in cells; negative when a turret reaches the camp. Large when the colony has none.</summary>
    public float TurretMargin { get; init; } = 999;

    /// <summary>Colony mortars that can reach the spot (it's beyond their minimum range and within their range).</summary>
    public int ColonyMortarsInReach { get; init; }

    /// <summary>Share of the cells on the line toward the colony that give cover (walls, rocks, trees, sandbags), 0–1.</summary>
    public float CoverShare { get; init; }

    /// <summary>The angle, at the colony's centre, between the spot and the nearest entrance a probe found, in
    /// degrees (0: the camp faces that door). Null when no probe has reported.</summary>
    public float? EntranceAngle { get; init; }

    /// <summary>Sides (0–2) from which the colony can walk round the camp across open ground.</summary>
    public int OpenFlanks { get; init; }

    /// <summary>Share of the camp's ground that can be built on, 0–1.</summary>
    public float BuildableShare { get; init; } = 1;

    /// <summary>Raiders can walk from the spot to the colony (and the map edge, to leave).</summary>
    public bool Reachable { get; init; } = true;

    /// <summary>Inside the colony's home area or under a roof.</summary>
    public bool InColony { get; init; }
}

/// <summary>How much each feature counts. First guesses for the balance pass (C14).</summary>
public sealed record SpotWeights
{
    public static readonly SpotWeights Default = new();

    /// <summary>The camp's mortars must clear their minimum range (vanilla's mortar: 29.9) from the camp's edge.</summary>
    public float MinDistance { get; init; } = 36;

    /// <summary>Farther than this, the camp is too far to matter (and the assault too long a walk).</summary>
    public float MaxDistance { get; init; } = 80;

    /// <summary>Cells the camp keeps outside any colony turret's reach.</summary>
    public float SafeMargin { get; init; } = 3;

    public float MinBuildable { get; init; } = 0.6f;

    public float Closeness { get; init; } = 1.0f;
    public float Cover { get; init; } = 1.5f;

    /// <summary>A cover share at or above this counts in full.</summary>
    public float FullCover { get; init; } = 0.35f;

    public float Entrance { get; init; } = 1.0f;
    public float PerMortar { get; init; } = 0.4f;
    public float MaxMortarPenalty { get; init; } = 1.2f;
    public float OneFlankOpen { get; init; } = 0.25f;
    public float BothFlanksOpen { get; init; } = 1.0f;
    public float Buildable { get; init; } = 0.5f;
}

/// <summary>A spot's score, its parts, or why it was ruled out.</summary>
public sealed record SpotScore(SpotFeatures Spot, float Total, string? Rejected, IReadOnlyList<(string Part, float Value)> Parts)
{
    public bool Ok => Rejected is null;

    public override string ToString() => Ok
        ? $"{Spot.Cell}: {Total:0.00} ({string.Join(", ", Parts.Select(p => $"{p.Part} {p.Value:+0.00;-0.00}"))})"
        : $"{Spot.Cell}: ruled out, {Rejected}";
}

/// <summary>
/// Ranks candidate siege camp spots (crisis spec §5.5): outside the colony's turret range and inside the Empire's own
/// mortar range; cover between camp and colony; facing the entrances a probe found; not easy to flank on both sides.
/// Closer is better among the spots that qualify, so the camp threatens the colony.
/// </summary>
public static class SpotScorer
{
    public static SpotScore Score(SpotFeatures s, SpotWeights? weights = null)
    {
        var w = weights ?? SpotWeights.Default;
        string? rejected = null;
        if (!s.Reachable)
        {
            rejected = "no path to the colony";
        }
        else if (s.InColony)
        {
            rejected = "inside the colony";
        }
        else if (s.DistanceToColony < w.MinDistance)
        {
            rejected = $"too close for the camp's mortars ({s.DistanceToColony:0} < {w.MinDistance:0})";
        }
        else if (s.DistanceToColony > w.MaxDistance)
        {
            rejected = $"too far ({s.DistanceToColony:0} > {w.MaxDistance:0})";
        }
        else if (s.TurretMargin < w.SafeMargin)
        {
            rejected = $"in reach of a colony turret (margin {s.TurretMargin:0})";
        }
        else if (s.BuildableShare < w.MinBuildable)
        {
            rejected = $"too little ground to build on ({s.BuildableShare:P0})";
        }

        if (rejected is not null)
        {
            return new SpotScore(s, 0, rejected, []);
        }

        var parts = new List<(string, float)>
        {
            ("close", w.Closeness * (1 - ((s.DistanceToColony - w.MinDistance) / Math.Max(1, w.MaxDistance - w.MinDistance)))),
            ("cover", w.Cover * Math.Min(1, s.CoverShare / Math.Max(0.01f, w.FullCover))),
            ("ground", w.Buildable * s.BuildableShare),
        };
        if (s.EntranceAngle is { } angle)
        {
            parts.Add(("entrance", w.Entrance * Math.Max(0, 1 - (angle / 90f))));
        }

        if (s.ColonyMortarsInReach > 0)
        {
            parts.Add(("mortars", -Math.Min(w.MaxMortarPenalty, w.PerMortar * s.ColonyMortarsInReach)));
        }

        if (s.OpenFlanks > 0)
        {
            parts.Add(("flanks", -(s.OpenFlanks >= 2 ? w.BothFlanksOpen : w.OneFlankOpen)));
        }

        return new SpotScore(s, parts.Sum(p => p.Item2), null, parts);
    }

    /// <summary>Every spot scored, the best first; ruled-out spots last. Ties go to the lower cell, so the order is
    /// the same every time.</summary>
    public static IReadOnlyList<SpotScore> Rank(IEnumerable<SpotFeatures> spots, SpotWeights? weights = null) =>
        spots.Select(s => Score(s, weights))
            .OrderByDescending(s => s.Ok).ThenByDescending(s => s.Total).ThenBy(s => s.Spot.Cell.X).ThenBy(s => s.Spot.Cell.Z)
            .ToList();

    public static SpotScore? Best(IEnumerable<SpotFeatures> spots, SpotWeights? weights = null) =>
        Rank(spots, weights).FirstOrDefault(s => s.Ok);

    /// <summary>The side of the camp that faces the colony: the camp is laid out with its front that way.</summary>
    public static Side Facing(Cell camp, Cell colony)
    {
        var dx = colony.X - camp.X;
        var dz = colony.Z - camp.Z;
        return Math.Abs(dx) > Math.Abs(dz) ? (dx > 0 ? Side.East : Side.West) : (dz >= 0 ? Side.North : Side.South);
    }
}
