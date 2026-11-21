using RimChronicles.Core.Engine;
using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Plots;

/// <summary>
/// Every minor plot in the save, and the design spec's limits: at most <see cref="MaxActive"/> active plots that count
/// toward the cap, at most one new plot per <see cref="NewPlotGapDays"/> days, beats spaced by the template's gap, no
/// beat while the pacing gate is closed, and no two beats within <see cref="BeatSpacingDays"/> days of each other.
/// Random numbers come in from the caller, so the rules are testable.
/// </summary>
public sealed class PlotBook : IArchivable
{
    public const int MaxActive = 3;
    public const int NewPlotGapDays = 15;
    public const int BeatSpacingDays = 2;
    public const int MaxFinishedKept = 30;

    public List<Plot> Active = [];

    /// <summary>Ended plots, newest last, kept for the decision log and the history tab.</summary>
    public List<Plot> Finished = [];

    /// <summary>The day the last cap-counting plot started, or -1.</summary>
    public int LastStartDay = -1;

    /// <summary>The day any beat last fired, or -1.</summary>
    public int LastBeatDay = -1;

    public int NextId = 1;

    public int CountingTowardCap(Func<string, PlotTemplate?> templates) =>
        Active.Count(p => templates(p.Template)?.CountsTowardCap ?? true);

    /// <summary>Null if a plot of this template may start today, else why not.</summary>
    public string? CannotStart(PlotTemplate template, int today, Func<string, PlotTemplate?> templates)
    {
        if (!template.CountsTowardCap)
        {
            return Active.Any(p => p.Template == template.Id) ? $"a {template.Id} plot is already running" : null;
        }

        if (CountingTowardCap(templates) >= MaxActive)
        {
            return $"{MaxActive} plots are already running";
        }

        if (LastStartDay >= 0 && today - LastStartDay < NewPlotGapDays)
        {
            return $"a plot started on day {LastStartDay}; the next may start on day {LastStartDay + NewPlotGapDays}";
        }

        return null;
    }

    /// <summary>Starts a plot. Its hook may fire after <paramref name="hookDelayDays"/> days.</summary>
    public Plot Start(PlotTemplate template, ProtagonistSlot protagonist, int today, int hookDelayDays, string why)
    {
        var plot = new Plot
        {
            Id = $"{template.Id.ToLowerInvariant()}-{NextId++}",
            Template = template.Id,
            StartDay = today,
            NextBeatDay = today + Math.Max(0, hookDelayDays),
            Protagonist = protagonist,
        };
        plot.Note(today, $"started: {why}");
        Active.Add(plot);
        if (template.CountsTowardCap)
        {
            LastStartDay = today;
        }

        return plot;
    }

    /// <summary>The plots whose next beat may fire today, oldest first; empty when the gate is closed or a beat fired
    /// in the last <see cref="BeatSpacingDays"/> days. Only the first is meant to fire today.</summary>
    public IReadOnlyList<Plot> DueToday(int today, IPacingGate gate, out string? blockedBecause)
    {
        blockedBecause = null;
        var due = Active.Where(p => !p.Ended && today >= p.NextBeatDay).OrderBy(p => p.NextBeatDay).ThenBy(p => p.StartDay).ToList();
        if (due.Count == 0)
        {
            return due;
        }

        if (LastBeatDay >= 0 && today - LastBeatDay < BeatSpacingDays)
        {
            blockedBecause = $"a beat fired on day {LastBeatDay}";
            return [];
        }

        blockedBecause = gate.BlockReason();
        return blockedBecause is null ? due : [];
    }

    /// <summary>Records that <paramref name="plot"/> moved to <paramref name="beat"/> today. The next beat waits
    /// <paramref name="gapDays"/> days (pick it from the template's range).</summary>
    public void Advance(Plot plot, PlotBeat beat, int today, int gapDays, string why)
    {
        plot.Beat = beat;
        plot.BeatDay = today;
        plot.NextBeatDay = today + Math.Max(1, gapDays);
        LastBeatDay = today;
        plot.Note(today, $"{beat}: {why}");
    }

    /// <summary>Ends a plot and moves it to <see cref="Finished"/>.</summary>
    public void End(Plot plot, int today, string outcome)
    {
        plot.Beat = PlotBeat.Ended;
        plot.BeatDay = today;
        plot.Outcome = outcome;
        plot.Note(today, $"ended: {outcome}");
        Active.Remove(plot);
        Finished.Add(plot);
        if (Finished.Count > MaxFinishedKept)
        {
            Finished.RemoveRange(0, Finished.Count - MaxFinishedKept);
        }
    }

    public Plot? Find(string id) => Active.FirstOrDefault(p => p.Id == id) ?? Finished.FirstOrDefault(p => p.Id == id);

    /// <summary>Active plots whose protagonist slot holds this pawn (as lead or co-lead).</summary>
    public IEnumerable<Plot> WithProtagonist(string pawnId) =>
        Active.Where(p => p.Protagonist.PawnId == pawnId || p.Protagonist.CoLeadId == pawnId);

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref Active, "active");
        archive.DeepList(ref Finished, "finished");
        archive.Value(ref LastStartDay, "lastStartDay", -1);
        archive.Value(ref LastBeatDay, "lastBeatDay", -1);
        archive.Value(ref NextId, "nextId", 1);
        Active.RemoveAll(p => string.IsNullOrEmpty(p.Id));
        NextId = Math.Max(NextId, 1);
    }
}
