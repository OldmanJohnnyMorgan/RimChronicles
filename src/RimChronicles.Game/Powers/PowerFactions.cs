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
}
