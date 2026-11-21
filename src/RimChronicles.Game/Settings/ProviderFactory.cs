using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Providers;
using System.Text.Json.Nodes;

namespace RimChronicles.Game.Settings;

/// <summary>Builds the chat provider from the settings. One shared HttpClient for the whole mod.</summary>
public static class ProviderFactory
{
    private static readonly HttpClient Http = new();

    /// <summary>The provider for these settings, or null when the tier is Off or the endpoint is unusable.</summary>
    public static IChatProvider? Create(RimChroniclesSettings settings, TimeSpan? timeout = null)
    {
        if (!settings.ModelEnabled)
        {
            return null;
        }

        return new OpenAiCompatProvider(Http, new OpenAiCompatOptions
        {
            ModelId = settings.Model,
            Endpoint = TierPresets.EndpointUri(settings.Endpoint),
            Model = settings.Model,
            ApiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey.Trim(),
            Timeout = timeout ?? TimeSpan.FromSeconds(60),
            ThinkingOffBody = new JsonObject { ["reasoning_effort"] = "none" },
            ExtraBody = new JsonObject { ["temperature"] = TierPresets.Temperature },
        });
    }
}
