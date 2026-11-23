namespace RimChronicles.Core.Crisis.Pieces;

/// <summary>Checks a piece fits its declared size and doors at every rotation (crisis spec §3.2: "each piece gets a
/// test that stamps it on an empty map and checks it fits its declared size and doors"). Run by the unit tests on the
/// repo's XML, and by the game at startup on every loaded piece.</summary>
public static class PieceCheck
{
    public const int MaxBuildOrder = 5;

    /// <summary>Every problem with the piece, or none.</summary>
    public static IReadOnlyList<string> Problems(PieceSource source)
    {
        var problems = new List<string>();
        if (source.SizeX <= 0 || source.SizeZ <= 0)
        {
            return [$"{source.Name}: size ({source.SizeX},{source.SizeZ}) must be positive"];
        }

        var meta = source.Meta;
        if (meta.Tags.Count == 0)
        {
            problems.Add($"{source.Name}: no tags");
        }

        if (meta.DoorSides.Count == 0)
        {
            problems.Add($"{source.Name}: no door sides, so nothing can connect to it");
        }

        if (meta.BuildOrder is < 1 or > MaxBuildOrder)
        {
            problems.Add($"{source.Name}: build order {meta.BuildOrder} must be 1 to {MaxBuildOrder}");
        }

        if (meta.ThreatCost < 0 || meta.DefenseValue < 0)
        {
            problems.Add($"{source.Name}: threat cost and defense value can't be negative");
        }

        // A generator piece (tagged Power) makes power rather than needing it.
        if (source.Things.Any(t => t.UsesPower) && !meta.NeedsPower && !meta.Tags.Contains("Power"))
        {
            problems.Add($"{source.Name}: {source.Things.First(t => t.UsesPower).Def} uses power, so needsPower must be true");
        }

        foreach (var thing in source.Things.Where(t => !t.Rotatable && t.PlacingRot != Side.North).Select(t => t.Def).Distinct())
        {
            problems.Add($"{source.Name}: {thing} isn't rotatable and doesn't face north; pieces don't support that");
        }

        foreach (var rotation in Enum.GetValues<Side>())
        {
            var spec = PieceSpec.Build(source, rotation);
            problems.AddRange(spec.Outside.Select(o => $"{source.Name} turned {rotation}: {o} is outside its size"));
            problems.AddRange(Overlaps(source, rotation).Select(o => $"{source.Name} turned {rotation}: {o}"));
            foreach (var side in spec.DoorSides)
            {
                var edge = spec.Edge(side).Select(c => spec[c]).ToList();
                if (!edge.Any(PieceSpec.Passable))
                {
                    problems.Add($"{source.Name} turned {rotation}: door side {side} is all wall");
                }
                else if (edge.Contains(CellKind.Wall) && !edge.Contains(CellKind.Door))
                {
                    problems.Add($"{source.Name} turned {rotation}: door side {side} is walled but has no door");
                }
            }
        }

        return problems.Distinct().ToList();
    }

    /// <summary>Two things on one cell (every thing in a piece is a building, and only one fits a cell).</summary>
    private static IEnumerable<string> Overlaps(PieceSource source, Side rotation)
    {
        var seen = new Dictionary<Cell, string>();
        foreach (var thing in source.Things)
        {
            foreach (var cell in PieceRotation.Occupied(thing, rotation))
            {
                if (seen.TryGetValue(cell, out var other))
                {
                    yield return $"{thing.Def} and {other} overlap at {cell}";
                }
                else
                {
                    seen[cell] = thing.Def;
                }
            }
        }
    }
}
