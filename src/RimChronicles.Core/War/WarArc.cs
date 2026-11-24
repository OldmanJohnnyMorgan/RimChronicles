using System.Globalization;
using RimChronicles.Core.Choir;
using RimChronicles.Core.Empire;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;

namespace RimChronicles.Core.War;

/// <summary>What the War in Heaven's plot does when its next beat is due (spec §7.2–§7.3).</summary>
public static class WarStep
{
    /// <summary>A settlement of the colony's side is under the rival's attack: break it.</summary>
    public const string DefendFront = "DefendFront";

    /// <summary>Strike a settlement of the rival.</summary>
    public const string StrikeRival = "StrikeRival";

    /// <summary>Escort the side's convoy to the front.</summary>
    public const string EscortConvoy = "EscortConvoy";

    /// <summary>Sabotage the rival's war machine (a resonator or spire, a relay or uplink).</summary>
    public const string SabotageRival = "SabotageRival";

    /// <summary>The side's final push on the rival's capital, with the colony at its head; taking it destroys the rival.</summary>
    public const string FinalPush = "FinalPush";

    /// <summary>Independent: strike or sabotage one power.</summary>
    public const string StrikeEmpire = "StrikeEmpire";
    public const string StrikeChoir = "StrikeChoir";
    public const string SabotageEmpire = "SabotageEmpire";
    public const string SabotageChoir = "SabotageChoir";

    /// <summary>The rival is destroyed: the side offers the colony its reward (the governorship, the place in the Chorus).</summary>
    public const string VictoryOffer = "VictoryOffer";

    /// <summary>Independent, one power destroyed: the survivor offers a treaty (spec D9); the colony may refuse.</summary>
    public const string TreatyOffer = "TreatyOffer";

    public const string Aftermath = "Aftermath";
    public const string Ending = "Ending";
}

/// <summary>The War in Heaven's own ending (spec §6, T5); the others are the powers' (Imperial, Choral, Independence).</summary>
public static class WarEnding
{
    /// <summary>Independent: both powers destroyed.</summary>
    public const string IndependenceTotal = "IndependenceTotal";
}

/// <summary>What the war arc reads besides the plot: the colony's side, the war, and which powers there are.</summary>
public sealed record WarContext
{
    /// <summary>A power id the colony sided with, <see cref="Side.Independent"/>, or <see cref="Side.Undeclared"/>.</summary>
    public required string Side { get; init; }

    public required WarInHeaven War { get; init; }

    /// <summary>Powers the colony opposes (for an undeclared colony at war with one).</summary>
    public IReadOnlyList<Power> Opposed { get; init; } = [];

    public Power? SidePower => Power.All.FirstOrDefault(p => p.Id == Side);

    public Power? Rival => SidePower is { } side ? Power.All.FirstOrDefault(p => p != side) : null;
}

/// <summary>
/// The War in Heaven as a plot (spec §7, tech spec §6): once it starts, the colony's side gives it jobs against the
/// rival (defend the front, strike, escort, sabotage, and the final push once the front stands high enough), and the
/// endings follow the side: the side's offer once the rival is destroyed; for an independent colony, the survivor's
/// treaty once one power falls (refusable), and total independence once both have. Plain decisions on plot facts, like
/// <see cref="PowerArc"/>.
/// </summary>
public static class WarArc
{
    public const string Prefix = "War:";

    /// <summary>The front score at which the side's final push opens.</summary>
    public const int FrontForPush = 4;

    private static readonly HashSet<string> LetterJobs = [WarStep.VictoryOffer, WarStep.TreatyOffer];

    /// <summary>The quest template behind a job.</summary>
    public static string? QuestTemplate(string step) => step switch
    {
        WarStep.DefendFront => QuestTemplates.DefendAlly,
        WarStep.EscortConvoy => QuestTemplates.Escort,
        WarStep.StrikeRival or WarStep.SabotageRival or WarStep.FinalPush
            or WarStep.StrikeEmpire or WarStep.StrikeChoir or WarStep.SabotageEmpire or WarStep.SabotageChoir => QuestTemplates.Sabotage,
        _ => null,
    };

    public static IReadOnlyList<PlotTransition> Transitions(Plot plot, WarContext context, int today) =>
        Director.Described(Options(plot, context, today), o => Describe(o.Step, context));

    /// <summary>What each step would do, for the director.</summary>
    public static string Describe(string step, WarContext c)
    {
        var side = c.SidePower?.DisplayName ?? "the colony";
        var rival = c.Rival?.DisplayName ?? "the enemy";
        return step switch
        {
            WarStep.DefendFront => $"a settlement of {side} is under attack by {rival}; the colony is asked to break it",
            WarStep.StrikeRival => $"{side} asks the colony to strike a settlement of {rival}",
            WarStep.EscortConvoy => $"{side} asks the colony to escort a convoy to the front",
            WarStep.SabotageRival => $"{side} asks the colony to sabotage {rival}'s war machine",
            WarStep.FinalPush => $"{side} makes its final push on {rival}'s capital, with the colony at its head; taking it destroys {rival}",
            WarStep.StrikeEmpire => "the colony strikes a settlement of the Empire",
            WarStep.StrikeChoir => "the colony strikes a settlement of the Hollow Choir",
            WarStep.SabotageEmpire => "the colony sabotages the Empire's war machine",
            WarStep.SabotageChoir => "the colony sabotages the Hollow Choir's war machine",
            WarStep.VictoryOffer => $"{rival} is destroyed; {side} offers the colony its reward, which ends the story",
            WarStep.TreatyOffer => "one power is destroyed; the other offers a treaty of independence (signing ends the story; refusing fights on)",
            WarStep.Aftermath => "the last job's outcome takes effect",
            WarStep.Ending => "the story reaches its ending",
            _ => step,
        };
    }

    private static List<PlotTransition> Options(Plot plot, WarContext c, int today)
    {
        if (c.War.State != WarState.Started)
        {
            return [];
        }

        if (plot.Fact(PowerArc.EndingFact) is { Length: > 0 } ending)
        {
            return [new($"Ending:{ending}", WarStep.Ending, 1)];
        }

        switch (JobState(plot, today))
        {
            case "waiting":
                return [];
            case "done":
                return [new($"{Prefix}Aftermath:{plot.Fact(PowerArc.Job)}", WarStep.Aftermath, 1)];
        }

        var options = new List<PlotTransition>();
        var empireDown = c.War.IsDestroyed(Power.Empire);
        var choirDown = c.War.IsDestroyed(Power.Choir);
        if (c.SidePower is { } side && c.Rival is { } rival)
        {
            if (c.War.IsDestroyed(rival))
            {
                options.Add(new($"{Prefix}{WarStep.VictoryOffer}", WarStep.VictoryOffer, 3));
            }
            else
            {
                options.Add(new($"{Prefix}{WarStep.DefendFront}", WarStep.DefendFront, 3));
                options.Add(new($"{Prefix}{WarStep.StrikeRival}", WarStep.StrikeRival, 2));
                options.Add(new($"{Prefix}{WarStep.EscortConvoy}", WarStep.EscortConvoy, 2));
                options.Add(new($"{Prefix}{WarStep.SabotageRival}", WarStep.SabotageRival, 1));
                if (c.War.Front >= FrontForPush)
                {
                    options.Add(new($"{Prefix}{WarStep.FinalPush}", WarStep.FinalPush, 4));
                }
            }
        }
        else if (c.Side == Side.Independent)
        {
            if (empireDown && choirDown)
            {
                return [new($"Ending:{WarEnding.IndependenceTotal}", WarStep.Ending, 1)];
            }

            if (empireDown || choirDown)
            {
                options.Add(new($"{Prefix}{WarStep.TreatyOffer}", WarStep.TreatyOffer, 3));
            }

            AddAgainst(options, Power.Empire, empireDown);
            AddAgainst(options, Power.Choir, choirDown);
        }
        else
        {
            foreach (var p in c.Opposed)
            {
                AddAgainst(options, p, c.War.IsDestroyed(p));
            }
        }

        if (options.Count > 0 && !PlotBook.HasWaitedThisBeat(plot))
        {
            options.Add(new(Director.WaitId, Director.WaitId, 1));
        }

        return options;
    }

    private static void AddAgainst(List<PlotTransition> options, Power power, bool destroyed)
    {
        if (destroyed)
        {
            return;
        }

        var empire = power == Power.Empire;
        options.Add(new($"{Prefix}{(empire ? WarStep.StrikeEmpire : WarStep.StrikeChoir)}", empire ? WarStep.StrikeEmpire : WarStep.StrikeChoir, 2));
        options.Add(new($"{Prefix}{(empire ? WarStep.SabotageEmpire : WarStep.SabotageChoir)}", empire ? WarStep.SabotageEmpire : WarStep.SabotageChoir, 1));
    }

    /// <summary>"none", "waiting" (offered or under way) or "done" (an answer to apply), as <see cref="PowerArc.JobState"/>.</summary>
    public static string JobState(Plot plot, int today)
    {
        if (plot.Fact(PowerArc.Job) is not { Length: > 0 } job)
        {
            return "none";
        }

        var letter = LetterJobs.Contains(job);
        if (letter && plot.Fact(PowerArc.Answer) is null or "" && int.TryParse(plot.Fact(PowerArc.JobDeadline), out var deadline) && today >= deadline)
        {
            plot.SetFact(PowerArc.Answer, PowerArc.Ignored);
        }

        var answer = letter ? plot.Fact(PowerArc.Answer) : QuestTemplate(job) is { } t ? plot.Fact(QuestLink.FactFor(t)) : null;
        return answer is null or "" or "pending" ? "waiting" : "done";
    }

    public static void StartJob(Plot plot, string step, string target, int today = 0)
    {
        plot.SetFact(PowerArc.Job, step);
        plot.SetFact(PowerArc.JobTarget, target);
        plot.SetFact(PowerArc.Answer, "");
        plot.SetFact(PowerArc.JobDeadline, LetterJobs.Contains(step)
            ? (today + PowerArc.LetterTimeoutDays).ToString(CultureInfo.InvariantCulture)
            : "");
        if (QuestTemplate(step) is { } t)
        {
            plot.SetFact(QuestLink.FactFor(t), "");
        }

        plot.SetFact(PlotBook.WaitedOnBeat, "");
    }

    /// <summary>A finished job: <see cref="Destroys"/> names the power a successful final push destroyed;
    /// <see cref="Ending"/> is set when the job reached an ending.</summary>
    public sealed record JobResult(string Job, string Target, bool Success, string? Ending = null, Power? Destroys = null);

    /// <summary>Reads the finished job, moves the front, and clears it.</summary>
    public static JobResult FinishJob(Plot plot, WarContext c)
    {
        var job = plot.Fact(PowerArc.Job) ?? "";
        var target = plot.Fact(PowerArc.JobTarget) ?? "";
        var answer = plot.Fact(PowerArc.Answer) ?? "";
        JobResult result;
        if (LetterJobs.Contains(job))
        {
            var accepted = answer == PowerArc.Accept;
            var ending = (job, accepted) switch
            {
                (WarStep.VictoryOffer, true) => c.SidePower?.ServiceEnding,
                (WarStep.TreatyOffer, true) => EmpireEnding.Independence,
                _ => null,
            };
            result = new JobResult(job, target, accepted, ending);
        }
        else
        {
            var success = QuestTemplate(job) is { } t && plot.Fact(QuestLink.FactFor(t)) == QuestOutcome.Success;
            c.War.Front += success ? 1 : -1;
            result = new JobResult(job, target, success, Destroys: success && job == WarStep.FinalPush ? c.Rival : null);
        }

        if (result.Ending is { } end)
        {
            plot.SetFact(PowerArc.EndingFact, end);
        }

        plot.SetFact(PowerArc.Job, "");
        plot.SetFact(PowerArc.JobTarget, "");
        return result;
    }

    /// <summary>The endings the War in Heaven can reach (spec §7.3).</summary>
    public static IReadOnlyList<string> Endings { get; } =
        [EmpireEnding.Imperial, ChoirEnding.Choral, EmpireEnding.Independence, WarEnding.IndependenceTotal];
}
