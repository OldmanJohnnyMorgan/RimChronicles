using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Llm.Anthropic;

/// <summary>Settings for the Claude reference model and judge.</summary>
public sealed record AnthropicOptions
{
    /// <summary>Report id, e.g. "claude-ref".</summary>
    public required string ModelId { get; init; }

    public string Model { get; init; } = "claude-opus-5-5";

    /// <summary>API key; null lets the SDK find a credential itself (ANTHROPIC_API_KEY and the like). The harness always passes one.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Effort for <see cref="ThinkingMode.Default"/>: "low", "medium", "high", "xhigh" or "max".
    /// Off maps to "low" and On to "high", since thinking can't be disabled on Claude Opus 5.5.</summary>
    public string Effort { get; init; } = "medium";

    /// <summary>Let the API re-serve a refused request on a fallback model (server-side fallbacks beta).</summary>
    public bool ServerSideFallbacks { get; init; } = true;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Tokens added to each request's output limit for thinking. Claude Opus 5.5 always thinks and its
    /// thinking counts against max_tokens, so the prompts' caps (sized for the answer alone) would cut it off.
    /// Answer length is held by the prompt and the word-count checks instead.</summary>
    public int ThinkingHeadroomTokens { get; init; } = 8000;
}

/// <summary>Claude through the official Anthropic SDK (Messages API). Never throws for API or transport errors.</summary>
public sealed class AnthropicProvider : IChatProvider
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;

    public AnthropicProvider(AnthropicOptions options)
    {
        _options = options;
        _client = options.ApiKey is { } key ? new AnthropicClient { ApiKey = key } : new AnthropicClient();
    }

    public string ModelId => _options.ModelId;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        var clock = Stopwatch.StartNew();

        try
        {
            var response = await _client.Beta.Messages.Create(BuildParams(request), timeout.Token).ConfigureAwait(false);
            clock.Stop();

            var raw = JsonSerializer.Serialize(response);
            if (response.StopReason == "refusal")
            {
                return ChatResult.Failure(ModelId, $"Refused ({response.StopDetails?.Category})", clock.Elapsed, raw);
            }

            var text = string.Concat(response.Content
                .Select(b => b.TryPickText(out var t) ? t.Text : null)
                .Where(t => t is not null));

            return new ChatResult
            {
                Ok = text.Length > 0,
                Text = text,
                Error = text.Length > 0 ? null : $"Empty answer (stop reason {response.StopReason})",
                InputTokens = (int)response.Usage.InputTokens,
                OutputTokens = (int)response.Usage.OutputTokens,
                Truncated = response.StopReason == "max_tokens",
                Latency = clock.Elapsed,
                ModelId = ModelId,
                RawResponse = raw,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ChatResult.Failure(ModelId, $"Timed out after {_options.Timeout.TotalSeconds:0} s", clock.Elapsed);
        }
        catch (AnthropicApiException ex)
        {
            return ChatResult.Failure(ModelId, $"API error: {ex.Message}", clock.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            return ChatResult.Failure(ModelId, $"Request failed: {ex.Message}", clock.Elapsed);
        }
    }

    private MessageCreateParams BuildParams(ChatRequest request)
    {
        var effort = request.Thinking switch
        {
            ThinkingMode.Off => "low",
            ThinkingMode.On => "high",
            _ => _options.Effort,
        };

        var outputConfig = new BetaOutputConfig { Effort = ParseEffort(effort) };
        if (request.Schema is { } schema && request.UseGrammar)
        {
            outputConfig = outputConfig with
            {
                Format = new BetaJsonOutputFormat
                {
                    Schema = schema.Schema.ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value)),
                },
            };
        }

        return new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = MaxTokensFor(request.MaxOutputTokens, _options.ThinkingHeadroomTokens),
            System = request.System,
            Messages = [new BetaUserMessageParam { Content = request.User }],
            OutputConfig = outputConfig,
            Betas = _options.ServerSideFallbacks ? [AnthropicBeta.ServerSideFallback2026_07_01] : null,
            Fallbacks = _options.ServerSideFallbacks ? new Default() : (BetaFallbacksParam?)null,
        };
    }

    /// <summary>The max_tokens sent: the answer's cap plus room for thinking. Internal for tests.</summary>
    internal static long MaxTokensFor(int answerTokens, int headroom) => (long)answerTokens + Math.Max(0, headroom);

    private static Effort ParseEffort(string effort) => effort switch
    {
        "low" => global::Anthropic.Models.Beta.Messages.Effort.Low,
        "medium" => global::Anthropic.Models.Beta.Messages.Effort.Medium,
        "high" => global::Anthropic.Models.Beta.Messages.Effort.High,
        "xhigh" => global::Anthropic.Models.Beta.Messages.Effort.Xhigh,
        "max" => global::Anthropic.Models.Beta.Messages.Effort.Max,
        _ => throw new ArgumentException($"Unknown effort '{effort}'."),
    };
}
