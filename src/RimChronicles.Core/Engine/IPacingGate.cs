namespace RimChronicles.Core.Engine;

/// <summary>Whether the colony is calm enough for a story beat. A beat never fires during a raid, a major-plot beat or
/// a mood crisis (design spec, Stage 3 limits).</summary>
public interface IPacingGate
{
    /// <summary>Null if a beat may fire now, else a short reason for the decision log ("raid on the map").</summary>
    string? BlockReason();
}

/// <summary>A gate that never blocks, for tests and Dev Mode.</summary>
public sealed class OpenGate : IPacingGate
{
    public static readonly OpenGate Instance = new();

    public string? BlockReason() => null;
}
