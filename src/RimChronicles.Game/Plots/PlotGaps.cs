using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Random waits scaled by the story intensity setting, so every plot's beats, holds and a power's stirring
/// speed up or slow down together.</summary>
public static class PlotGaps
{
    private static float Intensity => RimChroniclesMod.Settings.StoryIntensity;

    /// <summary>Days until a plot's next beat: a random pick from the template's range, scaled.</summary>
    public static int Roll(PlotTemplate template) => StoryIntensity.Scale(Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays), Intensity);

    /// <summary>How long a power stirs before it wakes, scaled.</summary>
    public static int Stirring() => StoryIntensity.Scale(Rand.RangeInclusive(PowerSaga.MinStirringDays, PowerSaga.MaxStirringDays), Intensity);

    /// <summary>The War in Heaven's odds multiplier: the steepness setting times the intensity.</summary>
    public static double WarSteepness() => StoryIntensity.ScaleOdds(RimChroniclesMod.Settings.WarOddsSteepness, Intensity);
}
