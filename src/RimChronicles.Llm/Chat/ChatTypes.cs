using System.Text.Json.Nodes;

namespace RimChronicles.Llm.Chat;

/// <summary>How much reasoning a model should do. Each provider maps this to its own request fields.</summary>
public enum ThinkingMode
{
    /// <summary>Ask the model not to think, where the model allows it.</summary>
    Off,

    /// <summary>Leave the model's own default.</summary>
    Default,

    /// <summary>Ask for more reasoning.</summary>
    On,
}

/// <summary>A JSON Schema the output must match, with a short name some servers require.</summary>
/// <param name="Name">Schema name sent to the server (letters, digits, underscores).</param>
/// <param name="Schema">The JSON Schema object.</param>
public sealed record JsonSchemaSpec(string Name, JsonObject Schema)
{
    /// <summary>Parses a schema from JSON text.</summary>
    public static JsonSchemaSpec Parse(string name, string json) =>
        new(name, JsonNode.Parse(json) as JsonObject
                  ?? throw new ArgumentException("Schema JSON must be an object.", nameof(json)));

    /// <summary>Returns a deep copy, so per-call edits (such as filling an enum) never touch a shared schema.</summary>
    public JsonSchemaSpec DeepCopy() => new(Name, (JsonObject)Schema.DeepClone());
}

/// <summary>One chat completion request: a system prompt and a single user message.</summary>
public sealed record ChatRequest
{
    public required string System { get; init; }
    public required string User { get; init; }
    public int MaxOutputTokens { get; init; } = 1024;

    /// <summary>The schema the output must match; null for free text.</summary>
    public JsonSchemaSpec? Schema { get; init; }

    /// <summary>When true and <see cref="Schema"/> is set, ask the server to constrain decoding to the schema.
    /// When false, the schema is only described in the prompt.</summary>
    public bool UseGrammar { get; init; }

    public ThinkingMode Thinking { get; init; } = ThinkingMode.Default;

    /// <summary>Extra top-level request fields merged into the provider's request body, for server-specific options.</summary>
    public IReadOnlyDictionary<string, JsonNode?>? ExtraBody { get; init; }
}

/// <summary>The result of a chat call. Providers return failures here instead of throwing.</summary>
public sealed record ChatResult
{
    public required bool Ok { get; init; }

    /// <summary>The answer text with any thinking removed; null when the call failed.</summary>
    public string? Text { get; init; }

    public string? Error { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }

    /// <summary>Reasoning tokens, when the server reports them separately.</summary>
    public int? ReasoningTokens { get; init; }

    /// <summary>The answer stopped at the output token limit (OpenAI <c>finish_reason: "length"</c>, Anthropic
    /// <c>max_tokens</c>), so the text may end mid-sentence. <see cref="Tasks.LlmTaskRunner"/> treats it as a failed
    /// attempt. Results cached before this flag existed read as false.</summary>
    public bool Truncated { get; init; }

    public TimeSpan Latency { get; init; }
    public string ModelId { get; init; } = "";

    /// <summary>The raw response body (or error body) for logging and the eval cache.</summary>
    public string RawResponse { get; init; } = "";

    public static ChatResult Failure(string modelId, string error, TimeSpan latency, string raw = "") =>
        new() { Ok = false, Error = error, ModelId = modelId, Latency = latency, RawResponse = raw };
}

/// <summary>A model endpoint. Implementations must not throw for model or transport errors;
/// they return <see cref="ChatResult.Ok"/> = false. Only cancellation by the caller's token propagates.</summary>
public interface IChatProvider
{
    /// <summary>A stable id for logs and reports, e.g. "qwen3.5-4b".</summary>
    string ModelId { get; }

    Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken);
}
