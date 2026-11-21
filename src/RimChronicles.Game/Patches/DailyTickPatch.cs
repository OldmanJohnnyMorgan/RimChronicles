using HarmonyLib;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Patches;

/// <summary>Spike test 2: a Harmony postfix on the game tick that fires once per in-game day. Later stages hang
/// the plot director's daily loop here.</summary>
[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
public static class DailyTickPatch
{
    public static void Postfix(TickManager __instance)
    {
        var ticks = __instance.TicksGame;
        if (ticks > 0 && ticks % GenDate.TicksPerDay == 0)
        {
            RimChroniclesGameComponent.Current?.OnNewDay(ticks / GenDate.TicksPerDay);
        }
    }
}
