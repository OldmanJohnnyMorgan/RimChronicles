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
        var running = PowerFactions.Running;
        var start = running.ToDictionary(p => p, p => component.Allegiance.StanceToward(p));
        var turnedByRival = new HashSet<Power>();

        // Envoy answers first, in the order the colony gave them: an earlier answer may have turned the other power's
        // plot against the colony, and a later answer to that power's envoy wins.
        foreach (var answer in component.Allegiance.TakeAnswers())
        {
            if (running.FirstOrDefault(p => p.Id == answer.Power) is not { } power)
            {
                continue;
            }

            if (PlotOf(component, power) is { } plot)
            {
                plot.SetFact(PowerArc.Stance, answer.Stance);
            }

            Sync(component, power, answer.Stance, running, today, turnedByRival);
        }

        foreach (var power in running)
        {
            if (PlotOf(component, power) is not { } plot
                || plot.Fact(PowerArc.Stance) is not { Length: > 0 } stance
                || stance == component.Allegiance.StanceToward(power))
            {
                continue;
            }

            Sync(component, power, stance, running, today, turnedByRival);
        }

        Announce(component, start, turnedByRival, running, today);
    }

    /// <summary>One letter for each power that ends the day the colony's enemy because the colony sided with another
    /// (the net result, so two answers on one day don't send a letter the second undoes).</summary>
    private static void Announce(RimChroniclesGameComponent component, Dictionary<Power, string> start, HashSet<Power> turnedByRival,
        IReadOnlyList<Power> running, int today)
    {
        if (running.FirstOrDefault(p => component.Allegiance.StanceToward(p) == PowerStance.Ally) is not { } side)
        {
            return;
        }

        foreach (var enemy in turnedByRival.Where(p => p != side))
        {
            var was = start.GetValueOrDefault(enemy, PowerStance.Neutral);
            if (!Allegiance.TurnsAgainst(was, component.Allegiance.StanceToward(enemy)) || PowerFactions.Of(enemy) is not { } faction)
            {
                continue;
            }

            var (label, text) = AllegianceText.TurnedAgainst(faction.Name, PowerFactions.Of(side)?.Name ?? side.DisplayName,
                was == PowerStance.Ally, Faction.OfPlayer.Name);
            PlotOf(component, enemy)?.Tell(today, label, text);
            Engine.StoryLetters.Send(label, text, LetterDefOf.ThreatBig, faction: faction);
        }
    }

    /// <summary>Brings a stance a power's plot set into the allegiance. The power's own driver moves its goodwill; the
    /// rivals' plots and goodwill are updated here, and the rivals it turned are noted for the day's letters.</summary>
    private static void Sync(RimChroniclesGameComponent component, Power power, string stance, IReadOnlyList<Power> running, int today,
        HashSet<Power> turnedByRival)
    {
        if (component.Allegiance.StanceToward(power) == stance)
        {
            return;
        }

        var changes = component.Allegiance.Set(power, stance, running, today, AllegianceCause.Plot);
        var rivals = changes.Where(c => c.Power != power).ToList();
        Apply(component, rivals, today);
        turnedByRival.UnionWith(rivals.Select(c => c.Power));
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
