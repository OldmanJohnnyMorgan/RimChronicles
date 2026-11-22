using RimChronicles.Llm;
using Verse;

namespace RimChronicles.Game;

// Kept in RimChronicles.Game: RimWorld writes the full class name into the settings file, so moving it would
// orphan saved settings.
/// <summary>Mod settings, edited in Options → Mod settings → Rim Chronicles and saved by RimWorld in its Config folder.</summary>
public sealed class RimChroniclesSettings : ModSettings
{
    public const int DefaultNewsCadenceDays = 3;
    public const int MinNewsCadenceDays = 1;
    public const int MaxNewsCadenceDays = 7;

    public ModelTier Tier = ModelTier.Recommended;
    public string Endpoint = TierPresets.DefaultEndpoint;
    public string Model = TierPresets.ModelFor(ModelTier.Recommended)!;
    public string ApiKey = "";
    public int NewsCadenceDays = DefaultNewsCadenceDays;

    /// <summary>The Awakened Empire major plot (Stage 5); it also needs Royalty.</summary>
    public bool MajorPlot = true;

    /// <summary>True when story features should call a model; false means template text everywhere.</summary>
    public bool ModelEnabled => Tier != ModelTier.Off && TierPresets.EndpointProblem(Endpoint) is null && !string.IsNullOrWhiteSpace(Model);

    /// <summary>Picks a tier and, unless it is Off, its preset model.</summary>
    public void ApplyTier(ModelTier tier)
    {
        Tier = tier;
        Model = TierPresets.ModelFor(tier) ?? Model;
    }

    public void ResetToDefaults()
    {
        Tier = ModelTier.Recommended;
        Endpoint = TierPresets.DefaultEndpoint;
        Model = TierPresets.ModelFor(ModelTier.Recommended)!;
        ApiKey = "";
        NewsCadenceDays = DefaultNewsCadenceDays;
        MajorPlot = true;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Tier, "tier", ModelTier.Recommended);
        Scribe_Values.Look(ref Endpoint, "endpoint", TierPresets.DefaultEndpoint);
        Scribe_Values.Look(ref Model, "model", TierPresets.ModelFor(ModelTier.Recommended)!);
        Scribe_Values.Look(ref ApiKey, "apiKey", "");
        Scribe_Values.Look(ref NewsCadenceDays, "newsCadenceDays", DefaultNewsCadenceDays);
        Scribe_Values.Look(ref MajorPlot, "majorPlot", true);

        Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? TierPresets.DefaultEndpoint : Endpoint.Trim();
        Model = string.IsNullOrWhiteSpace(Model) ? TierPresets.ModelFor(ModelTier.Recommended)! : Model.Trim();
        ApiKey ??= "";
        NewsCadenceDays = Math.Clamp(NewsCadenceDays, MinNewsCadenceDays, MaxNewsCadenceDays);
    }
}
