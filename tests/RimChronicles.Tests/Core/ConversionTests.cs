using RimChronicles.Core.Powers;

namespace RimChronicles.Tests.Core;

/// <summary>The summons' conversion levels (War in Heaven D7).</summary>
public class ConversionTests
{
    [Theory]
    [InlineData(6, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    [InlineData(18, 4)]
    [InlineData(22, 5)]
    [InlineData(26, 6)]
    [InlineData(40, 6)] // capped at 6
    [InlineData(0, 1)] // below a mechlink's own (impossible in game) still gives level 1
    public void Bandwidth_BecomesAPsylinkLevel(int bandwidth, int level) =>
        Assert.Equal(level, Conversion.PsylinkLevelFor(bandwidth));

    [Theory]
    [InlineData(1, 6, 0)]
    [InlineData(2, 10, 4)]
    [InlineData(3, 14, 8)]
    [InlineData(6, 26, 20)]
    [InlineData(9, 26, 20)] // clamped to 6
    [InlineData(0, 6, 0)] // clamped to 1
    public void APsylinkLevel_BecomesBandwidth(int level, int bandwidth, int bonus)
    {
        Assert.Equal(bandwidth, Conversion.BandwidthFor(level));
        Assert.Equal(bonus, Conversion.BonusBandwidthFor(level));
    }

    [Fact]
    public void ThereAndBack_KeepsTheLevel()
    {
        for (var level = 1; level <= 6; level++)
        {
            Assert.Equal(level, Conversion.PsylinkLevelFor(Conversion.BandwidthFor(level)));
        }
    }

    [Theory]
    [InlineData(14, 4, 10)]
    [InlineData(6, 0, 6)]
    [InlineData(3, 5, 0)]
    public void BandNodes_DoNotCount(int total, int bandNodes, int own) =>
        Assert.Equal(own, Conversion.OwnBandwidth(total, bandNodes));
}
