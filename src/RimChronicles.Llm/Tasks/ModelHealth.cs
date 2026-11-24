namespace RimChronicles.Llm.Tasks;

/// <summary>Decides when to tell the player a model isn't answering: once when a model's calls start failing to reach
/// it, and again only after it has answered in between. A model that answers but fails the checks is reachable: that
/// is the template fallback doing its job, not something the player must fix. Not thread-safe: call it from one thread.</summary>
public sealed class ModelHealth
{
    private readonly HashSet<string> _down = new(StringComparer.Ordinal);

    /// <summary>Records a task's attempts. Returns the notice to show when this call is the first since the model last
    /// answered (or since the start) to get no answer at all, else null.</summary>
    /// <param name="role">What the model does, for the notice: "narration" or "director".</param>
    public string? Observe(IReadOnlyList<TaskAttempt> attempts, string role)
    {
        if (attempts.Count == 0)
        {
            return null;
        }

        var model = attempts[^1].Result.ModelId;
        if (attempts.Any(a => a.Result.Ok))
        {
            _down.Remove(model);
            return null;
        }

        if (!_down.Add(model))
        {
            return null;
        }

        var why = attempts[^1].Result.Error is { Length: > 0 } error ? error : "no answer";
        var name = model.Length > 0 ? $"the {role} model ({model})" : $"the {role} model";
        return $"Rim Chronicles couldn't reach {name}: {why}.\n\nUntil it answers, the mod uses its written text instead. "
            + "To check the setup, open Options → Mod settings → Rim Chronicles and press Test connection.";
    }
}
