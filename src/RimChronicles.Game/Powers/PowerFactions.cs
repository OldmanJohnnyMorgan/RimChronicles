using RimChronicles.Core.Powers;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Powers;

/// <summary>The game faction behind each <see cref="Power"/> (War in Heaven tech spec §2). The Empire is Royalty's
/// faction, the Hollow Choir its own (<see cref="ChoirFaction"/>), each while it exists and isn't defeated.</summary>
public static class PowerFactions
{
    /// <summary>The powers this game runs: the Empire with the major plot on and Royalty active; the Hollow Choir with
    /// its setting on and Biotech active too. A power runs before its faction exists (the Choir is made when it wakes).</summary>
    public static IReadOnlyList<Power> Running => Power.All.Where(Runs).ToList();

    public static bool Runs(Power power) =>
        RimChroniclesMod.Settings.MajorPlot && ModsConfig.RoyaltyActive
        && (power == Power.Empire || (power == Power.Choir && RimChroniclesMod.Settings.HollowChoir && ChoirFaction.Available));

    public static Faction? Of(Power power)
    {
        if (power == Power.Empire)
        {
            return ModsConfig.RoyaltyActive && Faction.OfEmpire is { defeated: false } empire ? empire : null;
        }

        if (power == Power.Choir)
        {
            return ChoirFaction.Existing;
        }

        return null;
    }

    /// <summary>Moves the faction's goodwill to fit the colony's stance: an ally gets at least 25, an enemy -100
    /// (hostile), neutral leaves it alone.</summary>
    public static void ApplyStance(Faction faction, string stance)
    {
        var goodwill = faction.PlayerGoodwill;
        var change = stance switch
        {
            PowerStance.Ally => Math.Max(0, 25 - goodwill),
            PowerStance.Oppose => -100 - goodwill,
            _ => 0,
        };
        if (change != 0)
        {
            faction.TryAffectGoodwillWith(Faction.OfPlayer, change, canSendMessage: true, canSendHostilityLetter: true);
        }
    }
}
