using System.Text.Json.Nodes;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.Tasks;

/// <summary>A task call ready to run, with what it needs for scoring.</summary>
public sealed record EvalTask(TaskDefinition<string> Definition, TaskPromptInput Input, JsonSchemaSpec? BaseSchema, IReadOnlyList<string> Choices)
{
    /// <summary>Allowed length in words for prose tasks; it can depend on the snapshot (a quiet week's bulletin is short).</summary>
    public (int Min, int Max)? Words { get; init; }
}

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
        [WorldSummarizer.DirectorTask, WorldSummarizer.NewsBulletinTask, WorldSummarizer.HandoffTask, PlotLetterTask.TaskName];

    /// <summary>Word ranges from the design spec, except the bulletin minimum: lowered from 80 to 60 (2026-11-21)
    /// because an 80-word floor made models pad thin news weeks with embellishments.</summary>
    public static (int Min, int Max) WordRange(string task, WorldSnapshot s) => task switch
    {
        WorldSummarizer.NewsBulletinTask => WorldSummarizer.BulletinWordRange(s),
        WorldSummarizer.HandoffTask => WorldSummarizer.HandoffWordRange,
        PlotLetterTask.TaskName => PlotLetterTask.WordRange,
        _ => throw new ArgumentException($"{task} has no word range."),
    };

    public static bool UsesSchema(string task) => task == WorldSummarizer.DirectorTask;

    public static bool AppliesTo(string task, WorldSnapshot s) => task switch
    {
        WorldSummarizer.HandoffTask => s.Handoff is not null,
        PlotLetterTask.TaskName => s.PlotLetter is not null,
        _ => true,
    };

    public static EvalTask Build(PromptLibrary library, string task, WorldSnapshot s, ModelConfig model, bool grammar)
    {
        var prompt = library.Get(task);
        var tier = model.PromptTier;

        var input = task switch
        {
            WorldSummarizer.DirectorTask => WorldSummarizer.ForDirector(s, tier),
            WorldSummarizer.NewsBulletinTask => WorldSummarizer.ForNewsBulletin(s, tier),
            WorldSummarizer.HandoffTask => WorldSummarizer.ForHandoff(s),
            PlotLetterTask.TaskName => WorldSummarizer.ForPlotLetter(s.PlotLetter!, s.Colony.Name),
            _ => throw new ArgumentException($"Unknown task '{task}'."),
        };

        IReadOnlyList<IOutputCheck> checks;
        JsonSchemaSpec? schemaOverride = null;
        IReadOnlyList<string> choices = [];
        (int Min, int Max)? words = null;

        if (UsesSchema(task))
        {
            choices = WorldSummarizer.DirectorChoices(s);
            schemaOverride = WithChoiceEnum(prompt.Schema!, choices);
            checks = [new JsonSchemaCheck(schemaOverride.Schema), new AllowedChoiceCheck("choice", choices)];
        }
        else
        {
            words = WordRange(task, s);
            var (min, max) = words.Value;
            // The bulletin's checks come from Core, the same ones the game runs.
            checks = task switch
            {
                WorldSummarizer.NewsBulletinTask => BulletinTask.Checks(s, input),
                PlotLetterTask.TaskName => PlotLetterTask.Checks(s.PlotLetter!, input),
                WorldSummarizer.HandoffTask => HandoffTask.Checks(input),
                _ => [new WordCountCheck(min, max), new GroundingCheck(input.AllowedNames), new BannedPhraseCheck()],
            };
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

        return new EvalTask(definition, input, prompt.Schema, choices) { Words = words };
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

        var (min, max) = evalTask.Words ?? throw new ArgumentException($"{task} has no word range.");
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
