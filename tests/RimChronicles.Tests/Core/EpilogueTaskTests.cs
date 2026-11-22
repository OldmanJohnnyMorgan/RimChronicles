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
        Assert.Contains("- Bram (he, died before the end): held the gate", input.Variables["people"]);
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
}
