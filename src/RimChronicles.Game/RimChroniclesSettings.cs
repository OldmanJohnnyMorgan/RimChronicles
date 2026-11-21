using Verse;

namespace RimChronicles.Game;

/// <summary>Mod settings. Stage 1 has no settings window yet: edit the values in the mod's settings file under
/// RimWorld's Config folder, or keep the defaults (the Recommended preset on a local Ollama).</summary>
public sealed class RimChroniclesSettings : ModSettings
{
    public const string DefaultEndpoint = "http://localhost:11434/";
    public const string DefaultModel = "gemma4:e4b";

    public string Endpoint = DefaultEndpoint;
    public string Model = DefaultModel;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Endpoint, "endpoint", DefaultEndpoint);
        Scribe_Values.Look(ref Model, "model", DefaultModel);
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            Endpoint = DefaultEndpoint;
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            Model = DefaultModel;
        }
    }
}
