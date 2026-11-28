using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;

namespace RimChronicles.Tests.Core;

public class StoryIntensityTests
{
    [Theory]
    [InlineData(15, 1f, 15)]
    [InlineData(15, 2f, 8)]
    [InlineData(15, 0.5f, 30)]
    [InlineData(15, 9f, 8)] // clamped to 2
    [InlineData(1, 2f, 1)] // never below a day
    public void Scale_DividesWaitsByTheIntensity(int days, float intensity, int expected) =>
        Assert.Equal(expected, StoryIntensity.Scale(days, intensity));

    [Fact]
    public void PlotBook_NewPlotGapFollowsTheIntensity()
    {
        var book = new PlotBook();
        var template = new PlotTemplate { Id = "Nemesis" };
        book.Start(template, ProtagonistSlot.ForPawn("p1", "Anouk"), 10, 0, "first");

        Assert.NotNull(book.CannotStart(template, 20, _ => template)); // default: 15 days
        Assert.Null(book.CannotStart(template, 20, _ => template, intensity: 2f)); // 8 days
        Assert.Contains("day 40", book.CannotStart(template, 30, _ => template, intensity: 0.5f)); // 30 days
    }

    [Theory]
    [InlineData(1f, 1.0)]
    [InlineData(2f, 2.0)]
    [InlineData(0.5f, 0.5)]
    [InlineData(9f, 2.0)] // clamped to 2
    public void ScaleOdds_MultipliesChancesByTheIntensity(float intensity, double expected) =>
        Assert.Equal(expected, StoryIntensity.ScaleOdds(1.0, intensity), 6);

    [Fact]
    public void AQuietStory_MayStirTwiceAsLong()
    {
        var saga = new PowerSaga();
        saga.Awaken(AwakeningTrigger.Wealth, "90000", 40, StoryIntensity.Scale(PowerSaga.MaxStirringDays, StoryIntensity.Min), "Wyrmfold");
        Assert.Equal(40 + 2 * PowerSaga.MaxStirringDays, saga.NextStateDay);
    }
}
