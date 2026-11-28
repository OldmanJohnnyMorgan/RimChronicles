using RimWorld;
using Verse;
using Verse.AI;

namespace RimChronicles.Game.Powers.Summons;

/// <summary>
/// Dismantle a deactivated mech (War in Heaven W6, spec D8): any colonist can take apart a mech the summons (or, from W7,
/// a fallen Choir mechanitor) left deactivated. Vanilla's <c>JobDriver_DisassembleMech</c> has no overseer check; only
/// its own float menu insists on the mechanitor (ENGINE_NOTES, "War in Heaven survey"), so this queues the vanilla job.
/// </summary>
public sealed class FloatMenuOptionProvider_RcDismantle : FloatMenuOptionProvider
{
    protected override bool Drafted => true;

    protected override bool Undrafted => true;

    protected override bool Multiselect => false;

    protected override bool RequiresManipulation => true;

    protected override bool AppliesInt(FloatMenuContext context) =>
        ModsConfig.BiotechActive && context.FirstSelectedPawn is { IsColonist: true };

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
    {
        if (!clickedPawn.RaceProps.IsMechanoid || !clickedPawn.IsDeactivated())
        {
            yield break;
        }

        var worker = context.FirstSelectedPawn;
        if (!worker.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
        {
            yield return new FloatMenuOption("DisassembleMech".Translate(clickedPawn.LabelCap) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
            yield break;
        }

        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisassembleMech".Translate(clickedPawn.LabelCap), () =>
            worker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.DisassembleMech, clickedPawn), JobTag.Misc)), worker, new LocalTargetInfo(clickedPawn));
    }
}
