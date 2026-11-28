using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Plots;
using RimChronicles.Game.Powers;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>
/// The Hollow Choir's crisis in game (War in Heaven W7), the mirror of <see cref="CrisisDriver"/>: once a day it works
/// out the stage from the Choir's saga and plot, announces changes, keeps the <c>ChoirCrisis</c> plot running and fires
/// the operation the plot driver picks. Its state is <see cref="RimChroniclesGameComponent.ChoirCrisis"/>. Needs the
/// Choir (Biotech), the major plot and the crisis setting. The Empire's and the Choir's operations never overlap: one
/// waits while the other's runs, and for half the gap after it. Main thread only.
/// </summary>
public static class ChoirCrisisDriver
{
    private static readonly Power Choir = Power.Choir;

    private static readonly Dictionary<string, CrisisDriver.OperationHandler> Handlers = [];

    public static void RegisterOperation(string step, CrisisDriver.OperationHandler handler) => Handlers[step] = handler;

    /// <summary>The operations the game can run today: the Hum only while its resonators aren't silenced (a destroyed
    /// Resonator site, W8), and the cluster siege only while its drops aren't down.</summary>
    public static IReadOnlySet<string> Built(RimChroniclesGameComponent component)
    {
        var built = Handlers.Keys.ToHashSet();
        var today = GenDate.DaysPassed;
        if (component.ChoirCrisis.IsDown(CrisisCapability.Psykers, today))
        {
            built.Remove(CrisisOperations.TheHum);
        }

        if (component.ChoirCrisis.IsDown(CrisisCapability.Strikes, today))
        {
            built.Remove(CrisisOperations.ClusterSiege);
        }

        return built;
    }

    public static bool Available => RimChroniclesMod.Settings.Crisis && ChoirDriver.Available;

    public static Faction? Faction => PowerFactions.Of(Choir);

    /// <summary>True when the director model decides the Choir's crisis: a model that knows the War in Heaven.</summary>
    public static bool DirectorDecides => Available && ChoirDriver.DirectorDecides;

    public static void Register() => PlotDriver.Register(Choir.CrisisTemplateId,
        plot => RimChroniclesGameComponent.Current is { } c && Available && !OtherPowerBusy(c, Choir, GenDate.DaysPassed)
            ? CrisisArc.Transitions(plot, Context(c), GenDate.DaysPassed)
            : [],
        Fire, useDirector: () => DirectorDecides, subOptions: SubOptions);

    /// <summary>A cluster siege's goals, the ones the colony gives a target for (W8), as for the Imperial siege.</summary>
    public static IReadOnlyList<PlotTransition> SubOptions(Plot plot, PlotTransition option) =>
        option.Step == CrisisOperations.ClusterSiege && ColonyStrengthReader.HomeMap() is { } map
            ? Operations.OperationSpawner.AllowedGoals(map).Select(g => new PlotTransition(g.Id, g.Goal.ToString(), g.Weight, g.Desc)).ToList()
            : [];

    /// <summary>
    /// True while the other power's crisis has an operation running, or started one within half the pacing gap: the
    /// two crises take turns, so an independent colony fighting both never faces two operations at once (tech spec §10).
    /// </summary>
    public static bool OtherPowerBusy(RimChroniclesGameComponent component, Power power, int today) =>
        CrisisArc.OtherBusy(power == Power.Choir ? component.Crisis : component.ChoirCrisis, today, RimChroniclesMod.Settings.CrisisGapDays);

    public static void Daily(RimChroniclesGameComponent component)
    {
        if (!Available || Faction is not { } choir)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var (stage, why) = Stage(component, choir);
        Announce(component, choir, stage, why, today);
        var plot = Plot(component);
        if (stage != CrisisStage.None && plot is null)
        {
            Start(component, today, why);
        }
        else if (plot is not null && component.Choir.Ending.Length > 0)
        {
            component.Plots.End(plot, today, "the Choir's story ended");
        }
    }

    public static Plot? Plot(RimChroniclesGameComponent component) =>
        component.Plots.Active.FirstOrDefault(p => p.Template == Choir.CrisisTemplateId);

    public static (CrisisStage Stage, string Why) Stage(RimChroniclesGameComponent component, Faction choir) =>
        CrisisStages.Explain(new CrisisInputs
        {
            EmpirePlot = ChoirBeats.Current(component),
            Saga = component.Choir,
            EmpireHostile = choir.HostileTo(RimWorld.Faction.OfPlayer),
            OperationsSurvived = component.ChoirCrisis.OperationsSurvived,
            Power = Choir,
        });

    public static CrisisStage StageNow(RimChroniclesGameComponent component) =>
        Faction is { } choir ? Stage(component, choir).Stage : CrisisStage.None;

    public static CrisisContext Context(RimChroniclesGameComponent component) => new()
    {
        Stage = StageNow(component),
        State = component.ChoirCrisis,
        Built = Built(component),
        HoldsCaptive = ChoirOperations.Held().Any(),
        GapDays = RimChroniclesMod.Settings.CrisisGapDays,
        Power = Choir,
    };

    /// <summary>The budget for a stage, as the Empire's, weaker while the Choir is broken.</summary>
    public static float Budget(RimChroniclesGameComponent component, CrisisStage stage, ColonyStrength strength) =>
        ThreatBudget.Compute(stage, Find.Storyteller.difficulty.threatScale * RimChroniclesMod.Settings.CrisisDifficulty, strength,
            component.Choir.State == PowerState.Broken)
        * component.War.BudgetFactor(RimChroniclesMod.Settings.WarFrontFactor); // the War in Heaven pulls forces to the front (W10)

    /// <summary>Every Choir raid pulls out: the crisis is off or over, or the colony made peace with the Choir.</summary>
    public static bool Abandoned() =>
        !Available || Faction is not { } choir || !choir.HostileTo(RimWorld.Faction.OfPlayer)
        || RimChroniclesGameComponent.Current is not { } c || Stage(c, choir).Stage == CrisisStage.None;

    private static void Start(RimChroniclesGameComponent component, int today, string why)
    {
        if (PlotTemplateDef.Find(Choir.CrisisTemplateId) is not { } template)
        {
            return;
        }

        component.Plots.Start(template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = RimWorld.Faction.OfPlayer.Name },
            today, Rand.RangeInclusive(2, 5), why);
    }

    private static void Announce(RimChroniclesGameComponent component, Faction choir, CrisisStage stage, string why, int today)
    {
        var before = component.ChoirCrisis.AnnouncedStage;
        if (component.ChoirCrisis.StageChange(stage, today) is null)
        {
            return;
        }

        var colony = RimWorld.Faction.OfPlayer.Name;
        if (CrisisText.ChoirStageNews(stage, before, choir.Name, colony) is { Length: > 0 } news)
        {
            component.News.Add(new NewsEvent(today, Choir.NewsKind, news, choir.Name));
        }

        if (CrisisText.ChoirStageLetter(stage, before, choir.Name, colony) is { } letter)
        {
            StoryLetters.ChoirCrisis(letter.Label, letter.Text, LetterDefOf.ThreatBig);
        }

        Plot(component)?.Note(today, $"stage {CrisisStages.Label(stage)}: {why}");
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Choir crisis stage {CrisisStages.Label(before)} -> {CrisisStages.Label(stage)}: {why}.");
        }
    }

    private static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        if (CrisisOperations.Find(transition.Step, Choir) is not { } operation || !Handlers.TryGetValue(operation.Step, out var handler)
            || Faction is not { } choir || ColonyStrengthReader.HomeMap() is not { } map)
        {
            return false;
        }

        var stage = Stage(component, choir).Stage;
        var budget = Budget(component, stage, ColonyStrengthReader.Read()) * component.ChoirCrisis.NextBudgetFactor;
        if (!handler(map, operation, stage, budget, transition.Sub))
        {
            return false;
        }

        CrisisArc.OperationStarted(component.Plots, plot, operation, stage, budget, component.ChoirCrisis, today,
            PlotGaps.Roll(template));
        return true;
    }

    [DebugAction("Rim Chronicles", "Choir crisis: decide now", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevDecideNow()
    {
        if (RimChroniclesGameComponent.Current is not { } component || Plot(component) is not { } plot)
        {
            Messages.Message("Rim Chronicles: no Choir crisis plot (Choir crisis: show state says why).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var today = GenDate.DaysPassed;
        plot.NextBeatDay = today;
        component.Plots.LastBeatDay = -1;
        if (!PlotDriver.FireNow(component, plot, today))
        {
            Messages.Message("Rim Chronicles: the Choir's crisis couldn't decide now (Choir crisis: show state lists what's allowed).",
                MessageTypeDefOf.RejectInput, historical: false);
        }
        else if (!DirectorDecides)
        {
            Messages.Message("Rim Chronicles: the Choir's crisis picks by weight: the director model wasn't trained on it.", MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    [DebugAction("Rim Chronicles", "Choir crisis: show state", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevShow()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        if (!Available || Faction is not { } choir)
        {
            Find.WindowStack.Add(new Dialog_MessageBox(
                "The Choir's crisis is off: it needs Biotech and Royalty, the Hollow Choir awake (its faction made), the major plot and the crisis setting on."));
            return;
        }

        var today = GenDate.DaysPassed;
        var (stage, why) = Stage(component, choir);
        var strength = ColonyStrengthReader.Read();
        var allowed = CrisisArc.Allowed(Context(component), today, ignoreBuilt: true, out var blocked);
        var built = Built(component);
        var crisis = component.ChoirCrisis;
        var lines = new List<string>
        {
            $"Day {today}. Stage: {CrisisStages.Label(stage)} ({why}). Announced: {CrisisStages.Label(crisis.AnnouncedStage)} on day {crisis.AnnouncedDay}.",
            $"Colony strength: {strength}. Factor {ThreatBudget.StrengthFactor(strength):0.00}.",
            $"Budget today: {Budget(component, stage, strength):0}.",
            $"Operations allowed today: {(allowed.Count == 0 ? $"none ({blocked})" : string.Join(", ", allowed.Select(o => built.Contains(o.Step) ? o.Id : $"{o.Id} (not built yet, or knocked out)")))}.",
            $"Taking turns with the Empire's crisis: {(OtherPowerBusy(component, Choir, today) ? "yes, the Empire's operation is running or was too recent" : "no")}.",
            $"Plot: {(Plot(component) is { } plot ? $"{plot.Id}, {plot.Beat}, next beat day {plot.NextBeatDay}" : "not started")}.",
            $"Decided by: {(DirectorDecides ? "the director model" : "weighted pick (the director model wasn't trained on the War in Heaven, or no model is set up)")}.",
            $"Held Choir captives: {string.Join(", ", ChoirOperations.Held().Select(p => p.LabelShort).DefaultIfEmpty("none"))}.",
            "",
            "Operations:",
        };
        lines.AddRange(crisis.History.TakeLast(15).Select(h => $"  day {h.Day}: {h.Operation} at {CrisisStages.Label(h.Stage)}, budget {h.Budget:0}: {h.Outcome}"));
        if (crisis.History.Count == 0)
        {
            lines.Add("  none yet");
        }

        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }
}
