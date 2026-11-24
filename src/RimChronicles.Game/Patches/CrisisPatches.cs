using HarmonyLib;
using RimChronicles.Game.Crisis.Units;
using Verse;

namespace RimChronicles.Game.Patches;

// Crisis C3: crisis units keep exactly their kit.

/// <summary>A crisis unit loaded from a save gets its kit re-checked, so a save never keeps a random or banned psycast
/// (tech spec R10), whatever changed since it was saved.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
public static class CrisisUnitLoadPatch
{
    public static void Postfix(Pawn __instance)
    {
        if (Scribe.mode == LoadSaveMode.PostLoadInit && UnitKit.Unit(__instance.kindDef) is not null)
        {
            UnitKit.Enforce(__instance);
        }
    }
}

// Crisis C4: only the psyker AI's scorers decide what a crisis unit casts.

/// <summary>Vanilla AI casts whatever <c>AICastableAbilities</c> offers (<c>JobGiver_AIFightEnemy</c>,
/// <c>JobGiver_NPCCastNonCombatAbilities</c>). No Royalty psycast is AI-usable (C0), but weapon-trait and gene
/// abilities can be, and other mods can change that, so crisis units get nothing from it (tech spec R9).</summary>
[HarmonyPatch(typeof(RimWorld.Pawn_AbilityTracker), nameof(RimWorld.Pawn_AbilityTracker.AICastableAbilities))]
public static class CrisisNoVanillaCastingPatch
{
    public static bool Prefix(RimWorld.Pawn_AbilityTracker __instance, ref List<RimWorld.Ability> __result)
    {
        if (UnitKit.Unit(__instance.pawn?.kindDef) is null || __instance.pawn!.Faction == RimWorld.Faction.OfPlayer)
        {
            return true;
        }

        __result = [];
        return false;
    }
}

/// <summary>Every psycast a crisis unit casts is checked against what its scorers chose (crisis spec §9).</summary>
[HarmonyPatch(typeof(RimWorld.Psycast), nameof(RimWorld.Psycast.Activate), [typeof(LocalTargetInfo), typeof(LocalTargetInfo)])]
public static class CrisisPsycastCastPatch
{
    public static void Prefix(RimWorld.Psycast __instance)
    {
        if (__instance.pawn is { } pawn && pawn.Faction != RimWorld.Faction.OfPlayer && UnitKit.Unit(pawn.kindDef) is not null)
        {
            Crisis.Psykers.PsykerLog.Cast(pawn, __instance.def.defName);
        }
    }
}
