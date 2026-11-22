using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimChronicles.Game.Crisis.Operations;

/// <summary>
/// A hostile crisis Breacher with EMP grenades throws them at the nearest working colony turret it can hit (crisis spec
/// §5.1: "knocks out turrets"). Inserted into every humanlike think tree like the psyker node, so it works under
/// vanilla's breaching duties too; it does nothing for anyone else. Checked at most every 120 ticks per pawn.
/// </summary>
public sealed class JobGiver_RcEmpTurret : ThinkNode_JobGiver
{
    private const int CheckTicks = 120;
    private const float Reach = 30f;

    private static readonly Dictionary<int, int> NextCheck = [];

    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Downed || pawn.InMentalState || pawn.Faction is not { } faction || faction == Faction.OfPlayer
            || UnitKit.Unit(pawn.kindDef)?.role != UnitRole.Breacher
            || pawn.equipment?.PrimaryEq?.PrimaryVerb is not { } verb || verb.GetDamageDef() != DamageDefOf.EMP)
        {
            return null;
        }

        var now = Find.TickManager.TicksGame;
        if (NextCheck.TryGetValue(pawn.thingIDNumber, out var next) && now < next)
        {
            return null;
        }

        NextCheck[pawn.thingIDNumber] = now + CheckTicks;
        if (NextCheck.Count > 500)
        {
            NextCheck.Clear();
        }

        var turret = pawn.Map.listerBuildings.allBuildingsColonist.OfType<Building_Turret>()
            .Where(t => t.HostileTo(faction) && !t.IsBrokenDown() && t.GetComp<CompStunnable>()?.StunHandler is not { Stunned: true }
                && t.Position.DistanceTo(pawn.Position) <= Reach)
            .OrderBy(t => t.Position.DistanceTo(pawn.Position))
            .FirstOrDefault(t => verb.CanHitTarget(t));
        if (turret is null)
        {
            return null;
        }

        var job = JobMaker.MakeJob(JobDefOf.AttackStatic, turret);
        job.maxNumStaticAttacks = 2;
        job.expiryInterval = 600;
        return job;
    }
}
