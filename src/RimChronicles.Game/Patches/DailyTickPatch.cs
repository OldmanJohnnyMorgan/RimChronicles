using HarmonyLib;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Patches;

/// <summary>A Harmony postfix on the game tick that runs the daily work once per in-game day. It compares day numbers
/// rather than waiting for the exact tick a day starts on, so a jump in the clock (a dev time-skip, anything that sets
/// the tick counter) still counts as a new day. The plot director's daily loop hangs here in later stages.</summary>
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class DailyTickPatch
{
    public static void Postfix(TickManager __instance) =>
        RimChroniclesGameComponent.Current?.CheckDay(__instance.TicksGame / GenDate.TicksPerDay);
}
