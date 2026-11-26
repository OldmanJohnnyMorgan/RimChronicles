using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Game.Crisis.Captives;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Psykers;
using RimChronicles.Game.Crisis.Units;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>
/// The Choir's commander AI in game (War in Heaven W7, tech spec §4.3): each mechanitor holds and shoots from the spot
/// Core's <see cref="CommanderTactics"/> picks (behind its mechs, falling back when threatened), refreshed every 250
/// ticks; mechs assault with focus fire, or go for the captive in a retrieval. Main thread only.
/// </summary>
public static class ChoirCommand
{
    /// <summary>How close to its spot a commander holds (its duty radius).</summary>
    public const float HoldRadius = 3f;

    public static void UpdateDuties(Lord lord, LordJob_RcOperation job, Pawn? captive)
    {
        if (lord.Map is not { } map || lord.faction is not { } faction)
        {
            return;
        }

        var (view, _) = BattleViewBuilder.For(map, faction);
        var rally = new Cell(job.staging.x, job.staging.z);
        foreach (var pawn in lord.ownedPawns.Where(p => p.mindState is not null && p.Spawned))
        {
            if (ChoirRoster.IsMechanitor(UnitKit.Unit(pawn.kindDef)?.role))
            {
                var mechs = job.MechsOf(pawn).Select(m => m.thingIDNumber).ToHashSet();
                var spot = view.Find(pawn.thingIDNumber) is { } unit ? CommanderTactics.Position(view, unit, mechs, rally) : null;
                var focus = spot is { } s ? new IntVec3(s.X, 0, s.Z)
                    : pawn.mindState.duty?.def == CrisisDefs.RimChronicles_Command && pawn.mindState.duty.focus.Cell.IsValid ? pawn.mindState.duty.focus.Cell
                    : pawn.Position;
                pawn.mindState.duty = new PawnDuty(CrisisDefs.RimChronicles_Command, focus, HoldRadius);
            }
            else if (captive is not null)
            {
                pawn.mindState.duty = new PawnDuty(RetrievalDefs.RimChronicles_Retrieve, captive);
            }
            else
            {
                pawn.mindState.duty = new PawnDuty(CrisisDefs.RimChronicles_Assault);
            }
        }
    }
}
