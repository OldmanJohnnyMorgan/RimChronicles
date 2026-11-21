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
        PatchEach(new Harmony(HarmonyId));
        Plots.NemesisBeats.Register();
        Log.Message($"[Rim Chronicles] Loaded on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; "
            + $"Harmony patches applied; tier {Settings.Tier}, model {Settings.Model} at {Settings.Endpoint}.");
    }

    /// <summary>Applies each patch class on its own, so one failing patch (a 1.7 signature change, an overload) logs an
    /// error naming it and the others still apply. <c>PatchAll</c> would stop at the first failure.</summary>
    private static void PatchEach(Harmony harmony)
    {
        var failed = new List<string>();
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(RimChroniclesMod).Assembly)
                     .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0))
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception ex)
            {
                failed.Add(type.Name);
                Log.Error($"[Rim Chronicles] Patch {type.Name} failed and is skipped: {ex.GetBaseException().Message}");
            }
        }

        if (failed.Count > 0)
        {
            Log.Error($"[Rim Chronicles] {failed.Count} patch(es) failed: {string.Join(", ", failed)}. Related features won't work.");
        }
    }

    public override string SettingsCategory() => "Rim Chronicles";

    public override void DoSettingsWindowContents(Rect inRect) => SettingsWindow.Draw(inRect, Settings);
}
