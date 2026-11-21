using HarmonyLib;
using RimChronicles.Game.Settings;
using UnityEngine;
using Verse;

namespace RimChronicles.Game;

/// <summary>Mod entry point. RimWorld constructs it once at startup, before any game loads.</summary>
public sealed class RimChroniclesMod : Mod
{
    public const string HarmonyId = "oldmanjohnnymorgan.rimchronicles";

    public static RimChroniclesSettings Settings { get; private set; } = new();

    /// <summary>The mod's prompts folder, or null before the mod is constructed.</summary>
    public static string? PromptsRoot { get; private set; }

    public RimChroniclesMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<RimChroniclesSettings>();
        PromptsRoot = Path.Combine(content.RootDir, "prompts");
        new Harmony(HarmonyId).PatchAll(typeof(RimChroniclesMod).Assembly);
        Log.Message($"[Rim Chronicles] Loaded on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; "
            + $"Harmony patches applied; tier {Settings.Tier}, model {Settings.Model} at {Settings.Endpoint}.");
    }

    public override string SettingsCategory() => "Rim Chronicles";

    public override void DoSettingsWindowContents(Rect inRect) => SettingsWindow.Draw(inRect, Settings);
}
