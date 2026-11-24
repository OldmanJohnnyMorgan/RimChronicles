using RimChronicles.Core.Plots;
using RimChronicles.Core.Snapshots;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Settings;
using RimChronicles.Llm;
using RimChronicles.Llm.Tasks;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>
/// The plot director in game (Stage 4 M1), run once a day. When a plot's beat is due, its arc lists the allowed
/// transitions. One option fires at once; with several, the model picks one with a reason (the director prompt, on a
/// background task), or a weighted random pick stands in when no model is set up or it gives no valid answer. Every
/// decision is logged in the plot book. At most one decision is made per day, and plots space their beats two days apart.
/// </summary>
public static class PlotDriver
{
    /// <summary>The transitions a plot may take now.</summary>
    public delegate IReadOnlyList<PlotTransition> TransitionsFor(Plot plot);

    /// <summary>Fires the chosen transition (never "Wait", which the driver handles). Returns false if it couldn't fire
    /// today; it is chosen again tomorrow.</summary>
    public delegate bool BeatHandler(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition);

    /// <summary><paramref name="UseDirector"/> is false for arcs whose options must not reach the director model yet
    /// (the Imperial crisis until C11, tech spec R17): they always pick by weight, and are never recorded as training cases.</summary>
    private sealed record Arc(TransitionsFor Transitions, BeatHandler Fire, bool UseDirector);

    private static readonly Dictionary<string, Arc> Arcs = [];

    public static void Register(string templateId, TransitionsFor transitions, BeatHandler fire, bool useDirector = true) =>
        Arcs[templateId] = new(transitions, fire, useDirector);

    /// <summary>Called once per in-game day.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var book = component.Plots;
        var today = GenDate.DaysPassed;
        var due = book.DueToday(today, ColonyPacingGate.Instance, out var blocked);
        if (blocked is not null && Prefs.DevMode && book.Active.Any(p => today >= p.NextBeatDay))
        {
            Log.Message($"[Rim Chronicles] Plot beats wait: {blocked}.");
        }

        foreach (var plot in due.Where(p => !component.Deciding.Contains(p.Id)))
        {
            if (Decide(component, plot, today))
            {
                break; // one decision a day
            }
        }
    }

    /// <summary>Decides and fires one plot's next beat now (Dev Mode), ignoring its wait, the spacing and the gate.</summary>
    public static bool FireNow(RimChroniclesGameComponent component, Plot plot, int today) =>
        !component.Deciding.Contains(plot.Id) && Decide(component, plot, today);

    /// <summary>Starts a decision. Returns true if one was made or started (so no other plot decides today).</summary>
    private static bool Decide(RimChroniclesGameComponent component, Plot plot, int today)
    {
        if (PlotTemplateDef.Find(plot.Template) is not { } template || !Arcs.TryGetValue(plot.Template, out var arc))
        {
            return false;
        }

        var options = arc.Transitions(plot);
        if (options.Count == 0)
        {
            return false;
        }

        if (options.Count == 1)
        {
            return Apply(component, plot, template, arc, options[0], options, today, new DirectorChoice(options[0].Id, ""), "Only");
        }

        var fallback = Director.PickWeighted(options, Rand.Value);
        if (!arc.UseDirector)
        {
            return Apply(component, plot, template, arc, fallback, options, today,
                new DirectorChoice(fallback.Id, "picked by weight: this arc doesn't use the director yet"), "Random");
        }

        var settings = RimChroniclesMod.Settings;
        var director = ProviderFactory.CreateDirector(settings);
        var provider = director?.Provider;
        var prompts = PromptStore.Library;
        var modelReady = provider is not null && prompts is not null;

        // Main thread: read the world into plain data; the model call runs in the background.
        var snapshot = modelReady || settings.RecordDirectorCases ? Snapshot(plot, options, today) : null;
        if (settings.RecordDirectorCases)
        {
            DirectorRecorder.Save(snapshot!);
        }

        if (!modelReady)
        {
            return Apply(component, plot, template, arc, fallback, options, today,
                new DirectorChoice(fallback.Id, "picked by weight: no model is set up"), "Random");
        }

        var tier = director!.Value.Tier; // a separate director model gets the tier it was trained on
        var plotId = plot.Id;
        var library = prompts!;
        var chat = provider!;
        var world = snapshot!;
        var beat = plot.Beat;
        var mainThread = component.MainThread;
        var token = component.GameToken;
        component.Deciding.Add(plotId);
        Task.Run(async () =>
        {
            DirectorChoice choice;
            string source;
            try
            {
                var (definition, _) = Director.Build(library, world, tier, grammar: true,
                    new DelegateFallback<DirectorChoice>(() => new DirectorChoice(fallback.Id, "picked by weight: no valid answer from the model")));
                var outcome = await new LlmTaskRunner(chat).RunAsync(definition, token).ConfigureAwait(false);
                ModelNotices.Post(mainThread, outcome.Attempts, "director");
                choice = outcome.Value;
                source = outcome.Source == OutcomeSource.Template ? "Random" : outcome.Source.ToString();
            }
            catch (OperationCanceledException)
            {
                return; // the game ended
            }
            catch (Exception ex)
            {
                mainThread.Post(() => Log.Error($"[Rim Chronicles] The director failed; picking by weight. {ex}"));
                choice = new DirectorChoice(fallback.Id, "picked by weight: the director failed");
                source = "Random";
            }

            mainThread.Post(() =>
            {
                component.Deciding.Remove(plotId);
                if (component.Plots.Active.FirstOrDefault(p => p.Id == plotId) is not { } live || live.Beat != beat)
                {
                    return; // the plot ended or moved on while the model was thinking
                }

                var now = arc.Transitions(live);
                if (now.FirstOrDefault(o => o.Id == choice.Choice) is not { } chosen)
                {
                    return; // the options changed; it decides again on a later day
                }

                Apply(component, live, template, arc, chosen, now, GenDate.DaysPassed, choice, source);
            });
        }, token);
        return true;
    }

    /// <summary>The world as the director sees it for this decision: the snapshot, the plot's options with what each
    /// would do, and which plot is deciding.</summary>
    private static WorldSnapshot Snapshot(Plot plot, IReadOnlyList<PlotTransition> options, int today)
    {
        var world = WorldReader.Instance.Read();
        return world with
        {
            Id = SnapshotIds.ForDirectorCase(world.Colony.Name, today, plot.Id),
            AllowedTransitions = options.Select(o => o.Id).ToList(),
            Director = new DirectorCase
            {
                PlotId = plot.Id,
                DaysInBeat = Math.Max(0, today - (plot.BeatDay >= 0 ? plot.BeatDay : plot.StartDay)),
                Descriptions = options.Where(o => o.Desc.Length > 0).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First().Desc),
            },
        };
    }

    private static bool Apply(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, Arc arc, PlotTransition chosen,
        IReadOnlyList<PlotTransition> options, int today, DirectorChoice choice, string source)
    {
        var optionsText = string.Join(", ", options.Select(o => o.Id)); // as offered, before the beat changes them
        if (chosen.Id != Director.WaitId && !arc.Fire(component, plot, template, today, chosen))
        {
            return false; // couldn't fire today (e.g. the nemesis is still on the map): no decision is logged
        }

        component.Plots.Record(new DirectorDecision
        {
            Day = today, PlotId = plot.Id, Options = optionsText, Choice = chosen.Id, Reason = choice.Reason, Source = source,
        });

        if (chosen.Id == Director.WaitId)
        {
            PlotBook.Hold(plot, today, Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays), choice.Reason);
        }

        if (Prefs.DevMode && source != "Only")
        {
            Log.Message($"[Rim Chronicles] Director, {plot.Id}: {chosen.Id} ({source}). {choice.Reason}");
        }

        return true;
    }
}
