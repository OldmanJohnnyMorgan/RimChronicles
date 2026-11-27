using HarmonyLib;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Patches;

/// <summary>A Harmony postfix on the game tick that runs the daily work once per in-game day. It compares day numbers
/// rather than waiting for the exact tick a day starts on, so a jump in the clock (a dev time-skip, anything that sets
/// the tick counter) still counts as a new day. Each daily step is guarded on its own; this guard only catches a
/// failure around them, so nothing escapes into the tick.</summary>
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class DailyTickPatch
{
    public static void Postfix(TickManager __instance)
    {
        try
        {
            RimChroniclesGameComponent.Current?.CheckDay(__instance.TicksGame / GenDate.TicksPerDay);
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(DailyTickPatch), ex);
        }
    }
}
