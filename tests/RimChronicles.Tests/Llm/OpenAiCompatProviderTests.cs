using System.Net;
using System.Text.Json.Nodes;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Providers;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Llm;

public class OpenAiCompatProviderTests
{
    private static readonly OpenAiCompatOptions Options = new()
    {
        ModelId = "qwen-test",
        Endpoint = new Uri("http://localhost:11434"),
        Model = "qwen3.5:4b",
        ThinkingOnBody = new JsonObject { ["think"] = true },
        ThinkingOffBody = new JsonObject { ["think"] = false },
        ExtraBody = new JsonObject { ["temperature"] = 0.7 },
    };

    private static readonly JsonSchemaSpec Schema = JsonSchemaSpec.Parse("director",
        """{"type":"object","properties":{"choice":{"type":"string"}},"required":["choice"],"additionalProperties":false}""");

    private static ChatRequest Request(bool grammar = false, ThinkingMode thinking = ThinkingMode.Default) => new()
    {
        System = "sys",
        User = "usr",
        MaxOutputTokens = 200,
        Schema = Schema,
        UseGrammar = grammar,
        Thinking = thinking,
    };

    private const string OkResponse = """
        {"choices":[{"message":{"role":"assistant","content":"<think>hmm</think>\n{\"choice\":\"Wait\"}"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":120,"completion_tokens":40,"completion_tokens_details":{"reasoning_tokens":25}}}
        """;

    [Fact]
    public void BuildBody_WithGrammar_AddsJsonSchemaResponseFormat()
    {
        var body = OpenAiCompatProvider.BuildBody(Request(grammar: true), Options);

        Assert.Equal("json_schema", body["response_format"]!["type"]!.GetValue<string>());
        Assert.Equal("director", body["response_format"]!["json_schema"]!["name"]!.GetValue<string>());
        Assert.Equal("object", body["response_format"]!["json_schema"]!["schema"]!["type"]!.GetValue<string>());
        Assert.Equal(200, body["max_tokens"]!.GetValue<int>());
        Assert.Equal("qwen3.5:4b", body["model"]!.GetValue<string>());
    }

    [Fact]
    public void BuildBody_WithoutGrammar_HasNoResponseFormat()
    {
        var body = OpenAiCompatProvider.BuildBody(Request(grammar: false), Options);

        Assert.Null(body["response_format"]);
    }

    [Theory]
    [InlineData(ThinkingMode.On, true)]
    [InlineData(ThinkingMode.Off, false)]
    public void BuildBody_MergesThinkingFields(ThinkingMode mode, bool expected)
    {
        var body = OpenAiCompatProvider.BuildBody(Request(thinking: mode), Options);

        Assert.Equal(expected, body["think"]!.GetValue<bool>());
        Assert.Equal(0.7, body["temperature"]!.GetValue<double>());
    }

    [Fact]
    public void BuildBody_DefaultThinking_AddsNoThinkingFields()
    {
        var body = OpenAiCompatProvider.BuildBody(Request(), Options);

        Assert.Null(body["think"]);
    }

    [Fact]
    public void BuildBody_RequestExtraBodyOverridesOptions()
    {
        var request = Request() with { ExtraBody = new Dictionary<string, JsonNode?> { ["temperature"] = 0.1 } };

        var body = OpenAiCompatProvider.BuildBody(request, Options);

        Assert.Equal(0.1, body["temperature"]!.GetValue<double>());
    }

    [Fact]
    public void ParseResponse_StripsThinkingAndReadsUsage()
    {
        var result = OpenAiCompatProvider.ParseResponse(OkResponse, "m", TimeSpan.FromSeconds(1));

        Assert.True(result.Ok);
        Assert.Equal("""{"choice":"Wait"}""", result.Text);
        Assert.Equal(120, result.InputTokens);
        Assert.Equal(40, result.OutputTokens);
        Assert.Equal(25, result.ReasoningTokens);
    }

    [Fact]
    public void ParseResponse_OnlyThinkingCutOff_FailsWithLengthMessage()
    {
        const string raw = """{"choices":[{"message":{"content":"<think>still going"},"finish_reason":"length"}]}""";

        var result = OpenAiCompatProvider.ParseResponse(raw, "m", TimeSpan.Zero);

        Assert.False(result.Ok);
        Assert.Contains("token limit", result.Error);
    }

    [Fact]
    public void ParseResponse_TextCutOffAtTheLimit_IsMarkedTruncated()
    {
        const string raw = """{"choices":[{"message":{"content":"The raiders came at dawn and the"},"finish_reason":"length"}]}""";

        var result = OpenAiCompatProvider.ParseResponse(raw, "m", TimeSpan.Zero);

        Assert.True(result.Ok);
        Assert.True(result.Truncated);
        Assert.False(OpenAiCompatProvider.ParseResponse(OkResponse, "m", TimeSpan.Zero).Truncated);
    }

    [Fact]
    public async Task CompleteAsync_TransportErrorOtherThanHttp_ReturnsFailureInsteadOfThrowing()
    {
        var handler = new StubHttpHandler((_, _) => throw new IOException("connection reset mid-body"));
        var provider = new OpenAiCompatProvider(new HttpClient(handler), Options);

        var result = await provider.CompleteAsync(new ChatRequest { System = "s", User = "u", MaxOutputTokens = 10 }, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("connection reset", result.Error);
    }

    [Fact]
    public async Task CompleteAsync_CallerCancellation_StillThrows()
    {
        var handler = new StubHttpHandler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct));
        var provider = new OpenAiCompatProvider(new HttpClient(handler), Options);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.CompleteAsync(new ChatRequest { System = "s", User = "u", MaxOutputTokens = 10 }, cancel.Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"choices":[]}""")]
    public void ParseResponse_BadBody_Fails(string raw)
    {
        var result = OpenAiCompatProvider.ParseResponse(raw, "m", TimeSpan.Zero);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CompleteAsync_PostsToChatCompletionsWithBearerKey()
    {
        var handler = StubHttpHandler.Json(HttpStatusCode.OK, OkResponse);
        var provider = new OpenAiCompatProvider(new HttpClient(handler), Options with { ApiKey = "secret" });

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.True(result.Ok);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("http://localhost:11434/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer secret", request.Headers.Authorization!.ToString());
        Assert.Contains("\"usr\"", body);
    }

    [Fact]
    public async Task CompleteAsync_ServerError_ReturnsFailure()
    {
        var provider = new OpenAiCompatProvider(new HttpClient(StubHttpHandler.Json(HttpStatusCode.InternalServerError, "boom")), Options);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.StartsWith("HTTP 500", result.Error);
    }

    [Fact]
    public async Task CompleteAsync_Timeout_ReturnsFailureInsteadOfThrowing()
    {
        var slow = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = new OpenAiCompatProvider(new HttpClient(slow), Options with { Timeout = TimeSpan.FromMilliseconds(50) });

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.StartsWith("Timed out", result.Error);
    }

    [Fact]
    public async Task CompleteAsync_CallerCancellation_Propagates()
    {
        var slow = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = new OpenAiCompatProvider(new HttpClient(slow), Options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CompleteAsync(Request(), cts.Token));
    }

    [Fact]
    public async Task CompleteAsync_ConnectionRefused_ReturnsFailure()
    {
        var refusing = new StubHttpHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var provider = new OpenAiCompatProvider(new HttpClient(refusing), Options);

        var result = await provider.CompleteAsync(Request(), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Contains("Connection refused", result.Error);
    }
}
