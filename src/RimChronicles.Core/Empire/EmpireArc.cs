using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Empire;

/// <summary>The colony's stance toward the Empire, chosen in Act 1 (design spec, Stage 5).</summary>
public static class EmpireStance
{
    public const string Ally = "ally";
    public const string Oppose = "oppose";
    public const string Neutral = "neutral";
}

/// <summary>What the Empire plot does when its next beat is due. Each is a transition's <see cref="PlotTransition.Step"/>.</summary>
public static class EmpireStep
{
    /// <summary>Act 1: the envoy arrives and asks the colony's stance.</summary>
    public const string Envoy = "Envoy";

    /// <summary>Act 2 begins: the Empire marches.</summary>
    public const string Expansion = "Expansion";

    /// <summary>Ally: escort Imperial envoys to one of its settlements.</summary>
    public const string EscortEnvoy = "EscortEnvoy";

    /// <summary>Ally: sabotage a settlement the Empire means to take, which then falls to it.</summary>
    public const string ConquestJob = "ConquestJob";

    /// <summary>Oppose: break an Imperial siege of a settlement the Empire means to take.</summary>
    public const string DefendAlly = "DefendAlly";

    /// <summary>Oppose: sabotage an Imperial settlement.</summary>
    public const string SabotageImperial = "SabotageImperial";

    /// <summary>Neutral: the Empire demands tribute (pay, refuse, or swear fealty).</summary>
    public const string Tribute = "Tribute";

    /// <summary>A quest or the tribute letter has an answer: apply what it changes.</summary>
    public const string Aftermath = "Aftermath";

    /// <summary>Act 3: the reckoning. The Empire is Ascendant or Broken, and the ending path opens (M5).</summary>
    public const string Reckoning = "Reckoning";
}

/// <summary>
/// The Awakened Empire as a plot in the plot book (Stage 5 M4), so the director, quest links and beat spacing work as
/// for minor plots. The acts map onto beats: Act 1 is the Hook, Act 2 the Complication, Act 3 the Choice. The stance and
/// the tallies are plot facts. Plain decisions; the world (the saga) comes in as <see cref="EmpireSaga"/>.
/// </summary>
public static class EmpireArc
{
    public const string TemplateId = "AwakenedEmpire";

    public const string Stance = "stance";
    public const string Wins = "wins";
    public const string Losses = "losses";

    /// <summary>The Act 2 job under way: an <see cref="EmpireStep"/>.</summary>
    public const string Job = "job";

    /// <summary>The settlement the current job is about, for its aftermath.</summary>
    public const string JobTarget = "jobTarget";

    /// <summary>The answer to the tribute letter: <see cref="TributePaid"/>, <see cref="TributeRefused"/> or <see cref="TributeFealty"/>.</summary>
    public const string Tribute = "tribute";

    public const string TributePaid = "paid";
    public const string TributeRefused = "refused";
    public const string TributeFealty = "fealty";

    /// <summary>No answer by the deadline: stance unchanged, nothing paid, and the Empire takes offence.</summary>
    public const string TributeIgnored = "ignored";

    /// <summary>The day an unanswered tribute demand counts as ignored.</summary>
    public const string JobDeadline = "jobDeadline";

    public const int TributeTimeoutDays = 5;

    /// <summary>Act 2 ends when this many expansions have happened, or the colony has finished this many jobs.</summary>
    public const int ExpansionsForReckoning = 5;

    public const int JobsForReckoning = 3;

    /// <summary>Days the Act 1 envoy waits for an answer before the colony counts as neutral.</summary>
    public const int EnvoyTimeoutDays = 5;

    public static readonly string[] JobTemplates =
        [QuestTemplates.Escort, QuestTemplates.Sabotage, QuestTemplates.DefendAlly];

    public static int Count(Plot plot, string key) => int.TryParse(plot.Fact(key), out var n) ? n : 0;

    public static string StanceOf(Plot plot) => plot.Fact(Stance) is { Length: > 0 } s ? s : EmpireStance.Neutral;

    /// <summary>The quest fact for the current job, or null when the job isn't a quest.</summary>
    public static string? JobQuestFact(Plot plot) => plot.Fact(Job) switch
    {
        EmpireStep.EscortEnvoy => QuestLink.FactFor(QuestTemplates.Escort),
        EmpireStep.ConquestJob or EmpireStep.SabotageImperial => QuestLink.FactFor(QuestTemplates.Sabotage),
        EmpireStep.DefendAlly => QuestLink.FactFor(QuestTemplates.DefendAlly),
        _ => null,
    };

    /// <summary>Where the current job stands: "none", "waiting" (offered or under way) or "done" (an answer to apply).</summary>
    public static string JobState(Plot plot, int today)
    {
        if (plot.Fact(Job) is not { Length: > 0 } job)
        {
            return "none";
        }

        if (job == EmpireStep.Tribute && plot.Fact(Tribute) is null or "" && int.TryParse(plot.Fact(JobDeadline), out var deadline) && today >= deadline)
        {
            plot.SetFact(Tribute, TributeIgnored);
        }

        var answer = job == EmpireStep.Tribute ? plot.Fact(Tribute) : JobQuestFact(plot) is { } fact ? plot.Fact(fact) : null;
        return answer is null or "" or "pending" ? "waiting" : "done";
    }

    public static IReadOnlyList<PlotTransition> Transitions(Plot plot, EmpireSaga saga, int today)
    {
        switch (plot.Beat)
        {
            case PlotBeat.Hook when plot.BeatDay < 0:
                return [new("Act1:ImperialEnvoy", EmpireStep.Envoy, 1)];
            case PlotBeat.Hook:
                return saga.State == EmpireState.Awakened ? [new("Act2:TheEmpireMarches", EmpireStep.Expansion, 1)] : [];
            case PlotBeat.Complication:
                break;
            default:
                return []; // Act 3: the endings (M5) take it from here
        }

        switch (JobState(plot, today))
        {
            case "waiting":
                return [];
            case "done":
                return [new($"Act2:Aftermath:{plot.Fact(Job)}", EmpireStep.Aftermath, 1)];
        }

        if (saga.Expansions.Count >= ExpansionsForReckoning || Count(plot, Wins) + Count(plot, Losses) >= JobsForReckoning)
        {
            return [new("Act3:Reckoning", EmpireStep.Reckoning, 1)];
        }

        var options = StanceOf(plot) switch
        {
            EmpireStance.Ally => new List<PlotTransition>
            {
                new("Empire:EscortEnvoy", EmpireStep.EscortEnvoy, 3),
                new("Empire:ConquestJob", EmpireStep.ConquestJob, 2),
            },
            EmpireStance.Oppose => new List<PlotTransition>
            {
                new("Empire:DefendAlly", EmpireStep.DefendAlly, 3),
                new("Empire:SabotageImperial", EmpireStep.SabotageImperial, 2),
            },
            _ => new List<PlotTransition> { new("Empire:TributeDemand", EmpireStep.Tribute, 2) },
        };
        if (!PlotBook.HasWaitedThisBeat(plot))
        {
            options.Add(new(Director.WaitId, Director.WaitId, 1));
        }

        return options;
    }

    /// <summary>Starts an Act 2 job: remembers it, and clears the wait marker so the plot may wait again after it.</summary>
    public static void StartJob(Plot plot, string step, string target, int today = 0)
    {
        plot.SetFact(Job, step);
        plot.SetFact(JobTarget, target);
        plot.SetFact(Tribute, "");
        plot.SetFact(JobDeadline, step == EmpireStep.Tribute
            ? (today + TributeTimeoutDays).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "");
        if (JobQuestFact(plot) is { } fact)
        {
            plot.SetFact(fact, "");
        }

        plot.SetFact(PlotBook.WaitedOnBeat, "");
    }

    /// <summary>What a finished job did, as plain data for the game to apply.</summary>
    public sealed record JobResult(string Job, string Target, bool Success, string? NewStance);

    /// <summary>Reads the finished job, tallies it, and clears it. A quest succeeds only on "success"; the tribute
    /// answer may change the stance (a refusal means opposition; fealty, alliance); an ignored demand changes nothing but goodwill.</summary>
    public static JobResult FinishJob(Plot plot)
    {
        var job = plot.Fact(Job) ?? "";
        var target = plot.Fact(JobTarget) ?? "";
        JobResult result;
        if (job == EmpireStep.Tribute)
        {
            var answer = plot.Fact(Tribute);
            var stance = answer switch
            {
                TributeRefused => EmpireStance.Oppose,
                TributeFealty => EmpireStance.Ally,
                _ => null,
            };
            result = new JobResult(job, target, answer is TributePaid or TributeFealty, stance);
            if (stance is not null)
            {
                plot.SetFact(Stance, stance);
            }
        }
        else
        {
            var success = JobQuestFact(plot) is { } fact && plot.Fact(fact) == QuestOutcome.Success;
            result = new JobResult(job, target, success, null);
            plot.SetFact(success ? Wins : Losses, (Count(plot, success ? Wins : Losses) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        plot.SetFact(Job, "");
        plot.SetFact(JobTarget, "");
        return result;
    }

    /// <summary>How the Empire stands going into Act 3: its own conquests and the colony's help count for it; its
    /// defeats and the colony's resistance count against. Above zero, it is Ascendant; otherwise Broken.</summary>
    public static int ReckoningScore(Plot plot, EmpireSaga saga)
    {
        var help = StanceOf(plot) switch
        {
            EmpireStance.Ally => Count(plot, Wins),
            EmpireStance.Oppose => -Count(plot, Wins),
            _ => 0,
        };
        return saga.Conquests - saga.Defeats + help;
    }
}

/// <summary>The Empire plot's letters (template text) and news.</summary>
public static class EmpireLetters
{
    public static (string Label, string Text) Envoy(string empire, string colony) => (
        "An Imperial envoy",
        $"An envoy of {empire} has come to {colony}, under banner and escort. The Empire, the envoy says, is waking from a long "
        + "sleep, and every settlement on the rim will soon have to decide where it stands.\n\n"
        + $"Will {colony} stand with the Empire, stand against it, or keep out of its way?");

    public static (string Label, string Text) StanceTaken(string stance, string empire, string colony) => stance switch
    {
        EmpireStance.Ally => ("Allied with the Empire",
            $"{colony} has pledged itself to {empire}. The envoy leaves pleased. The Empire will have work for its friends."),
        EmpireStance.Oppose => ("At war with the Empire",
            $"{colony} has refused {empire}. The envoy leaves without a word, and {empire} now counts {colony} among its enemies."),
        _ => ("Keeping out of it",
            $"{colony} will keep to itself. The envoy shrugs: the Empire will remember who stood aside, and it will ask for its due."),
    };

    public static (string Label, string Text) Marches(string stance, string empire, string colony) => (
        "The Empire marches",
        $"{empire} is on the move. Its columns are taking the rim settlement by settlement.\n\n" + stance switch
        {
            EmpireStance.Ally => $"As its ally, {colony} can expect to be asked for help.",
            EmpireStance.Oppose => $"{colony} has chosen to stand in its way, and its neighbours may come asking for help.",
            _ => $"{colony} has kept out of it so far. The Empire will want something for that.",
        });

    public static (string Label, string Text) TributeDemand(string empire, string colony, int silver) => (
        "The Empire demands tribute",
        $"An Imperial tax-collector has come to {colony}. {empire} demands {silver} silver as the price of being left in peace.\n\n"
        + $"{colony} can pay, refuse, or bend the knee and join the Empire's cause.");

    public static (string Label, string Text) Reckoning(bool ascendant, string empire, string colony) => ascendant
        ? ("The Empire ascendant",
            $"{empire} now holds most of the rim. Its final demands are going out to every settlement still standing.\n\n"
            + $"Whatever {colony} does next will decide how this ends.")
        : ("The Empire breaking",
            $"{empire} is reeling from its losses. Its columns are pulling back and its officers quarrel.\n\n"
            + $"Whatever {colony} does next will decide how this ends.");

    public static string AftermathNews(EmpireArc.JobResult r, string empire, string colony) => (r.Job, r.Success) switch
    {
        (EmpireStep.EscortEnvoy, true) => $"Imperial envoys reached {r.Target} under the protection of {colony}",
        (EmpireStep.ConquestJob, true) => $"{r.Target} fell to {empire} after its defences were sabotaged",
        (EmpireStep.DefendAlly, true) => $"{colony} broke the Imperial siege of {r.Target}",
        (EmpireStep.DefendAlly, false) => $"{r.Target} fell to {empire} after a siege",
        (EmpireStep.SabotageImperial, true) => $"Saboteurs struck the Imperial settlement of {r.Target}",
        (EmpireStep.Tribute, _) when r.NewStance == EmpireStance.Oppose => $"{colony} refused to pay tribute to {empire}",
        (EmpireStep.Tribute, _) when r.NewStance == EmpireStance.Ally => $"{colony} swore fealty to {empire}",
        _ => "",
    };
}
