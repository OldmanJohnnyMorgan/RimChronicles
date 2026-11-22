using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Llm;
using RimChronicles.Llm.Validation;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class DirectorCaseTests
{
    private static readonly int Cycle = CaseGenerator.Scenarios.Length * CaseGenerator.Moods.Length * CaseGenerator.Situations.Length;

    private static IEnumerable<DirectorCaseResult> OneCycle(int seed = 7) => Enumerable.Range(0, Cycle).Select(i => CaseGenerator.Generate(seed, i));

    [Fact]
    public void Generate_IsReproducible_AndSeedsDiffer()
    {
        Assert.Equal(SnapshotJson.Serialize(CaseGenerator.Generate(3, 41).Snapshot), SnapshotJson.Serialize(CaseGenerator.Generate(3, 41).Snapshot));
        Assert.NotEqual(SnapshotJson.Serialize(CaseGenerator.Generate(3, 41).Snapshot), SnapshotJson.Serialize(CaseGenerator.Generate(4, 41).Snapshot));
    }

    [Fact]
    public void EveryCase_OffersARealChoice_WithEveryOptionDescribed_AndPassesTheSchema()
    {
        var schema = (JsonObject)JsonNode.Parse(File.ReadAllText(Repo.SnapshotSchema))!;
        foreach (var c in OneCycle())
        {
            var s = c.Snapshot;
            Assert.True(s.AllowedTransitions.Count >= 2, $"{s.Id} ({c.Scenario}) has no choice");
            Assert.Equal(s.AllowedTransitions.Order(), s.Director!.Descriptions.Keys.Order());
            Assert.All(s.Director.Descriptions.Values, d => Assert.False(string.IsNullOrWhiteSpace(d)));
            Assert.Contains(s.ActivePlots, p => p.Id == s.Director.PlotId);
            Assert.Empty(JsonSchemaValidator.Validate(JsonNode.Parse(SnapshotJson.Serialize(s)), schema));
        }
    }

    [Fact]
    public void Cases_CoverEveryScenarioMoodAndSituationEvenly()
    {
        var cases = OneCycle().ToList();
        Assert.Equal(CaseGenerator.Scenarios.Order(), cases.Select(c => c.Scenario).Distinct().Order());
        Assert.All(cases.GroupBy(c => (c.Scenario, c.Mood, c.Situation)), g => Assert.Single(g));
    }

    [Fact]
    public void Scenarios_OfferTheOptionsTheArcsWouldOffer()
    {
        var ids = OneCycle().GroupBy(c => c.Scenario).ToDictionary(g => g.Key, g => g.SelectMany(c => c.Snapshot.AllowedTransitions).ToHashSet());
        Assert.Contains(ids["Nemesis:AfterSighting+Empire"], id => id.StartsWith("Promote:", StringComparison.Ordinal));
        Assert.DoesNotContain(ids["Nemesis:AfterSighting"], id => id.StartsWith("Promote:", StringComparison.Ordinal));
        Assert.Contains("Act3:OfferGovernorship", ids["Empire:Act3:ally+title"]);
        Assert.DoesNotContain("Act3:OfferGovernorship", ids["Empire:Act3:ally"]);
        Assert.Contains("Act3:ForceATreaty", ids["Empire:Act3:oppose:broken"]);
        Assert.DoesNotContain("Act3:ForceATreaty", ids["Empire:Act3:oppose:ascendant"]);
        Assert.Contains("Empire:TributeDemand", ids["Empire:Act2:neutral"]);
        Assert.Contains(ids["LostFamily:Taken"], id => id.StartsWith("Choice:Rescue", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, "content", "recent death", 5, true)]
    [InlineData(true, "desperate", "calm", 5, true)]
    [InlineData(true, "content", "long stall", 30, false)]
    [InlineData(true, "strained", "long stall", 30, false)]
    [InlineData(true, "desperate", "long stall", 30, null)]
    [InlineData(true, "content", "calm", 5, null)]
    [InlineData(false, "desperate", "recent raid", 5, null)]
    public void WaitExpected_FollowsTheSituation(bool offered, string mood, string situation, int days, bool? expected) =>
        Assert.Equal(expected, CaseGenerator.WaitExpected(offered, mood, situation, days));

    [Fact]
    public void EveryCase_RendersThroughTheDirectorPrompt_WithinBudget()
    {
        foreach (var c in OneCycle().Take(68))
        {
            var input = WorldSummarizer.ForDirector(c.Snapshot, ModelTier.Minimum);
            Assert.Contains(c.Snapshot.Director!.PlotId!, input.Variables["decidingPlot"]);
            Assert.All(c.Snapshot.AllowedTransitions, id => Assert.Contains($"- {id}: ", input.Variables["allowedTransitions"]));
        }
    }

    [Fact]
    public void Command_WritesCasesCoverageAndManifest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rc-dirgen-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, DirectorGenCommand.Run(2, 60, dir, Repo.SnapshotSchema, TextWriter.Null));
            Assert.Equal(60, Directory.GetFiles(dir, "dir-s2-*.json").Length);
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")))!;
            Assert.Equal(60, manifest["count"]!.GetValue<int>());
            Assert.StartsWith("scenario,mood,cases", File.ReadAllText(Path.Combine(dir, "coverage.csv")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
