namespace RimChronicles.Core.Plots;

/// <summary>The "story intensity" setting (design spec, mod settings): how often minor plots start, the gaps between
/// every plot's beats, how long a power stirs, how fast the powers expand, and how soon the War in Heaven starts. 1 is
/// the default; 2 halves the waits (and doubles the war's daily odds), 0.5 doubles them.</summary>
public static class StoryIntensity
{
    public const float Min = 0.5f;
    public const float Max = 2f;
    public const float Default = 1f;

    /// <summary>A wait of <paramref name="days"/> at this intensity, at least one day.</summary>
    public static int Scale(int days, float intensity) => Math.Max(1, (int)Math.Round(days / Math.Clamp(intensity, Min, Max)));

    /// <summary>A daily chance's multiplier at this intensity (the War in Heaven's odds): the inverse of <see cref="Scale"/>.</summary>
    public static double ScaleOdds(double factor, float intensity) => factor * Math.Clamp(intensity, Min, Max);

    public static string Describe(float intensity) => intensity switch
    {
        < 0.75f => "quiet",
        > 1.5f => "busy",
        > 1.15f => "lively",
        < 0.9f => "calm",
        _ => "normal",
    };
}
