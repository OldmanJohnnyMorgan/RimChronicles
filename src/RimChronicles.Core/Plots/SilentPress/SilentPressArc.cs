namespace RimChronicles.Core.Plots.SilentPress;

/// <summary>What the Silent Press arc does when its next beat is due.</summary>
public enum SilentPressStep
{
    /// <summary>A trader mentions the Dispatch isn't sending anyone the colony's way.</summary>
    TraderClue,

    /// <summary>A visitor repeats a rumour that the colony murdered a journalist.</summary>
    VisitorClue,

    /// <summary>The outlet's envoy demands reparations: pay, haggle or refuse.</summary>
    Envoy,

    /// <summary>The colony paid (or haggled): a new reporter is assigned, cold at first (resolution).</summary>
    Reinstated,

    /// <summary>The colony refused: the blackout holds, goodwill drops, and settling stays open.</summary>
    Refused,

    /// <summary>A later offer to settle after a refusal.</summary>
    Reoffer,
}

/// <summary>The Silent Press arc's rules (design spec, Stage 2 "When the reporter dies"), as plain decisions over the
/// plot's beat and facts. It starts when the colony silences the reporter and doesn't count toward the plot cap.</summary>
public static class SilentPressArc
{
    public const string TemplateId = "SilentPress";

    public const string ReporterName = "reporterName";
    public const string Outlet = "outlet";
    public const string ReporterFaction = "reporterFaction";
    public const string Cause = "cause";
    public const string Clues = "clues";
    public const string Answer = "answer";
    public const string Cost = "cost";

    public const string AnswerPay = "pay";
    public const string AnswerHaggle = "haggle";
    public const string AnswerRefuse = "refuse";

    /// <summary>Haggling pays this share of the price.</summary>
    public const double HaggleShare = 0.6;

    public const int EnvoyTimeoutDays = 7;
    public const int ReofferDays = 30;

    /// <summary>Cold bulletins from the new reporter after paying in full, and after haggling.</summary>
    public const int ColdBulletinsPaid = 2;
    public const int ColdBulletinsHaggled = 3;

    public const int GoodwillLossRefused = 20;
    public const int GoodwillLossHaggled = 5;

    public static SilentPressStep NextStep(Plot plot)
    {
        var answer = plot.Fact(Answer) ?? "";
        return plot.Beat switch
        {
            PlotBeat.Hook => SilentPressStep.TraderClue,
            PlotBeat.Complication when plot.Fact(Clues) == "1" => SilentPressStep.VisitorClue,
            PlotBeat.Complication => SilentPressStep.Envoy,
            PlotBeat.Choice when answer is AnswerPay or AnswerHaggle => SilentPressStep.Reinstated,
            PlotBeat.Choice when plot.Fact("refused") == "yes" => SilentPressStep.Reoffer,
            _ => SilentPressStep.Refused, // refused, or never answered
        };
    }

    /// <summary>The reparations: a fiftieth of colony wealth, rounded to 50, between 300 and 3,000 silver.</summary>
    public static int Price(float colonyWealth) => (int)Math.Clamp(Math.Round(colonyWealth / 50f / 50f) * 50f, 300f, 3000f);

    public static int HagglePrice(int price) => (int)(Math.Round(price * HaggleShare / 10) * 10);
}

/// <summary>Template letters for Silent Press. The silence itself has no letter: the player has to notice it.</summary>
public static class SilentPressLetters
{
    public sealed record Facts(string Reporter, string Outlet, string Faction, string Colony)
    {
        public static Facts From(Plot plot, string colony) => new(
            plot.Fact(SilentPressArc.ReporterName) ?? "the correspondent",
            plot.Fact(SilentPressArc.Outlet) ?? "the Rim Dispatch",
            plot.Fact(SilentPressArc.ReporterFaction) ?? "",
            colony);

        public string OutletCap => Outlet.Length == 0 ? Outlet : char.ToUpperInvariant(Outlet[0]) + Outlet[1..];
    }

    public static (string Label, string Text) TraderClue(Facts f) => (
        "A trader's remark",
        $"A passing trader, haggling over the price of rice, mentions that {f.Outlet} isn't sending anyone out to {f.Colony} "
        + "anymore. \"Funny,\" the trader says. \"Used to be you couldn't keep them off the roads.\"\n\nThe trader doesn't say more, "
        + "and nobody asks.");

    public static (string Label, string Text) VisitorClue(Facts f) => (
        "A rumour on the road",
        $"A visitor repeats a rumour heard at a settlement down the road: that {f.Colony} murdered a journalist. "
        + $"The visitor laughs it off, but watches the colonists a little too closely afterwards.\n\nSomeone, somewhere, "
        + $"is asking what happened to {f.Reporter}.");

    public static (string Label, string Text) Envoy(Facts f, int price, int haggle) => (
        $"{f.OutletCap}: an envoy",
        $"An envoy from {f.Outlet} has come with a message. {f.Reporter} went to {f.Colony} and never came back, and "
        + $"{f.Outlet} knows it.\n\nThey want reparations: {price} silver. Pay, and they will send someone new. "
        + $"Haggle, and they may take {haggle}, with a long memory. Refuse, and {f.Colony} hears no more news from them.");

    public static (string Label, string Text) Reinstated(Facts f, bool haggled) => (
        $"{f.OutletCap}: reparations accepted",
        $"{f.OutletCap} has accepted {(haggled ? "the haggled sum, without warmth" : "the reparations")}. A new correspondent "
        + $"will be assigned to {f.Colony}.\n\nThey will be polite. They will not be friendly.");

    public static (string Label, string Text) Refused(Facts f, int goodwillLoss) => (
        $"{f.OutletCap}: silence",
        $"{f.Colony} refused. The envoy leaves without another word, and {f.Outlet} will send no more news this way."
        + (f.Faction.Length > 0 ? $"\n\n{f.Faction} has heard about it too, and thinks less of {f.Colony} ({-goodwillLoss} goodwill)." : ""));

    public static (string Label, string Text) Reoffer(Facts f, int price) => (
        $"{f.OutletCap}: an offer stands",
        $"Word comes that {f.Outlet} would still accept reparations for {f.Reporter}: {price} silver, and the news would "
        + "start again.\n\nThe offer comes without warmth. It comes all the same.");

    public const string PayOption = "Pay {0} silver";
    public const string HaggleOption = "Haggle: offer {0} silver";
    public const string RefuseOption = "Refuse";

    /// <summary>The persona of a new reporter's first, cold bulletins.</summary>
    public static string ColdPersona(string colony) =>
        $"cold, clipped and guarded about {colony}: polite, never friendly, and you never joke about the colony";
}
