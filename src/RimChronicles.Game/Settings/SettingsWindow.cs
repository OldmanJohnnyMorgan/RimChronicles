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

    /// <summary>"Other" is picked for the director model while its name is still blank; the name alone can't say so.</summary>
    private static bool _customDirector;

    private static Vector2 _scroll;

    /// <summary>Whether the separate-models section is open; starts open when one is set up. Not saved.</summary>
    private static bool? _advancedOpen;
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
        // One column: without this, rows that pass last frame's height (when an option shows more fields) wrap into a
        // second column off to the right, and the page's measured height shrinks so it never comes back.
        var list = new Listing_Standard { maxOneColumn = true };
        list.Begin(view);

        Heading(list, "Model");
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

        Heading(list, "News");
        var days = (int)Math.Round(list.SliderLabeled($"News bulletin every {settings.NewsCadenceDays} day(s)", settings.NewsCadenceDays,
            RimChroniclesSettings.MinNewsCadenceDays, RimChroniclesSettings.MaxNewsCadenceDays));
        settings.NewsCadenceDays = Math.Clamp(days, RimChroniclesSettings.MinNewsCadenceDays, RimChroniclesSettings.MaxNewsCadenceDays);

        Heading(list, "Story");
        list.CheckboxLabeled("Major plot: the Awakened Empire", ref settings.MajorPlot,
            "The Empire wakes and starts taking territory. Needs the Royalty DLC; without it, this does nothing.");
        if (settings.MajorPlot && !ModsConfig.RoyaltyActive)
        {
            list.Label("<color=#a0a0a0>Royalty isn't active, so the major plot is off.</color>");
        }

        if (settings.MajorPlot)
        {
            list.CheckboxLabeled("    Crisis: the Empire (and the Hollow Choir) attack a colony that opposes them", ref settings.Crisis,
                "The Empire: probing raids, breaches, psychic assaults, sieges and a final assault. The Hollow Choir: probing swarms, tunnel breaches, "
                + "the hum, burrowing mechs and the final chorus. Both escalate with their plots and take turns. Off: their raids are vanilla raids.");
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

            list.CheckboxLabeled("    The Hollow Choir: the Empire's old enemy wakes too", ref settings.HollowChoir,
                "A power of mechanitors and their mechs that mirrors the Empire's story. Needs the Biotech DLC; without it, this does nothing.");
            if (settings.HollowChoir && !ModsConfig.BiotechActive)
            {
                list.Label("<color=#a0a0a0>    Biotech isn't active, so the Hollow Choir is off.</color>");
            }

            if (settings.HollowChoir)
            {
                list.CheckboxLabeled("    The War in Heaven: the two powers fight it out", ref settings.WarInHeaven,
                    "Once both are awake and the colony is at war, each day there's a chance the Empire and the Hollow Choir turn their armies on each "
                    + "other, rising the longer the colony's war goes on. Your side's victory ends the story.");
                if (settings.WarInHeaven)
                {
                    settings.WarCertainDay = (int)Math.Round(list.SliderLabeled($"    It starts for certain on game day {settings.WarCertainDay}",
                        settings.WarCertainDay, RimChroniclesSettings.MinWarCertainDay, RimChroniclesSettings.MaxWarCertainDay) / 10f) * 10;
                    settings.WarOddsSteepness = (float)Math.Round(list.SliderLabeled($"    Odds before then: {settings.WarOddsSteepness:0.0}×",
                        settings.WarOddsSteepness, 0f, 3f, tooltip: "0: only on the certain day. 1: about 3% a day after six months of the colony's war."), 1);
                    settings.WarFrontFactor = (float)Math.Round(list.SliderLabeled($"    While it's on, crises send {settings.WarFrontFactor:P0} of their forces at the colony",
                        settings.WarFrontFactor, 0.5f, 1f, tooltip: "The rest is pulled away to the front."), 2);
                }
            }
        }

        list.Gap();
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

        Heading(list, "Advanced: separate models");
        _advancedOpen ??= settings.ProseModel.Length > 0 || settings.DirectorModel.Length > 0;
        if (list.ButtonText(_advancedOpen.Value ? "Hide" : "Show a separate prose or director model", widthPct: 0.45f))
        {
            _advancedOpen = !_advancedOpen.Value;
        }

        if (_advancedOpen.Value)
        {
            DrawSeparateModels(list, settings);
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
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Reset every Rim Chronicles setting to its default? This clears the model server, any separate prose or director "
                + "model, and the story settings.",
                () =>
                {
                    settings.ResetToDefaults();
                    _probeStatus = "";
                    _advancedOpen = null;
                    _customDirector = false;
                },
                destructive: true));
        }

        _height = list.CurHeight + 24f;
        list.End();
        Widgets.EndScrollView();
    }

    /// <summary>A section heading: a line, then the title in the medium font.</summary>
    private static void Heading(Listing_Standard list, string title)
    {
        if (list.CurHeight > 0f)
        {
            list.GapLine();
        }

        Text.Font = GameFont.Medium;
        list.Label(title);
        Text.Font = GameFont.Small;
    }

    /// <summary>The optional second and third models: one for narration, one for the director's decisions.</summary>
    private static void DrawSeparateModels(Listing_Standard list, RimChroniclesSettings settings)
    {
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

        list.Gap();
        list.Label("Director model: makes the director's decisions, while the main model writes");
        var preset = TierPresets.DirectorPreset(settings.DirectorModel);
        var custom = _customDirector || (settings.DirectorModel.Length > 0 && preset is null);
        if (list.RadioButton("    None: the main model decides", settings.DirectorModel.Length == 0 && !custom, tabIn: 12f))
        {
            settings.DirectorModel = "";
            _customDirector = false;
        }

        foreach (var choice in TierPresets.DirectorPresets)
        {
            if (list.RadioButton($"    {choice.Label}", !custom && preset?.Model == choice.Model, tabIn: 12f))
            {
                settings.DirectorModel = choice.Model;
                settings.DirectorTier = choice.Tier; // the tier it was trained on
                _customDirector = false;
            }
        }

        if (list.RadioButton("    Other: a model you name", custom, tabIn: 12f))
        {
            if (!custom)
            {
                settings.DirectorModel = "";
            }

            _customDirector = true;
        }

        if (custom)
        {
            settings.DirectorModel = list.TextEntryLabeled("        Director model name  ", settings.DirectorModel).Trim();
            if (settings.DirectorModel.Length == 0)
            {
                list.Label("<color=#a0a0a0>        Enter the model's name as the server knows it (e.g. from ollama list). Blank: the main model decides.</color>");
            }

            list.Label("        Director prompt tier: the tier the model was trained on");
            foreach (var tier in new[] { ModelTier.Minimum, ModelTier.Recommended, ModelTier.High })
            {
                if (list.RadioButton($"            {tier}", settings.DirectorTier == tier, tabIn: 12f))
                {
                    settings.DirectorTier = tier;
                }
            }

            if (TierPresets.SuggestedDirectorTier(settings.DirectorModel) is { } trained && trained != settings.DirectorTier)
            {
                list.Label($"<color=#e0a040>The name says this model was trained for {trained}: other tiers send it prompts it never saw.</color>");
            }
        }

        if (settings.DirectorModel.Length > 0)
        {
            settings.DirectorEndpoint = list.TextEntryLabeled("    Director address (blank: the main one)  ", settings.DirectorEndpoint);
            if (settings.DirectorEndpoint.Length > 0 && TierPresets.EndpointProblem(settings.DirectorEndpoint) is { } directorProblem)
            {
                list.Label($"<color=#e0a040>{directorProblem}</color>");
            }

            settings.DirectorApiKey = list.TextEntryLabeled("    Director API key (blank: the main one)  ", settings.DirectorApiKey);
            if (!ModelSlots.DirectorSeparate(settings.Slots) && settings.Tier != ModelTier.Off)
            {
                list.Label("<color=#e0a040>The director model isn't usable as set, so the main model makes the decisions.</color>");
            }

            if (settings.Tier == ModelTier.Off)
            {
                list.Label("<color=#a0a0a0>The tier is Off, so no model is called, this one included.</color>");
            }
        }
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
