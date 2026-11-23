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

    private static Vector2 _scroll;
    private static float _height = 1000f;

    private static readonly (string Id, string Label, string Tip)[] Arcs =
    [
        (Core.Plots.Nemesis.NemesisPlot.TemplateId, "Nemesis", "A raider who killed or maimed a colonist and escaped comes back into the story."),
        (Core.Plots.LostFamily.LostFamilyArc.TemplateId, "Lost Family", "A colonist's relative in another faction writes, is taken captive, and may be rescued."),
        (Core.Plots.SilentPress.SilentPressArc.TemplateId, "Silent Press", "Killing the reporter has consequences: clues, then an envoy demanding reparations."),
    ];

    public static void Draw(Rect rect, RimChroniclesSettings settings)
    {
        // The page is taller than the window: a scroll view, sized from the last frame's height.
        var view = new Rect(0f, 0f, rect.width - 16f, _height);
        Widgets.BeginScrollView(rect, ref _scroll, view);
        var list = new Listing_Standard();
        list.Begin(view);

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
        list.CheckboxLabeled("Major plot: the Awakened Empire", ref settings.MajorPlot,
            "The Empire wakes and starts taking territory. Needs the Royalty DLC; without it, this does nothing.");
        if (settings.MajorPlot && !ModsConfig.RoyaltyActive)
        {
            list.Label("<color=#a0a0a0>Royalty isn't active, so the major plot is off.</color>");
        }

        if (settings.MajorPlot)
        {
            list.CheckboxLabeled("    Imperial crisis: the Empire attacks a colony that opposes it", ref settings.Crisis,
                "Probing raids, breaches, psychic assaults, sieges and a final assault, escalating with the plot. Off: the Empire's raids are vanilla raids.");
            if (settings.Crisis)
            {
                var difficulty = list.SliderLabeled($"    Crisis difficulty: {settings.CrisisDifficulty:0.0}×", settings.CrisisDifficulty,
                    RimChroniclesSettings.MinCrisisDifficulty, RimChroniclesSettings.MaxCrisisDifficulty,
                    tooltip: "Scales the strength of Imperial operations, on top of the storyteller's threat scale.");
                settings.CrisisDifficulty = (float)Math.Round(difficulty * 10f) / 10f;
                var gap = (int)Math.Round(list.SliderLabeled($"    At least {settings.CrisisGapDays} days between Imperial operations", settings.CrisisGapDays,
                    Core.Crisis.CrisisArc.MinGapDays, Core.Crisis.CrisisArc.MaxGapDays));
                settings.CrisisGapDays = Math.Clamp(gap, Core.Crisis.CrisisArc.MinGapDays, Core.Crisis.CrisisArc.MaxGapDays);
                var delay = (int)Math.Round(list.SliderLabeled($"    Orbital strikes land {settings.StrikeDelaySeconds} seconds after the warning", settings.StrikeDelaySeconds,
                    Core.Crisis.Strikes.StrikeRules.MinDelaySeconds, Core.Crisis.Strikes.StrikeRules.MaxDelaySeconds,
                    tooltip: "Game time at normal speed. Every strike is marked and announced first; taking down its herald calls it off."));
                settings.StrikeDelaySeconds = Math.Clamp(delay, Core.Crisis.Strikes.StrikeRules.MinDelaySeconds, Core.Crisis.Strikes.StrikeRules.MaxDelaySeconds);
                var citadel = (int)Math.Round(list.SliderLabeled($"    Imperial Citadel map: {settings.CitadelMapSize}×{settings.CitadelMapSize}", settings.CitadelMapSize,
                    Core.Crisis.Sites.CitadelRings.MinMapSize, Core.Crisis.Sites.CitadelRings.MaxMapSize,
                    tooltip: "The capital's map for the finale. Smaller generates faster and runs smoother on slower PCs.") / 25f) * 25;
                settings.CitadelMapSize = Math.Clamp(citadel, Core.Crisis.Sites.CitadelRings.MinMapSize, Core.Crisis.Sites.CitadelRings.MaxMapSize);
                list.CheckboxLabeled("    Citadel: spawn inner defenders only as you reach them", ref settings.CitadelLazyRings,
                    "Faster on slower PCs: the courtyard's and keep's defenders appear when your colonists reach the ring before theirs.");
            }
        }

        list.GapLine();
        var intensity = list.SliderLabeled(
            $"Story intensity: {Core.Plots.StoryIntensity.Describe(settings.StoryIntensity)} ({settings.StoryIntensity:0.0}×)",
            settings.StoryIntensity, Core.Plots.StoryIntensity.Min, Core.Plots.StoryIntensity.Max,
            tooltip: "How often minor plots start, and how fast the Empire expands. 2× halves the waits; 0.5× doubles them.");
        settings.StoryIntensity = (float)Math.Round(intensity * 10f) / 10f;

        list.Label("Story arcs (turning one off stops new plots of that kind; running ones finish)");
        foreach (var (id, label, tip) in Arcs)
        {
            var on = settings.ArcEnabled(id);
            list.CheckboxLabeled($"    {label}", ref on, tip);
            settings.SetArcEnabled(id, on);
        }

        list.GapLine();
        list.Label("Separate prose model (optional): writes bulletins, plot letters and epilogues, while the main model makes the director's decisions");
        settings.ProseModel = list.TextEntryLabeled("Prose model (blank: use the main one)  ", settings.ProseModel);
        if (settings.ProseModel.Length > 0)
        {
            settings.ProseEndpoint = list.TextEntryLabeled("Prose address (blank: the main one)  ", settings.ProseEndpoint);
            if (settings.ProseEndpoint.Length > 0 && TierPresets.EndpointProblem(settings.ProseEndpoint) is { } proseProblem)
            {
                list.Label($"<color=#e0a040>{proseProblem}</color>");
            }

            settings.ProseApiKey = list.TextEntryLabeled("Prose API key (blank: the main one)  ", settings.ProseApiKey);
            if (settings.Tier == ModelTier.Off)
            {
                list.Label("<color=#a0a0a0>The tier is Off, so no model is called, this one included.</color>");
            }
        }

        list.GapLine();
        list.Label("Separate director model (optional): makes the director's decisions, such as a fine-tuned rimchron-director-e2b, while the main model writes");
        var directorBefore = settings.DirectorModel;
        settings.DirectorModel = list.TextEntryLabeled("Director model (blank: use the main one)  ", settings.DirectorModel);
        if (settings.DirectorModel != directorBefore && TierPresets.SuggestedDirectorTier(settings.DirectorModel) is { } suggested)
        {
            settings.DirectorTier = suggested; // typing rimchron-director-e4b picks Recommended
        }

        if (settings.DirectorModel.Length > 0)
        {
            settings.DirectorEndpoint = list.TextEntryLabeled("Director address (blank: the main one)  ", settings.DirectorEndpoint);
            if (settings.DirectorEndpoint.Length > 0 && TierPresets.EndpointProblem(settings.DirectorEndpoint) is { } directorProblem)
            {
                list.Label($"<color=#e0a040>{directorProblem}</color>");
            }

            settings.DirectorApiKey = list.TextEntryLabeled("Director API key (blank: the main one)  ", settings.DirectorApiKey);
            list.Label("Director prompt tier: the tier the director model was trained on (rimchron-director-e2b: Minimum; -e4b: Recommended)");
            foreach (var tier in new[] { ModelTier.Minimum, ModelTier.Recommended, ModelTier.High })
            {
                if (list.RadioButton($"    {tier}", settings.DirectorTier == tier, tabIn: 12f))
                {
                    settings.DirectorTier = tier;
                }
            }

            if (TierPresets.SuggestedDirectorTier(settings.DirectorModel) is { } trained && trained != settings.DirectorTier)
            {
                list.Label($"<color=#e0a040>The name says this model was trained for {trained}: other tiers send it prompts it never saw.</color>");
            }

            if (!ModelSlots.DirectorSeparate(settings.Slots) && settings.Tier != ModelTier.Off)
            {
                list.Label("<color=#e0a040>The director model isn't usable as set, so the main model makes the decisions.</color>");
            }

            if (settings.Tier == ModelTier.Off)
            {
                list.Label("<color=#a0a0a0>The tier is Off, so no model is called, this one included.</color>");
            }
        }

        if (Prefs.DevMode)
        {
            list.GapLine();
            list.CheckboxLabeled("Dev: record director decisions for training", ref settings.RecordDirectorCases,
                $"Saves each director decision with a choice as a case in {Plots.DirectorRecorder.Folder}, for the director-model training track.");
        }

        list.GapLine();
        if (list.ButtonText("Reset to defaults", widthPct: 0.3f))
        {
            settings.ResetToDefaults();
            _probeStatus = "";
        }

        _height = list.CurHeight + 24f;
        list.End();
        Widgets.EndScrollView();
    }

    private static async Task<ConnectionProbe.Result> ProbeAll(IChatProvider main, IChatProvider? director)
    {
        var first = await ConnectionProbe.RunAsync(main, CancellationToken.None).ConfigureAwait(false);
        if (director is null)
        {
            return first;
        }

        var second = await ConnectionProbe.RunAsync(director, CancellationToken.None).ConfigureAwait(false);
        return new ConnectionProbe.Result(first.Ok && second.Ok, $"Main: {first.Message} Director: {second.Message}");
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
                // A separate director model is tested too, after the main one.
                var director = ModelSlots.DirectorSeparate(settings.Slots) ? ProviderFactory.CreateDirector(settings, TimeSpan.FromSeconds(120))?.Provider : null;
                if (director is null && settings.DirectorModel.Length > 0)
                {
                    _probeStatus += " The director model isn't usable as set (check its address), so only the main model is tested.";
                }
                // Background task: touches no game state, only returns a result the window reads next frame.
                _probe = Task.Run(() => ProbeAll(provider, director));
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
