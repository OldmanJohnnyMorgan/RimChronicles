using HarmonyLib;
using RimChronicles.Core.Crisis;
using RimChronicles.Game.Crisis.Finale;
using Verse;

namespace RimChronicles.Game.Patches;

// Crisis C13: the High Stellarch's fall.

/// <summary>The High Stellarch killed: announced once, and it carries the colony's assault on the capital.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class StellarchKilledPatch
{
    public static void Postfix(Pawn __instance)
    {
        if (__instance.Dead && Stellarch.Is(__instance))
        {
            Stellarch.Fell(__instance, StellarchFate.Dead);
        }

        if (__instance.Dead && Crisis.Choir.FirstVoice.Is(__instance))
        {
            Crisis.Choir.FirstVoice.Fell(__instance, StellarchFate.Dead); // the First Voice's fall (War in Heaven W9)
        }
    }
}
