using RimChronicles.Llm.Validation;

namespace RimChronicles.Tests.Llm;

public class RepetitionCheckTests
{
    private const string Earlier =
        "A quiet week. The silence is almost oppressive these days. One assumes the rim is simply taking a collective breath before the next inevitable mess.";

    [Fact]
    public void FailsASentenceCopiedFromAnEarlierBulletin()
    {
        var result = new RepetitionCheck([Earlier]).Check(
            "Nothing new has come across the wire. One assumes the rim is simply taking a collective breath before the next inevitable mess.");

        Assert.False(result.Passed);
        Assert.Contains("one assumes the rim is simply taking a", result.Failures[0]);
    }

    [Fact]
    public void IgnoresCaseAndPunctuation()
    {
        var result = new RepetitionCheck([Earlier]).Check("ONE ASSUMES, the rim is simply taking a collective breath!");

        Assert.False(result.Passed);
    }

    [Fact]
    public void PassesShortCommonPhrasesAndNewWording()
    {
        var check = new RepetitionCheck([Earlier]);

        Assert.True(check.Check("A quiet week. One assumes the dust is merely settling before the next squabble.").Passed);
        Assert.True(check.Check("The silence is almost oppressive, and the traders are bored.").Passed);
    }

    [Fact]
    public void PassesWithNoEarlierBulletins() =>
        Assert.True(new RepetitionCheck([]).Check(Earlier).Passed);
}
