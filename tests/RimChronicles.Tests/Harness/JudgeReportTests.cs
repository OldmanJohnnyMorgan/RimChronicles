using System.Text;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Judging;
using RimChronicles.Eval.Reporting;
using RimChronicles.Eval.Running;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class CsvTests
{
    [Fact]
    public void Parse_HandlesQuotesCommasAndNewlines()
    {
        var sb = new StringBuilder();
        Csv.AppendRow(sb, ["a", "b,c", "say \"hi\"", "two\nlines", ""]);
        Csv.AppendRow(sb, ["x", "", "", "", "z"]);

        var rows = Csv.Parse(sb.ToString());

        Assert.Equal(["a", "b,c", "say \"hi\"", "two\nlines", ""], rows[0]);
        Assert.Equal(["x", "", "", "", "z"], rows[1]);
    }

    [Fact]
    public void ReadTable_KeysByHeaderAndPadsShortRows()
    {
        var table = Csv.ReadTable("id,name,note\r\n1,Mara\r\n2,Vex,\"escaped, again\"\r\n");

        Assert.Equal(2, table.Count);
        Assert.Equal("", table[0]["note"]);
        Assert.Equal("escaped, again", table[1]["note"]);
    }
}

public class JudgeVerdictTests
{
    private const string Good = """{"inventedFacts":[{"span":"Captain Orlov","reason":"not in the briefing"}],"voice":4,"clarity":5,"tone":3,"overall":3,"comment":"Clear, one invented name."}""";

    [Fact]
    public void Parse_ReadsEveryField()
    {
        var v = JudgeVerdict.Parse(Good);

        Assert.Equal("Captain Orlov", Assert.Single(v.InventedFacts).Span);
        Assert.False(v.InventedFacts[0].IsMinor); // no severity (judge v1) counts as major
        Assert.Equal((4, 5, 3, 3), (v.Voice, v.Clarity, v.Tone, v.Overall));
        Assert.Equal("Clear, one invented name.", v.Comment);
    }

    [Theory]
    [InlineData("""{"voice":4,"clarity":4,"tone":4,"overall":4,"comment":""}""")]
    [InlineData("""{"inventedFacts":[],"voice":9,"clarity":4,"tone":4,"overall":4,"comment":""}""")]
    [InlineData("not json")]
    public void Parse_BadVerdict_Throws(string text) => Assert.ThrowsAny<FormatException>(() => JudgeVerdict.Parse(text));

    [Theory]
    [InlineData(0, 5, true)]
    [InlineData(1, 3, true)]
    [InlineData(1, 4, false)]
    [InlineData(2, 2, true)]
    [InlineData(3, 3, false)]
    public void CapCheck_EnforcesTheRubric(int invented, int overall, bool passes)
    {
        var facts = string.Join(",", Enumerable.Repeat("""{"span":"x","reason":"y"}""", invented));
        var json = $$"""{"inventedFacts":[{{facts}}],"voice":3,"clarity":3,"tone":3,"overall":{{overall}},"comment":""}""";

        Assert.Equal(passes, new JudgeCapCheck().Check(json).Passed);
    }

    [Theory]
    [InlineData(0, 2, 4, true)]  // minor ones alone cap at 4
    [InlineData(0, 2, 5, false)]
    [InlineData(1, 3, 3, true)]  // one major caps at 3, whatever the minors
    [InlineData(1, 3, 4, false)]
    public void CapCheck_CountsSeverity(int major, int minor, int overall, bool passes)
    {
        var facts = string.Join(",",
            Enumerable.Repeat("""{"span":"x","severity":"major","reason":"y"}""", major)
                .Concat(Enumerable.Repeat("""{"span":"x","severity":"minor","reason":"y"}""", minor)));
        var json = $$"""{"inventedFacts":[{{facts}}],"voice":3,"clarity":3,"tone":3,"overall":{{overall}},"comment":""}""";

        Assert.Equal(passes, new JudgeCapCheck().Check(json).Passed);
    }

    [Theory]
    [InlineData("raw\\a.json")]
    [InlineData("raw/a.json")]
    public void RawPaths_FromWindowsOrLinuxRuns_Resolve(string rawFile) =>
        Assert.Equal(Path.Combine("run", "raw", "a.json"), RunPaths.Raw("run", rawFile));

    [Fact]
    public void JudgeCsv_FromJudgeV1_CountsEveryFactAsMajor()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("rc-judgecsv-").FullName, "judge.csv");
        File.WriteAllText(path,
            "cell,model,tier,task,snapshot,repeat,raw_file,judge_prompt_version,judge_outcome,invented_count,invented,voice,clarity,tone,overall,comment,error\n" +
            "c,m,High,news-bulletin,s,1,raw\\c.json,judge/v1,Model,2,a | b,3,3,3,2,ok,\n");

        var row = Assert.Single(JudgeFiles.Read(path));

        Assert.Equal((2, 0), (row.MajorCount, row.MinorCount));
    }
}

public class PassBarsTests
{
    [Fact]
    public void RepoBars_Load()
    {
        var bars = PassBars.Load(Repo.Path("harness", "passbars.json"));

        Assert.Equal(0.95, bars.For("director").JsonValidGrammarOff);
        Assert.Equal(["Recommended"], bars.For("news-bulletin").OverallTiers);
        Assert.Null(bars.For("inheritance-handoff").MedianLatencySeconds);
    }

    [Theory]
    [InlineData(1.0, 1.0, BarVerdict.Pass)]
    [InlineData(0.99, 1.0, BarVerdict.Fail)]
    [InlineData(null, 1.0, BarVerdict.None)]
    [InlineData(0.5, null, BarVerdict.None)]
    public void AtLeast(double? value, double? bar, BarVerdict expected) => Assert.Equal(expected, PassBars.AtLeast(value, bar));

    [Theory]
    [InlineData(1.0, 1.0, BarVerdict.Pass)]
    [InlineData(1.1, 1.0, BarVerdict.Fail)]
    public void AtMost(double? value, double? bar, BarVerdict expected) => Assert.Equal(expected, PassBars.AtMost(value, bar));

    private static SummaryRow Row(string tier, string grammar = "n/a", double? json = null, double? median = null, string task = "news-bulletin") =>
        new("m", tier, task, grammar, 10, 1, json, null, 1, 1, 1, 0, median, median, 100, null);

    [Fact]
    public void ReportRow_AppliesTierLimitedBars()
    {
        var bars = PassBars.Load(Repo.Path("harness", "passbars.json")).For("news-bulletin");
        var judge = new JudgeStats(10, 0, 0, 0, 3, 3, 3, Overall: 3.0);

        Assert.Equal(BarVerdict.Fail, new ReportRow(Row("Recommended"), judge, bars).Overall);
        Assert.Equal(BarVerdict.None, new ReportRow(Row("Minimum"), judge, bars).Overall);
        Assert.Equal(BarVerdict.None, new ReportRow(Row("Reference", median: 60), judge, bars).Latency);
        Assert.Equal(BarVerdict.Fail, new ReportRow(Row("High", median: 60), judge, bars).Latency);
    }

    [Fact]
    public void ReportRow_HoldsMajorInventedFactsToTheBarAndOnlyCountsMinorOnes()
    {
        var bars = PassBars.Load(Repo.Path("harness", "passbars.json")).For("news-bulletin");
        var embellished = new ReportRow(Row("High"), new JudgeStats(10, 0, MajorPer10: 1, MinorPer10: 30, 3, 3, 3, 3), bars);
        var invented = new ReportRow(Row("High"), new JudgeStats(10, 0, MajorPer10: 2, MinorPer10: 0, 3, 3, 3, 3), bars);

        Assert.Equal(BarVerdict.Pass, embellished.MajorInvented);
        Assert.Equal(BarVerdict.None, embellished.MinorInvented); // no minor bar since 2026-11-21
        Assert.Equal(BarVerdict.Fail, invented.MajorInvented);
    }

    [Fact]
    public void ReportRow_JsonBarDependsOnGrammar()
    {
        var bars = PassBars.Load(Repo.Path("harness", "passbars.json")).For("director");

        Assert.Equal(BarVerdict.Fail, new ReportRow(Row("Minimum", "on", json: 0.97, task: "director"), null, bars).JsonValid);
        Assert.Equal(BarVerdict.Pass, new ReportRow(Row("Minimum", "off", json: 0.97, task: "director"), null, bars).JsonValid);
        Assert.Equal(BarVerdict.Fail, new ReportRow(Row("Minimum", "off", json: 0.9, task: "director"), null, bars).AllBars);
    }
}

/// <summary>End-to-end on a small stub run: run, judge, report, compare, spot-check.</summary>
public sealed class JudgePipelineTests : IDisposable
{
    private readonly string _out = Directory.CreateTempSubdirectory("rc-m4-").FullName;

    public void Dispose() => Directory.Delete(_out, recursive: true);

    private static readonly HarnessConfig Config = HarnessConfig.Load(Repo.Path("harness", "models.stub.json"));

    private async Task<RunResult> StubRun(params string[] snapshots) => await Runner.RunAsync(new RunOptions
    {
        Config = Config,
        ConfigPath = Repo.Path("harness", "models.stub.json"),
        PromptsRoot = Repo.Prompts,
        SnapshotsDir = Repo.Snapshots,
        OutputRoot = _out,
        CacheDir = Path.Combine(_out, "cache"),
        Snapshots = snapshots,
    }, TextWriter.Null, CancellationToken.None);

    private JudgeOptions JudgeOptions(string folder, Func<ModelConfig, RimChronicles.Llm.Chat.IChatProvider>? factory = null) => new()
    {
        RunFolder = folder,
        JudgeModel = Config.Judge!,
        PromptsRoot = Repo.Prompts,
        CacheDir = Path.Combine(_out, "cache"),
        ProviderFactory = factory,
    };

    [Fact]
    public async Task ResultsCsv_RoundTrips()
    {
        var run = await StubRun("snap-09*");

        var read = ResultFiles.ReadResultsCsv(Path.Combine(run.Folder, "results.csv"));

        Assert.Equal(run.Cells.Select(c => c with { FirstText = null }), read);
    }

    [Fact]
    public async Task Judge_JudgesOnlyProseBlind()
    {
        var run = await StubRun("snap-09*");
        var judgeRequests = new List<RimChronicles.Llm.Chat.ChatRequest>();

        var rows = await Judge.RunAsync(JudgeOptions(run.Folder, _ => new RecordingProvider(judgeRequests)) with { UseCache = false },
            TextWriter.Null, CancellationToken.None);

        // snap-09 has a handoff and a plot letter: per stub model one bulletin, one handoff scene and one letter; the
        // director is not judged.
        Assert.Equal(6, rows.Count);
        Assert.All(rows, r => Assert.True(r.Judged));
        Assert.DoesNotContain(rows, r => r.Task == "director");
        Assert.All(judgeRequests, r => Assert.DoesNotContain("stub-", r.System + r.User));
        Assert.Contains("## Output to evaluate", judgeRequests[0].User);
        Assert.Equal(6, JudgeFiles.Read(Path.Combine(run.Folder, "judge.csv")).Count);
        Assert.Equal(6, Directory.GetFiles(Path.Combine(run.Folder, "judgments")).Length);
    }

    [Fact]
    public async Task Judge_FailedVerdicts_AreRecordedNotThrown()
    {
        var run = await StubRun("snap-01*");

        var rows = await Judge.RunAsync(JudgeOptions(run.Folder, _ => new FakeChatProvider()) with { UseCache = false },
            TextWriter.Null, CancellationToken.None);

        Assert.All(rows, r =>
        {
            Assert.Equal("Failed", r.JudgeOutcome);
            Assert.False(r.Judged);
            Assert.NotNull(r.Error);
        });
    }

    [Fact]
    public async Task Report_IncludesJudgeScoresAndEscapesOutputs()
    {
        var run = await StubRun("snap-09*", "snap-17*");
        await Judge.RunAsync(JudgeOptions(run.Folder), TextWriter.Null, CancellationToken.None);
        var bars = PassBars.Load(Repo.Path("harness", "passbars.json"));

        var data = RunData.Load(run.Folder, bars);
        var html = HtmlReport.Render(data, null);

        Assert.True(data.HasJudge);
        var bulletin = data.Rows.Single(r => r.Metrics.Model == "stub-min" && r.Metrics.Task == "news-bulletin");
        Assert.Equal(4.0, bulletin.Judge!.Overall);
        Assert.Equal((0.0, 0.0), (bulletin.Judge.MajorPer10, bulletin.Judge.MinorPer10));
        Assert.Contains("<h2>At a glance</h2>", html);
        Assert.Contains("Meets all bars", html);
        Assert.Contains("The stub judge approves of everything.", html);
        Assert.Contains("&quot;choice&quot;", html); // director JSON is shown escaped, never as raw markup
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, "<svg ").Count);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public async Task Report_WithoutJudge_SaysSo()
    {
        var run = await StubRun("snap-01*");

        var html = HtmlReport.Render(RunData.Load(run.Folder, PassBars.Empty), null);

        Assert.Contains("Not judged yet", html);
    }

    [Fact]
    public async Task Report_Compare_ShowsDeltas()
    {
        var older = await StubRun("snap-01*");
        var newer = await StubRun("snap-01*");
        var bars = PassBars.Load(Repo.Path("harness", "passbars.json"));

        var html = HtmlReport.Render(RunData.Load(newer.Folder, bars), RunData.Load(older.Folder, bars));

        Assert.Contains($"Compared with run {older.RunId}", html);
        Assert.Contains("100% → 100%", html);
    }

    [Fact]
    public async Task SpotCheck_IsBlindAndScoresAgreement()
    {
        var run = await StubRun("snap-0*");
        await Judge.RunAsync(JudgeOptions(run.Folder), TextWriter.Null, CancellationToken.None);

        var count = SpotCheck.Create(run.Folder, 0.1, seed: 7, TextWriter.Null);
        var samplePath = Path.Combine(run.Folder, SpotCheck.SampleFile);
        var sample = File.ReadAllText(samplePath);

        Assert.True(count >= 2, "at least one sample per judged task");
        Assert.DoesNotContain("stub-", sample);
        Assert.DoesNotContain("stub judge", sample);

        // Fill in: agree exactly on the first sample, differ by 2 on the second, leave the rest blank.
        var table = Csv.ReadTable(sample);
        var filled = new StringBuilder();
        Csv.AppendRow(filled, ["sample", "task", "task_given", "briefing", "output", "human_invented_count", "human_overall", "human_notes"]);
        for (var i = 0; i < table.Count; i++)
        {
            var r = table[i];
            var (inv, overall) = i switch { 0 => ("0", "4"), 1 => ("1", "2"), _ => ("", "") };
            Csv.AppendRow(filled, [r["sample"], r["task"], r["task_given"], r["briefing"], r["output"], inv, overall, ""]);
        }

        File.WriteAllText(samplePath, filled.ToString());
        var agreement = SpotCheck.Score(run.Folder, TextWriter.Null);

        Assert.Equal(2, agreement.Scored);
        Assert.Equal(0.5, agreement.OverallExact);
        Assert.Equal(0.5, agreement.OverallWithinOne);
        Assert.Equal(1.0, agreement.MeanAbsoluteDifference);
        Assert.Equal(0.5, agreement.InventedAgreement);
    }

    [Fact]
    public async Task SpotCheck_SameSeed_SameSample()
    {
        var run = await StubRun("snap-0*");
        await Judge.RunAsync(JudgeOptions(run.Folder), TextWriter.Null, CancellationToken.None);

        SpotCheck.Create(run.Folder, 0.1, seed: 3, TextWriter.Null);
        var first = File.ReadAllText(Path.Combine(run.Folder, SpotCheck.KeyFile));
        SpotCheck.Create(run.Folder, 0.1, seed: 3, TextWriter.Null);

        Assert.Equal(first, File.ReadAllText(Path.Combine(run.Folder, SpotCheck.KeyFile)));
    }

    /// <summary>The stub judge, recording every request it sees.</summary>
    private sealed class RecordingProvider(List<RimChronicles.Llm.Chat.ChatRequest> requests) : RimChronicles.Llm.Chat.IChatProvider
    {
        private readonly RimChronicles.Eval.Providers.StubChatProvider _stub = new("stub-judge");

        public string ModelId => _stub.ModelId;

        public Task<RimChronicles.Llm.Chat.ChatResult> CompleteAsync(RimChronicles.Llm.Chat.ChatRequest request, CancellationToken cancellationToken)
        {
            lock (requests)
            {
                requests.Add(request);
            }

            return _stub.CompleteAsync(request, cancellationToken);
        }
    }
}
