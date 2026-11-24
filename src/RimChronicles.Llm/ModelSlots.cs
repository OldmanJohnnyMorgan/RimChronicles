namespace RimChronicles.Llm;

/// <summary>A model to call: server, model name, API key, the tier its prompts are rendered for, and the temperature.</summary>
public sealed record ModelSlot(string Endpoint, string Model, string ApiKey, ModelTier PromptTier, double Temperature);

/// <summary>
/// Which model does what. The main model makes the director's decisions and writes the narration; an optional prose
/// model takes over the narration, and an optional director model takes over the decisions (for example a fine-tuned
/// <c>rimchron-director-e2b</c>, director-model track D7). Blank fields fall back to the main model's. Nothing is
/// called when the main tier is Off.
/// </summary>
public static class ModelSlots
{
    /// <summary>Settings as plain values, so the choice is testable without RimWorld.</summary>
    public sealed record Inputs
    {
        public ModelTier Tier { get; init; } = ModelTier.Recommended;
        public string Endpoint { get; init; } = TierPresets.DefaultEndpoint;
        public string Model { get; init; } = "";
        public string ApiKey { get; init; } = "";
        public string DirectorEndpoint { get; init; } = "";
        public string DirectorModel { get; init; } = "";
        public string DirectorApiKey { get; init; } = "";

        /// <summary>The tier the director model was trained on: a fine-tuned director has only seen prompts rendered
        /// at its tier's budget, so it must get the same.</summary>
        public ModelTier DirectorTier { get; init; } = ModelTier.Minimum;
    }

    /// <summary>The main model, or null when the tier is Off or the main settings are unusable.</summary>
    public static ModelSlot? Main(Inputs s) =>
        s.Tier != ModelTier.Off && TierPresets.EndpointProblem(s.Endpoint) is null && !string.IsNullOrWhiteSpace(s.Model)
            ? new ModelSlot(s.Endpoint.Trim(), s.Model.Trim(), s.ApiKey.Trim(), s.Tier, TierPresets.Temperature)
            : null;

    /// <summary>True when a separate director model is filled in and its server address (or the main one) is usable.</summary>
    public static bool DirectorSeparate(Inputs s) =>
        !string.IsNullOrWhiteSpace(s.DirectorModel) && s.DirectorTier != ModelTier.Off
        && TierPresets.EndpointProblem(Blank(s.DirectorEndpoint) ? s.Endpoint : s.DirectorEndpoint) is null;

    /// <summary>The model for the director's decisions: the separate director model when one is set up, else the main.</summary>
    public static ModelSlot? Director(Inputs s)
    {
        if (Main(s) is not { } main)
        {
            return null;
        }

        return DirectorSeparate(s)
            ? new ModelSlot((Blank(s.DirectorEndpoint) ? s.Endpoint : s.DirectorEndpoint).Trim(), s.DirectorModel.Trim(),
                (Blank(s.DirectorApiKey) ? s.ApiKey : s.DirectorApiKey).Trim(), s.DirectorTier, TierPresets.DirectorTemperature)
            : main;
    }

    /// <summary>True when the model that makes the director's decisions may also make the Imperial crisis's
    /// (<see cref="TierPresets.KnowsCrisis"/>). False with no model at all: the crisis then picks by weight anyway.</summary>
    public static bool DirectorKnowsCrisis(Inputs s) => Director(s) is { } slot && TierPresets.KnowsCrisis(slot.Model);

    /// <summary>Whether the deciding model may decide the Hollow Choir's plot, its crisis and the War in Heaven
    /// (<see cref="TierPresets.KnowsWarInHeaven"/>). False with no model at all.</summary>
    public static bool DirectorKnowsWarInHeaven(Inputs s) => Director(s) is { } slot && TierPresets.KnowsWarInHeaven(slot.Model);

    private static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);
}
