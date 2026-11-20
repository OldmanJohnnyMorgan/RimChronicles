using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Reporting;
using RimChronicles.Eval.Running;
using RimChronicles.Eval.Servers;
using RimChronicles.Llm.Chat;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class ServerProbeTests
{
    private static readonly ModelConfig Qwen = new()
    {
        Id = "qwen3.5-4b", Tier = "Recommended", Provider = ProviderKind.OpenAi, Endpoint = "http://localhost:11434", Model = "qwen3.5:4b",
    };

    /// <summary>A fake server answering GETs and POSTs by path; anything else is a 404.</summary>
    private static HttpClient Server(Dictionary<string, string> routes) =>
        new(new StubHttpHandler((req, _) => Task.FromResult(routes.TryGetValue(req.RequestUri!.AbsolutePath, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound))));

    private const string ModelsList = """{"data":[{"id":"qwen3.5:4b"},{"id":"gemma4:e4b"}]}""";

    [Fact]
    public async Task Ollama_ReportsVersionQuantizationAndVram()
    {
        var http = Server(new()
        {
            ["/v1/models"] = ModelsList,
            ["/api/version"] = """{"version":"0.12.3"}""",
            ["/api/show"] = """{"details":{"quantization_level":"Q4_K_M","parameter_size":"4.0B"}}""",
            ["/api/ps"] = """{"models":[{"name":"qwen3.5:4b","size":3400000000,"size_vram":3400000000}]}""",
        });

        var info = await ServerProbe.ProbeAsync(Qwen, CancellationToken.None, http);

        Assert.True(info.Reachable);
        Assert.Equal("ollama", info.ServerKind);
        Assert.Equal("0.12.3", info.ServerVersion);
        Assert.True(info.ModelListed);
        Assert.Equal("Q4_K_M", info.Quantization);
        Assert.Equal("4.0B", info.ParameterSize);
        Assert.Equal(3.4, info.VramGb!.Value, 3);
        Assert.False(info.PartlyOnCpu);
    }

    [Fact]
    public async Task Ollama_ModelSpillingToRam_IsPartlyOnCpu()
    {
        var http = Server(new()
        {
            ["/v1/models"] = ModelsList,
            ["/api/version"] = """{"version":"0.12.3"}""",
            ["/api/ps"] = """{"models":[{"name":"qwen3.5:4b","size":9000000000,"size_vram":7000000000}]}""",
        });

        Assert.True((await ServerProbe.ProbeAsync(Qwen, CancellationToken.None, http)).PartlyOnCpu);
    }

    [Fact]
    public async Task LlamaCpp_ReadsQuantizationFromTheModelFile()
    {
        var raon = Qwen with { Id = "raon", Model = "raon-1-nano", Endpoint = "http://localhost:8081" };
        var http = Server(new()
        {
            ["/v1/models"] = """{"data":[{"id":"raon-1-nano"}]}""",
            ["/props"] = """{"model_path":"/models/raon-1-nano-Q8_0.gguf","build_info":"b6100-krafton"}""",
        });

        var info = await ServerProbe.ProbeAsync(raon, CancellationToken.None, http);

        Assert.Equal("llama.cpp", info.ServerKind);
        Assert.Equal("Q8_0", info.Quantization);
        Assert.Equal("b6100-krafton", info.ServerVersion);
    }

    [Fact]
    public async Task MissingModel_IsNotListed()
    {
        var info = await ServerProbe.ProbeAsync(Qwen with { Model = "qwen3.5:9b" }, CancellationToken.None, Server(new() { ["/v1/models"] = ModelsList }));

        Assert.False(info.ModelListed);
        Assert.Contains("qwen3.5:4b", info.AvailableModels);
    }

    [Fact]
    public async Task NoServer_IsUnreachable()
    {
        var down = new HttpClient(new StubHttpHandler((_, _) => throw new HttpRequestException("Connection refused")));

        var info = await ServerProbe.ProbeAsync(Qwen, CancellationToken.None, down);

        Assert.False(info.Reachable);
        Assert.Contains("Is the server running?", info.Error);
    }

    [Theory]
    [InlineData("/models/gemma-4-12b-it-Q4_K_M.gguf", "Q4_K_M")]
    [InlineData("Qwen3-14B-IQ4_XS.gguf", "IQ4_XS")]
    [InlineData("model-f16.gguf", "F16")]
    [InlineData("model.gguf", null)]
    public void QuantFromFileName(string path, string? expected) => Assert.Equal(expected, ServerProbe.QuantFromFileName(path));
}

public class CheckCommandTests
{
    private static readonly HarnessConfig Config = new()
    {
        Models =
        [
            new ModelConfig
            {
                Id = "thinker", Tier = "Recommended", Provider = ProviderKind.OpenAi, Endpoint = "http://x", Model = "q",
                ThinkingByTask = new() { ["director"] = ThinkingMode.Off },
            },
        ],
    };

    private static ServerInfo Loaded(long vram, long size) =>
        new() { ModelId = "thinker", Reachable = true, ServerKind = "ollama", ModelListed = true, Quantization = "Q4_K_M", VramBytes = vram, SizeBytes = size };

    private static Task<IReadOnlyList<ModelCheck>> Check(IChatProvider provider, ServerInfo server) =>
        CheckCommand.RunAsync(Config, null, Repo.Prompts, Repo.Snapshots, 13, TextWriter.Null, CancellationToken.None,
            _ => provider, (_, _) => Task.FromResult(server));

    private static ChatResult Answer(string text, int? reasoning = null, string? raw = null) =>
        new() { Ok = true, Text = text, ReasoningTokens = reasoning, ModelId = "thinker", RawResponse = raw ?? "" };

    private const string Bulletin = "Word on the road is quiet this week, and the caravans are moving again.";

    [Fact]
    public async Task ThinkingThatWontSwitchOff_IsAWarning()
    {
        var provider = new FakeChatProvider(Answer("""{"choice":"Wait","reason":"quiet"}""", reasoning: 180), Answer(Bulletin));

        var check = Assert.Single(await Check(provider, Loaded(4_000_000_000, 4_000_000_000)));

        Assert.Empty(check.Problems);
        Assert.Contains(check.Warnings, w => w.Contains("thinking is set Off but the model still thought"));
        Assert.True(check.Calls[0].JsonValid);
    }

    [Fact]
    public async Task OverBudgetAndPartlyOnCpu_AreWarnings()
    {
        var provider = new FakeChatProvider(Answer("""{"choice":"Wait","reason":"quiet"}"""), Answer(Bulletin));

        var check = Assert.Single(await Check(provider, Loaded(14_500_000_000, 16_000_000_000)));

        Assert.Contains(check.Warnings, w => w.Contains("runs on the CPU"));
        Assert.Contains(check.Warnings, w => w.Contains("over the 13 GB budget"));
    }

    [Fact]
    public async Task MissingModel_IsAProblemNamingSimilarOnes()
    {
        var server = new ServerInfo { ModelId = "thinker", Reachable = true, ModelListed = false, AvailableModels = ["qwen3:14b", "gemma4:e4b"] };
        var config = Config with { Models = [Config.Models[0] with { Model = "qwen3.5:14b" }] };

        var checks = await CheckCommand.RunAsync(config, null, Repo.Prompts, Repo.Snapshots, 13, TextWriter.Null, CancellationToken.None,
            _ => new FakeChatProvider(), (_, _) => Task.FromResult(server));

        var problem = Assert.Single(Assert.Single(checks).Problems);
        Assert.Contains("similar: qwen3:14b", problem);
    }

    [Fact]
    public async Task FailedCall_IsAProblem()
    {
        var check = Assert.Single(await Check(new FakeChatProvider(FakeChatProvider.Error("HTTP 404: model not found")), Loaded(1, 1)));

        Assert.Contains(check.Problems, p => p.Contains("HTTP 404"));
    }

    [Theory]
    [InlineData("""{"choices":[{"message":{"content":"ok","reasoning_content":"let me think"}}]}""", true)]
    [InlineData("""{"choices":[{"message":{"content":"ok","reasoning":"hmm"}}]}""", true)]
    [InlineData("""{"choices":[{"message":{"content":"<think>x</think>ok"}}]}""", true)]
    [InlineData("""{"choices":[{"message":{"content":"ok","reasoning_content":""}}]}""", false)]
    [InlineData("not json", false)]
    public void ThinkingSeen_ReadsTheRawResponse(string raw, bool expected) =>
        Assert.Equal(expected, CheckCommand.ThinkingSeen(new ChatResult { Ok = true, Text = "ok", RawResponse = raw }));
}

public sealed class EnvironmentRecordingTests : IDisposable
{
    private readonly string _out = Directory.CreateTempSubdirectory("rc-env-").FullName;

    public void Dispose() => Directory.Delete(_out, recursive: true);

    [Fact]
    public async Task Run_RecordsServerInfoAndTheReportShowsIt()
    {
        var run = await Runner.RunAsync(new RunOptions
        {
            Config = HarnessConfig.Load(Repo.Path("harness", "models.stub.json")),
            ConfigPath = Repo.Path("harness", "models.stub.json"),
            PromptsRoot = Repo.Prompts,
            SnapshotsDir = Repo.Snapshots,
            OutputRoot = _out,
            CacheDir = Path.Combine(_out, "cache"),
            Snapshots = ["snap-01*"],
            Probe = (m, _) => Task.FromResult(new ServerInfo
            {
                ModelId = m.Id, Reachable = true, ServerKind = "ollama", Quantization = "Q4_K_M", VramBytes = 3_400_000_000, SizeBytes = 3_400_000_000,
            }),
        }, TextWriter.Null, CancellationToken.None);

        var env = JsonNode.Parse(File.ReadAllText(Path.Combine(run.Folder, "run.json")))!["environment"]!;
        Assert.Equal("Q4_K_M", env["stub-min"]!["quantization"]!.GetValue<string>());

        var data = RunData.Load(run.Folder, PassBars.Empty);
        Assert.Equal(3_400_000_000, data.Environment["stub-nogrammar"].VramBytes);

        var html = HtmlReport.Render(data, null);
        Assert.Contains("<th>Quantization</th>", html);
        Assert.Contains("3.4 GB", html);
    }
}
