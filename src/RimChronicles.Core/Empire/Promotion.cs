using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Plots.Nemesis;

namespace RimChronicles.Core.Empire;

/// <summary>
/// Minor plots promoted into the major one (design spec, Stage 5, "Links to minor plots"): a nemesis comes back as an
/// Imperial officer (a director option), and a Lost Family relative is caught in a settlement the Empire conquers.
/// Plain rules over plot facts; the game sets <see cref="EmpireOpen"/> daily.
/// </summary>
public static class Promotion
{
    /// <summary>How the plot was promoted: <see cref="ImperialOfficer"/> or <see cref="Conquered"/>; absent if not.</summary>
    public const string Fact = "promoted";

    public const string ImperialOfficer = "imperialOfficer";
    public const string Conquered = "conquered";

    /// <summary>Set daily on Nemesis plots: "yes" while the Empire is awake, at war with the colony and the story not over.</summary>
    public const string EmpireOpen = "empireOpen";

    /// <summary>The settlement whose fall caught the relative.</summary>
    public const string ConqueredAt = "conqueredAt";

    public static bool IsPromoted(Plot plot) => plot.Fact(Fact) is { Length: > 0 };

    /// <summary>A nemesis may join the Empire between the sighting and the choice, once, while the Empire is open.</summary>
    public static bool NemesisCanJoin(Plot plot) =>
        plot.Template == NemesisPlot.TemplateId && plot.BeatDay >= 0 && plot.Beat is PlotBeat.Hook or PlotBeat.Complication
        && !IsPromoted(plot) && plot.Fact(EmpireOpen) == "yes" && plot.Fact(NemesisPlot.NemesisFaction) != plot.Fact(EmpireName);

    /// <summary>The Empire's name, set with <see cref="EmpireOpen"/>, so a nemesis already in the Empire isn't promoted.</summary>
    public const string EmpireName = "empireName";

    /// <summary>True if the fall of a settlement of <paramref name="faction"/> catches this Lost Family relative: the
    /// plot has sent its letters (Hook) and the relative isn't yet taken.</summary>
    public static bool KinCaughtBy(Plot plot, string faction) =>
        plot.Template == LostFamilyArc.TemplateId && plot.Beat == PlotBeat.Hook && plot.BeatDay >= 0 && !IsPromoted(plot)
        && plot.Fact(LostFamilyArc.KinFaction) == faction && plot.Fact(LostFamilyArc.Captor) is null or "";

    public static (string Label, string Text) OfficerLetter(string nemesis, string oldFaction, string empire, string protagonist) => (
        $"{nemesis} in Imperial colours",
        $"Word comes from the trade roads: {nemesis}, once of {oldFaction}, now rides with {empire} as an officer of its columns.\n\n"
        + $"{protagonist} has not forgotten {nemesis}. Now the Empire stands behind {nemesis} too.");

    public static string OfficerNews(string nemesis, string oldFaction, string empire) => $"{nemesis}, once of {oldFaction}, now serves {empire} as an officer";

    public static (string Label, string Text) CaughtLetter(string kin, string settlement, string empire, string protagonist) => (
        $"Lost Family: {kin} caught in the war",
        $"{settlement} has fallen to {empire}, and {kin} was there when it fell. The Empire holds its captives at a camp not far away.\n\n"
        + $"{protagonist} has not slept since the news came.");
}
