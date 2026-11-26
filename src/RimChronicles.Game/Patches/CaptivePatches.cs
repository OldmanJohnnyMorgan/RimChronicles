using HarmonyLib;
using RimChronicles.Game.Crisis;
using RimChronicles.Game.Crisis.Captives;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Patches;

// Crisis C10: the colony's Imperial captives. Each patch only records and reacts; none changes what vanilla does.

/// <summary>An Imperial taken prisoner is recorded, and the player hears how to question them.</summary>
[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.CapturedBy))]
public static class CaptiveCapturedPatch
{
    public static void Postfix(Pawn ___pawn, Faction by)
    {
        if (by == Faction.OfPlayer && CrisisDriver.Available && ___pawn is { } pawn)
        {
            CaptiveOps.Captured(pawn);
            Crisis.Finale.Stellarch.Fell(pawn, Core.Crisis.StellarchFate.Captured); // only acts on the High Stellarch (C13)
        }

        if (by == Faction.OfPlayer && ___pawn is { } taken && Crisis.Choir.FirstVoice.Is(taken))
        {
            Crisis.Choir.FirstVoice.Fell(taken, Core.Crisis.StellarchFate.Captured); // the First Voice (War in Heaven W9)
        }
    }
}

/// <summary>A captive recruited after interrogation becomes a defector.</summary>
[HarmonyPatch(typeof(RecruitUtility), nameof(RecruitUtility.Recruit))]
public static class CaptiveRecruitedPatch
{
    public static void Postfix(Pawn pawn, Faction faction)
    {
        if (faction == Faction.OfPlayer && CrisisDriver.Available)
        {
            CaptiveOps.Recruited(pawn);
        }
    }
}

/// <summary>Releasing an Imperial captive buys a little goodwill and a lighter next operation.</summary>
[HarmonyPatch(typeof(GenGuest), nameof(GenGuest.PrisonerRelease))]
public static class CaptiveReleasedPatch
{
    public static void Prefix(Pawn p, out bool __state) => __state = p.IsPrisonerOfColony;

    public static void Postfix(Pawn p, bool __state)
    {
        if (__state && CrisisDriver.Available)
        {
            CaptiveOps.Released(p);
        }
    }
}

[HarmonyPatch(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut))]
public static class CaptiveExecutedPatch
{
    public static void Postfix(Pawn victim)
    {
        if (CrisisDriver.Available)
        {
            CaptiveOps.Executed(victim);
        }
    }
}

[HarmonyPatch(typeof(GenGuest), nameof(GenGuest.EnslavePrisoner))]
public static class CaptiveEnslavedPatch
{
    public static void Postfix(Pawn prisoner)
    {
        if (CrisisDriver.Available)
        {
            CaptiveOps.Enslaved(prisoner);
        }
    }
}

/// <summary>Harvesting an organ from an Imperial captive (a violation vanilla already counts as one).</summary>
[HarmonyPatch(typeof(Recipe_RemoveBodyPart), nameof(Recipe_RemoveBodyPart.ApplyOnPawn))]
public static class CaptiveHarvestedPatch
{
    public static void Prefix(Recipe_RemoveBodyPart __instance, Pawn pawn, BodyPartRecord part, out bool __state) =>
        __state = pawn.IsPrisonerOfColony && __instance.IsViolationOnPawn(pawn, part, Faction.OfPlayer);

    public static void Postfix(Pawn pawn, bool __state)
    {
        if (__state && CrisisDriver.Available)
        {
            CaptiveOps.Harvested(pawn);
        }
    }
}
