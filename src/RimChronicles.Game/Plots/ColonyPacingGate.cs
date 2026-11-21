using RimChronicles.Core.Engine;
using RimChronicles.Core.Snapshots;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Blocks story beats while a home map is under attack, or while the colony's mood is in crisis: average mood
/// in the "desperate" band, or half or more of the colonists in a mental break. Major-plot beats join this in Stage 5.</summary>
public sealed class ColonyPacingGate : IPacingGate
{
    public static readonly ColonyPacingGate Instance = new();

    public string? BlockReason()
    {
        foreach (var map in Find.Maps)
        {
            if (map.IsPlayerHome && GenHostility.AnyHostileActiveThreatToPlayer(map))
            {
                return "the colony is under attack";
            }
        }

        var colonists = PawnsFinder.AllMaps_FreeColonists;
        if (colonists.Count == 0)
        {
            return "no colonists";
        }

        var moods = colonists.Select(p => p.needs?.mood?.CurLevel).Where(m => m.HasValue).Select(m => m!.Value).ToList();
        if (ColonyMood.FromAverage(moods.Count == 0 ? null : moods.Average()) == ColonyMood.Desperate)
        {
            return "colony mood is in crisis";
        }

        if (colonists.Count(p => p.InMentalState) * 2 >= colonists.Count)
        {
            return "half the colonists are in a mental break";
        }

        return null;
    }
}
