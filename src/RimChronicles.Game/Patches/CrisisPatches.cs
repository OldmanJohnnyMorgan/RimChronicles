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
