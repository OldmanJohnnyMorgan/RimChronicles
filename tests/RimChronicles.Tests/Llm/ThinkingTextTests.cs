using RimChronicles.Llm.Chat;

namespace RimChronicles.Tests.Llm;

public class ThinkingTextTests
{
    [Theory]
    [InlineData("<think>plan</think>Answer", "Answer")]
    [InlineData("<thinking>\nmulti\nline\n</thinking>\n\nAnswer", "Answer")]
    [InlineData("<THINK>x</THINK> A <think>y</think>B", "A B")]
    [InlineData("<think>cut off mid-thought", "")]
    [InlineData("No thinking here", "No thinking here")]
    public void Strip_RemovesThinkingBlocks(string input, string expected) =>
        Assert.Equal(expected, ThinkingText.Strip(input));
}
