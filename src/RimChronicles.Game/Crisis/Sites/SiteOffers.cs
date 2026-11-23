using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Game.Crisis.Pieces;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>Which Imperial site the Empire plot's sabotage job offers (crisis C9): a weighted pick among the archetypes
/// the crisis stage allows, until the director picks it (C11, tech spec R7). Main thread only.</summary>
public static class SiteOffers
{
    /// <summary>All archetypes whose site part loaded and whose pieces exist.</summary>
    public static List<SiteArchetypeDef> Usable() => DefDatabase<SiteArchetypeDef>.AllDefsListForReading
        .Where(d => d.sitePart is not null && d.objectiveThing is not null && PieceLibrary.All.Any(p => p.Source.Meta.Tags.Contains(d.objectiveTag)))
        .ToList();

    /// <summary>The archetype to offer now, or null when the crisis isn't on or no archetype is allowed.</summary>
    public static SiteArchetypeDef? Pick(RimChroniclesGameComponent component, Faction empire)
    {
        if (!CrisisDriver.Available)
        {
            return null;
        }

        var stage = CrisisDriver.Stage(component, empire).Stage;
        var defs = Usable();
        var allowed = SiteArchetypes.Allowed(defs.Select(d => d.ToCore()), stage);
        return SiteArchetypes.Pick(allowed, Rand.Value) is { } picked ? defs.FirstOrDefault(d => d.archetype == picked.Name) : null;
    }
}
