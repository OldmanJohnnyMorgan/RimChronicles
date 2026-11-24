using RimChronicles.Core.Powers;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Game.Crisis.Pieces;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>Which Imperial site the Empire plot's sabotage job offers (crisis C9): the archetype the director picked in a
/// second call (C11, tech spec R7), else a weighted pick among those the crisis stage allows. Main thread only.</summary>
public static class SiteOffers
{
    /// <summary>All archetypes whose site part loaded and whose pieces exist.</summary>
    public static List<SiteArchetypeDef> Usable() => DefDatabase<SiteArchetypeDef>.AllDefsListForReading
        .Where(d => d.power == Core.Powers.Power.Empire.Id) // the Choir's sites come with its crisis (War in Heaven W8)
        .Where(d => d.sitePart is not null && d.objectiveThing is not null && PieceLibrary.All.Any(p => p.Source.Meta.Tags.Contains(d.objectiveTag)))
        .ToList();

    /// <summary>How far from the colony a conquered settlement may be to offer a Reclaimed Holding, in world tiles.</summary>
    public const float MaxHoldingTiles = 30;

    /// <summary>The archetypes the crisis stage allows now; empty when the crisis isn't on. Reclaimed Holding only while
    /// <see cref="Holding"/> finds a settlement.</summary>
    public static IReadOnlyList<SiteArchetype> Allowed(RimChroniclesGameComponent component, Faction empire) =>
        CrisisDriver.Available
            ? SiteArchetypes.Allowed(Usable().Select(d => d.ToCore()), CrisisDriver.Stage(component, empire).Stage, Holding(component, empire) is not null)
            : [];

    /// <summary>The nearest settlement the Empire conquered (in its saga) and still holds, within
    /// <see cref="MaxHoldingTiles"/> of the colony, with its former owner still standing; no map open and no quest on it.
    /// Null if there is none.</summary>
    public static (Settlement Settlement, Faction Former)? Holding(RimChroniclesGameComponent component, Faction empire)
    {
        if (Find.AnyPlayerHomeMap?.Tile is not { } home)
        {
            return null;
        }

        var questTargets = Find.QuestManager.QuestsListForReading
            .Where(q => q.State is QuestState.Ongoing or QuestState.NotYetAccepted)
            .SelectMany(q => q.QuestLookTargets).Where(t => t.HasWorldObject).Select(t => t.WorldObject).ToHashSet();
        var conquered = component.Empire.Expansions.Where(e => e.Result == Core.Powers.ExpansionResult.Conquered)
            .GroupBy(e => e.Settlement).ToDictionary(g => g.Key, g => g.OrderBy(e => e.Day).Last().Faction);
        return Find.WorldObjects.Settlements
            .Where(s => s.Faction == empire && !s.HasMap && !questTargets.Contains(s) && s.Tile.Layer == home.Layer
                        && conquered.ContainsKey(s.Label) && Find.WorldGrid.ApproxDistanceInTiles(home, s.Tile) <= MaxHoldingTiles)
            .Select(s => (Settlement: s, Former: Engine.StoryLetters.FactionNamed(conquered[s.Label])))
            .Where(x => x.Former is { defeated: false })
            .OrderBy(x => Find.WorldGrid.ApproxDistanceInTiles(home, x.Settlement.Tile))
            .Select(x => ((Settlement, Faction)?)(x.Settlement, x.Former!))
            .FirstOrDefault();
    }

    /// <summary>The archetype to offer now: <paramref name="chosen"/> (a <c>Site:*</c> id the director picked) if it is
    /// allowed, else a weighted pick. Null when the crisis isn't on or no archetype is allowed.</summary>
    public static SiteArchetypeDef? Pick(RimChroniclesGameComponent component, Faction empire, string? chosen = null)
    {
        var allowed = Allowed(component, empire);
        var picked = allowed.FirstOrDefault(a => a.Id == chosen) ?? SiteArchetypes.Pick(allowed, Rand.Value);
        return picked is null ? null : Usable().FirstOrDefault(d => d.archetype == picked.Name);
    }
}
