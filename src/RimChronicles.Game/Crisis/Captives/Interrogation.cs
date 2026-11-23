using RimChronicles.Core.Crisis.Captives;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimChronicles.Game.Crisis.Captives;

[DefOf]
public static class CaptiveDefs
{
    [MayRequireRoyalty]
    public static PrisonerInteractionModeDef RimChronicles_Interrogate = null!;

    [MayRequireRoyalty]
    public static JobDef RimChronicles_InterrogateJob = null!;

    [MayRequireRoyalty]
    public static InteractionDef RimChronicles_InterrogateInteraction = null!;

    static CaptiveDefs() => DefOfHelper.EnsureInitializedInCtor(typeof(CaptiveDefs));
}

/// <summary>
/// A warden interrogates an Imperial prisoner with the interrogate box ticked (crisis C10, modelled on vanilla's identity
/// interrogation): when the prisoner is due a warden visit. Prisoners who know nothing of the Empire, or have told all
/// they know, are skipped with the reason (tech spec R21).
/// </summary>
public sealed class WorkGiver_RcInterrogate : WorkGiver_Warden
{
    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!ShouldTakeCareOfPrisoner(pawn, t, forced) || t is not Pawn prisoner || !prisoner.guest.IsInteractionEnabled(CaptiveDefs.RimChronicles_Interrogate))
        {
            return false;
        }

        if (CaptiveRules.NothingToTell(CaptiveOps.RankOf(prisoner), CaptiveOps.State?.Captive(prisoner.ThingID)) is { } why)
        {
            JobFailReason.Is($"{prisoner.LabelShort} {why}");
            return false;
        }

        if (prisoner.InMentalState)
        {
            JobFailReason.Is("PawnIsInMentalState".Translate(prisoner));
            return false;
        }

        if (!prisoner.guest.ScheduledForInteraction)
        {
            JobFailReason.Is("PrisonerInteractedTooRecently".Translate());
            return false;
        }

        return base.HasJobOnThing(pawn, t, forced);
    }

    public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!ShouldTakeCareOfPrisoner(pawn, t, forced) || t is not Pawn prisoner)
        {
            return null;
        }

        return prisoner.guest.IsInteractionEnabled(CaptiveDefs.RimChronicles_Interrogate) && prisoner.guest.ScheduledForInteraction && prisoner.guest.IsPrisoner
            && (!prisoner.Downed || prisoner.InBed()) && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking) && pawn.CanReserve(t) && prisoner.Awake()
            ? JobMaker.MakeJob(CaptiveDefs.RimChronicles_InterrogateJob, t)
            : null;
    }
}

/// <summary>As vanilla's <c>JobDriver_InterrogatePrisoner</c>: one session, then the prisoner's visit is used up.</summary>
public sealed class JobDriver_RcInterrogate : JobDriver
{
    private Pawn Prisoner => (Pawn)job.targetA.Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOnMentalState(TargetIndex.A);
        this.FailOnNotAwake(TargetIndex.A);
        this.FailOn(() => !Prisoner.IsPrisonerOfColony || !Prisoner.guest.PrisonerIsSecure);
        yield return Toils_Interpersonal.GotoPrisoner(pawn, Prisoner, CaptiveDefs.RimChronicles_Interrogate);
        yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
        yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
        var talk = ToilMaker.MakeToil("RimChronicles_Interrogate");
        talk.initAction = () =>
        {
            if (!pawn.interactions.TryInteractWith(Prisoner, CaptiveDefs.RimChronicles_InterrogateInteraction))
            {
                pawn.jobs.curDriver.ReadyForNextToil();
            }
        };
        talk.socialMode = RandomSocialMode.Off;
        talk.defaultCompleteMode = ToilCompleteMode.Delay;
        talk.defaultDuration = 700;
        talk.activeSkill = () => SkillDefOf.Social;
        yield return talk;
        yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
    }
}

/// <summary>One session (Core's <see cref="CaptiveRules.Session"/>): wears down resistance and will, and with luck
/// gives up an intel item.</summary>
public sealed class InteractionWorker_RcInterrogate : InteractionWorker
{
    public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string? letterText, out string? letterLabel,
        out LetterDef? letterDef, out LookTargets? lookTargets)
    {
        letterText = null;
        letterLabel = null;
        letterDef = null;
        lookTargets = null;
        if (CaptiveOps.Record(recipient) is not { } record)
        {
            return;
        }

        var rank = record.Rank;
        var result = CaptiveRules.Session(rank, record, IntelDef.All, initiator.GetStatValue(StatDefOf.SocialImpact), Rand.Value, Rand.Value);
        if (recipient.guest is { } guest)
        {
            guest.resistance = Math.Max(0, guest.resistance - result.ResistanceLoss);
            guest.will = Math.Max(0, guest.will - result.WillLoss);
        }

        if (result.Intel is { } intel)
        {
            CaptiveOps.Learn(intel, $"{initiator.LabelShort} got {recipient.LabelShort}, an Imperial {CaptiveOps.Rank(rank)}, to talk.", new LookTargets(recipient));
        }
    }
}

/// <summary>Reading an Imperial dispatch from a site's records room gives one intel item, of any kind.</summary>
public sealed class CompUseEffect_ReadRecords : CompUseEffect
{
    public override void DoEffect(Pawn usedBy)
    {
        base.DoEffect(usedBy);
        var all = IntelDef.All.Where(i => i.Effect != IntelEffect.CitadelPlans || Rand.Chance(0.25f)).ToList();
        if (CaptiveRules.WeightedPick(all, Rand.Value) is { } intel)
        {
            CaptiveOps.Learn(intel, $"{usedBy.LabelShort} read an Imperial dispatch.", new LookTargets(usedBy));
        }
    }
}
