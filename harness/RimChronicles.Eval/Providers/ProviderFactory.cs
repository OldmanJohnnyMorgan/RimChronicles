using RimChronicles.Eval.Config;
using RimChronicles.Llm.Anthropic;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Providers;

namespace RimChronicles.Eval.Providers;

/// <summary>Builds the provider for a model entry in models.json.</summary>
public static class ProviderFactory
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static IChatProvider Create(ModelConfig m)
    {
        var apiKey = m.ApiKeyEnv is { } env ? Environment.GetEnvironmentVariable(env) : null;
        if (m.ApiKeyEnv is not null && string.IsNullOrEmpty(apiKey) && m.Provider == ProviderKind.OpenAi)
        {
            throw new InvalidOperationException($"{m.Id}: environment variable {m.ApiKeyEnv} is not set.");
        }

        return m.Provider switch
        {
            ProviderKind.Stub => new StubChatProvider(m.Id),
            ProviderKind.OpenAi => new OpenAiCompatProvider(Http, new OpenAiCompatOptions
            {
                ModelId = m.Id,
                Endpoint = new Uri(m.Endpoint!),
                Model = m.Model!,
                ApiKey = apiKey,
                Timeout = TimeSpan.FromSeconds(m.TimeoutSeconds),
                ThinkingOnBody = m.ThinkingOnBody,
                ThinkingOffBody = m.ThinkingOffBody,
                ExtraBody = m.ExtraBody,
            }),
            ProviderKind.Anthropic => new AnthropicProvider(new AnthropicOptions
            {
                ModelId = m.Id,
                Model = m.Model!,
                ApiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey,
                Effort = m.Effort ?? "medium",
                Timeout = TimeSpan.FromSeconds(m.TimeoutSeconds),
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(m), m.Provider, "Unknown provider."),
        };
    }
}
