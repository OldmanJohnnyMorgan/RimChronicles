using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>Completes once every living escortee is at the destination: in a caravan on its tile, or on its map.
/// Checked every few seconds of game time while the quest is under way.</summary>
public sealed class QuestPart_ChroniclesEscortArrival : QuestPartActivable
{
    private const int CheckIntervalTicks = 250;

    public List<Pawn> pawns = [];
    public Settlement? destination;

    public override IEnumerable<GlobalTargetInfo> QuestLookTargets
    {
        get
        {
            if (destination is not null)
            {
                yield return destination;
            }
        }
    }

    public override void QuestPartTick()
    {
        base.QuestPartTick();
        if (Find.TickManager.TicksGame % CheckIntervalTicks != 0 || destination is null || destination.Destroyed)
        {
            return;
        }

        var living = pawns.Where(p => p is { Dead: false }).ToList();
        if (living.Count > 0 && living.All(Arrived))
        {
            Complete();
        }
    }

    private bool Arrived(Pawn pawn) =>
        pawn.GetCaravan() is { } caravan ? caravan.Tile == destination!.Tile : pawn.MapHeld?.Parent == destination;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
        Scribe_References.Look(ref destination, "destination");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            pawns ??= [];
            pawns.RemoveAll(p => p is null);
        }
    }
}
