using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Core.Plots;

/// <summary>One thing a plot may do next. <see cref="Id"/> is what the director sees and copies back
/// ("Complication:VexRaids", "Wait"); <see cref="Step"/> is the arc's own name for it.</summary>
public sealed record PlotTransition(string Id, string Step, double Weight);

/// <summary>A director decision, kept as the plot's story outline and debug log (design spec, Stage 4).</summary>
public sealed class DirectorDecision : IArchivable
{
    public int Day;
    public string PlotId = "";
    public string Options = "";
    public string Choice = "";
    public string Reason = "";

    /// <summary>"Model", "ModelRetry", "Random" (no model, or no valid answer) or "Only" (a single option).</summary>
    public string Source = "";

    public void Expose(IArchive archive)
    {
        archive.Value(ref Day, "day");
        archive.Value(ref PlotId, "plotId", "");
        archive.Value(ref Options, "options", "");
        archive.Value(ref Choice, "choice", "");
        archive.Value(ref Reason, "reason", "");
        archive.Value(ref Source, "source", "");
        PlotId ??= "";
        Options ??= "";
        Choice ??= "";
        Reason ??= "";
        Source ??= "";
    }

    public override string ToString() => $"Day {Day}, {PlotId}: {Choice} ({Source}). {Reason}";
}

/// <summary>The director's choice and reason, as parsed from the model or picked at random.</summary>
public sealed record DirectorChoice(string Choice, string Reason);

/// <summary>The plot director (design spec, Stage 4): when a plot's beat is due, the model picks one of its allowed
/// transitions with a short reason, using the director prompt (grammar-constrained JSON); without a model, or without a
/// valid answer, a weighted random pick stands in. Built the same way for the game and the harness.</summary>
public static class Director
{
    public const string WaitId = "Wait";

    /// <summary>Pairs of beats that fired closer together than <paramref name="minDays"/> (the Stage 4 exit test: no
    /// overlapping beats). Waits don't count; they fire nothing.</summary>
    public static IReadOnlyList<(DirectorDecision First, DirectorDecision Second)> SpacingViolations(IReadOnlyList<DirectorDecision> decisions, int minDays)
    {
        var beats = decisions.Where(d => d.Choice != WaitId).OrderBy(d => d.Day).ToList();
        return beats.Zip(beats.Skip(1)).Where(pair => pair.Second.Day - pair.First.Day < minDays).ToList();
    }

    /// <summary>A weighted random pick. <paramref name="roll"/> is a number in [0, 1) from the caller.</summary>
    public static PlotTransition PickWeighted(IReadOnlyList<PlotTransition> options, double roll)
    {
        var total = options.Sum(o => Math.Max(0, o.Weight));
        if (total <= 0)
        {
            return options[0];
        }

        var target = Math.Clamp(roll, 0, 0.999999) * total;
        foreach (var option in options)
        {
            target -= Math.Max(0, option.Weight);
            if (target < 0)
            {
                return option;
            }
        }

        return options[^1];
    }

    /// <summary>A copy of the director schema whose "choice" may only be one of the allowed options.</summary>
    public static JsonSchemaSpec WithChoiceEnum(JsonSchemaSpec schema, IReadOnlyList<string> choices)
    {
        var copy = schema.DeepCopy();
        if (copy.Schema["properties"]?["choice"] is JsonObject choice)
        {
            choice["enum"] = new JsonArray(choices.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
        }

        return copy;
    }

    /// <summary>The director task for a snapshot whose <c>allowedTransitions</c> are the options.</summary>
    public static (TaskDefinition<DirectorChoice> Definition, TaskPromptInput Input) Build(
        PromptLibrary library, WorldSnapshot snapshot, ModelTier tier, bool grammar, IFallback<DirectorChoice> fallback)
    {
        var prompt = library.Get(WorldSummarizer.DirectorTask);
        var choices = WorldSummarizer.DirectorChoices(snapshot);
        var schema = WithChoiceEnum(prompt.Schema!, choices);
        var input = WorldSummarizer.ForDirector(snapshot, tier);
        return (new TaskDefinition<DirectorChoice>
        {
            Prompt = prompt,
            Variables = input.Variables,
            Checks = [new JsonSchemaCheck(schema.Schema), new AllowedChoiceCheck("choice", choices)],
            Parse = Parse,
            Fallback = fallback,
            Tier = tier,
            UseGrammar = grammar,
            Thinking = ThinkingMode.Off,
            SchemaOverride = schema,
        }, input);
    }

    private static DirectorChoice Parse(string text)
    {
        var json = JsonNode.Parse(JsonText.Extract(text)) ?? throw new JsonException("Empty director answer.");
        return new DirectorChoice(json["choice"]?.GetValue<string>() ?? "", json["reason"]?.GetValue<string>()?.Trim() ?? "");
    }
}
