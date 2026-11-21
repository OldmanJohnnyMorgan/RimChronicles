using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Core.Plots;

/// <summary>The inheritance-handoff task, built the same way for the game and the harness.</summary>
public static class HandoffTask
{
    public static IReadOnlyList<IOutputCheck> Checks(TaskPromptInput input)
    {
        var (min, max) = WorldSummarizer.HandoffWordRange;
        return [new WordCountCheck(min, max), new GroundingCheck(input.AllowedNames), new BannedPhraseCheck()];
    }

    public static (TaskDefinition<string> Definition, TaskPromptInput Input) Build(
        PromptLibrary library, WorldSnapshot snapshot, ModelTier tier, ThinkingMode thinking, IFallback<string> fallback)
    {
        var input = WorldSummarizer.ForHandoff(snapshot);
        return (new TaskDefinition<string>
        {
            Prompt = library.Get(WorldSummarizer.HandoffTask),
            Variables = input.Variables,
            Checks = Checks(input),
            Parse = text => text.Trim(),
            Fallback = fallback,
            Tier = tier,
            UseGrammar = false,
            Thinking = thinking,
        }, input);
    }

    /// <summary>The template handoff, used when the model is off or fails (rule 7), and for losses that aren't deaths.</summary>
    public static (string Label, string Text) Template(string oldName, string heirName, string relation, ProtagonistLoss loss,
        string unfinished, ProtagonistKind heirKind)
    {
        var lost = loss switch
        {
            ProtagonistLoss.Taken => $"{oldName} was taken, and no one knows where.",
            ProtagonistLoss.Left => $"{oldName} has left the colony.",
            _ => $"{oldName} is gone.",
        };
        var who = heirKind switch
        {
            ProtagonistKind.Colony => "No one close to them is left, so the whole colony takes it up.",
            ProtagonistKind.Animal => $"{heirName}, the animal bonded to {oldName}, will not leave the door, and the others have noticed.",
            _ => relation.Length > 0 ? $"It falls to {heirName}, who was {oldName}'s {relation}." : $"It falls to {heirName}.",
        };
        var resolve = heirKind == ProtagonistKind.Colony
            ? "Every colonist feels it now. The work goes on, but the talk at meals keeps turning to the road."
            : $"The others can see it in how {heirName} carries on: the work goes on, but the eyes keep turning to the road.";
        return ($"Story: {heirName} takes up the cause",
            $"{lost} The colony is quieter for it, and no one has said much since. What {oldName} left unfinished does not end here: "
            + $"{unfinished}.\n\n{who} {resolve} It will be finished, one way or another.");
    }
}
