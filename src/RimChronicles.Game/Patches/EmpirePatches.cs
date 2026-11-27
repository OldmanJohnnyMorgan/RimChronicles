using HarmonyLib;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Patches;

// Stage 5 M1: the awakening signs that happen once. Both only read game state and record a sign for the Empire.
// Every body is guarded (code review fix 3).

/// <summary>The player defeats an Imperial settlement: <c>CheckDefeated</c> destroys it when its map is cleared.</summary>
[HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
public static class ImperialSiteDefeatedPatch
{
    public static void Prefix(Settlement factionBase, out bool __state)
    {
        __state = false;
        try
        {
            __state = factionBase is { Destroyed: false } && EmpireDriver.EmpireFaction is { } empire && factionBase.Faction == empire;
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(ImperialSiteDefeatedPatch), ex);
        }
    }

    public static void Postfix(Settlement factionBase, bool __state)
    {
        try
        {
            if (__state && factionBase.Destroyed)
            {
                EmpireDriver.Notify_SiteDestroyed(factionBase.Label);
            }
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(ImperialSiteDefeatedPatch), ex);
        }
    }
}

/// <summary>A colonist renounces an Imperial title. The character card's renounce button calls <c>SetTitle(faction,
/// null, …)</c> with the default <c>sendLetter: true</c>; the game's own cleanup of inherited titles passes false.</summary>
[HarmonyPatch(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.SetTitle))]
public static class ImperialTitleRenouncedPatch
{
    public static void Prefix(Pawn_RoyaltyTracker __instance, Faction faction, RoyalTitleDef title, bool sendLetter)
    {
        try
        {
            if (title is null && sendLetter && Current.ProgramState == ProgramState.Playing && EmpireDriver.EmpireFaction is { } empire
                && faction == empire && __instance.pawn is { IsColonist: true } pawn && __instance.GetCurrentTitle(faction) is not null)
            {
                EmpireDriver.Notify_TitleRefused(pawn.LabelShort);
            }
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(ImperialTitleRenouncedPatch), ex);
        }
    }
}

/// <summary>A ship reactor starts up (<c>CompHibernatable.Startup</c>, which the reactor's start gizmo calls): the
/// awake Empire reacts (Stage 5 M5).</summary>
[HarmonyPatch(typeof(CompHibernatable), nameof(CompHibernatable.Startup))]
public static class ShipReactorStartupPatch
{
    public static void Postfix(CompHibernatable __instance)
    {
        try
        {
            if (Current.ProgramState == ProgramState.Playing && __instance.parent?.def == ThingDefOf.Ship_Reactor
                && __instance.State == HibernatableStateDefOf.Starting)
            {
                EmpireDriver.Notify_ShipStartup();
            }
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(ShipReactorStartupPatch), ex);
        }
    }
}
