using RimChronicles.Core.Plots;
using RimChronicles.Core.Snapshots;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Settings;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Tasks;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Writes plot letters (Stage 3 M3b): the model rewrites the beat's template letter on a background task, and
/// the letter is delivered through the main-thread queue. With the model off, unreachable or failing its checks, the
/// template letter goes out instead (rule 7). The beat itself has already happened; only the words wait.</summary>
public static class PlotNarrator
{
    /// <summary>A plot letter case for this plot and step, from the plot's facts.</summary>
    public static PlotLetterCase CaseFor(Plot plot, string step) => new()
    {
        Template = plot.Template,
        Step = step,
        Protagonist = plot.Protagonist.Name,
        Facts = plot.Facts.Where(f => f.Value.Length > 0).GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.Last().Value),
    };

    /// <summary>Writes the letter for <paramref name="c"/> and hands its text to <paramref name="deliver"/> on the main
    /// thread. <paramref name="deliver"/> runs at once with the template when no model is set up.</summary>
    public static void Write(RimChroniclesGameComponent component, PlotLetterCase c, string colony, string templateText, Action<string> deliver) =>
        Run(component, c.Step, templateText, deliver,
            (prompts, tier, fallback) => PlotLetterTask.Build(prompts, c, colony, tier, ThinkingMode.Off, fallback).Definition);

    /// <summary>Runs a prose task on a background task and hands the text (model or template) to <paramref name="deliver"/>
    /// on the main thread. <paramref name="build"/> runs on the background task and must use only plain data.</summary>
    public static void Run(RimChroniclesGameComponent component, string what, string templateText, Action<string> deliver,
        Func<Llm.Prompts.PromptLibrary, ModelTier, IFallback<string>, TaskDefinition<string>> build)
    {
        var settings = RimChroniclesMod.Settings;
        var provider = ProviderFactory.CreateProse(settings);
        var prompts = PromptStore.Library;
        if (provider is null || prompts is null)
        {
            deliver(templateText + Footer(nameof(OutcomeSource.Template), ""));
            return;
        }

        var tier = settings.Tier == ModelTier.Off ? ModelTier.Recommended : settings.Tier;
        var mainThread = component.MainThread;
        var token = component.GameToken;
        Task.Run(async () =>
        {
            string text;
            try
            {
                var definition = build(prompts, tier, new DelegateFallback<string>(() => templateText));
                var outcome = await new LlmTaskRunner(provider).RunAsync(definition, token).ConfigureAwait(false);
                ModelNotices.Post(mainThread, outcome.Attempts, "narration");
                text = outcome.Value + Footer(outcome.Source.ToString(), outcome.Source == OutcomeSource.Template ? "" : outcome.PromptVersion);
                if (outcome.Source == OutcomeSource.Template && Prefs.DevMode)
                {
                    var why = outcome.Attempts.LastOrDefault() is { } last ? last.Result.Ok ? string.Join("; ", last.Failures) : last.Result.Error : "no attempt";
                    mainThread.Post(() => Log.Warning($"[Rim Chronicles] {what} fell back to its template: {why}"));
                }
            }
            catch (OperationCanceledException)
            {
                return; // the game ended
            }
            catch (Exception ex)
            {
                mainThread.Post(() => Log.Error($"[Rim Chronicles] Writing {what} failed; using the template. {ex}"));
                text = templateText + Footer(nameof(OutcomeSource.Template), "");
            }

            mainThread.Post(() => deliver(text));
        }, token);
    }

    /// <summary>In Dev Mode, a grey note of who wrote the letter, as on bulletins.</summary>
    private static string Footer(string source, string version) =>
        Prefs.DevMode ? $"\n\n<color=#a0a0a0>({source}{(version.Length > 0 ? ", " + version : "")})</color>" : "";
}
