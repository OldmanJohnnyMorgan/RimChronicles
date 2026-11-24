using LudeonTK;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Powers;

/// <summary>
/// Keeps the colony's <see cref="Allegiance"/> and each power's plot in step (War in Heaven W2, tech spec R8). Once a
/// day, a stance a power's plot has set (the envoy's answer, tribute, a final demand, a treaty) comes into the
/// allegiance; if that sided the colony with one power, the others become its enemies, in their plots and in their
/// factions' goodwill. The plots' own rules are unchanged, so the director sees what it saw before. Main thread only.
/// </summary>
public static class AllegianceDriver
{
    /// <summary>The power's major plot, if it has started.</summary>
    public static Plot? PlotOf(RimChroniclesGameComponent component, Power power) =>
        power == Power.Empire ? EmpireBeats.Current(component) : power == Power.Choir ? ChoirBeats.Current(component) : null;

    public static void Daily(RimChroniclesGameComponent component)
    {
        var today = GenDate.DaysPassed;
        foreach (var power in PowerFactions.Running)
        {
            if (PlotOf(component, power) is not { } plot
                || plot.Fact(PowerArc.Stance) is not { Length: > 0 } stance
                || stance == component.Allegiance.StanceToward(power))
            {
                continue;
            }

            // The power's own plot set this stance and its driver moved the goodwill; only the rivals need updating.
            var changes = component.Allegiance.Set(power, stance, PowerFactions.Running, today, AllegianceCause.Plot);
            Apply(component, changes.Where(c => c.Power != power), today);
        }
    }

    /// <summary>Writes each changed stance into that power's plot and, once the plot is past its envoy, moves the
    /// faction's goodwill to match (as the Empire plot does for its own stance). A power with a faction but no plot yet
    /// (the Choir awake before its plot starts) has its goodwill moved at once.</summary>
    public static void Apply(RimChroniclesGameComponent component, IEnumerable<(Power Power, string Stance)> changes, int today)
    {
        foreach (var (power, stance) in changes)
        {
            if (PlotOf(component, power) is not { } plot)
            {
                // No plot yet (it reads the allegiance when it starts); a faction that already exists turns now.
                if (PowerFactions.Of(power) is { } existing)
                {
                    PowerFactions.ApplyStance(existing, stance);
                }

                continue;
            }

            plot.SetFact(PowerArc.Stance, stance);
            plot.Note(today, $"stance set to {stance} (allegiance)");
            if (plot.Beat != PlotBeat.Hook && PowerFactions.Of(power) is { } faction)
            {
                PowerFactions.ApplyStance(faction, stance);
            }
        }
    }

    [DebugAction("Rim Chronicles", "War in Heaven: set side", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevSetSide()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var running = PowerFactions.Running;
        var options = running
            .Select(power => new FloatMenuOption($"Side with {power.Id}", () =>
                Apply(component, component.Allegiance.Set(power, PowerStance.Ally, running, today, AllegianceCause.Dev), today)))
            .Append(new FloatMenuOption("Independent (against every power)", () =>
                Apply(component, component.Allegiance.DeclareIndependence(running, today, AllegianceCause.Dev), today)))
            .ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    [DebugAction("Rim Chronicles", "War in Heaven: show allegiance", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevShow()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var a = component.Allegiance;
        var running = PowerFactions.Running;
        var side = a.SideAmong(running) is { Length: > 0 } s ? s : "undeclared";
        var lines = running.Select(p => $"{p.Id}: {a.StanceToward(p)}")
            .Prepend($"Side: {side}")
            .Concat(a.Log.Count > 0 ? a.Log.Prepend("") : ["", "(no changes yet)"]);
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }
}
