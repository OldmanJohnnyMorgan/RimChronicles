using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>Points a site quest's "look at" targets at the camp's tile from the moment it is offered. Vanilla places
/// the site only when the quest is accepted, so before that the only target was the reward drop at home. Only the
/// tile is saved: an unspawned site can't be saved as a reference, so the site is found through the quest's own
/// spawn part.</summary>
public sealed class QuestPart_ChroniclesSiteMarker : QuestPart
{
    public PlanetTile tile;

    public override IEnumerable<GlobalTargetInfo> QuestLookTargets
    {
        get
        {
            foreach (var target in base.QuestLookTargets)
            {
                yield return target;
            }

            var site = quest?.PartsListForReading.OfType<QuestPart_SpawnWorldObject>().Select(p => p.worldObject).FirstOrDefault(w => w is Site);
            yield return site is { Spawned: true } ? site : new GlobalTargetInfo(tile);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref tile, "tile");
    }
}
