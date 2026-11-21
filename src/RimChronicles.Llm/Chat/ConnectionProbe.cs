namespace RimChronicles.Llm.Chat;

/// <summary>The settings window's Test Connection: one tiny request, reported in a line the player can act on.</summary>
public static class ConnectionProbe
{
    public sealed record Result(bool Ok, string Message);

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
            return new(false, $"Failed: {Explain(result.Error)}");
        }

        var seconds = result.Latency.TotalSeconds;
        var slow = seconds > 10 ? " That's slow: the first call loads the model, so try again." : "";
        return new(true, $"Connected to {provider.ModelId} in {seconds:0.0} s.{slow}");
    }

    private static string Explain(string? error)
    {
        error ??= "no answer";
        if (error.Contains("404", StringComparison.Ordinal) && error.Contains("model", StringComparison.OrdinalIgnoreCase))
        {
            return $"{error} (Is the model pulled? Try: ollama pull <model>)";
        }

        if (error.Contains("refused", StringComparison.OrdinalIgnoreCase) || error.Contains("No connection", StringComparison.OrdinalIgnoreCase))
        {
            return $"{error} (Is the model server running?)";
        }

        return error;
    }
}
