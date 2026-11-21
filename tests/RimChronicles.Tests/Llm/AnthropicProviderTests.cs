using RimChronicles.Llm.Anthropic;

namespace RimChronicles.Tests.Llm;

public class AnthropicProviderTests
{
    [Theory]
    [InlineData(500, 8000, 8500)]
    [InlineData(300, 0, 300)]
    [InlineData(300, -5, 300)]
    public void MaxTokens_AddThinkingHeadroomToTheAnswerCap(int answer, int headroom, long expected) =>
        Assert.Equal(expected, AnthropicProvider.MaxTokensFor(answer, headroom));
}
