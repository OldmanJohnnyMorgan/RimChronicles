using HarmonyLib;
using RimChronicles.Game.News;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Patches;

// Stage 2 M5: patches that follow the reporter pawn. Each one returns at once for any other pawn.

/// <summary>Who killed the reporter. The prefix records whether they were the colony's prisoner or recruit before the
/// game changes their state; the postfix classifies the death.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class ReporterKilledPatch
{
    public static void Prefix(Pawn __instance, out (bool IsReporter, bool Held, bool Recruited) __state)
    {
        var isReporter = RimChroniclesGameComponent.Current?.ReporterPawn is { } reporter && reporter == __instance;
        __state = (isReporter, isReporter && __instance.IsPrisonerOfColony, isReporter && __instance.Faction == Faction.OfPlayer);
    }

    public static void Postfix(Pawn __instance, DamageInfo? dinfo, (bool IsReporter, bool Held, bool Recruited) __state)
    {
        if (!__state.IsReporter || !__instance.Dead || RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        bool? killerIsColony = dinfo?.Instigator is { } killer ? killer.Faction == Faction.OfPlayer : null;
        ReporterLife.Died(component, __instance, killerIsColony, __state.Held, __state.Recruited);
    }
}

/// <summary>Remembers when the colony last hurt the reporter, so a later bleed-out still counts as the colony's kill.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
public static class ReporterHarmedPatch
{
    public static void Postfix(Pawn __instance, DamageInfo dinfo)
    {
        if (RimChroniclesGameComponent.Current is { ReporterPawn: { } reporter } component && reporter == __instance
            && dinfo.Instigator?.Faction == Faction.OfPlayer)
        {
            component.Desk.Reporter.LastColonyHarmTick = Find.TickManager.TicksGame;
        }
    }
}

/// <summary>Keeps the living reporter from being cleaned up as an unimportant world pawn. Spawning a world pawn removes
/// it from the game's keep-forever set, and leaving the map passes it back with the default discard rule.</summary>
[HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.PassToWorld))]
public static class ReporterKeptPatch
{
    public static void Postfix(WorldPawns __instance, Pawn pawn)
    {
        if (RimChroniclesGameComponent.Current?.ReporterPawn is { } reporter && reporter == pawn && !pawn.Dead
            && __instance.Contains(pawn))
        {
            __instance.ForcefullyKeptPawns.Add(pawn);
        }
    }
}
