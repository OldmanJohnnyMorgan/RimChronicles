using HarmonyLib;
using Verse;

namespace RimChronicles.Game;

/// <summary>Mod entry point (spike test 1). RimWorld constructs it once at startup, before any game loads.</summary>
public sealed class RimChroniclesMod : Mod
{
    public const string HarmonyId = "oldmanjohnnymorgan.rimchronicles";

    public static RimChroniclesSettings Settings { get; private set; } = new();

    public RimChroniclesMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<RimChroniclesSettings>();
        new Harmony(HarmonyId).PatchAll(typeof(RimChroniclesMod).Assembly);
        Log.Message($"[Rim Chronicles] Loaded on {Environment.Version} ({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}); "
            + $"Harmony patches applied; model {Settings.Model} at {Settings.Endpoint}.");
    }
}
