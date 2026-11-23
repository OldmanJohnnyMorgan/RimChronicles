using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Captives;

[DefOf]
public static class RetrievalDefs
{
    public static DutyDef RimChronicles_Retrieve = null!;

    static RetrievalDefs() => DefOfHelper.EnsureInitializedInCtor(typeof(RetrievalDefs));
}

/// <summary>
/// Retrieval's main phase (crisis C10): for a captive, the soldiers fight their way to them (digging through walls
/// where they must) while psykers and Heralds keep to a Praetorian; the lord frees the captive once a soldier stands
/// beside them. For a defector, it's an assault with every gun on them.
/// </summary>
public sealed class LordToil_RcRetrieve : LordToil
{
    private const float EscortRadius = 5f;

    public override bool AllowSatisfyLongNeeds => false;

    private Pawn? Target => (lord.LordJob as LordJob_RcOperation)?.target;

    public override void UpdateAllDuties()
    {
        var target = Target;
        var captive = target is { IsPrisonerOfColony: true };
        var praetorians = lord.ownedPawns.Where(p => UnitKit.Unit(p.kindDef)?.role == UnitRole.Praetorian && !p.Downed).ToList();
        var next = 0;
        foreach (var pawn in lord.ownedPawns.Where(p => p.mindState is not null))
        {
            var role = UnitKit.Unit(pawn.kindDef)?.role;
            if (role is UnitRole.Adept or UnitRole.Herald or UnitRole.Stellarch && praetorians.Count > 0)
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.Escort, praetorians[next++ % praetorians.Count], EscortRadius);
            }
            else if (captive && target is not null)
            {
                pawn.mindState.duty = new PawnDuty(RetrievalDefs.RimChronicles_Retrieve, target);
            }
            else
            {
                pawn.mindState.duty = new PawnDuty(CrisisDefs.RimChronicles_Assault);
            }
        }
    }

    /// <summary>Duties follow the captive as they're moved, and guards are replaced as they fall.</summary>
    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % 250 == 0)
        {
            UpdateAllDuties();
        }
    }
}

/// <summary>Walks up to the captive when there's a way in without breaking anything; otherwise the sapper after it
/// digs one.</summary>
public sealed class JobGiver_RcReachCaptive : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (pawn.mindState.duty?.focus.Pawn is not { Spawned: true } captive || captive.Map != pawn.Map
            || pawn.Position.DistanceTo(captive.Position) <= LordJob_RcOperation.FreeReach)
        {
            return null;
        }

        return pawn.CanReach(captive, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, canBashFences: true, TraverseMode.NoPassClosedDoors)
            ? JobMaker.MakeJob(JobDefOf.Goto, captive.Position, 500, checkOverrideOnExpiry: true)
            : null;
    }
}
