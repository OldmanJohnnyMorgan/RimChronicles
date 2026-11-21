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
        PlotBeat.Choice when plot.Fact(SecondRaidDone) == "yes" => NemesisStep.Vanished,
        _ => NemesisStep.SecondRaid,
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
}
