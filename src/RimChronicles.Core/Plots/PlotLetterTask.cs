using System.Globalization;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Core.Plots;

/// <summary>The plot-letter task (Stage 3 M3b), built the same way for the game and the harness. The model rewrites
/// the beat's template letter in the game's voice, keeping every fact and adding none.</summary>
public static class PlotLetterTask
{
    public const string TaskName = "plot-letter";

    /// <summary>Allowed letter length in words. The prompt asks for 40 to 100; the range leaves slack either side.</summary>
    public static readonly (int Min, int Max) WordRange = (30, 120);

    /// <summary>The template letter for a case: the fallback, and the plain version given to the model.</summary>
    public static (string Label, string Text) Template(PlotLetterCase c, string colony)
    {
        return c.Template switch
        {
            NemesisPlot.TemplateId => NemesisLetters.ForStep(c.Step, NemesisLetters.Facts.From(c.Facts, c.Protagonist, colony), BountyCost(c)),
            LostFamily.LostFamilyArc.TemplateId => LostFamily.LostFamilyLetters.ForStep(c.Step, LostFamily.LostFamilyLetters.Facts.From(c.Facts, c.Protagonist, colony)),
            _ => throw new ArgumentException($"No plot letters for arc '{c.Template}'."),
        };
    }

    public static int BountyCost(PlotLetterCase c) =>
        c.Facts.TryGetValue(NemesisArc.BountyCostFact, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost) ? cost : 0;

    public static IReadOnlyList<IOutputCheck> Checks(PlotLetterCase c, TaskPromptInput input)
    {
        var checks = new List<IOutputCheck>
        {
            new WordCountCheck(WordRange.Min, WordRange.Max), new GroundingCheck(input.AllowedNames), new BannedPhraseCheck(),
        };
        if (c.Step == "Choice" && BountyCost(c) > 0)
        {
            checks.Add(new RequiredTextCheck([BountyCost(c).ToString(CultureInfo.InvariantCulture)], "the bounty's price in silver"));
        }

        return checks;
    }

    public static (TaskDefinition<string> Definition, TaskPromptInput Input) Build(
        PromptLibrary library, PlotLetterCase c, string colony, ModelTier tier, ThinkingMode thinking, IFallback<string> fallback)
    {
        var input = WorldSummarizer.ForPlotLetter(c, colony);
        var definition = new TaskDefinition<string>
        {
            Prompt = library.Get(TaskName),
            Variables = input.Variables,
            Checks = Checks(c, input),
            Parse = text => text.Trim(),
            Fallback = fallback,
            Tier = tier,
            UseGrammar = false,
            Thinking = thinking,
        };
        return (definition, input);
    }
}
