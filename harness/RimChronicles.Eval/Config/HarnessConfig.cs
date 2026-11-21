using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Eval.Config;

/// <summary>harness/models.json: the candidate models, the judge and run defaults.</summary>
public sealed record HarnessConfig
{
    public IReadOnlyList<ModelConfig> Models { get; init; } = [];
    public ModelConfig? Judge { get; init; }
    public int Repeats { get; init; } = 1;

    /// <summary>Sets "temperature" in every local (OpenAI-compatible) model's extraBody, or removes it for "default",
    /// leaving the server's own setting. The cache key includes extraBody, so each setting is cached separately.</summary>
    public HarnessConfig WithLocalTemperature(string value)
    {
        double? t = value.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? null
            : double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 2
                ? parsed
                : throw new ArgumentException($"--temperature must be a number from 0 to 2 or \"default\", not '{value}'.");

        return this with
        {
            Models = Models.Select(m =>
            {
                if (m.Provider != ProviderKind.OpenAi)
                {
                    return m;
                }

                var extra = m.ExtraBody?.DeepClone().AsObject() ?? [];
                extra.Remove("temperature");
                if (t is { } temp)
                {
                    extra["temperature"] = temp;
                }

                return m with { ExtraBody = extra.Count == 0 ? null : extra };
            }).ToList(),
        };
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    public static HarnessConfig Load(string path)
    {
        var config = JsonSerializer.Deserialize<HarnessConfig>(File.ReadAllText(path), Options)
                     ?? throw new InvalidDataException($"{path} is empty.");
        var errors = config.Validate();
        return errors.Count == 0
            ? config
            : throw new InvalidDataException($"{path} is invalid:\n- " + string.Join("\n- ", errors));
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in Models.Append(Judge).OfType<ModelConfig>())
        {
            if (!ids.Add(m.Id))
            {
                errors.Add($"duplicate model id '{m.Id}'");
            }

            errors.AddRange(m.Validate().Select(e => $"{m.Id}: {e}"));
        }

        if (Repeats < 1)
        {
            errors.Add("repeats must be at least 1");
        }

        return errors;
    }
}

public enum ProviderKind
{
    /// <summary>Any OpenAI-compatible server (Ollama, llama.cpp, vLLM).</summary>
    OpenAi,

    /// <summary>Claude through the official Anthropic SDK.</summary>
    Anthropic,

    /// <summary>A deterministic fake for smoke tests; needs no server.</summary>
    Stub,
}

/// <summary>One model in the comparison.</summary>
public sealed record ModelConfig
{
    public required string Id { get; init; }

    /// <summary>"Minimum", "Recommended", "High" or "Reference".</summary>
    public required string Tier { get; init; }

    public required ProviderKind Provider { get; init; }

    /// <summary>Server base URL (OpenAI-compatible providers).</summary>
    public string? Endpoint { get; init; }

    /// <summary>The model name the server expects.</summary>
    public string? Model { get; init; }

    /// <summary>Environment variable holding the API key, if one is needed. Keys never go in this file.</summary>
    public string? ApiKeyEnv { get; init; }

    public bool SupportsGrammar { get; init; } = true;

    /// <summary>Default thinking mode for every task.</summary>
    public ThinkingMode Thinking { get; init; } = ThinkingMode.Default;

    /// <summary>Per-task overrides, e.g. { "director": "Off" }.</summary>
    public Dictionary<string, ThinkingMode> ThinkingByTask { get; init; } = [];

    public JsonObject? ThinkingOnBody { get; init; }
    public JsonObject? ThinkingOffBody { get; init; }
    public JsonObject? ExtraBody { get; init; }

    /// <summary>Anthropic only: effort for the Default thinking mode.</summary>
    public string? Effort { get; init; }

    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Requests in flight at once. Keep 1 for a local GPU so latency numbers are clean.</summary>
    public int Concurrency { get; init; } = 1;

    /// <summary>The prompt tier this model's prompts are rendered for (Reference uses High).</summary>
    public ModelTier PromptTier => Tier switch
    {
        "Minimum" => ModelTier.Minimum,
        "Recommended" => ModelTier.Recommended,
        _ => ModelTier.High,
    };

    public ThinkingMode ThinkingFor(string task) => ThinkingByTask.GetValueOrDefault(task, Thinking);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Tier is not ("Minimum" or "Recommended" or "High" or "Reference"))
        {
            errors.Add($"unknown tier '{Tier}'");
        }

        if (Provider == ProviderKind.OpenAi && (Endpoint is null || Model is null))
        {
            errors.Add("openAi models need endpoint and model");
        }

        if (Provider == ProviderKind.Anthropic && Model is null)
        {
            errors.Add("anthropic models need model");
        }

        if (Concurrency < 1 || TimeoutSeconds < 1)
        {
            errors.Add("concurrency and timeoutSeconds must be at least 1");
        }

        return errors;
    }

    /// <summary>The settings that change what the model returns, for the response cache key.</summary>
    public string Identity() => JsonSerializer.Serialize(new JsonObject
    {
        ["provider"] = Provider.ToString(),
        ["endpoint"] = Endpoint,
        ["model"] = Model,
        ["effort"] = Effort,
        ["thinkingOn"] = ThinkingOnBody?.DeepClone(),
        ["thinkingOff"] = ThinkingOffBody?.DeepClone(),
        ["extra"] = ExtraBody?.DeepClone(),
    });
}
