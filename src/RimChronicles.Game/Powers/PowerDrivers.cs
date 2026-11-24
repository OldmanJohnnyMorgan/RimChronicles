using RimChronicles.Core.Powers;
using RimChronicles.Game.Empire;
using RimWorld;

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

        CrossWake(component);
        AllegianceDriver.Daily(component);
    }

    /// <summary>War in Heaven spec §4: once one power is awake and has attacked, a rival still asleep wakes at once.</summary>
    private static void CrossWake(RimChroniclesGameComponent component)
    {
        var running = PowerFactions.Running;
        if (!running.Contains(Power.Empire) || !running.Contains(Power.Choir))
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var empire = component.Empire;
        var choir = component.Choir;
        if (CrossWaking.RivalShouldWake(empire, CrossWaking.HasAttacked(empire, component.Crisis.History.Count > 0), choir))
        {
            ChoirDriver.WakeAtOnce(component, AwakeningTrigger.Rival, EmpireDriver.EmpireFaction?.Name ?? "", today);
        }
        else if (CrossWaking.RivalShouldWake(choir, CrossWaking.HasAttacked(choir), empire))
        {
            EmpireDriver.WakeAtOnce(component, AwakeningTrigger.Rival, ChoirFaction.Existing?.Name ?? "", today);
        }
    }
}
