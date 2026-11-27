using HarmonyLib;
using RimChronicles.Game.Engine;
using Verse;

namespace RimChronicles.Game.Patches;

/// <summary>Quitting to the main menu ends the mod's game (code review fix 8): background work stops and the old game's
/// component, with every pawn it tracks, no longer stays referenced until the next game starts. Guarded (fix 3).</summary>
[HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
public static class GameEndedPatch
{
    public static void Prefix()
    {
        try
        {
            RimChroniclesGameComponent.Current?.End();
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(GameEndedPatch), ex);
        }
    }
}
