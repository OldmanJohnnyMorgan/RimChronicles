using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using UnityEngine;
using Verse;

namespace RimChronicles.Game.Settings;

/// <summary>Draws the mod settings page. UI code is the one place Unity stub types (Rect) are allowed; see
/// docs/ENGINE_NOTES.md. Runs on the main thread each frame while the page is open.</summary>
public static class SettingsWindow
{
    private static readonly ModelTier[] Tiers = [ModelTier.Off, ModelTier.Minimum, ModelTier.Recommended, ModelTier.High];

    private static Task<ConnectionProbe.Result>? _probe;
    private static string _probeStatus = "";

    public static void Draw(Rect rect, RimChroniclesSettings settings)
    {
        var list = new Listing_Standard();
        list.Begin(rect);

        list.Label("Model tier");
        foreach (var tier in Tiers)
        {
            if (list.RadioButton(TierPresets.Describe(tier), settings.Tier == tier, tabIn: 12f))
            {
                settings.ApplyTier(tier);
            }
        }

        list.GapLine();
        list.Label("Model server (any OpenAI-compatible endpoint; Ollama by default)");
        settings.Endpoint = list.TextEntryLabeled("Address  ", settings.Endpoint);
        if (TierPresets.EndpointProblem(settings.Endpoint) is { } problem)
        {
            list.Label($"<color=#e0a040>{problem}</color>");
        }

        settings.Model = list.TextEntryLabeled("Model  ", settings.Model);
        settings.ApiKey = list.TextEntryLabeled("API key (optional)  ", settings.ApiKey);
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            list.Label("<color=#a0a0a0>The key is saved in plain text in RimWorld's Config folder.</color>");
        }

        DrawConnectionTest(list, settings);

        list.GapLine();
        var days = (int)Math.Round(list.SliderLabeled($"News bulletin every {settings.NewsCadenceDays} day(s)", settings.NewsCadenceDays,
            RimChroniclesSettings.MinNewsCadenceDays, RimChroniclesSettings.MaxNewsCadenceDays));
        settings.NewsCadenceDays = Math.Clamp(days, RimChroniclesSettings.MinNewsCadenceDays, RimChroniclesSettings.MaxNewsCadenceDays);

        list.GapLine();
        if (list.ButtonText("Reset to defaults", widthPct: 0.3f))
        {
            settings.ResetToDefaults();
            _probeStatus = "";
        }

        list.End();
    }

    private static void DrawConnectionTest(Listing_Standard list, RimChroniclesSettings settings)
    {
        if (_probe is { IsCompleted: true } done)
        {
            _probeStatus = done.IsCompletedSuccessfully
                ? (done.Result.Ok ? $"<color=#80d080>{done.Result.Message}</color>" : $"<color=#e06060>{done.Result.Message}</color>")
                : $"<color=#e06060>Failed: {done.Exception?.GetBaseException().Message}</color>";
            _probe = null;
        }

        var running = _probe is not null;
        if (list.ButtonText(running ? "Testing…" : "Test connection", widthPct: 0.3f) && !running)
        {
            if (ProviderFactory.Create(settings, TimeSpan.FromSeconds(120)) is { } provider)
            {
                _probeStatus = "Testing… (the first call can take a while to load the model)";
                // Background task: touches no game state, only returns a result the window reads next frame.
                _probe = Task.Run(() => ConnectionProbe.RunAsync(provider, CancellationToken.None));
            }
            else
            {
                _probeStatus = settings.Tier == ModelTier.Off
                    ? "The tier is Off, so no model is called."
                    : "Fix the address and model first.";
            }
        }

        if (_probeStatus.Length > 0)
        {
            list.Label(_probeStatus);
        }
    }
}
