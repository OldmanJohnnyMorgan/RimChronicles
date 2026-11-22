using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Core.Empire;

/// <summary>The epilogue task (Stage 5 M5), built the same way for the game and the harness: the end of the Empire
/// story, naming the colonists who mattered. The template epilogue is the fallback.</summary>
public static class EpilogueTask
{
    public const string TaskName = "epilogue";

    /// <summary>Allowed length in words. The prompt asks for 80 to 160; the range leaves slack either side. Lowered
    /// from 100–280 with v2 (2026-11-22): the cases hold few facts, and small models padded to reach 100.</summary>
    public static readonly (int Min, int Max) WordRange = (60, 220);

    /// <summary>The people the epilogue must name: the first three.</summary>
    public static IReadOnlyList<string> Required(EpilogueCase c) => c.People.Take(3).Select(p => p.Name).ToList();

    public static string Headline(string ending) => ending switch
    {
        EmpireEnding.Resistance => "Resistance victory",
        EmpireEnding.Imperial => "Imperial victory",
        _ => "Independence",
    };

    /// <summary>What the ending means, for the prompt and the template.</summary>
    public static string Meaning(EpilogueCase c, string colony) => c.Ending switch
    {
        EmpireEnding.Resistance => $"{colony} and its allies destroyed the capital of {c.Empire}, and the Empire fell.",
        EmpireEnding.Imperial => $"{colony} served {c.Empire} and became an Imperial province, governed in the Empire's name.",
        _ => $"{c.Empire} signed a treaty recognising {colony} as a free state, bound to no one.",
    };

    public static string Template(EpilogueCase c, string colony)
    {
        var people = c.People.Take(4).Select(p => p.Dead ? $"{p.Name}, who {p.Deed}, and did not live to see it" : $"{p.Name}, who {p.Deed}").ToList();
        var middle = people.Count == 0
            ? $"Many hands built {colony}, and many were lost along the way."
            : $"It will be told of {string.Join("; of ", people)}.";
        var close = c.Ending switch
        {
            EmpireEnding.Resistance => "The Imperial banners came down one by one. The rim is quieter now, and poorer, and free.",
            EmpireEnding.Imperial => "The banners over the gate are Imperial now. The colony has walls, and law, and a place in something larger.",
            _ => "The treaty hangs framed in the common room. No one bows to anyone here.",
        };
        return $"{Meaning(c, colony)}\n\n{middle}\n\n{close}";
    }

    public static IReadOnlyList<IOutputCheck> Checks(EpilogueCase c, TaskPromptInput input) =>
    [
        new WordCountCheck(WordRange.Min, WordRange.Max), new GroundingCheck(input.AllowedNames), new BannedPhraseCheck(),
        new RequiredTextCheck(Required(c), "every one of the first three colonists by name"),
    ];

    public static (TaskDefinition<string> Definition, TaskPromptInput Input) Build(
        PromptLibrary library, EpilogueCase c, string colony, ModelTier tier, ThinkingMode thinking, IFallback<string> fallback)
    {
        var input = WorldSummarizer.ForEpilogue(c, colony);
        return (new TaskDefinition<string>
        {
            Prompt = library.Get(TaskName),
            Variables = input.Variables,
            Checks = Checks(c, input),
            Parse = text => text.Trim(),
            Fallback = fallback,
            Tier = tier,
            UseGrammar = false,
            Thinking = thinking,
        }, input);
    }
}
