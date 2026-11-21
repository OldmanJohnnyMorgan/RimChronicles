using RimChronicles.Core.Plots;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PlotLetterTaskTests
{
    private static PlotLetterCase Case(string step, string? cost = null)
    {
        var facts = new Dictionary<string, string>
        {
            ["nemesisName"] = "Vex", ["nemesisFaction"] = "Vex's Crew", ["victimName"] = "Dorin", ["relation"] = "husband",
            ["grievance"] = "killed", ["place"] = "the gorge road",
        };
        if (cost is not null)
        {
            facts["bountyCost"] = cost;
        }

        return new PlotLetterCase { Template = "Nemesis", Step = step, Protagonist = "Mara", Facts = facts };
    }

    public static TheoryData<string> Steps => new()
    {
        "Sighting", "Raid", "Choice", "SecondRaid", "Ending:KilledByColony", "Ending:KilledByBounty", "Ending:KilledOtherwise",
        "Ending:Captured", "Ending:Vanished", "Ending:FeudFaded",
    };

    [Theory]
    [MemberData(nameof(Steps))]
    public void EveryStepHasATemplateThatPassesItsOwnChecks(string step)
    {
        var c = Case(step, step == "Choice" ? "350" : null);
        var input = WorldSummarizer.ForPlotLetter(c, "Hearthstead");
        var (label, text) = PlotLetterTask.Template(c, "Hearthstead");

        var failures = PlotLetterTask.Checks(c, input).SelectMany(check => check.Check(text).Failures).ToList();

        Assert.StartsWith("Nemesis:", label);
        Assert.True(failures.Count == 0, $"{step}: {text}\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void ChoiceLetters_MustNameThePrice()
    {
        var c = Case("Choice", "350");
        var input = WorldSummarizer.ForPlotLetter(c, "Hearthstead");

        var check = Assert.Single(PlotLetterTask.Checks(c, input), x => x is RequiredTextCheck);

        Assert.False(check.Check("Vex escaped again. Pay the hunters, or wait for Vex to return.").Passed);
        Assert.True(check.Check("Vex escaped again. The hunters want 350 silver.").Passed);
        Assert.DoesNotContain(PlotLetterTask.Checks(Case("Raid"), input), x => x is RequiredTextCheck);
    }

    [Fact]
    public void Variables_GiveTheFactsThePurposeAndThePlainLetter()
    {
        var input = WorldSummarizer.ForPlotLetter(Case("Sighting"), "Hearthstead");

        Assert.Equal(["arc", "facts", "plainLetter", "purpose"], input.Variables.Keys.Order());
        Assert.Contains("- Raider: Vex", input.Variables["facts"]);
        Assert.Contains("- Colony: Hearthstead", input.Variables["facts"]);
        Assert.Contains("Victim's relation to the protagonist: husband", input.Variables["facts"]);
        Assert.Contains("unsettle", input.Variables["purpose"]);
        Assert.Contains("Mara's husband Dorin", input.Variables["plainLetter"]);
        Assert.Contains("Vex", input.AllowedNames);
    }

    [Fact]
    public void UnknownSteps_AndArcs_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => PlotLetterTask.Template(Case("Dance"), "C"));
        Assert.Throws<ArgumentException>(() => PlotLetterTask.Template(Case("Sighting") with { Template = "Grief" }, "C"));
        Assert.Throws<ArgumentException>(() => PlotLetterTask.Template(Case("Sighting") with { Template = "LostFamily" }, "C"));
    }

    [Fact]
    public async Task WithNoModel_TheTemplateIsTheLetter()
    {
        var c = Case("Raid");
        var (plainLabel, plainText) = PlotLetterTask.Template(c, "Hearthstead");
        var (definition, _) = PlotLetterTask.Build(PromptLibrary.Load(Repo.Prompts), c, "Hearthstead", ModelTier.Recommended,
            ThinkingMode.Off, new DelegateFallback<string>(() => plainText));

        var outcome = await new LlmTaskRunner(provider: null).RunAsync(definition, CancellationToken.None);

        Assert.Equal((OutcomeSource.Template, plainText), (outcome.Source, outcome.Value));
        Assert.Contains("leads the raid", plainLabel);
    }

    [Fact]
    public void HarnessCases_RenderForEveryTier()
    {
        var cases = Directory.GetFiles(Repo.Snapshots, "*.json").Select(SnapshotJson.Load).Where(s => s.PlotLetter is not null).ToList();

        Assert.Equal(6, cases.Count);
        Assert.Equal(["Choice", "Ending:KilledByColony", "Letter", "Raid", "Sighting", "Taken"], cases.Select(s => s.PlotLetter!.Step).Order());
        Assert.All(cases, s => Assert.NotEmpty(WorldSummarizer.ForPlotLetter(s.PlotLetter!, s.Colony.Name).Variables["plainLetter"]));
    }
}
