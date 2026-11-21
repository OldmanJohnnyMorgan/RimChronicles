namespace RimChronicles.Core.News;

/// <summary>The game's view of a relation, without RimWorld types.</summary>
public enum RelationKind
{
    Hostile,
    Neutral,
    Ally,
}

/// <summary>Turns game events into public news sentences. Plain facts only: the bulletin prompt adds the voice.</summary>
public static class NewsPhrasing
{
    /// <summary>A game condition (weather, disaster) starting or ending. <paramref name="place"/> is null for a
    /// world-wide condition, else the colony's name.</summary>
    public static string Condition(string label, string? place, bool ended)
    {
        var where = place is null ? "across the region" : $"around {place}";
        return ended ? $"The {label} {where} has ended" : $"The {label} has begun {where}";
    }

    /// <summary>A change in relations between two factions; <paramref name="other"/> may be the colony.</summary>
    public static string RelationChanged(string faction, string other, RelationKind now, RelationKind before) => now switch
    {
        RelationKind.Hostile => $"{faction} turned hostile to {other}",
        RelationKind.Ally => $"{faction} became allies of {other}",
        _ when before == RelationKind.Hostile => $"{faction} made peace with {other}",
        _ => $"{faction} is no longer allied with {other}",
    };

    public static string SettlementDestroyed(string settlement, string faction, string destroyer) =>
        $"{destroyer} destroyed {settlement}, a settlement of {faction}";

    public static string FactionDefeated(string faction) => $"{faction} has lost its last settlement and is no more";

    public static string LeaderDied(string faction, string title, string leader, string? successor) => successor is null
        ? $"{leader}, {title} of {faction}, has died"
        : $"{leader}, {title} of {faction}, has died, and {successor} now leads them";

    /// <summary>A reporter's death not caused by the colony; the replacement's first bulletin leads with it.</summary>
    public static string ReporterDied(string name, string outlet) => $"{name}, correspondent for {outlet}, has died";
}
