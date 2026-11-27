using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Llm.Providers;

/// <summary>Settings for one OpenAI-compatible endpoint (llama.cpp server, Ollama, vLLM, LM Studio...).</summary>
public sealed record OpenAiCompatOptions
{
    /// <summary>Report id, e.g. "qwen3.5-4b".</summary>
    public required string ModelId { get; init; }

    /// <summary>Server base URL, e.g. "http://localhost:11434". "/v1/chat/completions" is appended.</summary>
    public required Uri Endpoint { get; init; }

    /// <summary>The model name the server expects, e.g. "qwen3.5:4b".</summary>
    public required string Model { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>Per-request timeout. The game never waits on this; it bounds background work.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Fields merged into the body when <see cref="ThinkingMode.On"/> is requested
    /// (e.g. {"chat_template_kwargs": {"enable_thinking": true}} for Qwen on llama.cpp, {"think": true} on Ollama).</summary>
    public JsonObject? ThinkingOnBody { get; init; }

    /// <summary>Fields merged into the body when <see cref="ThinkingMode.Off"/> is requested.</summary>
    public JsonObject? ThinkingOffBody { get; init; }

    /// <summary>Fields merged into every request (sampling settings and the like).</summary>
    public JsonObject? ExtraBody { get; init; }
}

/// <summary>Calls POST {endpoint}/v1/chat/completions. Never throws except when the caller's token is cancelled: HTTP,
/// transport, timeout and parse errors all come back as a failed <see cref="ChatResult"/>.</summary>
public sealed class OpenAiCompatProvider(HttpClient http, OpenAiCompatOptions options) : IChatProvider
{
    private const int MaxErrorBodyChars = 2000;

    public string ModelId => options.ModelId;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var body = BuildBody(request, options);
        var url = new Uri(options.Endpoint, "v1/chat/completions");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        var clock = Stopwatch.StartNew();
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            }

            using var response = await http.SendAsync(message, timeout.Token).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            clock.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return ChatResult.Failure(ModelId, $"HTTP {(int)response.StatusCode}: {Truncate(raw)}", clock.Elapsed, raw);
            }

            return ParseResponse(raw, ModelId, clock.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ChatResult.Failure(ModelId, $"Timed out after {options.Timeout.TotalSeconds:0.#} s", clock.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            return ChatResult.Failure(ModelId, $"Request failed: {ex.Message}", clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A dropped connection mid-body (IOException), a bad address and the like: still a failed call, not a throw.
            return ChatResult.Failure(ModelId, $"Request failed: {ex.GetType().Name}: {ex.Message}", clock.Elapsed);
        }
    }

    /// <summary>Builds the request body. Internal for tests.</summary>
    internal static JsonObject BuildBody(ChatRequest request, OpenAiCompatOptions options)
    {
        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = request.System },
                new JsonObject { ["role"] = "user", ["content"] = request.User },
            },
            ["max_tokens"] = request.MaxOutputTokens,
            ["stream"] = false,
        };

        if (request.Schema is { } schema && request.UseGrammar)
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = schema.Name,
                    ["strict"] = true,
                    ["schema"] = schema.Schema.DeepClone(),
                },
            };
        }

        Merge(body, options.ExtraBody);
        Merge(body, request.Thinking switch
        {
            ThinkingMode.On => options.ThinkingOnBody,
            ThinkingMode.Off => options.ThinkingOffBody,
            _ => null,
        });
        if (request.ExtraBody is { } extra)
        {
            foreach (var (key, value) in extra)
            {
                body[key] = value?.DeepClone();
            }
        }

        return body;
    }

    /// <summary>Parses a chat completion response. Internal for tests.</summary>
    internal static ChatResult ParseResponse(string raw, string modelId, TimeSpan latency)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(raw);
        }
        catch (JsonException ex)
        {
            return ChatResult.Failure(modelId, $"Response is not JSON: {ex.Message}", latency, raw);
        }

        var choice = root?["choices"] is JsonArray { Count: > 0 } choices ? choices[0] : null;
        var content = StringOrNull(choice?["message"]?["content"]);
        if (content is null)
        {
            return ChatResult.Failure(modelId, "Response has no choices[0].message.content", latency, raw);
        }

        var usage = root?["usage"];
        var text = ThinkingText.Strip(content);
        var finish = StringOrNull(choice?["finish_reason"]);

        return new ChatResult
        {
            Ok = text.Length > 0,
            Text = text,
            Error = text.Length > 0 ? null : finish == "length" ? "Output cut off by the token limit before any answer" : "Empty answer",
            InputTokens = IntOrZero(usage?["prompt_tokens"]),
            OutputTokens = IntOrZero(usage?["completion_tokens"]),
            ReasoningTokens = IntOrNull(usage?["completion_tokens_details"]?["reasoning_tokens"]),
            Truncated = finish == "length",
            Latency = latency,
            ModelId = modelId,
            RawResponse = raw,
        };
    }

    private static void Merge(JsonObject target, JsonObject? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var (key, value) in source)
        {
            target[key] = value?.DeepClone();
        }
    }

    private static int IntOrZero(JsonNode? node) => IntOrNull(node) ?? 0;

    private static int? IntOrNull(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    private static string? StringOrNull(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string Truncate(string s) => s.Length <= MaxErrorBodyChars ? s : s[..MaxErrorBodyChars] + "...";
}
