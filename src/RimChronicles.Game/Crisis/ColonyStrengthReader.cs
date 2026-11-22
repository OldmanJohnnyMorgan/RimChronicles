using RimChronicles.Core.Crisis;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis;

/// <summary>Reads the colony's fighting strength into plain data for the threat budget (crisis spec §6). Main thread only.</summary>
public static class ColonyStrengthReader
{
    /// <summary>Apparel sharp armor (summed over worn pieces) that counts as the best gear.</summary>
    private const float FullArmor = 2f;

    public static ColonyStrength Read()
    {
        var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
        var fighters = colonists.Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent)).ToList();
        var turrets = Find.Maps.Where(m => m.IsPlayerHome)
            .SelectMany(m => m.listerBuildings.allBuildingsColonist)
            .OfType<Building_Turret>()
            .ToList();
        return new ColonyStrength
        {
            Fighters = fighters.Count,
            Gear = fighters.Count == 0 ? 0f : fighters.Average(Gear),
            Defenses = turrets.Sum(t => t.def.building.IsMortar || t.def.size.x * t.def.size.z > 1 ? 1.5f : 1f),
            PsylinkLevels = colonists.Sum(p => p.GetPsylinkLevel()),
            Wealth = WealthUtility.PlayerWealth,
        };
    }

    /// <summary>The home map operations go to: the one with the most colonists.</summary>
    public static Map? HomeMap() => Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();

    /// <summary>0 to 1: armor (60%) and weapon quality (40%; none counts as 0).</summary>
    private static float Gear(Pawn pawn)
    {
        var armor = pawn.apparel?.WornApparel.Sum(a => a.GetStatValue(StatDefOf.ArmorRating_Sharp)) ?? 0f;
        var weapon = pawn.equipment?.Primary is { } primary
            ? primary.TryGetQuality(out var quality) ? (int)quality / (float)(int)QualityCategory.Legendary : 0.3f
            : 0f;
        return 0.6f * Math.Clamp(armor / FullArmor, 0f, 1f) + 0.4f * weapon;
    }
}
