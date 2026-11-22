namespace RimChronicles.Core.Plots.Nemesis;

/// <summary>What the Nemesis arc does when its next beat is due.</summary>
public enum NemesisStep
{
    /// <summary>The hook: a sighting letter, and the sighting becomes public news.</summary>
    Sighting,

    /// <summary>The complication: the nemesis leads a raid on the colony.</summary>
    Raid,

    /// <summary>The choice: a letter offering a bounty or waiting for the nemesis to return.</summary>
    Choice,

    /// <summary>The bounty was posted: hunters catch the nemesis (resolution).</summary>
    BountyKill,

    /// <summary>The colony chose to wait (or didn't answer): the nemesis returns with a second raid.</summary>
    SecondRaid,

    /// <summary>The nemesis survived the second raid too: they vanish for good (resolution).</summary>
    Vanished,

    /// <summary>The colony chose to hunt: a Hunt quest sends them to the nemesis's camp (Stage 4).</summary>
    Hunt,

    /// <summary>Promotion (Stage 5): the nemesis joins the awakened Empire as an officer; the beat stays where it was.</summary>
    ImperialOfficer,
}

/// <summary>How a Nemesis plot ended, which picks the payoff, the letter and the news.</summary>
public enum NemesisEnd
{
    KilledByColony,
    KilledByBounty,
    KilledOtherwise,
    Captured,
    Vanished,
    FeudFaded,
}

/// <summary>The Nemesis arc's rules, as plain decisions over the plot's beat and facts (design spec, Stage 3).</summary>
public static class NemesisArc
{
    public const string Choice = "choice";
    public const string ChoiceBounty = "bounty";
    public const string ChoiceWait = "wait";
    public const string ChoiceHunt = "hunt";

    /// <summary>The plot fact that carries the Hunt quest's outcome.</summary>
    public static readonly string HuntQuestFact = QuestLink.FactFor(QuestTemplates.Hunt);
    public const string SecondRaidDone = "secondRaid";
    public const string Place = "place";
    public const string BountyCostFact = "bountyCost";

    /// <summary>The step to fire now, from the beat the plot is on.</summary>
    public static NemesisStep NextStep(Plot plot) => plot.Beat switch
    {
        PlotBeat.Hook when plot.BeatDay < 0 => NemesisStep.Sighting,
        PlotBeat.Hook => NemesisStep.Raid,
        PlotBeat.Complication => NemesisStep.Choice,
        PlotBeat.Choice when plot.Fact(Choice) == ChoiceBounty => NemesisStep.BountyKill,
        PlotBeat.Choice when plot.Fact(Choice) == ChoiceHunt && plot.Fact(HuntQuestFact) is null => NemesisStep.Hunt,
        PlotBeat.Choice when plot.Fact(SecondRaidDone) == "yes" => NemesisStep.Vanished,
        _ => NemesisStep.SecondRaid, // waited, didn't answer, or the hunt failed or was ignored
    };

    /// <summary>The beat a step moves the plot to.</summary>
    public static PlotBeat BeatAfter(NemesisStep step) => step switch
    {
        NemesisStep.Sighting => PlotBeat.Hook,
        NemesisStep.Raid => PlotBeat.Complication,
        _ => PlotBeat.Choice,
    };

    /// <summary>The bounty's price in silver: a hundredth of colony wealth, between 200 and 1,500. Never chosen by the model.</summary>
    public static int BountyCost(float colonyWealth) => (int)Math.Clamp(Math.Round(colonyWealth / 100f / 50f) * 50f, 200f, 1500f);

    /// <summary>Days the bounty hunters take after the bounty is posted.</summary>
    public const int MinBountyDays = 3;

    public const int MaxBountyDays = 8;

    /// <summary>Days the choice letter stays open; unanswered, the colony waits.</summary>
    public const int ChoiceTimeoutDays = 5;

    public const string Sightings = "sightings";

    /// <summary>The most sightings before the nemesis must act.</summary>
    public const int MaxSightings = 2;

    /// <summary>What the director may choose from now. One option fires without asking (the first sighting, and the
    /// consequences of the colony's answer); otherwise the director picks, with "Wait" for a quiet stretch at most once
    /// per beat.</summary>
    public static IReadOnlyList<PlotTransition> Transitions(Plot plot)
    {
        var name = Compact(plot.Fact(NemesisPlot.NemesisName) ?? "Nemesis");
        var options = new List<PlotTransition>();
        switch (plot.Beat)
        {
            case PlotBeat.Hook when plot.BeatDay < 0:
                return [new($"Hook:{name}Sighted", nameof(NemesisStep.Sighting), 1)];
            case PlotBeat.Hook:
                options.Add(new($"Complication:{name}LeadsARaid", nameof(NemesisStep.Raid), 3));
                var seen = int.TryParse(plot.Fact(Sightings), out var n) ? n : 1; // the hook was the first sighting
                if (seen < MaxSightings)
                {
                    options.Add(new($"Hook:{name}SightedAgain", nameof(NemesisStep.Sighting), 1));
                }

                break;
            case PlotBeat.Complication:
                options.Add(new($"Choice:BountyOrWaitFor{name}", nameof(NemesisStep.Choice), 3));
                break;
            default:
                if (HuntUnderWay(plot))
                {
                    return []; // the Hunt quest is offered or under way: its outcome decides
                }

                var step = NextStep(plot);
                return [new($"{BeatAfter(step)}:{step}", step.ToString(), 1)];
        }

        if (Empire.Promotion.NemesisCanJoin(plot))
        {
            options.Add(new($"Promote:{name}JoinsTheEmpire", nameof(NemesisStep.ImperialOfficer), 2));
        }

        if (!PlotBook.HasWaitedThisBeat(plot))
        {
            options.Add(new(Director.WaitId, Director.WaitId, 1));
        }

        return options;
    }

    /// <summary>True while the colony's Hunt quest for the nemesis is offered or under way.</summary>
    public static bool HuntUnderWay(Plot plot) => plot.Fact(HuntQuestFact) == "pending";

    /// <summary>Days the plot waits on a Hunt quest before deciding again; its outcome makes it due sooner.</summary>
    public const int HuntWaitDays = QuestTemplates.SiteDays + 5;

    /// <summary>"Kael Dunmore" → "KaelDunmore", "'Hatchet' Imre" → "HatchetImre": a name fit for a transition id.</summary>
    public static string Compact(string name) => new(name.Where(char.IsLetterOrDigit).ToArray());
}
