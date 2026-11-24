using RimChronicles.Core.Powers;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Powers.Summons;

/// <summary>
/// What the summons does to a pawn (War in Heaven W6, spec §6.1, D7, D8; ENGINE_NOTES "War in Heaven survey"). The
/// Empire turns a mechanitor into a psycaster of the matching level: its mechs are deactivated where they stand (vanilla
/// <c>CompMechanoid.Deactivate</c>, so they never go feral), the mechlink comes off and the psylink goes on. The Hollow
/// Choir does the reverse: the psylink and its psycasts come off (removing the psylink leaves its psycasts, so they go
/// one by one), and a mechlink goes on with the matching bandwidth. Main thread only.
/// </summary>
public static class SummonsConversion
{
    public const string ChoirBandwidthDef = "RimChronicles_ChoirBandwidth";

    public static bool IsMechanitor(Pawn pawn) => MechanitorUtility.IsMechanitor(pawn);

    public static bool IsPsycaster(Pawn pawn) => pawn.GetPsylinkLevel() > 0;

    /// <summary>The pawn's own bandwidth: everything but tuned band nodes, which stay at the colony.</summary>
    public static int OwnBandwidth(Pawn pawn)
    {
        var bandNodes = pawn.health.hediffSet.hediffs.OfType<Hediff_BandNode>().Sum(h => h.AdditionalBandwidth);
        var total = pawn.mechanitor?.TotalBandwidth ?? (int)pawn.GetStatValue(StatDefOf.MechBandwidth);
        return Conversion.OwnBandwidth(total, bandNodes);
    }

    /// <summary>Converts the pawn for <paramref name="power"/> and returns what they became, for the letter.</summary>
    public static string Convert(Pawn pawn, Power power) =>
        power == Power.Empire ? ToPsycaster(pawn) : power == Power.Choir ? ToMechanitor(pawn) : "";

    public static string ToPsycaster(Pawn pawn)
    {
        var level = Conversion.PsylinkLevelFor(OwnBandwidth(pawn));
        DeactivateMechs(pawn);
        foreach (var link in pawn.health.hediffSet.hediffs.Where(h => h.def == HediffDefOf.MechlinkImplant || h.def.defName == ChoirBandwidthDef).ToList())
        {
            pawn.health.RemoveHediff(link);
        }

        SetPsylink(pawn, Math.Max(level, pawn.GetPsylinkLevel()));
        return $"a psycaster of level {pawn.GetPsylinkLevel()}";
    }

    public static string ToMechanitor(Pawn pawn)
    {
        var level = Math.Max(1, pawn.GetPsylinkLevel());
        if (pawn.GetMainPsylinkSource() is { } psylink)
        {
            pawn.health.RemoveHediff(psylink);
        }

        foreach (var psycast in pawn.abilities?.abilities.Where(a => a.def.IsPsycast).Select(a => a.def).ToList() ?? [])
        {
            pawn.abilities!.RemoveAbility(psycast);
        }

        if (!MechanitorUtility.IsMechanitor(pawn) && pawn.health.hediffSet.GetBrain() is { } brain)
        {
            pawn.health.AddHediff(HediffDefOf.MechlinkImplant, brain);
        }

        if (DefDatabase<HediffDef>.GetNamedSilentFail(ChoirBandwidthDef) is { } tuning && Conversion.BonusBandwidthFor(level) > 0)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(tuning) ?? pawn.health.AddHediff(tuning);
            hediff.Severity = level;
        }

        return $"a mechanitor with {Conversion.BandwidthFor(level)} bandwidth";
    }

    /// <summary>The mechs this mechanitor oversees are deactivated where they stand, for the colony to dismantle.</summary>
    public static int DeactivateMechs(Pawn pawn)
    {
        var mechs = pawn.mechanitor?.OverseenPawns.ToList() ?? [];
        foreach (var mech in mechs)
        {
            mech.TryGetComp<CompMechanoid>()?.Deactivate();
        }

        return mechs.Count;
    }

    private static void SetPsylink(Pawn pawn, int level)
    {
        if (pawn.GetMainPsylinkSource() is null)
        {
            pawn.ChangePsylinkLevel(1, sendLetter: false); // adds the psylink at level 1
        }

        if (pawn.GetMainPsylinkSource() is { } psylink && psylink.level < level)
        {
            psylink.ChangeLevel(level - psylink.level, sendLetter: false);
        }
    }
}
