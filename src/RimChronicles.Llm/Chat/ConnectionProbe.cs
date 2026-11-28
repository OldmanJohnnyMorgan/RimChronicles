namespace RimChronicles.Llm.Chat;

/// <summary>The settings window's Test Connection: one tiny request, reported in a line the player can act on.</summary>
public static class ConnectionProbe
{
    /// <summary>What went wrong, when the window can offer a fix: download the model, or get the server.</summary>
    public enum Problem { None, Unreachable, MissingModel }

    public sealed record Result(bool Ok, string Message, Problem Problem = Problem.None);

    public static async Task<Result> RunAsync(IChatProvider provider, CancellationToken cancellationToken)
    {
        var request = new ChatRequest
        {
            System = "You are a connection test. Reply with the single word OK.",
            User = "Reply with OK.",
            MaxOutputTokens = 16,
            Thinking = ThinkingMode.Off,
        };

        ChatResult result;
        try
        {
            result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(false, "Cancelled.");
        }

        if (!result.Ok)
        {
            var problem = IsMissingModel(result.Error) ? Problem.MissingModel : IsUnreachable(result.Error) ? Problem.Unreachable : Problem.None;
            return new(false, $"Failed: {Explain(result.Error)}", problem);
        }

        var seconds = result.Latency.TotalSeconds;
        var slow = seconds > 10 ? " That's slow: the first call loads the model, so try again." : "";
        return new(true, $"Connected to {provider.ModelId} in {seconds:0.0} s.{slow}");
    }

    /// <summary>What to check when the server can't be reached at all.</summary>
    public const string ServerHint = "Is the model server running? For Ollama, install it from ollama.com and start it.";

    /// <summary>Whether a failure means the server has no such model, so downloading it would help.</summary>
    public static bool IsMissingModel(string? error) =>
        error is not null && error.Contains("404", StringComparison.Ordinal) && error.Contains("model", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a failure means nothing answered at the address.</summary>
    public static bool IsUnreachable(string? error) =>
        error is not null && (error.Contains("refused", StringComparison.OrdinalIgnoreCase) || error.Contains("No connection", StringComparison.OrdinalIgnoreCase));

    private static string Explain(string? error)
    {
        error ??= "no answer";
        if (IsMissingModel(error))
        {
            return $"{error} (The model isn't downloaded: press Download model, or run ollama pull <model>.)";
        }

        if (IsUnreachable(error))
        {
            return $"{error} ({ServerHint})";
        }

        return error;
    }
}
