using System.Net;
using System.Text;
using RimChronicles.Llm;
using RimChronicles.Llm.Providers;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Llm;

public class OllamaPullTests
{
    private static readonly Uri Endpoint = new("http://localhost:11434/");

    private static StubHttpHandler Lines(params string[] lines) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Join("\n", lines) + "\n", Encoding.UTF8, "application/x-ndjson"),
        }));

    private static async Task<(OllamaPull.Result Result, List<PullStatus> Seen)> Run(StubHttpHandler handler, string model = "gemma4:e4b")
    {
        var seen = new List<PullStatus>();
        var result = await OllamaPull.RunAsync(new HttpClient(handler), Endpoint, model, null, seen.Add, CancellationToken.None);
        return (result, seen);
    }

    [Fact]
    public async Task ASuccessfulPull_ReportsEachLine_AndPostsTheModel()
    {
        var handler = Lines(
            """{"status":"pulling manifest"}""",
            """{"status":"pulling abc","digest":"sha256:abc","total":2000000000,"completed":500000000}""",
            """{"status":"verifying sha256 digest"}""",
            """{"status":"success"}""");
        var (result, seen) = await Run(handler);

        Assert.True(result.Ok);
        Assert.Equal(4, seen.Count);
        Assert.Equal("Downloading: 0.5 of 2.0 GB (25%)", seen[1].Describe());
        Assert.Equal("pulling manifest", seen[0].Describe());
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("http://localhost:11434/api/pull", request.RequestUri!.ToString());
        Assert.Contains("\"model\":\"gemma4:e4b\"", body);
        Assert.Contains("\"stream\":true", body);
    }

    [Fact]
    public async Task AnErrorLine_Fails()
    {
        var (result, _) = await Run(Lines("""{"status":"pulling manifest"}""", """{"error":"pull model manifest: file does not exist"}"""));
        Assert.False(result.Ok);
        Assert.Contains("file does not exist", result.Message);
    }

    [Fact]
    public async Task AStreamThatEndsWithoutSuccess_Fails()
    {
        var (result, _) = await Run(Lines("""{"status":"pulling manifest"}"""));
        Assert.False(result.Ok);
        Assert.Contains("stopped", result.Message);
    }

    [Fact]
    public async Task AServerWithoutPull_SaysOnlyOllamaCan()
    {
        var (result, _) = await Run(StubHttpHandler.Json(HttpStatusCode.NotFound, "404 page not found"));
        Assert.False(result.Ok);
        Assert.Contains("only Ollama", result.Message);
    }

    [Fact]
    public async Task AnUnreachableServer_PointsAtOllama()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var (result, _) = await Run(handler);
        Assert.False(result.Ok);
        Assert.Contains("ollama.com", result.Message);
    }

    [Fact]
    public async Task ABlankName_IsNotSent()
    {
        var handler = Lines("""{"status":"success"}""");
        var (result, _) = await Run(handler, "  ");
        Assert.False(result.Ok);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("rimchron-director-e4b", "rimchron-director-e4b")]
    [InlineData(" OldmanJohnnyMorgan/RimChron-Director-E4B ", "rimchron-director-e4b")]
    [InlineData("registry.ollama.ai/oldmanjohnnymorgan/rimchron-director-e4b:latest", "rimchron-director-e4b")]
    [InlineData("hf.co/someone/repo:Q4_K_M", "repo:q4_k_m")]
    [InlineData("gemma4:e4b", "gemma4:e4b")]
    [InlineData(null, "")]
    public void BareName_DropsTheNamespaceAndLatest(string? model, string bare) =>
        Assert.Equal(bare, TierPresets.BareName(model));

    [Fact]
    public void Downloads_ListsEachModelOnEachServerOnce()
    {
        var main = new ModelSlot("http://localhost:11434", "gemma4:e4b", "", ModelTier.Recommended, 0.4);
        var director = new ModelSlot("http://localhost:11434/", "oldmanjohnnymorgan/rimchron-director-e4b", "", ModelTier.Recommended, 0.2);
        var sameAsMain = main with { Temperature = 0.2 };
        var elsewhere = main with { Endpoint = "http://other:11434/" };

        var list = ModelSlots.Downloads(main, null, director, sameAsMain, elsewhere);

        Assert.Equal([main, director, elsewhere], list);
    }
}
