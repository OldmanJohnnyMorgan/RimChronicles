using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Game.Crisis.Pieces;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>Which Imperial site the Empire plot's sabotage job offers (crisis C9): the archetype the director picked in a
/// second call (C11, tech spec R7), else a weighted pick among those the crisis stage allows. Main thread only.</summary>
public static class SiteOffers
{
    /// <summary>All archetypes whose site part loaded and whose pieces exist.</summary>
    public static List<SiteArchetypeDef> Usable() => DefDatabase<SiteArchetypeDef>.AllDefsListForReading
        .Where(d => d.sitePart is not null && d.objectiveThing is not null && PieceLibrary.All.Any(p => p.Source.Meta.Tags.Contains(d.objectiveTag)))
        .ToList();

    /// <summary>The archetypes the crisis stage allows now; empty when the crisis isn't on.</summary>
    public static IReadOnlyList<SiteArchetype> Allowed(RimChroniclesGameComponent component, Faction empire) =>
        CrisisDriver.Available ? SiteArchetypes.Allowed(Usable().Select(d => d.ToCore()), CrisisDriver.Stage(component, empire).Stage) : [];

    /// <summary>The archetype to offer now: <paramref name="chosen"/> (a <c>Site:*</c> id the director picked) if it is
    /// allowed, else a weighted pick. Null when the crisis isn't on or no archetype is allowed.</summary>
    public static SiteArchetypeDef? Pick(RimChroniclesGameComponent component, Faction empire, string? chosen = null)
    {
        var allowed = Allowed(component, empire);
        var picked = allowed.FirstOrDefault(a => a.Id == chosen) ?? SiteArchetypes.Pick(allowed, Rand.Value);
        return picked is null ? null : Usable().FirstOrDefault(d => d.archetype == picked.Name);
    }
}
