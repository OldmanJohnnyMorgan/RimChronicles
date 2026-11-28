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
        Choir.ChoirEnding.Choral => "Choral victory",
        Choir.ChoirEnding.Silenced => "The Choir silenced",
        War.WarEnding.IndependenceTotal => "Total independence",
        _ => "Independence",
    };

    /// <summary>What the ending means, for the prompt and the template. With a rival, the story ended in the War in
    /// Heaven (W11), and the meaning says how the rival fell.</summary>
    public static string Meaning(EpilogueCase c, string colony) => (c.Ending, c.Rival) switch
    {
        (EmpireEnding.Resistance, _) => $"{colony} and its allies destroyed the capital of {c.Empire}, and the Empire fell.",
        (EmpireEnding.Imperial, { } rival) => $"{c.Empire} won the War in Heaven with {colony} at its side, and {rival} was destroyed. "
            + $"{colony} became an Imperial province, governed in the Empire's name.",
        (EmpireEnding.Imperial, _) => $"{colony} served {c.Empire} and became an Imperial province, governed in the Empire's name.",
        (Choir.ChoirEnding.Choral, { } rival) => $"{c.Empire} won the War in Heaven with {colony} at its side, and {rival} was destroyed. "
            + $"{colony} took its place in the Chorus, and its people sing with the Choir now.",
        (Choir.ChoirEnding.Choral, _) => $"{colony} joined the Chorus of {c.Empire}: its people took their place among the Choir's mechanitors.",
        (Choir.ChoirEnding.Silenced, _) => $"{colony} stormed the Cathedral of {c.Empire}, and the Choir fell silent.",
        (War.WarEnding.IndependenceTotal, { } rival) => $"{colony} brought down both great powers, {c.Empire} and {rival}. No one is left on the rim to rule it.",
        (_, { } rival) => $"With {rival} destroyed in the War in Heaven, {c.Empire}, the last power left, signed a treaty recognising {colony} as a free state.",
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
            Choir.ChoirEnding.Choral => "At night the mechs on the walls hum with the rest of the Chorus. The colony never sleeps alone now.",
            Choir.ChoirEnding.Silenced => "The hills don't hum at night anymore. The silence took some getting used to.",
            War.WarEnding.IndependenceTotal => "The ruins of two capitals lie out past the hills. The colony's walls are the only ones that matter now.",
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
