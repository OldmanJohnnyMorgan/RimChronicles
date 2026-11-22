using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Llm;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class DirectorDatasetTests
{
    [Fact]
    public void Split_GoesByCase_WithTestSeedsHeldOut()
    {
        var o = new DatasetOptions { LabelDirs = [], CaseDirs = [], OutDir = "", PromptsRoot = "", TestSeeds = new HashSet<int> { 9001 } };
        var test = CaseGenerator.Generate(9001, 3).Snapshot;
        Assert.Equal("test", DirectorDataset.Split(test, o));

        var splits = Enumerable.Range(0, 400).Select(i => DirectorDataset.Split(CaseGenerator.Generate(1, i).Snapshot, o)).ToList();
        Assert.DoesNotContain("test", splits);
        Assert.InRange(splits.Count(s => s == "val"), 20, 60); // about 10%
        Assert.Equal(splits, Enumerable.Range(0, 400).Select(i => DirectorDataset.Split(CaseGenerator.Generate(1, i).Snapshot, o)).ToList());

        var exported = test with { Id = "dir-game-x-day5-nemesis-1", Director = test.Director! with { Seed = null } };
        Assert.Contains(DirectorDataset.Split(exported, o), new[] { "test", "val", "train" });
    }

    [Fact]
    public void Variants_TrainGetsThreeCopies_OthersOne_AndEveryChoiceIsAllowed()
    {
        var s = CaseGenerator.Generate(1, 7).Snapshot;
        var best = s.AllowedTransitions[0];
        var rng = new Random(3);
        var train = Enumerable.Range(0, 50).SelectMany(_ => DirectorDataset.Variants(s, best, train: true, rng)).ToList();
        Assert.Equal(150, train.Count);
        Assert.All(train, v => Assert.Contains(v.Variant.Choice, v.Variant.Snapshot.AllowedTransitions));
        Assert.Contains(train, v => v.Augment.Contains("ids", StringComparison.Ordinal));
        Assert.Single(DirectorDataset.Variants(s, best, train: false, rng));
    }

    [Fact]
    public void RenameIds_KeepsDescriptionsAndWait()
    {
        var s = CaseGenerator.Generate(1, 7).Snapshot;
        var best = s.AllowedTransitions.First(t => t != "Wait");
        var (renamed, choice) = DirectorDataset.RenameIds(s, best);
        Assert.Matches("^C[0-9]$", choice);
        Assert.Equal(s.Director!.Descriptions[best], renamed.Director!.Descriptions[choice]);
        Assert.Equal(s.AllowedTransitions.Contains("Wait"), renamed.AllowedTransitions.Contains("Wait"));
        Assert.DoesNotContain(renamed.AllowedTransitions, t => t.Contains(':', StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WritesSplitsWithRenderedPromptsAndPlainTargets()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-ds-" + Guid.NewGuid().ToString("N"));
        var cases = Path.Combine(root, "cases");
        var labels = Path.Combine(root, "labels");
        Directory.CreateDirectory(cases);
        Directory.CreateDirectory(labels);
        try
        {
            var lines = new List<string>();
            foreach (var (seed, i) in new[] { (1, 0), (1, 1), (9001, 0) })
            {
                var s = CaseGenerator.Generate(seed, i).Snapshot;
                File.WriteAllText(Path.Combine(cases, s.Id + ".json"), SnapshotJson.Serialize(s));
                lines.Add(new JsonObject { ["case"] = s.Id, ["best"] = s.AllowedTransitions[0], ["agreement"] = 1.0, ["reason"] = "It's time." }.ToJsonString());
            }

            File.WriteAllLines(Path.Combine(labels, "labels-final.jsonl"), lines);
            var outDir = Path.Combine(root, "out");
            Assert.Equal(0, DirectorDataset.Build(new DatasetOptions
            {
                LabelDirs = [labels], CaseDirs = [cases], OutDir = outDir, PromptsRoot = Repo.Prompts, Tier = ModelTier.Minimum,
                TestSeeds = new HashSet<int> { 9001 },
            }, TextWriter.Null));

            var test = File.ReadAllLines(Path.Combine(outDir, "test.jsonl")).Select(l => JsonNode.Parse(l)!).ToList();
            var row = Assert.Single(test);
            Assert.Equal("director/v2", row["promptVersion"]!.GetValue<string>());
            Assert.Contains("## Deciding now", row["user"]!.GetValue<string>());
            Assert.Contains("It's time.", row["target"]!.GetValue<string>()); // no '
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "manifest.json")))!;
            Assert.Equal(1, manifest["cases"]!["test"]!.GetValue<int>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
