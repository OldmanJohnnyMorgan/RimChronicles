using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>When the target settlement's map is generated, places the Sabotage objective near its centre, owned by the
/// settlement's faction and tagged so its destruction signals the quest.</summary>
public sealed class QuestPart_ChroniclesSpawnObjective : QuestPart
{
    public string inSignal = "";
    public MapParent? mapParent;
    public ThingDef? thingDef;
    public string tag = "";

    public override IEnumerable<GlobalTargetInfo> QuestLookTargets
    {
        get
        {
            if (mapParent is not null)
            {
                yield return mapParent;
            }
        }
    }

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag != inSignal || thingDef is null || mapParent?.Map is not { } map)
        {
            return;
        }

        if (!CellFinder.TryFindRandomCellNear(map.Center, map, 25, c => Fits(c, map), out var cell)
            && !CellFinder.TryFindRandomCell(map, c => Fits(c, map), out cell))
        {
            Log.Warning($"[Rim Chronicles] No room for the Sabotage objective on {mapParent.Label}.");
            return;
        }

        var thing = ThingMaker.MakeThing(thingDef, thingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(thingDef) : null);
        thing.SetFaction(mapParent.Faction);
        QuestUtility.AddQuestTag(ref thing.questTags, tag);
        GenSpawn.Spawn(thing, cell, map, Rot4.North);
    }

    /// <summary>The footprint and a one-cell margin are in bounds, standable and free of buildings.</summary>
    private bool Fits(IntVec3 cell, Map map) =>
        GenAdj.OccupiedRect(cell, Rot4.North, thingDef!.size).ExpandedBy(1).All(c => c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) is null);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal", "");
        Scribe_References.Look(ref mapParent, "mapParent");
        Scribe_Defs.Look(ref thingDef, "thingDef");
        Scribe_Values.Look(ref tag, "tag", "");
    }
}
