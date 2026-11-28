using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Crisis;

/// <summary>How far the Imperial crisis has escalated against the colony (crisis spec §6). "Probing" is stage 1; the
/// crisis spec calls it "Stirring", renamed so it can't be confused with <see cref="PowerState.Stirring"/> (tech spec R4).</summary>
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

    public required PowerSaga Saga { get; init; }

    /// <summary>True when the Empire faction is hostile to the colony.</summary>
    public bool EmpireHostile { get; init; }

    /// <summary>Crisis operations the colony has come through (repelled or retreated from).</summary>
    public int OperationsSurvived { get; init; }

    /// <summary>Whose crisis (War in Heaven W7): the Empire's unless said otherwise. It only changes the reasons' wording.</summary>
    public Power Power { get; init; } = Power.Empire;
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
        if (saga.State is PowerState.Dormant or PowerState.Stirring)
        {
            return (CrisisStage.None, $"{Name(inputs)} isn't awake");
        }

        if (saga.Ending.Length > 0 || plot is null || plot.Ended || plot.Fact(PowerArc.EndingFact) is { Length: > 0 })
        {
            return (CrisisStage.None, $"{Name(inputs)}'s story has no running plot, or it has ended");
        }

        if (plot.Beat is not (PlotBeat.Complication or PlotBeat.Choice))
        {
            return (CrisisStage.None, $"Act 1: {Name(inputs)} hasn't marched yet");
        }

        var stance = PowerArc.StanceOf(plot);
        if (stance == PowerStance.Ally)
        {
            return (CrisisStage.None, $"the colony is {Name(inputs)}'s ally");
        }

        if (stance != PowerStance.Oppose && !inputs.EmpireHostile)
        {
            return (CrisisStage.None, $"the colony keeps out of {Name(inputs)}'s way");
        }

        var (stage, why) = Raw(inputs, plot, saga);
        if (saga.State == PowerState.Broken && stage > CrisisStage.Reclamation)
        {
            return (CrisisStage.Reclamation, $"{why}, but {Name(inputs)} is broken");
        }

        if (stage > CrisisStage.Probing && TributeCapped(plot))
        {
            return (CrisisStage.Probing, $"{why}, but the colony has only refused tribute so far");
        }

        return (stage, why);
    }

    private static (CrisisStage, string) Raw(CrisisInputs inputs, Plot plot, PowerSaga saga)
    {
        if (plot.Fact(PowerArc.Job) == PowerStep.StormCapital)
        {
            return (CrisisStage.Finale, inputs.Power == Power.Choir ? "the colony is storming the Choir's Cathedral" : "the colony is storming the Imperial capital");
        }

        if (saga.NextShipRaidDay >= 0)
        {
            return (CrisisStage.Finale, "the colony is starting a ship reactor");
        }

        if (plot.Fact(PowerArc.StanceCause) == PowerArc.CauseFinalDemand)
        {
            return (CrisisStage.Finale, $"the colony refused {Name(inputs)}'s final demand");
        }

        if (plot.Beat == PlotBeat.Choice)
        {
            return saga.State == PowerState.Ascendant
                ? (CrisisStage.War, $"Act 3, and {Name(inputs)} is ascendant")
                : (CrisisStage.Reclamation, "Act 3");
        }

        var expansions = saga.Expansions.Count;
        if (expansions >= PowerArc.ExpansionsForReckoning - WarBeforeReckoning)
        {
            return (CrisisStage.War, $"Act 2, {expansions} {Adjective(inputs)} expansions");
        }

        if (expansions >= ReclamationAfter || inputs.OperationsSurvived >= ReclamationAfter)
        {
            return (CrisisStage.Reclamation, $"Act 2, {expansions} {Adjective(inputs)} expansions and {inputs.OperationsSurvived} operations survived");
        }

        return (CrisisStage.Probing, $"Act 2: {Name(inputs)} is testing the colony");
    }

    /// <summary>How the reasons name the power: "the Empire" (as before W7) or "the Hollow Choir".</summary>
    private static string Name(CrisisInputs inputs) => inputs.Power.DisplayName;

    private static string Adjective(CrisisInputs inputs) => inputs.Power == Power.Choir ? "Choir" : "Imperial";

    /// <summary>A neutral colony that refused tribute faces probing raids only, until it finishes a job against the
    /// Empire or Act 3 begins (decided 2026-11-22, Q3).</summary>
    public static bool TributeCapped(Plot plot) =>
        plot.Beat == PlotBeat.Complication
        && plot.Fact(PowerArc.StanceCause) == PowerArc.CauseTribute
        && int.TryParse(plot.Fact(PowerArc.JobsAtStanceChange), out var then)
        && PowerArc.Count(plot, PowerArc.Wins) + PowerArc.Count(plot, PowerArc.Losses) <= then;

    public static string Label(CrisisStage stage) => stage switch
    {
        CrisisStage.Probing => "probing",
        CrisisStage.Reclamation => "reclamation",
        CrisisStage.War => "war",
        CrisisStage.Finale => "finale",
        _ => "none",
    };
}
