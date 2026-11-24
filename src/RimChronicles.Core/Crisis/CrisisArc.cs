using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Crisis;

/// <summary>What the crisis arc reads besides the plot, as plain data.</summary>
public sealed record CrisisContext
{
    public required CrisisStage Stage { get; init; }
    public required CrisisState State { get; init; }

    /// <summary>The operations the game can run so far (their steps). Others are never offered.</summary>
    public required IReadOnlySet<string> Built { get; init; }

    /// <summary>True while the colony holds an Imperial captive or defector.</summary>
    public bool HoldsCaptive { get; init; }

    /// <summary>Days between crisis operations (the pacing gate, a setting).</summary>
    public int GapDays { get; init; } = CrisisArc.DefaultGapDays;

    /// <summary>Whose crisis this is (War in Heaven W7): the Empire's unless said otherwise.</summary>
    public Powers.Power Power { get; init; } = Powers.Power.Empire;
}

/// <summary>
/// The Imperial crisis as a plot in the plot book (tech spec R6): it offers the operations its stage allows, spaced by
/// the pacing gap and each operation's cooldown, plus Wait once between operations. It never counts toward the plot
/// cap. Until C11 its picks are made by weight, never by the director model.
/// </summary>
public static class CrisisArc
{
    public const string TemplateId = "ImperialCrisis";

    public const int DefaultGapDays = 8;
    public const int MinGapDays = 3;
    public const int MaxGapDays = 30;

    /// <summary>The transitions offered now. Empty while there's no crisis, an operation is running, or the gap or
    /// every cooldown hasn't passed.</summary>
    public static IReadOnlyList<PlotTransition> Transitions(Plot plot, CrisisContext context, int today)
    {
        var options = Allowed(context, today, ignoreBuilt: false, out _);
        if (options.Count == 0)
        {
            return [];
        }

        var list = options.Select(o => new PlotTransition(o.Id, o.Step, o.Weight, o.Desc)).ToList();
        if (!PlotBook.HasWaitedThisBeat(plot))
        {
            list.Add(new PlotTransition(Director.WaitId, Director.WaitId, 1, Director.WaitDesc));
        }

        return list;
    }

    /// <summary>The operations allowed today. With <paramref name="ignoreBuilt"/>, also those the game can't run yet
    /// (for the Dev Mode view). <paramref name="blocked"/> says why none are, if none are.</summary>
    public static IReadOnlyList<OperationSpec> Allowed(CrisisContext context, int today, bool ignoreBuilt, out string? blocked)
    {
        blocked = null;
        if (context.Stage == CrisisStage.None)
        {
            blocked = "no crisis";
            return [];
        }

        if (context.State.Running is { } running)
        {
            blocked = $"{running.Operation} is under way (day {running.Day})";
            return [];
        }

        var gap = Math.Clamp(context.GapDays, MinGapDays, MaxGapDays);
        var last = context.State.LastOperationDay;
        if (last >= 0 && today - last < gap)
        {
            blocked = $"the last operation was on day {last}; the next may come on day {last + gap}";
            return [];
        }

        var allowed = CrisisOperations.For(context.Power).Where(o =>
                o.MinStage <= context.Stage
                && (!o.FinaleOnly || context.Stage == CrisisStage.Finale)
                && (!o.NeedsCaptive || context.HoldsCaptive)
                && (ignoreBuilt || context.Built.Contains(o.Step))
                && CooledDown(o, context.State, today))
            .ToList();
        if (allowed.Count == 0)
        {
            blocked = ignoreBuilt ? "every allowed operation is cooling down" : "no allowed operation is built yet";
        }

        // Intel warned of the next operation (C10): while the warning holds and that operation is allowed, it's the one.
        if (context.State.Forecast is { } forecast && forecast.Valid(today) && allowed.FirstOrDefault(o => o.Step == forecast.Operation) is { } warned)
        {
            if (today < forecast.EarliestDay)
            {
                blocked = $"intel says {warned.Id} comes no sooner than day {forecast.EarliestDay}";
                return [];
            }

            return [warned];
        }

        return allowed;
    }

    public static bool CooledDown(OperationSpec operation, CrisisState state, int today)
    {
        var last = state.LastDayOf(operation.Step);
        return last < 0 || today - last >= operation.CooldownDays;
    }

    /// <summary>An operation fired: it counts as the plot's beat, and the plot may wait again before the next one.</summary>
    public static void OperationStarted(PlotBook book, Plot plot, OperationSpec operation, CrisisStage stage, float budget, CrisisState state, int today, int gapDays)
    {
        state.Started(operation.Step, stage, budget, today);
        plot.SetFact(PlotBook.WaitedOnBeat, "");
        book.Advance(plot, PlotBeat.Complication, today, gapDays, $"{operation.Id} at stage {CrisisStages.Label(stage)}, budget {budget:0}");
    }
}
