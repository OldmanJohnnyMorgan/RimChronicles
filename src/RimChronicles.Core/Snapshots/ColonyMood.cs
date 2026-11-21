namespace RimChronicles.Core.Snapshots;

/// <summary>Turns the colonists' average mood into the snapshot's three-word scale.</summary>
public static class ColonyMood
{
    public const string Content = "content";
    public const string Strained = "strained";
    public const string Desperate = "desperate";

    /// <summary>Maps an average mood level (0–1, as the game's mood need reports it) to a word. Below 0.35 is
    /// near the game's minor break threshold, and below 0.2 is near the extreme one.</summary>
    public static string FromAverage(float? averageMood) => averageMood switch
    {
        null => Content,
        < 0.2f => Desperate,
        < 0.35f => Strained,
        _ => Content,
    };
}
