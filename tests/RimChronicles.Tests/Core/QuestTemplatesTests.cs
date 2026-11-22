using System.Xml.Linq;
using RimChronicles.Core.Plots;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class QuestTemplatesTests
{
    [Theory]
    [InlineData(35f, 150)]
    [InlineData(800f, 480)]
    [InlineData(10000f, 1500)]
    public void RewardSilver_ScalesWithThreatPointsWithinLimits(float points, int silver) =>
        Assert.Equal(silver, QuestTemplates.RewardSilver(points));

    private static readonly string[] All =
        [QuestTemplates.Hunt, QuestTemplates.Rescue, QuestTemplates.Deliver, QuestTemplates.Escort, QuestTemplates.DefendAlly, QuestTemplates.Sabotage];

    [Fact]
    public void ScriptDefs_FollowTheModPrefix() =>
        Assert.Equal(
            ["RimChronicles_Hunt", "RimChronicles_Rescue", "RimChronicles_Deliver", "RimChronicles_Escort", "RimChronicles_DefendAlly", "RimChronicles_Sabotage"],
            All.Select(QuestTemplates.ScriptDef));

    /// <summary>Every template has a quest def, started only by plots, whose root class exists in the Game sources.</summary>
    [Fact]
    public void EveryTemplate_HasAPlotOnlyQuestDef_WithARealRoot()
    {
        var defs = XDocument.Load(Repo.Path("Defs", "QuestScriptDefs", "RimChronicles_Quests.xml")).Root!.Elements("QuestScriptDef")
            .ToDictionary(d => (string)d.Element("defName")!);
        var sources = string.Join("\n", Directory.GetFiles(Repo.Path("src", "RimChronicles.Game", "Quests"), "*.cs").Select(File.ReadAllText));
        foreach (var template in All)
        {
            var def = defs[QuestTemplates.ScriptDef(template)];
            Assert.Equal("false", (string?)def.Element("randomlySelectable"));
            Assert.Equal("true", (string?)def.Element("isRootSpecial"));
            var root = ((string)def.Element("root")!.Attribute("Class")!).Split('.')[^1];
            Assert.Contains($"class {root} ", sources);
        }
    }
}
