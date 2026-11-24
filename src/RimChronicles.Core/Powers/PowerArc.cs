using RimChronicles.Core.Empire;
using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Powers;

/// <summary>The colony's stance toward the Empire, chosen in Act 1 (design spec, Stage 5).</summary>
public static class PowerStance
{
    public const string Ally = "ally";
    public const string Oppose = "oppose";
    public const string Neutral = "neutral";
}

/// <summary>What the Empire plot does when its next beat is due. Each is a transition's <see cref="PlotTransition.Step"/>.</summary>
public static class PowerStep
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
/// A power's major plot in the plot book (the Awakened Empire, Stage 5 M4; the Hollow Choir from W4), so the director,
/// quest links and beat spacing work as for minor plots. The acts map onto beats: Act 1 is the Hook, Act 2 the
/// Complication, Act 3 the Choice. The stance and the tallies are plot facts. Plain decisions; the world (the saga)
/// comes in as <see cref="PowerSaga"/>, and option ids and descriptions come from its <see cref="Power"/>.
/// </summary>
public static class PowerArc
{
    
    public const string Stance = "stance";
    public const string Wins = "wins";
    public const string Losses = "losses";

    /// <summary>The job under way: an <see cref="PowerStep"/>.</summary>
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

    /// <summary>What last turned the colony against the Empire, when a letter answer did: <see cref="CauseTribute"/>,
    /// <see cref="CauseFinalDemand"/> or <see cref="CauseTreaty"/>. The crisis stages read it.</summary>
    public const string StanceCause = "stanceCause";

    public const string CauseTribute = "tribute";
    public const string CauseFinalDemand = "finalDemand";
    public const string CauseTreaty = "treaty";

    /// <summary>Jobs finished (wins and losses) when <see cref="StanceCause"/> was set.</summary>
    public const string JobsAtStanceChange = "jobsAtStanceChange";

    public const int LetterTimeoutDays = 5;

    /// <summary>Act 2 ends when this many expansions have happened, or the colony has finished this many jobs.</summary>
    public const int ExpansionsForReckoning = 5;

    public const int JobsForReckoning = 3;

    /// <summary>Days the Act 1 envoy waits for an answer before the colony counts as neutral.</summary>
    public const int EnvoyTimeoutDays = 5;

    /// <summary>Days to take the Imperial capital once the assault is accepted.</summary>
    public const int CapitalDays = 60;

    private static readonly HashSet<string> LetterJobs = [PowerStep.Tribute, PowerStep.Governorship, PowerStep.FinalDemand, PowerStep.Treaty];

    public static int Count(Plot plot, string key) => int.TryParse(plot.Fact(key), out var n) ? n : 0;

    public static string StanceOf(Plot plot) => plot.Fact(Stance) is { Length: > 0 } s ? s : PowerStance.Neutral;

    /// <summary>The quest fact for the current job, or null when the job isn't a quest.</summary>
    public static string? JobQuestFact(Plot plot) => plot.Fact(Job) switch
    {
        PowerStep.EscortEnvoy => QuestLink.FactFor(QuestTemplates.Escort),
        PowerStep.ConquestJob or PowerStep.SabotageImperial or PowerStep.StormCapital => QuestLink.FactFor(QuestTemplates.Sabotage),
        PowerStep.DefendAlly => QuestLink.FactFor(QuestTemplates.DefendAlly),
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

    public static IReadOnlyList<PlotTransition> Transitions(Plot plot, PowerSaga saga, int today) =>
        Director.Described(Options(plot, saga, today), o => saga.Power.Text.Describe(o.Step));

    private static IReadOnlyList<PlotTransition> Options(Plot plot, PowerSaga saga, int today)
    {
        switch (plot.Beat)
        {
            case PlotBeat.Hook when plot.BeatDay < 0:
                return [new(saga.Power.EnvoyOption, PowerStep.Envoy, 1)];
            case PlotBeat.Hook:
                return saga.State == PowerState.Awakened ? [new(saga.Power.MarchOption, PowerStep.Expansion, 1)] : [];
            case PlotBeat.Complication:
            case PlotBeat.Choice:
                break;
            default:
                return [];
        }

        if (plot.Fact(EndingFact) is { Length: > 0 } ending)
        {
            return [new($"Ending:{ending}", PowerStep.Ending, 1)];
        }

        switch (JobState(plot, today))
        {
            case "waiting":
                return [];
            case "done":
                return [new($"{(plot.Beat == PlotBeat.Choice ? "Act3" : "Act2")}:Aftermath:{plot.Fact(Job)}", PowerStep.Aftermath, 1)];
        }

        var options = plot.Beat == PlotBeat.Choice ? Act3Options(plot, saga) : Act2Options(plot, saga);
        if (options.Count > 0 && options[0].Step == PowerStep.Reckoning)
        {
            return options;
        }

        if (!PlotBook.HasWaitedThisBeat(plot))
        {
            options.Add(new(Director.WaitId, Director.WaitId, 1));
        }

        return options;
    }

    private static List<PlotTransition> Act2Options(Plot plot, PowerSaga saga)
    {
        if (saga.Expansions.Count >= ExpansionsForReckoning || Count(plot, Wins) + Count(plot, Losses) >= JobsForReckoning)
        {
            return [new("Act3:Reckoning", PowerStep.Reckoning, 1)];
        }

        return StanceOf(plot) switch
        {
            PowerStance.Ally => [new($"{saga.Power.OptionPrefix}:EscortEnvoy", PowerStep.EscortEnvoy, 3), new($"{saga.Power.OptionPrefix}:ConquestJob", PowerStep.ConquestJob, 2)],
            PowerStance.Oppose => [new($"{saga.Power.OptionPrefix}:DefendAlly", PowerStep.DefendAlly, 3), new($"{saga.Power.OptionPrefix}:{saga.Power.Option("SabotageImperial")}", PowerStep.SabotageImperial, 2)],
            _ => [new($"{saga.Power.OptionPrefix}:TributeDemand", PowerStep.Tribute, 2)],
        };
    }

    /// <summary>Act 3, the ending paths (design spec, Stage 5): an ally with a high title may accept a governorship
    /// (Imperial victory), and keeps serving until then; an opponent may storm the capital (Resistance victory), and
    /// against a broken Empire may instead force a treaty (Independence); a neutral colony faces the Ascendant Empire's
    /// final demand, or the Broken Empire's treaty.</summary>
    private static List<PlotTransition> Act3Options(Plot plot, PowerSaga saga)
    {
        var broken = saga.State == PowerState.Broken;
        var options = new List<PlotTransition>();
        switch (StanceOf(plot))
        {
            case PowerStance.Ally:
                if (saga.HighTitleHolder.Length > 0)
                {
                    options.Add(new($"Act3:{saga.Power.Option("OfferGovernorship")}", PowerStep.Governorship, 4));
                }

                options.Add(new($"{saga.Power.OptionPrefix}:EscortEnvoy", PowerStep.EscortEnvoy, 2));
                options.Add(new($"{saga.Power.OptionPrefix}:ConquestJob", PowerStep.ConquestJob, 2));
                break;
            case PowerStance.Oppose:
                options.Add(new($"Act3:{saga.Power.Option("StormTheCapital")}", PowerStep.StormCapital, 3));
                if (broken)
                {
                    options.Add(new("Act3:ForceATreaty", PowerStep.Treaty, 2));
                }

                break;
            default:
                options.Add(broken ? new("Act3:ForceATreaty", PowerStep.Treaty, 3) : new("Act3:FinalDemand", PowerStep.FinalDemand, 3));
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
    public static JobResult FinishJob(Plot plot, Power? power = null)
    {
        power ??= Power.Empire;
        var job = plot.Fact(Job) ?? "";
        var target = plot.Fact(JobTarget) ?? "";
        var answer = plot.Fact(Answer) ?? "";
        JobResult result;
        if (LetterJobs.Contains(job))
        {
            result = (job, answer) switch
            {
                (PowerStep.Tribute, TributePaid) => new JobResult(job, target, true, null),
                (PowerStep.Tribute, TributeRefused) => new JobResult(job, target, false, PowerStance.Oppose),
                (PowerStep.Tribute, TributeFealty) => new JobResult(job, target, true, PowerStance.Ally),
                (PowerStep.Governorship, Accept) => new JobResult(job, target, true, null, power.ServiceEnding),
                (PowerStep.FinalDemand, Accept) => new JobResult(job, target, true, PowerStance.Ally),
                (PowerStep.FinalDemand, _) => new JobResult(job, target, false, PowerStance.Oppose), // refused, or ignored: war
                (PowerStep.Treaty, Accept) => new JobResult(job, target, true, null, EmpireEnding.Independence),
                (PowerStep.Treaty, Decline) => new JobResult(job, target, false, PowerStance.Oppose),
                _ => new JobResult(job, target, false, null),
            };
        }
        else
        {
            var success = JobQuestFact(plot) is { } fact && plot.Fact(fact) == QuestOutcome.Success;
            result = new JobResult(job, target, success, null, success && job == PowerStep.StormCapital ? power.DefeatEnding : null);
            plot.SetFact(success ? Wins : Losses, (Count(plot, success ? Wins : Losses) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (result.NewStance is { } stance)
        {
            plot.SetFact(Stance, stance);
            if (stance == PowerStance.Oppose)
            {
                plot.SetFact(StanceCause, job switch
                {
                    PowerStep.Tribute => CauseTribute,
                    PowerStep.FinalDemand => CauseFinalDemand,
                    _ => CauseTreaty,
                });
                plot.SetFact(JobsAtStanceChange, (Count(plot, Wins) + Count(plot, Losses)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
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
    public static int ReckoningScore(Plot plot, PowerSaga saga)
    {
        var help = StanceOf(plot) switch
        {
            PowerStance.Ally => Count(plot, Wins),
            PowerStance.Oppose => -Count(plot, Wins),
            _ => 0,
        };
        return saga.Conquests - saga.Defeats + help;
    }
}
