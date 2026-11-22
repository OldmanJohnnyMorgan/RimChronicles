using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Empire;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Plots;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis;

/// <summary>
/// The Imperial crisis in game (crisis C1): once a day it works out the stage from the Empire's saga and plot,
/// announces stage changes in the news, keeps the <c>ImperialCrisis</c> plot running while there's a crisis, and fires
/// the operation the plot driver picks. Operations come with later milestones and register here; until one does,
/// the crisis offers nothing. Needs Royalty, the major plot and the crisis setting on. Main thread only.
/// </summary>
public static class CrisisDriver
{
    /// <summary>Starts an operation on a home map with a budget. Returns false if it couldn't start today.</summary>
    public delegate bool OperationHandler(Map map, OperationSpec operation, CrisisStage stage, float budget);

    private static readonly Dictionary<string, OperationHandler> Handlers = [];

    /// <summary>Makes an operation available to the crisis (each operation milestone calls this at startup).</summary>
    public static void RegisterOperation(string step, OperationHandler handler) => Handlers[step] = handler;

    public static IReadOnlySet<string> Built => Handlers.Keys.ToHashSet();

    /// <summary>True when the crisis can run in this game: Royalty, the major plot and the crisis setting (Q1: without
    /// Royalty the crisis is off).</summary>
    public static bool Available => RimChroniclesMod.Settings.Crisis && EmpireDriver.Available;

    public static void Register() => PlotDriver.Register(CrisisArc.TemplateId,
        plot => RimChroniclesGameComponent.Current is { } c && Available ? CrisisArc.Transitions(plot, Context(c), GenDate.DaysPassed) : [],
        Fire, useDirector: false);

    public static void Daily(RimChroniclesGameComponent component)
    {
        if (!Available || EmpireDriver.EmpireFaction is not { } empire)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var (stage, why) = Stage(component, empire);
        Announce(component, empire, stage, why, today);
        var plot = Plot(component);
        if (stage != CrisisStage.None && plot is null)
        {
            Start(component, today, why);
        }
        else if (plot is not null && component.Empire.Ending.Length > 0)
        {
            component.Plots.End(plot, today, "the Empire's story ended");
        }
    }

    public static Plot? Plot(RimChroniclesGameComponent component) =>
        component.Plots.Active.FirstOrDefault(p => p.Template == CrisisArc.TemplateId);

    public static (CrisisStage Stage, string Why) Stage(RimChroniclesGameComponent component, Faction empire) =>
        CrisisStages.Explain(new CrisisInputs
        {
            EmpirePlot = EmpireBeats.Current(component),
            Saga = component.Empire,
            EmpireHostile = empire.HostileTo(Faction.OfPlayer),
            OperationsSurvived = component.Crisis.OperationsSurvived,
        });

    public static CrisisContext Context(RimChroniclesGameComponent component) => new()
    {
        Stage = EmpireDriver.EmpireFaction is { } empire ? Stage(component, empire).Stage : CrisisStage.None,
        State = component.Crisis,
        Built = Built,
        HoldsCaptive = false, // captives come with C10
        GapDays = RimChroniclesMod.Settings.CrisisGapDays,
    };

    /// <summary>Today's budget for a stage: the storyteller's threat scale times the crisis difficulty setting.</summary>
    public static float Budget(RimChroniclesGameComponent component, CrisisStage stage, ColonyStrength strength) =>
        ThreatBudget.Compute(stage, Find.Storyteller.difficulty.threatScale * RimChroniclesMod.Settings.CrisisDifficulty, strength,
            component.Empire.State == EmpireState.Broken);

    private static void Start(RimChroniclesGameComponent component, int today, string why)
    {
        if (PlotTemplateDef.Find(CrisisArc.TemplateId) is not { } template)
        {
            return;
        }

        component.Plots.Start(template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = Faction.OfPlayer.Name },
            today, Rand.RangeInclusive(2, 5), why);
    }

    private static void Announce(RimChroniclesGameComponent component, Faction empire, CrisisStage stage, string why, int today)
    {
        var before = component.Crisis.AnnouncedStage;
        if (component.Crisis.StageChange(stage, today) is null)
        {
            return;
        }

        var colony = Faction.OfPlayer.Name;
        if (CrisisText.StageNews(stage, before, empire.Name, colony) is { Length: > 0 } news)
        {
            component.News.Add(new NewsEvent(today, EmpireSaga.NewsKind, news, empire.Name));
        }

        if (CrisisText.StageLetter(stage, before, empire.Name, colony) is { } letter)
        {
            Find.LetterStack.ReceiveLetter(letter.Label, letter.Text, LetterDefOf.ThreatBig);
        }

        Plot(component)?.Note(today, $"stage {CrisisStages.Label(stage)}: {why}");
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Crisis stage {CrisisStages.Label(before)} -> {CrisisStages.Label(stage)}: {why}.");
        }
    }

    private static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        if (CrisisOperations.Find(transition.Step) is not { } operation || !Handlers.TryGetValue(operation.Step, out var handler)
            || EmpireDriver.EmpireFaction is not { } empire || ColonyStrengthReader.HomeMap() is not { } map)
        {
            return false;
        }

        var stage = Stage(component, empire).Stage;
        var budget = Budget(component, stage, ColonyStrengthReader.Read());
        if (!handler(map, operation, stage, budget))
        {
            return false;
        }

        CrisisArc.OperationStarted(component.Plots, plot, operation, stage, budget, component.Crisis, today,
            Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays));
        return true;
    }

    [DebugAction("Rim Chronicles", "Crisis: show state", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevShow()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        if (!Available || EmpireDriver.EmpireFaction is not { } empire)
        {
            Find.WindowStack.Add(new Dialog_MessageBox("The crisis is off: it needs Royalty, the Empire faction, the major plot and the crisis setting on."));
            return;
        }

        var today = GenDate.DaysPassed;
        var (stage, why) = Stage(component, empire);
        var strength = ColonyStrengthReader.Read();
        var context = Context(component);
        var allowed = CrisisArc.Allowed(context, today, ignoreBuilt: true, out var blocked);
        var crisis = component.Crisis;
        var lines = new List<string>
        {
            $"Day {today}. Stage: {CrisisStages.Label(stage)} ({why}). Announced: {CrisisStages.Label(crisis.AnnouncedStage)} on day {crisis.AnnouncedDay}.",
            $"Colony strength: {strength}. Factor {ThreatBudget.StrengthFactor(strength):0.00}.",
            $"Budget today: {Budget(component, stage, strength):0} (threat scale {Find.Storyteller.difficulty.threatScale:0.00}, crisis difficulty {RimChroniclesMod.Settings.CrisisDifficulty:0.0}).",
            $"Operations allowed today: {(allowed.Count == 0 ? $"none ({blocked})" : string.Join(", ", allowed.Select(o => Built.Contains(o.Step) ? o.Id : $"{o.Id} (not built yet)")))}.",
            $"Plot: {(Plot(component) is { } plot ? $"{plot.Id}, {plot.Beat}, next beat day {plot.NextBeatDay}" : "not started")}.",
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
