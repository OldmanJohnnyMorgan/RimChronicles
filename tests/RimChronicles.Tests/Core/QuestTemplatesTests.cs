using RimChronicles.Core.Plots;

namespace RimChronicles.Tests.Core;

public class QuestTemplatesTests
{
    [Theory]
    [InlineData(35f, 150)]
    [InlineData(800f, 480)]
    [InlineData(10000f, 1500)]
    public void RewardSilver_ScalesWithThreatPointsWithinLimits(float points, int silver) =>
        Assert.Equal(silver, QuestTemplates.RewardSilver(points));

    [Fact]
    public void ScriptDefs_FollowTheModPrefix() =>
        Assert.Equal(["RimChronicles_Hunt", "RimChronicles_Rescue", "RimChronicles_Deliver"],
            new[] { QuestTemplates.Hunt, QuestTemplates.Rescue, QuestTemplates.Deliver }.Select(QuestTemplates.ScriptDef));
}
