using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.Tasks;

/// <summary>A task call ready to run, with what it needs for scoring.</summary>
public sealed record EvalTask(TaskDefinition<string> Definition, TaskPromptInput Input, JsonSchemaSpec? BaseSchema, IReadOnlyList<string> Choices);

/// <summary>Deterministic measurements of one model output.</summary>
public sealed record OutputMetrics
{
    public bool? JsonValid { get; init; }
    public bool? ChoiceAllowed { get; init; }
    public int? WordCount { get; init; }
    public bool? LengthOk { get; init; }
    public IReadOnlyList<string> UnknownNames { get; init; } = [];
    public bool? BannedPhrase { get; init; }
}

/// <summary>Builds the eval version of each task (same prompts and checks the mod will use) and scores outputs.</summary>
public static class EvalTasks
{
    public static readonly IReadOnlyList<string> All =
        [WorldSummarizer.DirectorTask, WorldSummarizer.NewsBulletinTask, WorldSummarizer.HandoffTask];

    /// <summary>Word ranges from the design spec.</summary>
    public static (int Min, int Max) WordRange(string task) => task switch
    {
        WorldSummarizer.NewsBulletinTask => (80, 150),
        WorldSummarizer.HandoffTask => (60, 120),
        _ => throw new ArgumentException($"{task} has no word range."),
    };

    public static bool UsesSchema(string task) => task == WorldSummarizer.DirectorTask;

    public static bool AppliesTo(string task, WorldSnapshot s) => task != WorldSummarizer.HandoffTask || s.Handoff is not null;

    public static EvalTask Build(PromptLibrary library, string task, WorldSnapshot s, ModelConfig model, bool grammar)
    {
        var prompt = library.Get(task);
        var tier = model.PromptTier;

        var input = task switch
        {
            WorldSummarizer.DirectorTask => WorldSummarizer.ForDirector(s, tier),
            WorldSummarizer.NewsBulletinTask => WorldSummarizer.ForNewsBulletin(s, tier),
            WorldSummarizer.HandoffTask => WorldSummarizer.ForHandoff(s),
            _ => throw new ArgumentException($"Unknown task '{task}'."),
        };

        IReadOnlyList<IOutputCheck> checks;
        JsonSchemaSpec? schemaOverride = null;
        IReadOnlyList<string> choices = [];

        if (UsesSchema(task))
        {
            choices = WorldSummarizer.DirectorChoices(s);
            schemaOverride = WithChoiceEnum(prompt.Schema!, choices);
            checks = [new JsonSchemaCheck(schemaOverride.Schema), new AllowedChoiceCheck("choice", choices)];
        }
        else
        {
            var (min, max) = WordRange(task);
            checks = [new WordCountCheck(min, max), new GroundingCheck(input.AllowedNames), new BannedPhraseCheck()];
        }

        var definition = new TaskDefinition<string>
        {
            Prompt = prompt,
            Variables = input.Variables,
            Checks = checks,
            Parse = text => text,
            Fallback = new DelegateFallback<string>(() => "(template)"),
            Tier = tier,
            UseGrammar = grammar,
            Thinking = model.ThinkingFor(task),
            SchemaOverride = schemaOverride,
        };

        return new EvalTask(definition, input, prompt.Schema, choices);
    }

    /// <summary>A copy of the schema whose "choice" property may only take the allowed options.</summary>
    public static JsonSchemaSpec WithChoiceEnum(JsonSchemaSpec schema, IReadOnlyList<string> choices)
    {
        var copy = schema.DeepCopy();
        if (copy.Schema["properties"]?["choice"] is JsonObject choice)
        {
            choice["enum"] = new JsonArray(choices.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
        }

        return copy;
    }

    /// <summary>Scores one output on the deterministic metrics that apply to its task.</summary>
    public static OutputMetrics Score(string task, string? text, EvalTask evalTask)
    {
        if (text is null)
        {
            return new OutputMetrics();
        }

        if (UsesSchema(task))
        {
            return new OutputMetrics
            {
                JsonValid = new JsonSchemaCheck(evalTask.BaseSchema!.Schema).Check(text).Passed,
                ChoiceAllowed = new AllowedChoiceCheck("choice", evalTask.Choices).Check(text).Passed,
            };
        }

        var (min, max) = WordRange(task);
        var words = WordCountCheck.Count(text);
        return new OutputMetrics
        {
            WordCount = words,
            LengthOk = words >= min && words <= max,
            UnknownNames = new GroundingCheck(evalTask.Input.AllowedNames).FindUnknownNames(text),
            BannedPhrase = !new BannedPhraseCheck().Check(text).Passed,
        };
    }
}
