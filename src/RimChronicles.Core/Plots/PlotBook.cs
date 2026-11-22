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

    /// <summary>Every director decision, oldest first, capped at <see cref="MaxDecisionsKept"/>.</summary>
    public List<DirectorDecision> Decisions = [];

    public const int MaxDecisionsKept = 200;

    /// <summary>Quests started for plots, oldest first. Finished links are kept a while for the log, then dropped.</summary>
    public List<QuestLink> Quests = [];

    /// <summary>Links a quest to a plot; its outcome will come back as the plot fact <see cref="QuestLink.FactKey"/>.</summary>
    public QuestLink LinkQuest(Plot plot, int questId, string template, int today)
    {
        var link = new QuestLink { QuestId = questId, PlotId = plot.Id, Template = template, StartDay = today };
        Quests.Add(link);
        plot.SetFact(link.FactKey, "pending");
        plot.Note(today, $"quest offered: {template}");
        return link;
    }

    /// <summary>Records a quest's outcome on its plot (if the plot is still running) and makes the plot due, so the
    /// director acts on it at the next daily check. Returns the plot, or null.</summary>
    public Plot? QuestEnded(QuestLink link, string outcome, int today)
    {
        link.Outcome = outcome;
        if (Active.FirstOrDefault(p => p.Id == link.PlotId) is not { } plot)
        {
            return null;
        }

        plot.SetFact(link.FactKey, outcome);
        plot.NextBeatDay = Math.Min(plot.NextBeatDay, today);
        plot.Note(today, $"quest {link.Template}: {outcome}");
        return plot;
    }

    /// <summary>Drops finished links older than <paramref name="keepDays"/>.</summary>
    public void PruneQuests(int today, int keepDays = 60) => Quests.RemoveAll(q => !q.Pending && today - q.StartDay > keepDays);

    public void Record(DirectorDecision decision)
    {
        Decisions.Add(decision);
        if (Decisions.Count > MaxDecisionsKept)
        {
            Decisions.RemoveRange(0, Decisions.Count - MaxDecisionsKept);
        }
    }

    /// <summary>A plot holds off for <paramref name="days"/> days without firing a beat (the director's "Wait").</summary>
    public static void Hold(Plot plot, int today, int days, string why)
    {
        plot.NextBeatDay = today + Math.Max(1, days);
        plot.SetFact(WaitedOnBeat, plot.Beat.ToString());
        plot.Note(today, $"waited: {why}");
    }

    /// <summary>Fact recording the beat a plot last waited on, so it waits at most once per beat.</summary>
    public const string WaitedOnBeat = "waitedOnBeat";

    public static bool HasWaitedThisBeat(Plot plot) => plot.Fact(WaitedOnBeat) == plot.Beat.ToString();

    public int CountingTowardCap(Func<string, PlotTemplate?> templates) =>
        Active.Count(p => templates(p.Template)?.CountsTowardCap ?? true);

    /// <summary>Null if a plot of this template may start today, else why not.
    /// <paramref name="intensity"/> is the story-intensity setting: it scales the gap between new plots.</summary>
    public string? CannotStart(PlotTemplate template, int today, Func<string, PlotTemplate?> templates, float intensity = StoryIntensity.Default)
    {
        if (!template.CountsTowardCap)
        {
            return Active.Any(p => p.Template == template.Id) ? $"a {template.Id} plot is already running" : null;
        }

        if (CountingTowardCap(templates) >= MaxActive)
        {
            return $"{MaxActive} plots are already running";
        }

        var gap = StoryIntensity.Scale(NewPlotGapDays, intensity);
        if (LastStartDay >= 0 && today - LastStartDay < gap)
        {
            return $"a plot started on day {LastStartDay}; the next may start on day {LastStartDay + gap}";
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
        archive.DeepList(ref Decisions, "decisions");
        archive.DeepList(ref Quests, "quests");
        Active.RemoveAll(p => string.IsNullOrEmpty(p.Id));
        NextId = Math.Max(NextId, 1);
    }
}
