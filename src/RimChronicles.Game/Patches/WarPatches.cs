using HarmonyLib;
using RimChronicles.Game.Powers;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Patches;

/// <summary>The Royal Ascent wakes the Hollow Choir (War in Heaven spec §5, W5). The ascent is Royalty's
/// <c>EndGame_RoyalAscent</c> quest, which has no <c>QuestScriptDefOf</c> field; <c>Quest.Accept</c> only acts while the
/// quest is not yet accepted, so the prefix notes that and the postfix fires on the change (ENGINE_NOTES, W0).</summary>
[HarmonyPatch(typeof(Quest), nameof(Quest.Accept))]
public static class RoyalAscentPatch
{
    public const string QuestDefName = "EndGame_RoyalAscent";

    public static void Prefix(Quest __instance, out bool __state) =>
        __state = __instance.State == QuestState.NotYetAccepted && __instance.root?.defName == QuestDefName;

    public static void Postfix(Quest __instance, bool __state)
    {
        if (__state && __instance.State != QuestState.NotYetAccepted && Current.ProgramState == ProgramState.Playing)
        {
            ChoirDriver.Notify_RoyalAscent();
        }
    }
}
