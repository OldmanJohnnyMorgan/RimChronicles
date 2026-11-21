namespace RimChronicles.Core.Plots;

/// <summary>The four beats every arc moves through, then <see cref="Ended"/>.</summary>
public enum PlotBeat
{
    Hook,
    Complication,
    Choice,
    Resolution,
    Ended,
}

/// <summary>How a plot of one kind behaves. The Game layer loads these from XML defs; Core only needs the rules.</summary>
public sealed record PlotTemplate
{
    /// <summary>e.g. "Nemesis", "SilentPress".</summary>
    public required string Id { get; init; }

    /// <summary>False for plots the player causes (Silent Press), which don't count toward the 3-plot cap.</summary>
    public bool CountsTowardCap { get; init; } = true;

    /// <summary>Days between beats: each beat waits a random number of days in this range.</summary>
    public int MinGapDays { get; init; } = 5;

    public int MaxGapDays { get; init; } = 15;

    /// <summary>How the arc changes when the protagonist slot passes to an heir, as a short note for the narration
    /// and the decision log (e.g. "the hunt becomes revenge"). Empty if it doesn't change.</summary>
    public string InheritanceMutation { get; init; } = "";
}
