using System.Text.Json.Nodes;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Llm;

public class LlmTaskRunnerTests
{
    private static readonly string[] Allowed = ["Wait", "Complication:VexSighted"];

    private static readonly PromptTemplate Prompt = new(
        new PromptMeta { Task = "director", Version = "v1", MaxOutputTokens = 100 },
        "Pick a beat.",
        "Options: {{options}}",
        schema: JsonSchemaSpec.Parse("director", """{"type":"object","properties":{"choice":{"type":"string"}},"required":["choice"]}"""));

    private static TaskDefinition<string> Director(ModelTier tier = ModelTier.Recommended) => new()
    {
        Prompt = Prompt,
        Variables = new Dictionary<string, string> { ["options"] = string.Join(", ", Allowed) },
        Checks = [new AllowedChoiceCheck("choice", Allowed)],
        Parse = text => JsonNode.Parse(JsonText.Extract(text))!["choice"]!.GetValue<string>(),
        Fallback = new DelegateFallback<string>(() => "Wait (template)"),
        Tier = tier,
    };

    [Fact]
    public async Task ValidFirstAnswer_IsUsed()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice":"Complication:VexSighted"}"""));

        var outcome = await new LlmTaskRunner(provider).RunAsync(Director(), CancellationToken.None);

        Assert.Equal("Complication:VexSighted", outcome.Value);
        Assert.Equal(OutcomeSource.Model, outcome.Source);
        Assert.Equal("director/v1", outcome.PromptVersion);
        var request = Assert.Single(provider.Requests);
        Assert.True(request.UseGrammar);
        Assert.Equal(100, request.MaxOutputTokens);
    }

    [Fact]
    public async Task TruncatedAnswer_IsRetriedAndNeverShips()
    {
        var cut = FakeChatProvider.Answer("""{"choice":"Wait"}""") with { Truncated = true };
        var provider = new FakeChatProvider(cut, cut);

        var outcome = await new LlmTaskRunner(provider).RunAsync(Director(), CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Equal([LlmTaskRunner.TruncatedFailure], outcome.Attempts[0].Failures);
        Assert.Contains("shorter answer", provider.Requests[1].User);
    }

    [Fact]
    public async Task InvalidFirstAnswer_RetriesWithFeedback()
    {
        var provider = new FakeChatProvider(
            FakeChatProvider.Answer("""{"choice":"Raid"}"""),
            FakeChatProvider.Answer("""{"choice":"Wait"}"""));

        var outcome = await new LlmTaskRunner(provider).RunAsync(Director(), CancellationToken.None);

        Assert.Equal("Wait", outcome.Value);
        Assert.Equal(OutcomeSource.ModelRetry, outcome.Source);
        Assert.Equal(2, outcome.Attempts.Count);
        Assert.NotEmpty(outcome.Attempts[0].Failures);
        Assert.Contains("Your previous answer was rejected", provider.Requests[1].User);
        Assert.Contains("must be exactly one of", provider.Requests[1].User);
    }

    [Fact]
    public async Task TwoInvalidAnswers_FallBackToTemplate()
    {
        var provider = new FakeChatProvider(
            FakeChatProvider.Answer("""{"choice":"Raid"}"""),
            FakeChatProvider.Answer("not json"));

        var outcome = await new LlmTaskRunner(provider).RunAsync(Director(), CancellationToken.None);

        Assert.Equal("Wait (template)", outcome.Value);
        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Equal(2, outcome.Attempts.Count);
    }

    [Fact]
    public async Task ProviderErrors_RetryThenFallBack()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Error("HTTP 500"), FakeChatProvider.Error("Timed out"));

        var outcome = await new LlmTaskRunner(provider).RunAsync(Director(), CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Equal(["HTTP 500"], outcome.Attempts[0].Failures);
        Assert.DoesNotContain("rejected", provider.Requests[1].User);
    }

    [Fact]
    public async Task NoProvider_UsesTemplateWithoutCalling()
    {
        var outcome = await new LlmTaskRunner(null).RunAsync(Director(), CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Empty(outcome.Attempts);
    }

    [Fact]
    public async Task TierOff_UsesTemplateWithoutCalling()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice":"Wait"}"""));

        var outcome = await new LlmTaskRunner(provider).RunAsync(Director(ModelTier.Off), CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task MissingPromptVariable_FallsBackWithoutCalling()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice":"Wait"}"""));
        var task = Director() with { Variables = new Dictionary<string, string>() };

        var outcome = await new LlmTaskRunner(provider).RunAsync(task, CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task ParseException_CountsAsFailure()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice":"Wait"}"""), FakeChatProvider.Answer("""{"choice":"Wait"}"""));
        var task = Director() with { Parse = _ => throw new FormatException("bad") };

        var outcome = await new LlmTaskRunner(provider).RunAsync(task, CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Contains("Output could not be read: bad", outcome.Attempts[0].Failures);
    }

    [Fact]
    public async Task SchemaOverride_IsSentInsteadOfThePromptSchema()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice":"Wait"}"""));
        var custom = JsonSchemaSpec.Parse("director", """{"type":"object","properties":{"choice":{"enum":["Wait"]}}}""");

        await new LlmTaskRunner(provider).RunAsync(Director() with { SchemaOverride = custom }, CancellationToken.None);

        Assert.Same(custom, provider.Requests[0].Schema);
    }

    [Fact]
    public async Task GrammarOff_IsPassedThrough()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice":"Wait"}"""));

        await new LlmTaskRunner(provider).RunAsync(Director() with { UseGrammar = false }, CancellationToken.None);

        Assert.False(provider.Requests[0].UseGrammar);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LlmTaskRunner(new FakeChatProvider()).RunAsync(Director(), cts.Token));
    }
}
