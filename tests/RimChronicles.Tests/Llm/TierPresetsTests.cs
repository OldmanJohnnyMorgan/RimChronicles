using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Llm;

public class TierPresetsTests
{
    [Theory]
    [InlineData(ModelTier.Off, null)]
    [InlineData(ModelTier.Minimum, "gemma4:e2b")]
    [InlineData(ModelTier.Recommended, "gemma4:e4b")]
    [InlineData(ModelTier.High, "gemma4:12b")]
    public void ModelFor_MatchesStage0Picks(ModelTier tier, string? model) =>
        Assert.Equal(model, TierPresets.ModelFor(tier));

    [Fact]
    public void Presets_MatchTheHarnessConfig()
    {
        var config = File.ReadAllText(Repo.Path("harness", "models.json"));
        foreach (var tier in new[] { ModelTier.Minimum, ModelTier.Recommended, ModelTier.High })
        {
            Assert.Contains($"\"model\": \"{TierPresets.ModelFor(tier)}\"", config);
        }
    }

    [Theory]
    [InlineData("http://localhost:11434/", null)]
    [InlineData("https://example.com/v1", null)]
    [InlineData("", "Enter")]
    [InlineData("localhost:11434", "http")]
    [InlineData("ftp://x", "http")]
    public void EndpointProblem_ExplainsBadAddresses(string endpoint, string? contains)
    {
        var problem = TierPresets.EndpointProblem(endpoint);
        if (contains is null)
        {
            Assert.Null(problem);
        }
        else
        {
            Assert.Contains(contains, problem);
        }
    }

    [Theory]
    [InlineData("http://localhost:11434", "http://localhost:11434/v1/chat/completions")]
    [InlineData(" http://host:8080/api/ ", "http://host:8080/api/v1/chat/completions")]
    [InlineData("http://localhost:1234/v1", "http://localhost:1234/v1/chat/completions")] // LM Studio's own address
    [InlineData("http://localhost:8000/v1/", "http://localhost:8000/v1/chat/completions")]
    [InlineData("https://host/proxy/V1", "https://host/proxy/v1/chat/completions")]
    [InlineData("http://host/v10", "http://host/v10/v1/chat/completions")] // only a whole "v1" segment is dropped
    public void EndpointUri_ResolvesTheChatPathUnderIt(string endpoint, string expected) =>
        Assert.Equal(expected, new Uri(TierPresets.EndpointUri(endpoint), "v1/chat/completions").ToString());

    private sealed class FakeProvider(ChatResult result) : IChatProvider
    {
        public string ModelId => "gemma4:e4b";

        public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    [Fact]
    public async Task Probe_ReportsSuccessWithLatency()
    {
        var ok = new ChatResult { Ok = true, Text = "OK", Latency = TimeSpan.FromSeconds(0.4), ModelId = "gemma4:e4b" };

        var result = await ConnectionProbe.RunAsync(new FakeProvider(ok), CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Contains("gemma4:e4b in 0.4 s", result.Message);
    }

    [Fact]
    public async Task Probe_HintsAtAMissingModel()
    {
        var failed = ChatResult.Failure("gemma4:e4b", "HTTP 404: model 'gemma4:e4b' not found", TimeSpan.Zero);

        var result = await ConnectionProbe.RunAsync(new FakeProvider(failed), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("ollama pull", result.Message);
    }
}
