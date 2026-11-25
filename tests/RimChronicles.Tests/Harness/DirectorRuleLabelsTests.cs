using System.Text.Json.Nodes;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

/// <summary>The rule labels for siege goals and crisis sites (W13).</summary>
public class DirectorRuleLabelsTests
{
    private static readonly IReadOnlyList<SiteArchetype> EmpireSites = SiteArchetypeXml.Load(Repo.Path("Defs", "SiteArchetypeDefs"));
    private static readonly IReadOnlyList<SiteArchetype> ChoirSites = SiteArchetypeXml.Load(Repo.Path("Defs", "SiteArchetypeDefs"), Power.Choir.Id);

    private static WorldSnapshot Case(string id)
    {
        var index = int.Parse(id[^5..], System.Globalization.CultureInfo.InvariantCulture);
        return id.Contains("-w", StringComparison.Ordinal)
            ? CaseGenerator.GenerateWar(9001, index, ChoirSites).Snapshot
            : CaseGenerator.GenerateCrisis(9001, index, EmpireSites).Snapshot;
    }

    /// <summary>The rule is what the test labeller followed: a check of the labeller, not of a model.</summary>
    [Fact]
    public void Rule_AgreesWithTheTestLabels()
    {
        int total = 0, agree = 0;
        foreach (var pack in new[] { "test-crisis-v1", "test-war-v1" })
        {
            foreach (var line in File.ReadAllLines(Repo.Path("training", "data", "labels", pack, "labels-final.jsonl")).Where(l => l.Length > 0))
            {
                var label = JsonNode.Parse(line)!;
                var group = label["group"]!.GetValue<string>();
                if (!group.EndsWith("/SiegeGoal", StringComparison.Ordinal) && !group.EndsWith("/Site", StringComparison.Ordinal))
                {
                    continue;
                }

                var s = Case(label["case"]!.GetValue<string>());
                Assert.Equal(group, DirectorLabels.Group(s));
                total++;
                agree += DirectorRuleLabels.Label(s)!.Value.Best == label["best"]!.GetValue<string>() ? 1 : 0;
            }
        }

        Assert.Equal(84, total);
        Assert.True(agree >= 80, $"the rule agreed with {agree} of {total} test labels");
    }

    [Fact]
    public void Label_OtherGroups_IsNull() =>
        Assert.Null(DirectorRuleLabels.Label(CaseGenerator.Generate(1, 0).Snapshot));

    [Fact]
    public void Label_EveryRuleCase_PicksAnAllowedOptionWithAShortReason()
    {
        var cases = Enumerable.Range(0, 400).Select(i => CaseGenerator.GenerateCrisis(1, i, EmpireSites).Snapshot)
            .Concat(Enumerable.Range(0, 400).Select(i => CaseGenerator.GenerateWar(1, i, ChoirSites).Snapshot));
        var labelled = 0;
        foreach (var s in cases)
        {
            if (DirectorRuleLabels.Label(s) is not var (best, reason))
            {
                continue;
            }

            labelled++;
            Assert.Contains(best, s.AllowedTransitions);
            Assert.InRange(reason.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, 5, DirectorLabels.MaxReasonWords);
        }

        Assert.True(labelled > 50);
    }

    [Fact]
    public void Run_RefusesTestSeedCases()
    {
        var dir = Directory.CreateTempSubdirectory("rule-labels-");
        try
        {
            var cases = Directory.CreateDirectory(Path.Combine(dir.FullName, "cases"));
            var s = CaseGenerator.GenerateCrisis(9001, 0, EmpireSites).Snapshot;
            File.WriteAllText(Path.Combine(cases.FullName, s.Id + ".json"), SnapshotJson.Serialize(s));
            Assert.Equal(1, DirectorRuleLabels.Run([cases.FullName], [], new HashSet<int> { 9001 }, Path.Combine(dir.FullName, "out"), TextWriter.Null));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
