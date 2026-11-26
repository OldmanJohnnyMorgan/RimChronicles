namespace RimChronicles.Llm;

/// <summary>The model each tier preset picks, as chosen by Stage 0 (docs/stage0-results.md). All are Ollama tags,
/// run with thinking off and temperature 0.4.</summary>
public static class TierPresets
{
    public const string DefaultEndpoint = "http://localhost:11434/";

    /// <summary>Sampling temperature for every preset; Stage 0 found server defaults invent more.</summary>
    public const double Temperature = 0.4;

    /// <summary>Temperature for a separate director model: a fine-tuned director was exported at 0.2 (training spec §6),
    /// and a decision wants the model's best pick, not variety.</summary>
    public const double DirectorTemperature = 0.2;

    /// <summary>The preset's model, or null for <see cref="ModelTier.Off"/> (templates only).</summary>
    public static string? ModelFor(ModelTier tier) => tier switch
    {
        ModelTier.Minimum => "gemma4:e2b",
        ModelTier.Recommended => "gemma4:e4b",
        ModelTier.High => "gemma4:12b",
        _ => null,
    };

    /// <summary>The fine-tuned directors offered as choices in the settings window, each with the tier it was trained on
    /// and the vocabulary its training cases covered (War in Heaven tech spec R12). A model joins this list only after its
    /// report is committed to <c>harness/reports/</c>.</summary>
    public static readonly IReadOnlyList<(string Model, ModelTier Tier, string Label, DirectorVocabulary Vocabulary)> DirectorPresets =
    [
        ("rimchron-director-e2b", ModelTier.Minimum, "rimchron-director-e2b: fine-tuned on Gemma 4 E2B (about 2 GB of VRAM), Minimum prompts", DirectorVocabulary.Base),
        ("rimchron-director-e4b", ModelTier.Recommended, "rimchron-director-e4b: fine-tuned on Gemma 4 E4B (about 4 GB of VRAM), Recommended prompts", DirectorVocabulary.WarInHeaven), // director-e4b-war-v2 (#157)
    ];

    /// <summary>
    /// The plots this model may decide (War in Heaven tech spec R12). A fine-tuned director has only seen the options it
    /// was trained on: its preset says how far its training went, and an unlisted <c>rimchron-director-*</c> name counts as
    /// trained on the base arcs only. Any other model is prompted, not fine-tuned, and reads the options' descriptions like
    /// any others, so it knows everything. What a model doesn't know picks by weight.
    /// </summary>
    public static DirectorVocabulary Vocabulary(string? model)
    {
        if (DirectorPreset(model) is { } preset)
        {
            return preset.Vocabulary;
        }

        return (model ?? "").Trim().StartsWith(FineTunedDirectorPrefix, StringComparison.OrdinalIgnoreCase)
            ? DirectorVocabulary.Base
            : DirectorVocabulary.WarInHeaven;
    }

    /// <summary>Whether this model may decide the Hollow Choir's plot, its crisis and the War in Heaven.</summary>
    public static bool KnowsWarInHeaven(string? model) => Vocabulary(model) >= DirectorVocabulary.WarInHeaven;

    /// <summary>The name every fine-tuned director starts with (training/README.md, D6).</summary>
    public const string FineTunedDirectorPrefix = "rimchron-director";

    /// <summary>
    /// Whether this model may make the Imperial crisis's decisions (crisis C11, tech spec R17). A fine-tuned director has
    /// only seen the options it was trained on, so it decides the crisis only when its preset says its training included
    /// the crisis arc; an unlisted <c>rimchron-director-*</c> name counts as trained before it. Any other model is
    /// prompted, not fine-tuned, and reads the options' descriptions like any others. Otherwise the crisis picks by weight.
    /// </summary>
    public static bool KnowsCrisis(string? model) => Vocabulary(model) >= DirectorVocabulary.Crisis;

    /// <summary>The preset matching a director model name (trimmed, any case), or null for a custom name or blank.</summary>
    public static (string Model, ModelTier Tier, string Label, DirectorVocabulary Vocabulary)? DirectorPreset(string? model)
    {
        var name = (model ?? "").Trim();
        foreach (var preset in DirectorPresets)
        {
            if (string.Equals(preset.Model, name, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return null;
    }

    /// <summary>The tier a fine-tuned director was trained on, from its name (<c>rimchron-director-e2b</c>: Minimum;
    /// <c>-e4b</c>: Recommended; <c>-12b</c>: High), or null when the name doesn't say.</summary>
    public static ModelTier? SuggestedDirectorTier(string? model)
    {
        var name = (model ?? "").Trim().ToLowerInvariant();
        return name.Contains("e2b", StringComparison.Ordinal) ? ModelTier.Minimum
            : name.Contains("e4b", StringComparison.Ordinal) ? ModelTier.Recommended
            : name.Contains("12b", StringComparison.Ordinal) ? ModelTier.High
            : null;
    }

    /// <summary>One line for the settings window.</summary>
    public static string Describe(ModelTier tier) => tier switch
    {
        ModelTier.Off => "Off: no model. Template text everywhere; the story still runs.",
        ModelTier.Minimum => "Minimum (Gemma 4 E2B, about 2 GB of VRAM): director decisions and short bulletins.",
        ModelTier.Recommended => "Recommended (Gemma 4 E4B, about 4 GB of VRAM): full narration. The default.",
        ModelTier.High => "High (Gemma 4 12B, about 9 GB of VRAM): richer prose.",
        _ => tier.ToString(),
    };

    /// <summary>Checks an endpoint URL: null if it is usable, else why not.</summary>
    public static string? EndpointProblem(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "Enter the server address, e.g. http://localhost:11434/";
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "The address must start with http:// or https://";
        }

        return null;
    }

    /// <summary>The endpoint as a base URI with a trailing slash, so "v1/chat/completions" resolves under it.</summary>
    public static Uri EndpointUri(string endpoint)
    {
        var text = endpoint.Trim();
        return new Uri(text.EndsWith('/') ? text : text + "/");
    }
}

/// <summary>How far a director model's training went (War in Heaven tech spec R12): each level adds plots to the ones
/// before it.</summary>
public enum DirectorVocabulary
{
    /// <summary>The minor plots and the Awakened Empire (director-model track D0–D11).</summary>
    Base,

    /// <summary>Plus the Imperial crisis: operations, siege goals and sites (crisis C11).</summary>
    Crisis,

    /// <summary>Plus the Hollow Choir, its crisis and the War in Heaven (War in Heaven W12).</summary>
    WarInHeaven,
}
