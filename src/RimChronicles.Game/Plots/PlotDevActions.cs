using LudeonTK;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Dev Mode actions for minor plots, under "Rim Chronicles". Arcs add their own (force seed, advance beat).</summary>
public static class PlotDevActions
{
    [DebugAction("Rim Chronicles", "Show plots", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowPlots()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var book = component.Plots;
        var today = GenDate.DaysPassed;
        var gate = ColonyPacingGate.Instance.BlockReason() ?? "open";
        var lines = new List<string>
        {
            $"Day {today}. Active: {book.Active.Count}. Last plot started day {book.LastStartDay}; last beat day {book.LastBeatDay}. Pacing gate: {gate}.",
        };
        foreach (var plot in book.Active.Concat(book.Finished.TakeLast(3)))
        {
            var who = plot.Protagonist.CoLeadName.Length > 0 ? $"{plot.Protagonist.Name} with {plot.Protagonist.CoLeadName}" : plot.Protagonist.Name;
            lines.Add("");
            lines.Add($"{plot.Id} ({plot.Template}), {plot.Beat}, protagonist {who}, next beat day {plot.NextBeatDay}");
            lines.AddRange(plot.Log.TakeLast(6).Select(l => "  " + l));
        }

        LetterSink.Instance.Send($"Rim Chronicles: plots ({book.Active.Count} active)", string.Join("\n", lines));
    }
}
