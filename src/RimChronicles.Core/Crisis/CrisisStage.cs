using RimChronicles.Core.Empire;
using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Crisis;

/// <summary>How far the Imperial crisis has escalated against the colony (crisis spec §6). "Probing" is stage 1; the
/// crisis spec calls it "Stirring", renamed so it can't be confused with <see cref="EmpireState.Stirring"/> (tech spec R4).</summary>
public enum CrisisStage
{
    /// <summary>No crisis: the Empire isn't awake, isn't hostile, or the story is over.</summary>
    None,
    Probing,
    Reclamation,
    War,
    Finale,
}

/// <summary>What the stage rules read from the world, as plain data. The game fills it in each day.</summary>
public sealed record CrisisInputs
{
    /// <summary>The Empire plot, or null if it hasn't started.</summary>
    public Plot? EmpirePlot { get; init; }

    public required EmpireSaga Saga { get; init; }

    /// <summary>True when the Empire faction is hostile to the colony.</summary>
    public bool EmpireHostile { get; init; }

    /// <summary>Crisis operations the colony has come through (repelled or retreated from).</summary>
    public int OperationsSurvived { get; init; }
}

/// <summary>The crisis stage, worked out from the Empire's saga and plot each day (tech spec §4.1). Never stored on
/// its own, so it can't drift from the plot.</summary>
public static class CrisisStages
{
    /// <summary>Act 2 moves from Probing to Reclamation after this many Imperial expansions (or this many survived operations).</summary>
    public const int ReclamationAfter = 2;

    /// <summary>Act 2 reaches War this many expansions before the reckoning.</summary>
    public const int WarBeforeReckoning = 1;

    public static CrisisStage Of(CrisisInputs inputs) => Explain(inputs).Stage;

    /// <summary>The stage and why, for the Dev Mode view and the decision log.</summary>
    public static (CrisisStage Stage, string Why) Explain(CrisisInputs inputs)
    {
        var saga = inputs.Saga;
        var plot = inputs.EmpirePlot;
        if (saga.State is EmpireState.Dormant or EmpireState.Stirring)
        {
            return (CrisisStage.None, "the Empire isn't awake");
        }

        if (saga.Ending.Length > 0 || plot is null || plot.Ended || plot.Fact(EmpireArc.EndingFact) is { Length: > 0 })
        {
            return (CrisisStage.None, "the Empire's story has no running plot, or it has ended");
        }

        if (plot.Beat is not (PlotBeat.Complication or PlotBeat.Choice))
        {
            return (CrisisStage.None, "Act 1: the Empire hasn't marched yet");
        }

        var stance = EmpireArc.StanceOf(plot);
        if (stance == EmpireStance.Ally)
        {
            return (CrisisStage.None, "the colony is the Empire's ally");
        }

        if (stance != EmpireStance.Oppose && !inputs.EmpireHostile)
        {
            return (CrisisStage.None, "the colony keeps out of the Empire's way");
        }

        var (stage, why) = Raw(inputs, plot, saga);
        if (saga.State == EmpireState.Broken && stage > CrisisStage.Reclamation)
        {
            return (CrisisStage.Reclamation, $"{why}, but the Empire is broken");
        }

        if (stage > CrisisStage.Probing && TributeCapped(plot))
        {
            return (CrisisStage.Probing, $"{why}, but the colony has only refused tribute so far");
        }

        return (stage, why);
    }

    private static (CrisisStage, string) Raw(CrisisInputs inputs, Plot plot, EmpireSaga saga)
    {
        if (plot.Fact(EmpireArc.Job) == EmpireStep.StormCapital)
        {
            return (CrisisStage.Finale, "the colony is storming the Imperial capital");
        }

        if (saga.NextShipRaidDay >= 0)
        {
            return (CrisisStage.Finale, "the colony is starting a ship reactor");
        }

        if (plot.Fact(EmpireArc.StanceCause) == EmpireArc.CauseFinalDemand)
        {
            return (CrisisStage.Finale, "the colony refused the Empire's final demand");
        }

        if (plot.Beat == PlotBeat.Choice)
        {
            return saga.State == EmpireState.Ascendant
                ? (CrisisStage.War, "Act 3, and the Empire is ascendant")
                : (CrisisStage.Reclamation, "Act 3");
        }

        var expansions = saga.Expansions.Count;
        if (expansions >= EmpireArc.ExpansionsForReckoning - WarBeforeReckoning)
        {
            return (CrisisStage.War, $"Act 2, {expansions} Imperial expansions");
        }

        if (expansions >= ReclamationAfter || inputs.OperationsSurvived >= ReclamationAfter)
        {
            return (CrisisStage.Reclamation, $"Act 2, {expansions} Imperial expansions and {inputs.OperationsSurvived} operations survived");
        }

        return (CrisisStage.Probing, "Act 2: the Empire is testing the colony");
    }

    /// <summary>A neutral colony that refused tribute faces probing raids only, until it finishes a job against the
    /// Empire or Act 3 begins (decided 2026-11-22, Q3).</summary>
    public static bool TributeCapped(Plot plot) =>
        plot.Beat == PlotBeat.Complication
        && plot.Fact(EmpireArc.StanceCause) == EmpireArc.CauseTribute
        && int.TryParse(plot.Fact(EmpireArc.JobsAtStanceChange), out var then)
        && EmpireArc.Count(plot, EmpireArc.Wins) + EmpireArc.Count(plot, EmpireArc.Losses) <= then;

    public static string Label(CrisisStage stage) => stage switch
    {
        CrisisStage.Probing => "probing",
        CrisisStage.Reclamation => "reclamation",
        CrisisStage.War => "war",
        CrisisStage.Finale => "finale",
        _ => "none",
    };
}
