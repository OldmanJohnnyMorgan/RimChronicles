using System.Text;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Llm.Tasks;

/// <summary>
/// Runs a task: render the prompt, call the model, validate, retry once with the failures, then fall back to
/// template text. Never throws for model, transport, prompt or parse errors; only cancellation propagates.
/// </summary>
/// <param name="provider">The model, or null when the LLM is switched off (templates only).</param>
/// <param name="maxAttempts">Model attempts before falling back (the design spec says 2: one retry).</param>
public sealed class LlmTaskRunner(IChatProvider? provider, int maxAttempts = 2)
{
    /// <summary>The failure recorded for an answer cut off at the output token limit.</summary>
    public const string TruncatedFailure = "The answer was cut off by the length limit. Write a shorter answer that ends cleanly.";

    public async Task<TaskOutcome<T>> RunAsync<T>(TaskDefinition<T> task, CancellationToken cancellationToken)
    {
        var version = $"{task.Prompt.Meta.Task}/{task.Prompt.Meta.Version}";
        if (provider is null || task.Tier == ModelTier.Off)
        {
            return Fallback(task, [], version);
        }

        RenderedPrompt rendered;
        try
        {
            rendered = task.Prompt.Render(task.Tier, task.Variables);
        }
        catch (PromptRenderException)
        {
            return Fallback(task, [], version);
        }

        var schema = task.SchemaOverride ?? rendered.Schema;
        var attempts = new List<TaskAttempt>();
        var user = rendered.User;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var request = new ChatRequest
            {
                System = rendered.System,
                User = user,
                MaxOutputTokens = rendered.MaxOutputTokens,
                Schema = schema,
                UseGrammar = task.UseGrammar && schema is not null,
                Thinking = task.Thinking,
            };

            var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!result.Ok || result.Text is null)
            {
                attempts.Add(new TaskAttempt(request, result, [result.Error ?? "No answer"]));
                continue;
            }

            if (result.Truncated)
            {
                // Cut off at the token limit: the text may stop mid-sentence, so it never ships; the retry asks for less.
                List<string> cut = [TruncatedFailure];
                attempts.Add(new TaskAttempt(request, result, cut));
                user = WithFeedback(rendered.User, cut);
                continue;
            }

            var failures = RunChecks(task.Checks, result.Text);
            if (failures.Count == 0)
            {
                if (TryParse(task, result.Text, out var value, out var parseError))
                {
                    attempts.Add(new TaskAttempt(request, result, []));
                    var source = attempt == 1 ? OutcomeSource.Model : OutcomeSource.ModelRetry;
                    return new TaskOutcome<T>(value, source, attempts, rendered.Version);
                }

                failures.Add(parseError!);
            }

            attempts.Add(new TaskAttempt(request, result, failures));
            user = WithFeedback(rendered.User, failures);
        }

        return Fallback(task, attempts, rendered.Version);
    }

    private static TaskOutcome<T> Fallback<T>(TaskDefinition<T> task, IReadOnlyList<TaskAttempt> attempts, string version) =>
        new(task.Fallback.Create(), OutcomeSource.Template, attempts, version);

    private static List<string> RunChecks(IReadOnlyList<IOutputCheck> checks, string text)
    {
        var failures = new List<string>();
        foreach (var check in checks)
        {
            try
            {
                failures.AddRange(check.Check(text).Failures);
            }
            catch (Exception ex)
            {
                failures.Add($"{check.Name} check could not run: {ex.Message}");
            }
        }

        return failures;
    }

    private static bool TryParse<T>(TaskDefinition<T> task, string text, out T value, out string? error)
    {
        try
        {
            value = task.Parse(text);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            value = default!;
            error = $"Output could not be read: {ex.Message}";
            return false;
        }
    }

    /// <summary>The retry prompt: the original user message plus what was wrong with the last answer.</summary>
    internal static string WithFeedback(string user, IReadOnlyList<string> failures)
    {
        var sb = new StringBuilder(user);
        sb.Append("\n\nYour previous answer was rejected:\n");
        foreach (var failure in failures)
        {
            sb.Append("- ").Append(failure).Append('\n');
        }

        sb.Append("Answer again, following every rule.");
        return sb.ToString();
    }
}
