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

    /// <summary>Act 3: the reckoning. The Empire is Ascendant or Broken, and the ending paths open.</summary>
    public const string Reckoning = "Reckoning";

    /// <summary>Act 3, ally with a high title: the Empire offers a governorship (accept: Imperial victory).</summary>
    public const string Governorship = "Governorship";

    /// <summary>Act 3, opposed: assault the Imperial capital (taken: Resistance victory).</summary>
    public const string StormCapital = "StormCapital";

    /// <summary>Act 3, neutral against an Ascendant Empire: submit (ally) or refuse (war).</summary>
    public const string FinalDemand = "FinalDemand";

    /// <summary>Act 3, against a Broken Empire: sign a treaty of independence (Independence victory) or fight on.</summary>
    public const string Treaty = "Treaty";

    /// <summary>The plot reached an ending: the epilogue and the victory screen.</summary>
    public const string Ending = "Ending";
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

    /// <summary>The job under way: an <see cref="EmpireStep"/>.</summary>
    public const string Job = "job";

    /// <summary>The settlement the current job is about, for its aftermath.</summary>
    public const string JobTarget = "jobTarget";

    /// <summary>The answer to a letter job (tribute, governorship, final demand, treaty).</summary>
    public const string Answer = "answer";

    public const string TributePaid = "paid";
    public const string TributeRefused = "refused";
    public const string TributeFealty = "fealty";
    public const string Accept = "accept";
    public const string Decline = "decline";

    /// <summary>No answer by the deadline.</summary>
    public const string Ignored = "ignored";

    /// <summary>The day an unanswered letter job counts as ignored.</summary>
    public const string JobDeadline = "jobDeadline";

    /// <summary>The ending the plot has reached (an <see cref="EmpireEnding"/>), once it has.</summary>
    public const string EndingFact = "ending";

    public const int LetterTimeoutDays = 5;

    /// <summary>Act 2 ends when this many expansions have happened, or the colony has finished this many jobs.</summary>
    public const int ExpansionsForReckoning = 5;

    public const int JobsForReckoning = 3;

    /// <summary>Days the Act 1 envoy waits for an answer before the colony counts as neutral.</summary>
    public const int EnvoyTimeoutDays = 5;

    /// <summary>Days to take the Imperial capital once the assault is accepted.</summary>
    public const int CapitalDays = 60;

    private static readonly HashSet<string> LetterJobs = [EmpireStep.Tribute, EmpireStep.Governorship, EmpireStep.FinalDemand, EmpireStep.Treaty];

    public static int Count(Plot plot, string key) => int.TryParse(plot.Fact(key), out var n) ? n : 0;

    public static string StanceOf(Plot plot) => plot.Fact(Stance) is { Length: > 0 } s ? s : EmpireStance.Neutral;

    /// <summary>The quest fact for the current job, or null when the job isn't a quest.</summary>
    public static string? JobQuestFact(Plot plot) => plot.Fact(Job) switch
    {
        EmpireStep.EscortEnvoy => QuestLink.FactFor(QuestTemplates.Escort),
        EmpireStep.ConquestJob or EmpireStep.SabotageImperial or EmpireStep.StormCapital => QuestLink.FactFor(QuestTemplates.Sabotage),
        EmpireStep.DefendAlly => QuestLink.FactFor(QuestTemplates.DefendAlly),
        _ => null,
    };

    /// <summary>Where the current job stands: "none", "waiting" (offered or under way) or "done" (an answer to apply).
    /// A letter job with no answer by its deadline counts as <see cref="Ignored"/>.</summary>
    public static string JobState(Plot plot, int today)
    {
        if (plot.Fact(Job) is not { Length: > 0 } job)
        {
            return "none";
        }

        var letter = LetterJobs.Contains(job);
        if (letter && plot.Fact(Answer) is null or "" && int.TryParse(plot.Fact(JobDeadline), out var deadline) && today >= deadline)
        {
            plot.SetFact(Answer, Ignored);
        }

        var answer = letter ? plot.Fact(Answer) : JobQuestFact(plot) is { } fact ? plot.Fact(fact) : null;
        return answer is null or "" or "pending" ? "waiting" : "done";
    }

    public static IReadOnlyList<PlotTransition> Transitions(Plot plot, EmpireSaga saga, int today) =>
        Director.Described(Options(plot, saga, today), o => Describe(o.Step, plot));

    /// <summary>What each step would do, for the director.</summary>
    public static string Describe(string step, Plot plot) => step switch
    {
        EmpireStep.Envoy => "an Imperial envoy asks the colony to stand with the Empire, against it, or aside",
        EmpireStep.Expansion => "the Empire starts taking settlements by force (Act 2)",
        EmpireStep.EscortEnvoy => "the Empire asks its ally to escort its envoys to an Imperial settlement",
        EmpireStep.ConquestJob => "the Empire asks its ally to sabotage a settlement it means to take",
        EmpireStep.DefendAlly => "a neighbour under Imperial siege asks the colony to break the siege",
        EmpireStep.SabotageImperial => "the colony is offered a chance to sabotage an Imperial settlement",
        EmpireStep.Tribute => "the Empire demands tribute: pay, refuse (war), or swear fealty",
        EmpireStep.Aftermath => "the last job's outcome takes effect",
        EmpireStep.Reckoning => "Act 3: the Empire becomes ascendant or breaks, and the endings open",
        EmpireStep.Governorship => "the Empire offers a governorship; accepting ends the story in Imperial victory",
        EmpireStep.StormCapital => "the colony is offered the assault on the Imperial capital",
        EmpireStep.FinalDemand => "the Empire's final demand: submit, or be counted among its enemies",
        EmpireStep.Treaty => "the Empire offers a treaty of independence; signing ends the story",
        EmpireStep.Ending => "the story reaches its ending",
        _ => step,
    };

    private static IReadOnlyList<PlotTransition> Options(Plot plot, EmpireSaga saga, int today)
    {
        switch (plot.Beat)
        {
            case PlotBeat.Hook when plot.BeatDay < 0:
                return [new("Act1:ImperialEnvoy", EmpireStep.Envoy, 1)];
            case PlotBeat.Hook:
                return saga.State == EmpireState.Awakened ? [new("Act2:TheEmpireMarches", EmpireStep.Expansion, 1)] : [];
            case PlotBeat.Complication:
            case PlotBeat.Choice:
                break;
            default:
                return [];
        }

        if (plot.Fact(EndingFact) is { Length: > 0 } ending)
        {
            return [new($"Ending:{ending}", EmpireStep.Ending, 1)];
        }

        switch (JobState(plot, today))
        {
            case "waiting":
                return [];
            case "done":
                return [new($"{(plot.Beat == PlotBeat.Choice ? "Act3" : "Act2")}:Aftermath:{plot.Fact(Job)}", EmpireStep.Aftermath, 1)];
        }

        var options = plot.Beat == PlotBeat.Choice ? Act3Options(plot, saga) : Act2Options(plot, saga);
        if (options.Count > 0 && options[0].Step == EmpireStep.Reckoning)
        {
            return options;
        }

        if (!PlotBook.HasWaitedThisBeat(plot))
        {
            options.Add(new(Director.WaitId, Director.WaitId, 1));
        }

        return options;
    }

    private static List<PlotTransition> Act2Options(Plot plot, EmpireSaga saga)
    {
        if (saga.Expansions.Count >= ExpansionsForReckoning || Count(plot, Wins) + Count(plot, Losses) >= JobsForReckoning)
        {
            return [new("Act3:Reckoning", EmpireStep.Reckoning, 1)];
        }

        return StanceOf(plot) switch
        {
            EmpireStance.Ally => [new("Empire:EscortEnvoy", EmpireStep.EscortEnvoy, 3), new("Empire:ConquestJob", EmpireStep.ConquestJob, 2)],
            EmpireStance.Oppose => [new("Empire:DefendAlly", EmpireStep.DefendAlly, 3), new("Empire:SabotageImperial", EmpireStep.SabotageImperial, 2)],
            _ => [new("Empire:TributeDemand", EmpireStep.Tribute, 2)],
        };
    }

    /// <summary>Act 3, the ending paths (design spec, Stage 5): an ally with a high title may accept a governorship
    /// (Imperial victory), and keeps serving until then; an opponent may storm the capital (Resistance victory), and
    /// against a broken Empire may instead force a treaty (Independence); a neutral colony faces the Ascendant Empire's
    /// final demand, or the Broken Empire's treaty.</summary>
    private static List<PlotTransition> Act3Options(Plot plot, EmpireSaga saga)
    {
        var broken = saga.State == EmpireState.Broken;
        var options = new List<PlotTransition>();
        switch (StanceOf(plot))
        {
            case EmpireStance.Ally:
                if (saga.HighTitleHolder.Length > 0)
                {
                    options.Add(new("Act3:OfferGovernorship", EmpireStep.Governorship, 4));
                }

                options.Add(new("Empire:EscortEnvoy", EmpireStep.EscortEnvoy, 2));
                options.Add(new("Empire:ConquestJob", EmpireStep.ConquestJob, 2));
                break;
            case EmpireStance.Oppose:
                options.Add(new("Act3:StormTheCapital", EmpireStep.StormCapital, 3));
                if (broken)
                {
                    options.Add(new("Act3:ForceATreaty", EmpireStep.Treaty, 2));
                }

                break;
            default:
                options.Add(broken ? new("Act3:ForceATreaty", EmpireStep.Treaty, 3) : new("Act3:FinalDemand", EmpireStep.FinalDemand, 3));
                break;
        }

        return options;
    }

    /// <summary>Starts a job: remembers it, and clears the wait marker so the plot may wait again after it. Letter jobs
    /// get a deadline <see cref="LetterTimeoutDays"/> days on.</summary>
    public static void StartJob(Plot plot, string step, string target, int today = 0)
    {
        plot.SetFact(Job, step);
        plot.SetFact(JobTarget, target);
        plot.SetFact(Answer, "");
        plot.SetFact(JobDeadline, LetterJobs.Contains(step)
            ? (today + LetterTimeoutDays).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "");
        if (JobQuestFact(plot) is { } fact)
        {
            plot.SetFact(fact, "");
        }

        plot.SetFact(PlotBook.WaitedOnBeat, "");
    }

    /// <summary>What a finished job did, as plain data for the game to apply. <see cref="Ending"/> is set when the job
    /// reached an ending.</summary>
    public sealed record JobResult(string Job, string Target, bool Success, string? NewStance, string? Ending = null);

    /// <summary>Reads the finished job, tallies it, and clears it. Quests succeed only on "success". Letter answers may
    /// change the stance or reach an ending; see <see cref="Act3Options"/>.</summary>
    public static JobResult FinishJob(Plot plot)
    {
        var job = plot.Fact(Job) ?? "";
        var target = plot.Fact(JobTarget) ?? "";
        var answer = plot.Fact(Answer) ?? "";
        JobResult result;
        if (LetterJobs.Contains(job))
        {
            result = (job, answer) switch
            {
                (EmpireStep.Tribute, TributePaid) => new JobResult(job, target, true, null),
                (EmpireStep.Tribute, TributeRefused) => new JobResult(job, target, false, EmpireStance.Oppose),
                (EmpireStep.Tribute, TributeFealty) => new JobResult(job, target, true, EmpireStance.Ally),
                (EmpireStep.Governorship, Accept) => new JobResult(job, target, true, null, EmpireEnding.Imperial),
                (EmpireStep.FinalDemand, Accept) => new JobResult(job, target, true, EmpireStance.Ally),
                (EmpireStep.FinalDemand, _) => new JobResult(job, target, false, EmpireStance.Oppose), // refused, or ignored: war
                (EmpireStep.Treaty, Accept) => new JobResult(job, target, true, null, EmpireEnding.Independence),
                (EmpireStep.Treaty, Decline) => new JobResult(job, target, false, EmpireStance.Oppose),
                _ => new JobResult(job, target, false, null),
            };
        }
        else
        {
            var success = JobQuestFact(plot) is { } fact && plot.Fact(fact) == QuestOutcome.Success;
            result = new JobResult(job, target, success, null, success && job == EmpireStep.StormCapital ? EmpireEnding.Resistance : null);
            plot.SetFact(success ? Wins : Losses, (Count(plot, success ? Wins : Losses) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (result.NewStance is { } stance)
        {
            plot.SetFact(Stance, stance);
        }

        if (result.Ending is { } ending)
        {
            plot.SetFact(EndingFact, ending);
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

/// <summary>The three endings of the major plot (design spec, Stage 5).</summary>
public static class EmpireEnding
{
    /// <summary>The colony and its allies destroyed the Imperial capital.</summary>
    public const string Resistance = "Resistance";

    /// <summary>A colonist rose high in the Empire's service and accepted a governorship.</summary>
    public const string Imperial = "Imperial";

    /// <summary>The colony forced a treaty recognising it as a free state.</summary>
    public const string Independence = "Independence";
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

    public static (string Label, string Text) Governorship(string empire, string colony, string holder) => (
        "An Imperial governorship",
        $"{empire} has taken note of {holder}'s service. An Imperial herald brings the offer: {colony} and the lands around it, "
        + $"to be governed in the Empire's name by {holder}.\n\nAccepting would end {colony}'s story as a free colony, and begin it as an Imperial province.");

    public static (string Label, string Text) FinalDemand(string empire, string colony) => (
        "The Empire's final demand",
        $"{empire} has stopped asking. Its herald reads the terms aloud: {colony} will submit and serve the Empire, or be counted among its enemies.\n\n"
        + "There is no third option on the parchment.");

    public static (string Label, string Text) Treaty(string empire, string colony) => (
        "A treaty of independence",
        $"{empire} is in no state to fight on. Its envoy comes with a treaty: the Empire will recognise {colony} as a free state, "
        + "bound to no one, if the fighting stops.\n\nSigning ends the war, and the story of the Empire with it.");

    public static (string Name, string Description) CapitalQuest(string empire, string capital, string allies) => (
        $"Storm {capital}",
        $"{capital} is the heart of {empire}. Take a force there and destroy it, and the Empire falls with it."
        + (allies.Length > 0 ? $" {allies} stand with you." : "")
        + $" You have {EmpireArc.CapitalDays} days.");

    public static string EndingNews(string ending, string empire, string colony) => ending switch
    {
        EmpireEnding.Resistance => $"{colony} destroyed the capital of {empire}, and the Empire has fallen",
        EmpireEnding.Imperial => $"{colony} became an Imperial province, governed in the name of {empire}",
        _ => $"{empire} recognised {colony} as a free state",
    };

    public static string AftermathNews(EmpireArc.JobResult r, string empire, string colony) => (r.Job, r.Success) switch
    {
        (EmpireStep.EscortEnvoy, true) => $"Imperial envoys reached {r.Target} under the protection of {colony}",
        (EmpireStep.ConquestJob, true) => $"{r.Target} fell to {empire} after its defences were sabotaged",
        (EmpireStep.DefendAlly, true) => $"{colony} broke the Imperial siege of {r.Target}",
        (EmpireStep.DefendAlly, false) => $"{r.Target} fell to {empire} after a siege",
        (EmpireStep.SabotageImperial, true) => $"Saboteurs struck the Imperial settlement of {r.Target}",
        (EmpireStep.Tribute, _) when r.NewStance == EmpireStance.Oppose => $"{colony} refused to pay tribute to {empire}",
        (EmpireStep.Tribute, _) when r.NewStance == EmpireStance.Ally => $"{colony} swore fealty to {empire}",
        (EmpireStep.FinalDemand, _) when r.NewStance == EmpireStance.Ally => $"{colony} submitted to {empire}",
        (EmpireStep.FinalDemand, _) => $"{colony} refused the final demand of {empire}",
        (EmpireStep.StormCapital, false) => $"An assault on the Imperial capital of {r.Target} failed",
        _ => "",
    };
}
