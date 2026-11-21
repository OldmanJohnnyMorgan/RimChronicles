using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Prompts;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class WorldSummarizerTests
{
    private static WorldSnapshot Snap(string id) => SnapshotJson.Load(Repo.Path("harness", "snapshots", id + ".json"));

    private static IEnumerable<WorldSnapshot> All() =>
        Directory.GetFiles(Repo.Snapshots, "*.json").Order().Select(SnapshotJson.Load);

    private static string Dump(TaskPromptInput input) =>
        string.Join("\n\n", input.Variables.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"=== {kv.Key} ===\n{kv.Value}"));

    [Theory]
    [InlineData(ModelTier.Recommended)]
    [InlineData(ModelTier.Minimum)]
    public void Director_MatchesGolden(ModelTier tier) =>
        Golden.Match($"director.snap-07.{tier}.txt", Dump(WorldSummarizer.ForDirector(Snap("snap-07-mid-strained"), tier)));

    [Fact]
    public void NewsBulletin_MatchesGolden() =>
        Golden.Match("news-bulletin.snap-17.Recommended.txt", Dump(WorldSummarizer.ForNewsBulletin(Snap("snap-17-late-strained"), ModelTier.Recommended)));

    [Fact]
    public void Handoff_MatchesGolden() =>
        Golden.Match("inheritance-handoff.snap-09.txt", Dump(WorldSummarizer.ForHandoff(Snap("snap-09-mid-desperate"))));

    [Fact]
    public void Output_IsDeterministic()
    {
        var a = Dump(WorldSummarizer.ForDirector(Snap("snap-19-late-desperate"), ModelTier.Recommended));
        var b = Dump(WorldSummarizer.ForDirector(Snap("snap-19-late-desperate"), ModelTier.Recommended));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Director_ResolvesPawnIdsToNames()
    {
        var input = WorldSummarizer.ForDirector(Snap("snap-07-mid-strained"), ModelTier.Recommended);

        Assert.Contains("widow of Dorin", input.Variables["worldSummary"]);
        Assert.Contains("Raider 'Vex' killed Dorin and escaped", input.Variables["worldSummary"]);
        Assert.Contains("protagonist Mara", input.Variables["plotState"]);
        Assert.DoesNotMatch(@"\bp\d+\b", input.Variables["worldSummary"]);
    }

    [Fact]
    public void NewsDigest_LeavesOutColonyInternals()
    {
        var digest = WorldSummarizer.ForNewsBulletin(Snap("snap-07-mid-strained"), ModelTier.Recommended).Variables["digest"];

        Assert.DoesNotContain("Mara", digest);
        Assert.DoesNotContain("Dorin", digest);
        Assert.Contains("Imperial envoys visited two outlander towns", digest);
        Assert.Contains("sending envoys and making demands", digest);
    }

    [Fact]
    public void NewsDigest_IncludesPublicPlotHints()
    {
        var digest = WorldSummarizer.ForNewsBulletin(Snap("snap-12-mid-desperate"), ModelTier.Recommended).Variables["digest"];

        Assert.Contains("The Gallows Crew is holding a doctor captive at their camp", digest);
    }

    [Fact]
    public void NewsDigest_ListsEachStoryOnce()
    {
        var digest = WorldSummarizer.ForNewsBulletin(Snap("snap-17-late-strained"), ModelTier.Recommended).Variables["digest"];

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(digest, "took Dunwold"));
    }

    [Fact]
    public void NewsDigest_SplitsNewNewsFromAlreadyReported()
    {
        var digest = WorldSummarizer.ForNewsBulletin(Snap("snap-08-mid-content"), ModelTier.Recommended).Variables["digest"];

        var fresh = digest.IndexOf("News since your last bulletin:", StringComparison.Ordinal);
        var old = digest.IndexOf("Already reported", StringComparison.Ordinal);
        var jade = digest.IndexOf("jade sculpture", StringComparison.Ordinal);
        var fair = digest.IndexOf("trade fair", StringComparison.Ordinal);
        Assert.True(fresh >= 0 && fresh < jade && jade < old && old < fair, digest);
    }

    [Theory]
    [InlineData("snap-03-early-strained")]
    [InlineData("snap-17-late-strained")]
    public void NewsDigest_QuietStretch_SaysThereIsNoNewNews(string id) =>
        Assert.Contains("News since your last bulletin: none.",
            WorldSummarizer.ForNewsBulletin(Snap(id), ModelTier.Recommended).Variables["digest"]);

    [Fact]
    public void EverySnapshotWithEarlierBulletins_MarksWhatTheyReported() =>
        Assert.All(Directory.GetFiles(Repo.Snapshots, "*.json").Select(SnapshotJson.Load).Where(s => s.PreviousBulletins.Count > 0),
            s => Assert.NotEmpty(s.ReportedEvents));

    [Fact]
    public void FirstBulletin_SaysThereAreNoEarlierOnes() =>
        Assert.Contains("first bulletin",
            WorldSummarizer.ForNewsBulletin(Snap("snap-01-early-content"), ModelTier.Recommended).Variables["previousBulletins"]);

    [Fact]
    public void AllowedNames_ComeFromTheVariables()
    {
        var names = WorldSummarizer.ForNewsBulletin(Snap("snap-17-late-strained"), ModelTier.Recommended).AllowedNames;

        Assert.Contains("Dunwold", names);
        Assert.Contains("Severin", names);
        Assert.Contains("Ilse", names);
        Assert.DoesNotContain("Thane", names);
    }

    [Theory]
    [InlineData(ModelTier.Minimum)]
    [InlineData(ModelTier.Recommended)]
    public void EverySnapshot_FitsTheTierBudget(ModelTier tier)
    {
        var maxChars = WorldSummarizer.TokenBudget(tier) * 4;

        Assert.All(All(), s =>
        {
            Assert.True(WorldSummarizer.ForDirector(s, tier).Variables["worldSummary"].Length <= maxChars, $"{s.Id} director over budget");
            Assert.True(WorldSummarizer.ForNewsBulletin(s, tier).Variables["digest"].Length <= maxChars, $"{s.Id} digest over budget");
        });
    }

    [Fact]
    public void TightBudget_TrimsOldestHistoryFirst()
    {
        var big = Snap("snap-07-mid-strained") with
        {
            Tales = Enumerable.Range(1, 200).Select(i => new TaleInfo { Day = i, Type = "SocialFight", Actor = "p7", Victim = "p8" }).ToList(),
        };

        var summary = WorldSummarizer.ForDirector(big, ModelTier.Minimum).Variables["worldSummary"];

        Assert.True(summary.Length <= WorldSummarizer.TokenBudget(ModelTier.Minimum) * 4);
        Assert.Contains("Day 200:", summary);
        Assert.DoesNotContain("Day 1:", summary);
    }

    [Fact]
    public void Handoff_WithoutCase_Throws() =>
        Assert.Throws<InvalidOperationException>(() => WorldSummarizer.ForHandoff(Snap("snap-01-early-content")));
}

/// <summary>The prompts in prompts/ and the variables Core supplies must agree exactly.</summary>
public class PromptContractTests
{
    private static readonly PromptLibrary Library = PromptLibrary.Load(Repo.Prompts);

    private static IEnumerable<WorldSnapshot> All() =>
        Directory.GetFiles(Repo.Snapshots, "*.json").Order().Select(SnapshotJson.Load);

    [Fact]
    public void Library_HasEveryTask() =>
        Assert.Equal(["director", "inheritance-handoff", "judge", "news-bulletin"], Library.Tasks.Order());

    [Theory]
    [InlineData(ModelTier.Minimum)]
    [InlineData(ModelTier.Recommended)]
    [InlineData(ModelTier.High)]
    public void EverySnapshot_RendersEveryTask(ModelTier tier)
    {
        foreach (var s in All())
        {
            AssertRenders(WorldSummarizer.DirectorTask, WorldSummarizer.ForDirector(s, tier), tier);
            AssertRenders(WorldSummarizer.NewsBulletinTask, WorldSummarizer.ForNewsBulletin(s, tier), tier);
            if (s.Handoff is not null)
            {
                AssertRenders(WorldSummarizer.HandoffTask, WorldSummarizer.ForHandoff(s), tier);
            }
        }
    }

    [Fact]
    public void Judge_UsesTheHarnessVariables() =>
        Assert.Equal(["briefing", "output", "taskDescription"], Library.Get("judge").Variables.Order());

    [Fact]
    public void SchemaPrompts_HaveSchemas()
    {
        Assert.NotNull(Library.Get("director").Schema);
        Assert.NotNull(Library.Get("judge").Schema);
        Assert.Null(Library.Get("news-bulletin").Schema);
    }

    private static void AssertRenders(string task, TaskPromptInput input, ModelTier tier)
    {
        var prompt = Library.Get(task);

        // Exact match both ways: no missing variables, and no supplied variable left unused.
        Assert.Equal(prompt.Variables.Order(), input.Variables.Keys.Order());
        var rendered = prompt.Render(tier, input.Variables);
        Assert.DoesNotContain("{{", rendered.System + rendered.User);
    }
}
