using HarmonyLib;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Game.Plots;
using Verse;

namespace RimChronicles.Game.Patches;

// Stage 3 M2: patches that record grudges for the Nemesis arc. Both only read game state.

/// <summary>A colonist killed by a raider who could become a nemesis.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class GrudgeKillPatch
{
    public static void Prefix(Pawn __instance, out bool __state) => __state = __instance.IsColonist;

    public static void Postfix(Pawn __instance, DamageInfo? dinfo, bool __state)
    {
        if (__state && __instance.Dead && dinfo?.Instigator is Pawn killer && Current.ProgramState == ProgramState.Playing
            && RimChroniclesGameComponent.Current is { } component && NemesisTracker.CanBeNemesis(killer, __instance))
        {
            NemesisTracker.Record(component, killer, __instance, GrudgeKind.Killed, "");
        }
    }
}

/// <summary>A colonist losing a body part at least as big as a hand to a raider. The game adds a missing part as a
/// hediff with the damage that caused it.</summary>
// AddHediff has two overloads in 1.7 (Hediff and HediffDef); name the Hediff one exactly.
[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
    [typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult)])]
public static class GrudgeMaimPatch
{
    public static void Postfix(Pawn ___pawn, Hediff hediff, BodyPartRecord? part, DamageInfo? dinfo)
    {
        if (hediff is Hediff_MissingPart && part is not null && ___pawn is { IsColonist: true, Dead: false }
            && part.coverageAbsWithChildren >= NemesisTracker.HandCoverage
            && dinfo?.Instigator is Pawn attacker && Current.ProgramState == ProgramState.Playing
            && RimChroniclesGameComponent.Current is { } component && NemesisTracker.CanBeNemesis(attacker, ___pawn))
        {
            NemesisTracker.Record(component, attacker, ___pawn, GrudgeKind.Maimed, part.LabelShort);
        }
    }
}

/// <summary>A nemesis dying, by any hand: ends their plot (the killer decides the ending and payoff).</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class NemesisKilledPatch
{
    public static void Postfix(Pawn __instance, DamageInfo? dinfo)
    {
        if (!__instance.Dead || RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var id = __instance.ThingID;
        if (component.Plots.Active.FirstOrDefault(p => p.Template == NemesisPlot.TemplateId && p.Fact(NemesisPlot.NemesisId) == id) is { } plot)
        {
            NemesisBeats.NemesisDied(component, plot, dinfo?.Instigator);
        }
    }
}
