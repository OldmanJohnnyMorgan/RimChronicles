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

    /// <summary>How often minor plots start and how fast the Empire expands (<see cref="Core.Plots.StoryIntensity"/>).</summary>
    public float StoryIntensity = Core.Plots.StoryIntensity.Default;

    /// <summary>The Imperial crisis: Empire operations against an opposed colony (needs the major plot and Royalty).</summary>
    public bool Crisis = true;

    public const float MinCrisisDifficulty = 0.5f;
    public const float MaxCrisisDifficulty = 2f;

    /// <summary>Scales the crisis's threat budget, on top of the storyteller's threat scale.</summary>
    public float CrisisDifficulty = 1f;

    /// <summary>Minimum days between crisis operations.</summary>
    public int CrisisGapDays = Core.Crisis.CrisisArc.DefaultGapDays;

    /// <summary>Seconds between an orbital strike being called and landing (6–10).</summary>
    public int StrikeDelaySeconds = Core.Crisis.Strikes.StrikeRules.DefaultDelaySeconds;

    /// <summary>The Citadel's map size (crisis spec §4.5; C12). Smaller is faster on slow PCs.</summary>
    public int CitadelMapSize = Core.Crisis.Sites.CitadelRings.DefaultMapSize;

    /// <summary>Spawn the Citadel's courtyard and keep garrisons only when the colonists reach the ring before them.</summary>
    public bool CitadelLazyRings;

    /// <summary>Arc types turned off: they start no new plots (running ones finish).</summary>
    public List<string> DisabledArcs = [];

    /// <summary>An optional second model for narration (bulletins, plot letters, epilogues); the main one keeps the
    /// director's decisions. Empty model: narration uses the main model.</summary>
    public string ProseEndpoint = "";
    public string ProseModel = "";
    public string ProseApiKey = "";

    /// <summary>An optional model for the director's decisions only, such as a fine-tuned <c>rimchron-director-e2b</c>
    /// (director-model track D7). Empty model: the main model decides. <see cref="DirectorTier"/> is the tier its
    /// prompts are rendered for, which must be the one it was trained on.</summary>
    public string DirectorEndpoint = "";
    public string DirectorModel = "";
    public string DirectorApiKey = "";
    public ModelTier DirectorTier = ModelTier.Minimum;

    /// <summary>Dev Mode: save every director decision as a training case (director-model track D3).</summary>
    public bool RecordDirectorCases;

    public bool ArcEnabled(string templateId) => !DisabledArcs.Contains(templateId);

    public void SetArcEnabled(string templateId, bool enabled)
    {
        DisabledArcs.Remove(templateId);
        if (!enabled)
        {
            DisabledArcs.Add(templateId);
        }
    }

    /// <summary>True when a separate prose model is set up and usable.</summary>
    public bool ProseModelEnabled => ModelEnabled && !string.IsNullOrWhiteSpace(ProseModel)
        && TierPresets.EndpointProblem(string.IsNullOrWhiteSpace(ProseEndpoint) ? Endpoint : ProseEndpoint) is null;

    /// <summary>The settings as plain values for <see cref="ModelSlots"/>.</summary>
    public ModelSlots.Inputs Slots => new()
    {
        Tier = Tier, Endpoint = Endpoint, Model = Model, ApiKey = ApiKey,
        DirectorEndpoint = DirectorEndpoint, DirectorModel = DirectorModel, DirectorApiKey = DirectorApiKey, DirectorTier = DirectorTier,
    };

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
        StoryIntensity = Core.Plots.StoryIntensity.Default;
        Crisis = true;
        CrisisDifficulty = 1f;
        CrisisGapDays = Core.Crisis.CrisisArc.DefaultGapDays;
        StrikeDelaySeconds = Core.Crisis.Strikes.StrikeRules.DefaultDelaySeconds;
        CitadelMapSize = Core.Crisis.Sites.CitadelRings.DefaultMapSize;
        CitadelLazyRings = false;
        DisabledArcs = [];
        ProseEndpoint = "";
        ProseModel = "";
        ProseApiKey = "";
        DirectorEndpoint = "";
        DirectorModel = "";
        DirectorApiKey = "";
        DirectorTier = ModelTier.Minimum;
        RecordDirectorCases = false;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Tier, "tier", ModelTier.Recommended);
        Scribe_Values.Look(ref Endpoint, "endpoint", TierPresets.DefaultEndpoint);
        Scribe_Values.Look(ref Model, "model", TierPresets.ModelFor(ModelTier.Recommended)!);
        Scribe_Values.Look(ref ApiKey, "apiKey", "");
        Scribe_Values.Look(ref NewsCadenceDays, "newsCadenceDays", DefaultNewsCadenceDays);
        Scribe_Values.Look(ref MajorPlot, "majorPlot", true);
        Scribe_Values.Look(ref StoryIntensity, "storyIntensity", Core.Plots.StoryIntensity.Default);
        Scribe_Values.Look(ref Crisis, "crisis", true);
        Scribe_Values.Look(ref CrisisDifficulty, "crisisDifficulty", 1f);
        Scribe_Values.Look(ref CrisisGapDays, "crisisGapDays", Core.Crisis.CrisisArc.DefaultGapDays);
        Scribe_Values.Look(ref StrikeDelaySeconds, "strikeDelaySeconds", Core.Crisis.Strikes.StrikeRules.DefaultDelaySeconds);
        Scribe_Values.Look(ref CitadelMapSize, "citadelMapSize", Core.Crisis.Sites.CitadelRings.DefaultMapSize);
        Scribe_Values.Look(ref CitadelLazyRings, "citadelLazyRings");
        Scribe_Collections.Look(ref DisabledArcs, "disabledArcs", LookMode.Value);
        Scribe_Values.Look(ref ProseEndpoint, "proseEndpoint", "");
        Scribe_Values.Look(ref ProseModel, "proseModel", "");
        Scribe_Values.Look(ref ProseApiKey, "proseApiKey", "");
        Scribe_Values.Look(ref DirectorEndpoint, "directorEndpoint", "");
        Scribe_Values.Look(ref DirectorModel, "directorModel", "");
        Scribe_Values.Look(ref DirectorApiKey, "directorApiKey", "");
        Scribe_Values.Look(ref DirectorTier, "directorTier", ModelTier.Minimum);
        Scribe_Values.Look(ref RecordDirectorCases, "recordDirectorCases", false);

        Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? TierPresets.DefaultEndpoint : Endpoint.Trim();
        Model = string.IsNullOrWhiteSpace(Model) ? TierPresets.ModelFor(ModelTier.Recommended)! : Model.Trim();
        ApiKey ??= "";
        NewsCadenceDays = Math.Clamp(NewsCadenceDays, MinNewsCadenceDays, MaxNewsCadenceDays);
        StoryIntensity = Math.Clamp(StoryIntensity, Core.Plots.StoryIntensity.Min, Core.Plots.StoryIntensity.Max);
        DisabledArcs ??= [];
        DisabledArcs.RemoveAll(string.IsNullOrEmpty);
        ProseEndpoint = ProseEndpoint?.Trim() ?? "";
        ProseModel = ProseModel?.Trim() ?? "";
        ProseApiKey ??= "";
        DirectorEndpoint = DirectorEndpoint?.Trim() ?? "";
        DirectorModel = DirectorModel?.Trim() ?? "";
        DirectorApiKey ??= "";
        DirectorTier = DirectorTier == ModelTier.Off ? ModelTier.Minimum : DirectorTier;
    }
}
