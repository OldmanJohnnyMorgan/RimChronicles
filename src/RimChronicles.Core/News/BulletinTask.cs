using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Core.News;

/// <summary>The news-bulletin task, built the same way for the game and for the Stage 0 harness, so the harness
/// keeps testing exactly what the game runs.</summary>
public static class BulletinTask
{
    /// <summary>The checks a bulletin must pass: length scaled to the news, no names outside the digest, no banned
    /// phrases, and no sentence copied from the last two bulletins.</summary>
    public static IReadOnlyList<IOutputCheck> Checks(WorldSnapshot snapshot, TaskPromptInput input)
    {
        var (min, max) = WorldSummarizer.BulletinWordRange(snapshot);
        return
        [
            new WordCountCheck(min, max), new GroundingCheck(input.AllowedNames), new BannedPhraseCheck(),
            new RepetitionCheck(snapshot.PreviousBulletins.TakeLast(2)),
        ];
    }

    public static (TaskDefinition<string> Definition, TaskPromptInput Input) Build(
        PromptLibrary library, WorldSnapshot snapshot, ModelTier tier, ThinkingMode thinking, IFallback<string> fallback)
    {
        var input = WorldSummarizer.ForNewsBulletin(snapshot, tier);
        var definition = new TaskDefinition<string>
        {
            Prompt = library.Get(WorldSummarizer.NewsBulletinTask),
            Variables = input.Variables,
            Checks = Checks(snapshot, input),
            Parse = text => text.Trim(),
            Fallback = fallback,
            Tier = tier,
            UseGrammar = false,
            Thinking = thinking,
        };
        return (definition, input);
    }
}
