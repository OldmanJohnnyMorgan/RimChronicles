using RimChronicles.Core.Plots;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Runs the minor plots once a day (Stage 3). M1 only checks which plot is due and logs why a beat waits;
/// arcs register their beat handlers from M2 on.</summary>
public static class PlotDriver
{
    /// <summary>Fires the next beat of one plot, given today's date. Returns false if the arc has no handler yet.</summary>
    public delegate bool BeatHandler(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today);

    private static readonly Dictionary<string, BeatHandler> Handlers = [];

    public static void Register(string templateId, BeatHandler handler) => Handlers[templateId] = handler;

    /// <summary>Called once per in-game day.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var book = component.Plots;
        var today = GenDate.DaysPassed;
        var due = book.DueToday(today, ColonyPacingGate.Instance, out var blocked);
        if (blocked is not null && Prefs.DevMode && book.Active.Any(p => today >= p.NextBeatDay))
        {
            Log.Message($"[Rim Chronicles] Plot beats wait: {blocked}.");
        }

        // One beat per day at most; the plot book also spaces beats two days apart.
        foreach (var plot in due)
        {
            if (PlotTemplateDef.Find(plot.Template) is { } template && Handlers.TryGetValue(plot.Template, out var handler)
                && handler(component, plot, template, today))
            {
                break;
            }
        }
    }

    /// <summary>Fires one plot's handler now (Dev Mode). False if it has none or the beat couldn't fire.</summary>
    public static bool FireNow(RimChroniclesGameComponent component, Plot plot, int today) =>
        PlotTemplateDef.Find(plot.Template) is { } template && Handlers.TryGetValue(plot.Template, out var handler)
        && handler(component, plot, template, today);
}
