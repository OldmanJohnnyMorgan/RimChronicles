using RimChronicles.Core.Powers;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Powers;

/// <summary>The game faction behind each <see cref="Power"/> (War in Heaven tech spec §2). The Empire is Royalty's
/// faction, while it exists and isn't defeated. The Hollow Choir's faction comes with W3.</summary>
public static class PowerFactions
{
    public static Faction? Of(Power power)
    {
        if (power == Power.Empire)
        {
            return ModsConfig.RoyaltyActive && Faction.OfEmpire is { defeated: false } empire ? empire : null;
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
