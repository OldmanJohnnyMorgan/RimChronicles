using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Quests;

/// <summary>When a quest site's map is generated, spawns a specific pawn on it: a Hunt target among the camp's
/// defenders, or (when vanilla's prisoner site part is missing) a Rescue captive who joins the colony when a colonist
/// offers help.</summary>
public sealed class QuestPart_ChroniclesSpawnPawn : QuestPart
{
    public string inSignal = "";
    public Pawn? pawn;
    public MapParent? mapParent;
    public bool joinDefenders;
    public bool willJoinIfRescued;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag != inSignal || pawn is null || pawn.Dead || pawn.Spawned || mapParent?.Map is not { } map)
        {
            return;
        }

        var lord = joinDefenders ? map.lordManager.lords.FirstOrDefault(l => l.faction == pawn.Faction && l.ownedPawns.Count > 0) : null;
        var anchor = lord?.ownedPawns.FirstOrDefault(p => p.Spawned)?.Position ?? map.Center;
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(anchor, map, 6), map);
        lord?.AddPawn(pawn);
        if (willJoinIfRescued)
        {
            pawn.mindState.WillJoinColonyIfRescued = true;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal", "");
        Scribe_References.Look(ref pawn, "pawn");
        Scribe_References.Look(ref mapParent, "mapParent");
        Scribe_Values.Look(ref joinDefenders, "joinDefenders");
        Scribe_Values.Look(ref willJoinIfRescued, "willJoinIfRescued");
    }
}
