using RimChronicles.Core.Powers;
using RimChronicles.Game.Empire;

namespace RimChronicles.Game.Powers;

/// <summary>Runs each running power's day, in <see cref="Power.All"/> order (War in Heaven tech spec §3), then syncs
/// the colony's allegiance with their plots. Main thread only.</summary>
public static class PowerDrivers
{
    public static void Daily(RimChroniclesGameComponent component)
    {
        foreach (var power in PowerFactions.Running)
        {
            if (power == Power.Empire)
            {
                EmpireDriver.Daily(component);
            }
            else if (power == Power.Choir)
            {
                ChoirDriver.Daily(component);
            }
        }

        AllegianceDriver.Daily(component);
    }
}
