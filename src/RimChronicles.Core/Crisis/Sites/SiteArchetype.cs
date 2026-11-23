namespace RimChronicles.Core.Crisis.Sites;

/// <summary>Pieces a site asks for by tag: how many (required), or how likely and how many at most (optional).</summary>
public sealed record PieceRequest(string Tag, int Count = 1, double Weight = 1, int Max = 1);

/// <summary>
/// A kind of Imperial site (crisis spec §4.1), as plain data: its size, its objective room, required and optional
/// pieces, gates and outer defenses, threat budget range, and the capability destroying it knocks out. The game reads
/// it from a <c>SiteArchetypeDef</c>.
/// </summary>
public sealed record SiteArchetype
{
    public const string Prefix = "Site:";

    /// <summary>The archetype's name ("PsychicRelay"); its id for the director is <see cref="Id"/>.</summary>
    public required string Name { get; init; }

    public string Desc { get; init; } = "";

    /// <summary>The plan's grid, walls and the open ground around them included.</summary>
    public int Width { get; init; } = 40;

    public int Height { get; init; } = 40;

    /// <summary>The tag of the piece holding the objective; it's placed deepest from the entry side.</summary>
    public required string ObjectiveTag { get; init; }

    public IReadOnlyList<PieceRequest> Required { get; init; } = [];
    public IReadOnlyList<PieceRequest> Optional { get; init; } = [];

    /// <summary>Tag of the pieces set inside the gates as outer defenses.</summary>
    public string DefenseTag { get; init; } = "Line";

    /// <summary>Gates in the wall on the entry side, 1–3.</summary>
    public int Gates { get; init; } = 1;

    public float MinBudget { get; init; } = 500;
    public float MaxBudget { get; init; } = 3000;

    /// <summary>The most of the budget that pieces (defenses and optional emplacements) may take; the rest is defenders.</summary>
    public float PieceShare { get; init; } = 0.5f;

    /// <summary>Chance that a slot left after the required pieces gets an optional one.</summary>
    public float OptionalChance { get; init; } = 0.7f;

    public CrisisStage MinStage { get; init; } = CrisisStage.Probing;
    public double Weight { get; init; } = 1;

    /// <summary>The capability destroying the objective knocks out (<see cref="CrisisCapability"/>), or null.</summary>
    public string? Capability { get; init; }

    public int CapabilityDays { get; init; }

    public string Id => Prefix + Name;

    /// <summary>The budget a site gets for the threat points it was made with.</summary>
    public float Budget(float points) => Math.Clamp(points, MinBudget, MaxBudget);
}

/// <summary>Which archetypes the crisis may offer at a stage, and the weighted pick (the director picks from C11).</summary>
public static class SiteArchetypes
{
    public static IReadOnlyList<SiteArchetype> Allowed(IEnumerable<SiteArchetype> all, CrisisStage stage) =>
        stage == CrisisStage.None ? [] : all.Where(a => a.MinStage <= stage && a.Weight > 0).OrderBy(a => a.Name, StringComparer.Ordinal).ToList();

    /// <summary>A weighted pick with <paramref name="roll"/> in [0, 1); null when nothing is allowed.</summary>
    public static SiteArchetype? Pick(IReadOnlyList<SiteArchetype> allowed, double roll)
    {
        var total = allowed.Sum(a => a.Weight);
        if (total <= 0)
        {
            return null;
        }

        var at = Math.Clamp(roll, 0, 0.999999) * total;
        foreach (var archetype in allowed)
        {
            if ((at -= archetype.Weight) < 0)
            {
                return archetype;
            }
        }

        return allowed[^1];
    }
}
