using RimChronicles.Core.Empire;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm.Prompts;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class EpilogueTaskTests
{
    private static readonly EpilogueCase Case = new()
    {
        Ending = EmpireEnding.Resistance,
        Empire = "The Sun Throne",
        Stance = "oppose",
        People =
        [
            new EpiloguePerson { Name = "Ysolde", Pronoun = "she", Deed = "led the coalition" },
            new EpiloguePerson { Name = "Bram", Pronoun = "he", Deed = "held the gate", Dead = true },
            new EpiloguePerson { Name = "Hesper", Deed = "sabotaged Calder Mill" },
            new EpiloguePerson { Name = "Jory", Deed = "kept the wounded alive" },
        ],
        Events = ["The Sun Throne took Calder Mill."],
    };

    [Fact]
    public void Template_TellsTheEndingAndNamesEveryone()
    {
        var text = EpilogueTask.Template(Case, "Last Lantern");
        Assert.StartsWith("Last Lantern and its allies destroyed the capital of The Sun Throne", text);
        Assert.Contains("Bram, who held the gate, and did not live to see it", text);
        Assert.Contains("Jory", text);
    }

    [Fact]
    public void Input_GivesThePeopleAndEvents_AndTheChecksRequireTheFirstThreeNames()
    {
        var input = WorldSummarizer.ForEpilogue(Case, "Last Lantern");
        Assert.Contains("- Bram (he): held the gate, and died before the end", input.Variables["people"]);
        Assert.Contains("- The Sun Throne took Calder Mill.", input.Variables["events"]);
        Assert.Equal(["Ysolde", "Bram", "Hesper"], EpilogueTask.Required(Case));

        var required = EpilogueTask.Checks(Case, input).Single(c => c.Name == "required-text");
        Assert.False(required.Check("Ysolde and Bram were there.").Passed);
        Assert.True(required.Check("Ysolde, Bram and Hesper were there.").Passed);
    }

    [Fact]
    public void Prompt_UsesExactlyTheInputVariables()
    {
        var prompt = PromptLibrary.Load(Repo.Prompts).Get(EpilogueTask.TaskName);
        var input = WorldSummarizer.ForEpilogue(Case, "Last Lantern");
        Assert.Equal(prompt.Variables.OrderBy(v => v), input.Variables.Keys.OrderBy(v => v));
    }

    // ---- War in Heaven W11: the Choir's endings and the war's

    [Theory]
    [InlineData("Choral", null, "joined the Chorus of The Hollow Choir", "Choral victory")]
    [InlineData("Choral", "The Sun Throne", "won the War in Heaven with Last Lantern at its side, and The Sun Throne was destroyed", "Choral victory")]
    [InlineData("Imperial", "The Hollow Choir", "won the War in Heaven with Last Lantern at its side, and The Hollow Choir was destroyed", "Imperial victory")]
    [InlineData("Silenced", null, "stormed the Cathedral of The Hollow Choir", "The Choir silenced")]
    [InlineData("Independence", "The Sun Throne", "With The Sun Throne destroyed in the War in Heaven, The Hollow Choir, the last power left", "Independence")]
    [InlineData("IndependenceTotal", "The Sun Throne", "brought down both great powers, The Hollow Choir and The Sun Throne", "Total independence")]
    public void TheNewEndings_HaveTheirMeaning_TheirHeadline_AndATemplate(string ending, string? rival, string meaning, string headline)
    {
        var c = Case with { Ending = ending, Empire = "The Hollow Choir", Rival = rival };
        Assert.Contains(meaning, EpilogueTask.Meaning(c, "Last Lantern"));
        Assert.Equal(headline, EpilogueTask.Headline(ending));
        var text = EpilogueTask.Template(c, "Last Lantern");
        Assert.All(c.People, p => Assert.Contains(p.Name, text));
        Assert.Contains(meaning, WorldSummarizer.ForEpilogue(c, "Last Lantern").Variables["ending"]);
    }

    [Fact]
    public void TheOldEndings_ReadAsBefore_WithoutARival()
    {
        Assert.Equal("Last Lantern and its allies destroyed the capital of The Sun Throne, and the Empire fell.", EpilogueTask.Meaning(Case, "Last Lantern"));
        Assert.Equal("The Sun Throne signed a treaty recognising Last Lantern as a free state, bound to no one.",
            EpilogueTask.Meaning(Case with { Ending = EmpireEnding.Independence }, "Last Lantern"));
    }
}
