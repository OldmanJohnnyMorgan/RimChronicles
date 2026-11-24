using System.Text.Json.Nodes;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Llm;
using RimChronicles.Llm.Validation;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

/// <summary>The crisis arc's director cases (crisis C11).</summary>
public class CrisisCaseTests
{
    private static readonly IReadOnlyList<RimChronicles.Core.Crisis.Sites.SiteArchetype> Sites = SiteArchetypeXml.Load(Repo.Path("Defs", "SiteArchetypeDefs"));

    private static readonly int Cycle = CaseGenerator.CrisisScenarios.Length * CaseGenerator.Moods.Length * CaseGenerator.Situations.Length;

    private static IEnumerable<DirectorCaseResult> OneCycle(int seed = 7) =>
        Enumerable.Range(0, Cycle).Select(i => CaseGenerator.GenerateCrisis(seed, i, Sites));

    [Fact]
    public void TheSiteArchetypes_LoadFromTheGamesXml()
    {
        Assert.Equal(["ForwardOutpost", "OrbitalUplink", "PsychicRelay"], Sites.Select(s => s.Name).Order());
        Assert.All(Sites, s => Assert.False(string.IsNullOrWhiteSpace(s.Desc)));
    }

    [Fact]
    public void CrisisCases_AreReproducible_AndNeverShareAnIdWithTheOtherCases()
    {
        var a = CaseGenerator.GenerateCrisis(9001, 5, Sites).Snapshot;
        Assert.Equal(SnapshotJson.Serialize(a), SnapshotJson.Serialize(CaseGenerator.GenerateCrisis(9001, 5, Sites).Snapshot));
        Assert.Equal("dir-s9001-c00005", a.Id);
        Assert.NotEqual(a.Id, CaseGenerator.Generate(9001, 5).Snapshot.Id);
    }

    [Fact]
    public void EveryCrisisCase_OffersARealChoice_WithEveryOptionDescribed_AndPassesTheSchema()
    {
        var schema = (JsonObject)JsonNode.Parse(File.ReadAllText(Repo.SnapshotSchema))!;
        foreach (var c in OneCycle())
        {
            var s = c.Snapshot;
            Assert.True(s.AllowedTransitions.Count >= 2, $"{s.Id} ({c.Scenario}) has no choice");
            Assert.Equal(s.AllowedTransitions.Order(), s.Director!.Descriptions.Keys.Order());
            Assert.All(s.Director.Descriptions.Values, d => Assert.False(string.IsNullOrWhiteSpace(d)));
            Assert.Contains(s.ActivePlots, p => p.Id == s.Director.PlotId);
            Assert.NotNull(s.Crisis);
            Assert.Empty(JsonSchemaValidator.Validate(JsonNode.Parse(SnapshotJson.Serialize(s)), schema));
        }
    }

    [Fact]
    public void TheScenarios_OfferWhatTheGameWould()
    {
        foreach (var c in OneCycle())
        {
            var options = c.Snapshot.AllowedTransitions;
            var group = DirectorLabels.Group(c.Snapshot);
            switch (c.Scenario)
            {
                case "Crisis:SiegeGoal":
                    Assert.All(options, o => Assert.StartsWith("Goal:", o));
                    Assert.Equal("ImperialCrisis/SiegeGoal", group);
                    break;
                case "Crisis:Site":
                    Assert.All(options, o => Assert.StartsWith("Site:", o));
                    Assert.Equal("ImperialCrisis/Site", group);
                    break;
                default:
                    Assert.All(options, o => Assert.True(o == "Wait" || o.StartsWith(CrisisOperations.Prefix, StringComparison.Ordinal), o));
                    Assert.StartsWith("ImperialCrisis/", group);
                    Assert.DoesNotContain(options, o => o == "Operation:FinalAssault" && c.Scenario != "Crisis:Finale");
                    break;
            }
        }
    }

    [Fact]
    public void TheCrisisBlock_ReachesTheDirectorPrompt()
    {
        var s = CaseGenerator.GenerateCrisis(7, 3, Sites).Snapshot;
        Assert.Contains($"Imperial crisis, stage {s.Crisis!.Stage}", WorldSummarizer.ForDirector(s, ModelTier.Recommended).Variables["worldSummary"]);
    }
}
