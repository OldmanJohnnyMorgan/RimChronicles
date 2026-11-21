using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Commands;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Eval.Running;
using RimChronicles.Eval.Tasks;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class CliArgsTests
{
    private static CliArgs Parse(params string[] args) =>
        new(args, new HashSet<string> { "models", "repeats" }, new HashSet<string> { "dry-run" });

    [Fact]
    public void ParsesValuesFlagsAndLists()
    {
        var cli = Parse("--models", "a, b", "--dry-run", "extra");

        Assert.Equal(["a", "b"], cli.List("models"));
        Assert.True(cli.Has("dry-run"));
        Assert.Equal(["extra"], cli.Positional);
        Assert.Equal(3, cli.Int("repeats", 3));
    }

    [Fact]
    public void UnknownOption_Throws() => Assert.Throws<ArgumentException>(() => Parse("--modles", "a"));

    [Fact]
    public void MissingValue_Throws() => Assert.Throws<ArgumentException>(() => Parse("--models"));

    [Fact]
    public void BadNumber_Throws() => Assert.Throws<ArgumentException>(() => Parse("--repeats", "0").Int("repeats", 1));
}

public sealed class HarnessConfigTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("rc-config-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "models.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void RepoConfig_Loads()
    {
        var config = HarnessConfig.Load(Repo.Path("harness", "models.json"));

        Assert.Equal(4, config.Models.Count);
        Assert.DoesNotContain(config.Models, m => m.Id == "raon-1-nano"); // deferred; see the comment in models.json
        Assert.DoesNotContain(config.Models, m => m.Id.StartsWith("qwen", StringComparison.Ordinal)); // dropped 2026-11-21
        Assert.Equal(["Minimum", "Recommended", "High", "Reference"], config.Models.Select(m => m.Tier).Distinct());
        // Local models think on no task (the token caps are sized for the answer), using the field Ollama's OpenAI endpoint honours.
        foreach (var local in config.Models.Where(m => m.Provider == ProviderKind.OpenAi))
        {
            Assert.All(new[] { "director", "news-bulletin", "inheritance-handoff" }, t => Assert.Equal(ThinkingMode.Off, local.ThinkingFor(t)));
            Assert.Equal("none", local.ThinkingOffBody?["reasoning_effort"]?.GetValue<string>());
        }

        Assert.Equal(ThinkingMode.Off, config.Models.Single(m => m.Id == "claude-ref").ThinkingFor("director"));
        Assert.Equal("claude-sonnet-5-5", config.Judge!.Model); // moved from Opus 5.5 on 2026-11-21 to cut cost
    }

    [Fact]
    public void LocalTemperature_SetsOrRemovesItOnLocalModelsOnly()
    {
        var config = HarnessConfig.Load(Repo.Path("harness", "models.json"));

        var warm = config.WithLocalTemperature("0.8");
        Assert.All(warm.Models.Where(m => m.Provider == ProviderKind.OpenAi), m => Assert.Equal(0.8, m.ExtraBody!["temperature"]!.GetValue<double>()));
        Assert.Null(warm.Models.Single(m => m.Id == "claude-ref").ExtraBody);

        var server = config.WithLocalTemperature("default");
        Assert.All(server.Models, m => Assert.Null(m.ExtraBody?["temperature"]));
        Assert.NotEqual(config.Models[0].Identity(), server.Models[0].Identity()); // cached separately

        Assert.Throws<ArgumentException>(() => config.WithLocalTemperature("hot"));
    }

    [Theory]
    [InlineData("""{ "models": [ { "id": "a", "tier": "Huge", "provider": "Stub" } ] }""", "unknown tier")]
    [InlineData("""{ "models": [ { "id": "a", "tier": "Minimum", "provider": "OpenAi" } ] }""", "need endpoint and model")]
    [InlineData("""{ "models": [ { "id": "a", "tier": "Minimum", "provider": "Stub" }, { "id": "a", "tier": "Minimum", "provider": "Stub" } ] }""", "duplicate model id")]
    [InlineData("""{ "repeats": 0, "models": [] }""", "repeats must be")]
    public void InvalidConfig_IsRejected(string json, string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => HarnessConfig.Load(Write(json)));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void UnknownField_IsRejected() =>
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            HarnessConfig.Load(Write("""{ "models": [ { "id": "a", "tier": "Minimum", "provider": "Stub", "grammer": true } ] }""")));
}

public class EvalTaskTests
{
    private static readonly PromptLibrary Library = PromptLibrary.Load(Repo.Prompts);
    private static readonly ModelConfig Model = new() { Id = "m", Tier = "Minimum", Provider = ProviderKind.Stub };
    private static WorldSnapshot Snap(string id) => SnapshotJson.Load(Repo.Path("harness", "snapshots", id + ".json"));

    [Theory]
    [InlineData("snap-04-early-strained", 3, 60, 150)] // a busy week: 3 items
    [InlineData("snap-01-early-content", 2, 40, 120)]  // two new items: 2 items, no padding
    [InlineData("snap-08-mid-content", 1, 30, 100)]   // a quiet week: short, not padded
    [InlineData("snap-17-late-strained", 0, 30, 100)]
    public void BulletinWordRange_IsShortOnAQuietWeek(string snapshot, int newItems, int min, int max)
    {
        var s = Snap(snapshot);

        Assert.Equal(newItems, WorldSummarizer.NewNewsCount(s));
        Assert.Equal((min, max), EvalTasks.WordRange("news-bulletin", s));
        Assert.Equal((min, max), EvalTasks.Build(Library, "news-bulletin", s, Model, grammar: false).Words);
    }

    [Fact]
    public void BulletinWordRange_ScoresLengthAgainstTheSnapshotsRange()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 45));

        Assert.True(EvalTasks.Score("news-bulletin", text, EvalTasks.Build(Library, "news-bulletin", Snap("snap-08-mid-content"), Model, false)).LengthOk);
        Assert.False(EvalTasks.Score("news-bulletin", text, EvalTasks.Build(Library, "news-bulletin", Snap("snap-04-early-strained"), Model, false)).LengthOk);
    }

    [Fact]
    public void Director_FillsTheChoiceEnum()
    {
        var task = EvalTasks.Build(Library, "director", Snap("snap-07-mid-strained"), Model, grammar: true);

        var choiceEnum = task.Definition.SchemaOverride!.Schema["properties"]!["choice"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>());
        Assert.Equal(["Wait", "Complication:VexSighted", "Complication:ImperialBounty"], choiceEnum);
        Assert.Null(Library.Get("director").Schema!.Schema["properties"]!["choice"]!["enum"]);
        Assert.True(task.Definition.UseGrammar);
    }

    [Fact]
    public void Score_Director()
    {
        var task = EvalTasks.Build(Library, "director", Snap("snap-07-mid-strained"), Model, grammar: false);

        var good = EvalTasks.Score("director", """{"choice":"Wait","reason":"quiet"}""", task);
        var illegal = EvalTasks.Score("director", """{"choice":"Raid","reason":"fun"}""", task);
        var broken = EvalTasks.Score("director", "Wait.", task);

        Assert.True(good.JsonValid);
        Assert.True(good.ChoiceAllowed);
        Assert.True(illegal.JsonValid);
        Assert.False(illegal.ChoiceAllowed);
        Assert.False(broken.JsonValid);
    }

    [Fact]
    public void Score_Prose()
    {
        var task = EvalTasks.Build(Library, "news-bulletin", Snap("snap-17-late-strained"), Model, grammar: false);
        var text = "Dunwold has fallen to the Shattered Empire, and Captain Orlov says Harlow Ford is next. " + string.Join(' ', Enumerable.Repeat("word", 80));

        var metrics = EvalTasks.Score("news-bulletin", text, task);

        Assert.Equal(95, metrics.WordCount);
        Assert.True(metrics.LengthOk);
        Assert.Equal(["Orlov"], metrics.UnknownNames); // "Captain" is stock vocabulary
        Assert.False(metrics.BannedPhrase);
    }

    [Fact]
    public void Score_NoAnswer_HasNoMetrics() =>
        Assert.Null(EvalTasks.Score("director", null, EvalTasks.Build(Library, "director", Snap("snap-01-early-content"), Model, false)).JsonValid);
}

public sealed class CachingChatProviderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("rc-cache-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly ChatRequest Request = new() { System = "s", User = "u" };

    [Fact]
    public async Task SecondCall_IsServedFromDisk()
    {
        var inner = new FakeChatProvider(FakeChatProvider.Answer("one"), FakeChatProvider.Answer("two"));
        var stats = new CacheStats();

        var first = await new CachingChatProvider(inner, _dir, "model", 1, stats).CompleteAsync(Request, CancellationToken.None);
        var second = await new CachingChatProvider(inner, _dir, "model", 1, stats).CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("one", first.Text);
        Assert.Equal("one", second.Text);
        Assert.Single(inner.Requests);
        Assert.Equal((1, 1), (stats.Hits, stats.Misses));
    }

    [Fact]
    public async Task RepeatAndModelAndRequest_ChangeTheKey()
    {
        var stats = new CacheStats();
        var inner = new FakeChatProvider();
        string Key(string model, int repeat, ChatRequest r) => new CachingChatProvider(inner, _dir, model, repeat, stats).Key(r);

        var baseKey = Key("m", 1, Request);

        Assert.NotEqual(baseKey, Key("m", 2, Request));
        Assert.NotEqual(baseKey, Key("other", 1, Request));
        Assert.NotEqual(baseKey, Key("m", 1, Request with { User = "changed prompt" }));
        Assert.NotEqual(baseKey, Key("m", 1, Request with { UseGrammar = true }));
        Assert.Equal(baseKey, Key("m", 1, Request with { }));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Failures_AreNotCached()
    {
        var inner = new FakeChatProvider(FakeChatProvider.Error("HTTP 500"), FakeChatProvider.Answer("ok"));
        var stats = new CacheStats();

        await new CachingChatProvider(inner, _dir, "m", 1, stats).CompleteAsync(Request, CancellationToken.None);
        var second = await new CachingChatProvider(inner, _dir, "m", 1, stats).CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("ok", second.Text);
        Assert.Equal(2, inner.Requests.Count);
    }
}

public sealed class RunnerTests : IDisposable
{
    private readonly string _out = Directory.CreateTempSubdirectory("rc-run-").FullName;

    public void Dispose() => Directory.Delete(_out, recursive: true);

    private RunOptions Options(HarnessConfig? config = null, Func<ModelConfig, IChatProvider>? factory = null) => new()
    {
        Config = config ?? HarnessConfig.Load(Repo.Path("harness", "models.stub.json")),
        ConfigPath = Repo.Path("harness", "models.stub.json"),
        PromptsRoot = Repo.Prompts,
        SnapshotsDir = Repo.Snapshots,
        OutputRoot = _out,
        CacheDir = Path.Combine(_out, "cache"),
        ProviderFactory = factory,
    };

    [Fact]
    public void Plan_ExpandsTheMatrix()
    {
        var (cells, snapshots) = RunPlan.Build(Options());

        // stub-min: director x2 grammar modes (40) + news (20) + handoff (3); stub-nogrammar: 20 + 20 + 3.
        Assert.Equal(20, snapshots.Count);
        Assert.Equal(63, cells.Count(c => c.Model.Id == "stub-min"));
        Assert.Equal(43, cells.Count(c => c.Model.Id == "stub-nogrammar"));
        Assert.DoesNotContain(cells, c => c.Model.Id == "stub-nogrammar" && c.Grammar == "on");
    }

    [Fact]
    public void Plan_Filters()
    {
        var (cells, _) = RunPlan.Build(Options() with { Models = ["stub-min"], Tasks = ["director"], Snapshots = ["snap-0*"], Repeats = 2 });

        Assert.Equal(9 * 2 * 2, cells.Count);
        Assert.All(cells, c => Assert.StartsWith("snap-0", c.SnapshotId));
    }

    [Theory]
    [InlineData("nope", null)]
    [InlineData(null, "poetry")]
    public void Plan_UnknownModelOrTask_Throws(string? model, string? task) =>
        Assert.Throws<ArgumentException>(() => RunPlan.Build(Options() with
        {
            Models = model is null ? null : [model],
            Tasks = task is null ? null : [task],
        }));

    [Fact]
    public async Task StubRun_WritesTheRunFolderAndCachesResponses()
    {
        var first = await Runner.RunAsync(Options() with { Snapshots = ["snap-09*", "snap-17*"] }, TextWriter.Null, CancellationToken.None);
        var second = await Runner.RunAsync(Options() with { Snapshots = ["snap-09*", "snap-17*"] }, TextWriter.Null, CancellationToken.None);

        Assert.All(first.Cells, c => Assert.Equal("Model", c.Outcome));
        Assert.Equal(0, first.CacheHits);
        Assert.Equal(first.Cells.Count, second.CacheHits);
        Assert.Equal(0, second.CacheMisses);

        var csv = File.ReadAllLines(Path.Combine(first.Folder, "results.csv"));
        Assert.Equal(first.Cells.Count + 1, csv.Length);
        Assert.StartsWith("run_id,model,tier,task", csv[0]);

        var run = JsonNode.Parse(File.ReadAllText(Path.Combine(first.Folder, "run.json")))!;
        Assert.Equal("v1", run["promptVersions"]!["director"]!.GetValue<string>());
        Assert.Equal(first.Cells.Count, run["cells"]!.GetValue<int>());

        var raw = JsonNode.Parse(File.ReadAllText(Path.Combine(first.Folder, first.Cells[0].RawFile)))!;
        Assert.Equal("Model", raw["outcome"]!.GetValue<string>());
        Assert.Contains("## Allowed options", raw["attempts"]![0]!["request"]!["user"]!.GetValue<string>());

        Assert.True(File.Exists(Path.Combine(first.Folder, "summary.csv")));
    }

    [Fact]
    public async Task FailingModel_FallsBackAndIsCountedAsFallback()
    {
        var config = HarnessConfig.Load(Repo.Path("harness", "models.stub.json"));
        var result = await Runner.RunAsync(
            Options(config, _ => new FakeChatProvider()) with { Models = ["stub-min"], Tasks = ["news-bulletin"], Snapshots = ["snap-01*"] },
            TextWriter.Null, CancellationToken.None);

        var row = Assert.Single(result.Cells);
        Assert.Equal("Template", row.Outcome);
        Assert.False(row.FirstOk);
        Assert.Equal(2, row.Attempts);
        Assert.Equal(1.0, Summary.Build(result.Cells).Single().FallbackRate);
    }

    [Fact]
    public async Task LocalModels_GetOneUntimedWarmUpCall()
    {
        var local = new ModelConfig { Id = "local", Tier = "Minimum", Provider = ProviderKind.OpenAi, Endpoint = "http://x", Model = "m" };
        var fake = new FakeChatProvider();
        var result = await Runner.RunAsync(
            Options(new HarnessConfig { Models = [local] }, _ => fake) with
            {
                Tasks = ["news-bulletin"], Snapshots = ["snap-01*"], UseCache = false,
                Probe = (m, _) => Task.FromResult(new RimChronicles.Eval.Servers.ServerInfo { ModelId = m.Id }),
            },
            TextWriter.Null, CancellationToken.None);

        Assert.Equal("Say ready.", fake.Requests[0].User);
        Assert.Single(fake.Requests, r => r.User == "Say ready.");
        Assert.Equal(2, Assert.Single(result.Cells).Attempts); // the warm-up isn't one of the cell's attempts
    }

    [Fact]
    public async Task DryRun_CallsNothing()
    {
        var calls = 0;
        var result = await Runner.RunAsync(
            Options(factory: m => { calls++; return new StubChatProvider(m.Id); }) with { DryRun = true },
            TextWriter.Null, CancellationToken.None);

        Assert.Empty(result.Cells);
        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(result.Folder));
    }

    [Theory]
    [InlineData(new double[] { }, 0.5, null)]
    [InlineData(new double[] { 3 }, 0.9, 3.0)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0.5, 5.0)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0.9, 9.0)]
    public void Percentile_IsNearestRank(double[] sorted, double p, double? expected) =>
        Assert.Equal(expected, Summary.Percentile(sorted, p));
}

public sealed class ValidateCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("rc-validate-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Repo_IsValid() =>
        Assert.Equal(0, ValidateCommand.Run(Repo.Snapshots, Repo.SnapshotSchema, Repo.Prompts, Repo.Path("harness", "models.json"), TextWriter.Null));

    [Fact]
    public void BrokenSnapshot_IsReported()
    {
        File.Copy(Path.Combine(Repo.Snapshots, "snap-07-mid-strained.json"), Path.Combine(_dir, "snap-07-mid-strained.json"));
        File.WriteAllText(Path.Combine(_dir, "snap-99-bad.json"), """{"id":"wrong","day":1,"season":"Summer","year":5500,"colony":{"name":"A","wealth":1,"population":1,"mood":"content"},"allowedTransitions":["Wait"]}""");
        var log = new StringWriter();

        var exit = ValidateCommand.Run(_dir, Repo.SnapshotSchema, Repo.Prompts, null, log);

        Assert.Equal(1, exit);
        Assert.Contains("snap-99-bad.json: $.season must be one of", log.ToString());
        Assert.Contains("does not match the file name", log.ToString());
    }

    [Fact]
    public void ReportedEventThatIsNotPublicNews_IsReported()
    {
        var text = File.ReadAllText(Path.Combine(Repo.Snapshots, "snap-07-mid-strained.json"))
            .Replace("\"reportedEvents\": [", "\"reportedEvents\": [\n    \"Something nobody heard about\",", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_dir, "snap-07-mid-strained.json"), text);
        var log = new StringWriter();

        Assert.Equal(1, ValidateCommand.Run(_dir, Repo.SnapshotSchema, Repo.Prompts, null, log));
        Assert.Contains("reportedEvents entry is not a public event or plot hint: 'Something nobody heard about'", log.ToString());
    }
}
