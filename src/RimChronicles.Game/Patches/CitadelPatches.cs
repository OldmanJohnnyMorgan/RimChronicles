using HarmonyLib;
using RimChronicles.Game.Crisis.Sites;
using RimChronicles.Game.Engine;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Patches;

// Crisis C12: the Imperial Citadel. Both bodies are guarded (code review fix 3).

/// <summary>The Empire's capital generates as the Citadel, at the settings' size, while the crisis is at war (C0:
/// every map goes through <c>MapGenerator.GenerateMap</c>; the attack passes the world's default size).</summary>
[HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
public static class CitadelMapPatch
{
    public static void Prefix(ref IntVec3 mapSize, MapParent parent, ref MapGeneratorDef mapGenerator)
    {
        try
        {
            if (CitadelMaps.IsCitadel(parent) && CitadelMaps.Generator is { } citadel)
            {
                var size = Math.Clamp(RimChroniclesMod.Settings.CitadelMapSize, Core.Crisis.Sites.CitadelRings.MinMapSize, Core.Crisis.Sites.CitadelRings.MaxMapSize);
                mapSize = new IntVec3(size, 1, size);
                mapGenerator = citadel;
            }
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(CitadelMapPatch), ex);
        }
    }
}

/// <summary>While a courtyard relay stands, the keep's walls and doors take a tenth of the damage (crisis spec §4.5).
/// <c>Thing.TakeDamage</c> isn't virtual, so this catches every building type (C0).</summary>
[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class CitadelKeepShieldPatch
{
    public static void Prefix(Thing __instance, ref DamageInfo dinfo)
    {
        try
        {
            if (__instance is Building { Spawned: true } building && MapComponent_RcCitadel.For(building.Map) is { ShieldUp: true } citadel && citadel.Shields(building))
            {
                dinfo.SetAmount(dinfo.Amount * CitadelMaps.KeepShieldFactor);
            }
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(CitadelKeepShieldPatch), ex);
        }
    }
}
