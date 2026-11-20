using System.Text.RegularExpressions;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Llm.Prompts;

/// <summary>A prompt's meta.json.</summary>
public sealed record PromptMeta
{
    public required string Task { get; init; }
    public required string Version { get; init; }
    public int MaxOutputTokens { get; init; } = 512;

    /// <summary>Schema file name in the prompt folder (e.g. "schema.json"), or null for free text.</summary>
    public string? Schema { get; init; }

    /// <summary>Per-tier overrides, keyed by tier name.</summary>
    public Dictionary<string, PromptTierOverride> Tiers { get; init; } = [];
}

/// <summary>Values that replace the defaults for one tier.</summary>
public sealed record PromptTierOverride
{
    public int? MaxOutputTokens { get; init; }

    /// <summary>File used instead of user.md for this tier, e.g. "user.Minimum.md".</summary>
    public string? UserTemplate { get; init; }
}

/// <summary>The system and user text for one call, ready to send.</summary>
public sealed record RenderedPrompt(string System, string User, int MaxOutputTokens, JsonSchemaSpec? Schema, string Version);

/// <summary>Thrown when a template uses a variable the caller did not supply.</summary>
public sealed class PromptRenderException(string message) : Exception(message);

/// <summary>One loaded prompt version. Rendering is plain {{name}} substitution; templates hold no logic.</summary>
public sealed partial class PromptTemplate
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}")]
    private static partial Regex Placeholder();

    private readonly string _system;
    private readonly string _user;
    private readonly IReadOnlyDictionary<ModelTier, string> _userByTier;

    public PromptTemplate(PromptMeta meta, string system, string user,
        IReadOnlyDictionary<ModelTier, string>? userByTier = null, JsonSchemaSpec? schema = null)
    {
        Meta = meta;
        _system = system;
        _user = user;
        _userByTier = userByTier ?? new Dictionary<ModelTier, string>();
        Schema = schema;
    }

    public PromptMeta Meta { get; }
    public JsonSchemaSpec? Schema { get; }

    /// <summary>Every variable name used by any of this prompt's templates.</summary>
    public IReadOnlySet<string> Variables =>
        new[] { _system, _user }.Concat(_userByTier.Values)
            .SelectMany(t => Placeholder().Matches(t).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Renders the prompt for a tier. Throws <see cref="PromptRenderException"/> when a variable is missing.</summary>
    public RenderedPrompt Render(ModelTier tier, IReadOnlyDictionary<string, string> variables)
    {
        Meta.Tiers.TryGetValue(tier.ToString(), out var over);
        var user = _userByTier.TryGetValue(tier, out var tierUser) ? tierUser : _user;

        return new RenderedPrompt(
            Fill(_system, variables),
            Fill(user, variables),
            over?.MaxOutputTokens ?? Meta.MaxOutputTokens,
            Schema?.DeepCopy(),
            $"{Meta.Task}/{Meta.Version}");
    }

    private string Fill(string template, IReadOnlyDictionary<string, string> variables)
    {
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var result = Placeholder().Replace(template, m =>
        {
            var name = m.Groups[1].Value;
            if (variables.TryGetValue(name, out var value))
            {
                return value;
            }

            missing.Add(name);
            return m.Value;
        });

        if (missing.Count > 0)
        {
            throw new PromptRenderException(
                $"Prompt {Meta.Task}/{Meta.Version} is missing variables: {string.Join(", ", missing)}");
        }

        return result;
    }
}
