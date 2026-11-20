using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Llm.Tasks;

/// <summary>Where a task's final value came from.</summary>
public enum OutcomeSource
{
    /// <summary>The model's first answer passed every check.</summary>
    Model,

    /// <summary>The model's second answer passed, after a failed first try.</summary>
    ModelRetry,

    /// <summary>Template text, because the model was off, failed or never answered validly.</summary>
    Template,
}

/// <summary>Produces the template version of a task's output. Must always succeed.</summary>
public interface IFallback<out T>
{
    T Create();
}

/// <summary>A fallback built from a delegate.</summary>
public sealed class DelegateFallback<T>(Func<T> create) : IFallback<T>
{
    public T Create() => create();
}

/// <summary>
/// One LLM task call, built per use so checks can carry this call's data (allowed choices, allowed names).
/// </summary>
public sealed record TaskDefinition<T>
{
    public required PromptTemplate Prompt { get; init; }
    public required IReadOnlyDictionary<string, string> Variables { get; init; }
    public required IReadOnlyList<IOutputCheck> Checks { get; init; }

    /// <summary>Turns validated text into the task's value. Exceptions count as a failed check.</summary>
    public required Func<string, T> Parse { get; init; }

    public required IFallback<T> Fallback { get; init; }

    public ModelTier Tier { get; init; } = ModelTier.Recommended;
    public bool UseGrammar { get; init; } = true;
    public ThinkingMode Thinking { get; init; } = ThinkingMode.Default;

    /// <summary>Replaces the prompt's schema for this call, e.g. with the director's allowed choices filled into an enum.</summary>
    public JsonSchemaSpec? SchemaOverride { get; init; }
}

/// <summary>One model attempt, kept for logs, the decision log and the eval harness.</summary>
public sealed record TaskAttempt(ChatRequest Request, ChatResult Result, IReadOnlyList<string> Failures);

/// <summary>The value a task produced, and how it got there.</summary>
public sealed record TaskOutcome<T>(T Value, OutcomeSource Source, IReadOnlyList<TaskAttempt> Attempts, string PromptVersion)
{
    public bool FromModel => Source != OutcomeSource.Template;
}
