using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Providers;
using System.Text.Json.Nodes;

namespace RimChronicles.Game.Settings;

/// <summary>Builds the chat provider from the settings. One shared HttpClient for the whole mod.</summary>
public static class ProviderFactory
{
    private static readonly HttpClient Http = new();

    /// <summary>The provider for these settings, or null when the tier is Off or the endpoint is unusable. Used for the
    /// director's decisions, and for narration when no separate prose model is set.</summary>
    public static IChatProvider? Create(RimChroniclesSettings settings, TimeSpan? timeout = null) =>
        settings.ModelEnabled ? Make(settings.Endpoint, settings.Model, settings.ApiKey, timeout) : null;

    /// <summary>The provider for narration (bulletins, plot letters, epilogues): the separate prose model when one is
    /// set up, else the main one.</summary>
    public static IChatProvider? CreateProse(RimChroniclesSettings settings, TimeSpan? timeout = null) =>
        settings.ProseModelEnabled
            ? Make(string.IsNullOrWhiteSpace(settings.ProseEndpoint) ? settings.Endpoint : settings.ProseEndpoint, settings.ProseModel,
                string.IsNullOrWhiteSpace(settings.ProseApiKey) ? settings.ApiKey : settings.ProseApiKey, timeout)
            : Create(settings, timeout);

    /// <summary>The provider for the director's decisions, and the tier its prompts are rendered for: the separate
    /// director model when one is set up, else the main one. Null when no model is called.</summary>
    public static (IChatProvider Provider, ModelTier Tier)? CreateDirector(RimChroniclesSettings settings, TimeSpan? timeout = null) =>
        ModelSlots.Director(settings.Slots) is { } slot
            ? (Make(slot.Endpoint, slot.Model, slot.ApiKey, timeout, slot.Temperature), slot.PromptTier)
            : null;

    private static IChatProvider Make(string endpoint, string model, string apiKey, TimeSpan? timeout, double temperature = TierPresets.Temperature)
    {
        return new OpenAiCompatProvider(Http, new OpenAiCompatOptions
        {
            ModelId = model,
            Endpoint = TierPresets.EndpointUri(endpoint),
            Model = model,
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
            Timeout = timeout ?? TimeSpan.FromSeconds(60),
            ThinkingOffBody = new JsonObject { ["reasoning_effort"] = "none" },
            ExtraBody = new JsonObject { ["temperature"] = temperature },
        });
    }
}
