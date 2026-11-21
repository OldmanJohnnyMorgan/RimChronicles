using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;

namespace RimChronicles.Tests.Core;

public class NemesisArcTests
{
    private static Plot NewPlot(string grievance = GrudgeKind.Killed, string protagonist = "Mara")
    {
        var plot = new Plot { Template = NemesisPlot.TemplateId, Protagonist = ProtagonistSlot.ForPawn("p1", protagonist) };
        plot.SetFact(NemesisPlot.NemesisName, "Vex");
        plot.SetFact(NemesisPlot.NemesisFaction, "the Venom Gang");
        plot.SetFact(NemesisPlot.VictimName, grievance == GrudgeKind.Maimed ? protagonist : "Tomas");
        plot.SetFact(NemesisPlot.Relation, grievance == GrudgeKind.Maimed ? "" : "husband");
        plot.SetFact(NemesisPlot.Grievance, grievance);
        plot.SetFact(NemesisPlot.Part, grievance == GrudgeKind.Maimed ? "arm" : "");
        plot.SetFact(NemesisArc.Place, "Kelso");
        return plot;
    }

    [Fact]
    public void Steps_FollowTheAgreedBeats()
    {
        var book = new PlotBook();
        var plot = NewPlot();
        book.Active.Add(plot);

        Assert.Equal(NemesisStep.Sighting, NemesisArc.NextStep(plot));
        book.Advance(plot, NemesisArc.BeatAfter(NemesisStep.Sighting), 1, 5, "sighting");
        Assert.Equal(NemesisStep.Raid, NemesisArc.NextStep(plot));
        book.Advance(plot, NemesisArc.BeatAfter(NemesisStep.Raid), 6, 5, "raid");
        Assert.Equal(NemesisStep.Choice, NemesisArc.NextStep(plot));
        book.Advance(plot, NemesisArc.BeatAfter(NemesisStep.Choice), 11, 5, "choice");

        Assert.Equal(NemesisStep.SecondRaid, NemesisArc.NextStep(plot)); // no answer: the colony waits
        plot.SetFact(NemesisArc.Choice, NemesisArc.ChoiceBounty);
        Assert.Equal(NemesisStep.BountyKill, NemesisArc.NextStep(plot));
        plot.SetFact(NemesisArc.Choice, NemesisArc.ChoiceWait);
        Assert.Equal(NemesisStep.SecondRaid, NemesisArc.NextStep(plot));
        plot.SetFact(NemesisArc.SecondRaidDone, "yes");
        Assert.Equal(NemesisStep.Vanished, NemesisArc.NextStep(plot));
    }

    [Theory]
    [InlineData(5000f, 200)]
    [InlineData(48000f, 500)]
    [InlineData(400000f, 1500)]
    public void BountyCost_ScalesWithWealthWithinLimits(float wealth, int expected) =>
        Assert.Equal(expected, NemesisArc.BountyCost(wealth));

    [Fact]
    public void Letters_NameTheDeedFromTheProtagonistsSide()
    {
        var killed = NemesisLetters.Facts.From(NewPlot(), "New Arrivals");
        var maimed = NemesisLetters.Facts.From(NewPlot(GrudgeKind.Maimed, "Ana"), "New Arrivals");

        Assert.Contains("Vex of the Venom Gang, who killed Mara's husband Tomas", NemesisLetters.Sighting(killed).Text);
        Assert.Contains("cost Ana their arm", NemesisLetters.Raid(maimed).Text);
        Assert.Contains("for 300 silver", NemesisLetters.Choice(killed, 300).Text);
        Assert.Equal("The raider Vex of the Venom Gang was seen near Kelso", NemesisLetters.SightingNews(killed));
    }

    [Theory]
    [InlineData(NemesisEnd.KilledByColony, "killed by the people of New Arrivals", "was killed by New Arrivals")]
    [InlineData(NemesisEnd.KilledByBounty, "bounty hunters caught up", "Bounty hunters killed")]
    [InlineData(NemesisEnd.Captured, "now a prisoner of New Arrivals", "is a prisoner of New Arrivals")]
    [InlineData(NemesisEnd.Vanished, "slipped away once more", null)]
    [InlineData(NemesisEnd.FeudFaded, "feud fades", null)]
    public void Endings_HaveALetterAndNewsWhenPublic(NemesisEnd end, string letter, string? news)
    {
        var f = NemesisLetters.Facts.From(NewPlot(), "New Arrivals");

        Assert.Contains(letter, NemesisLetters.Ending(f, end).Text, StringComparison.OrdinalIgnoreCase);
        if (news is null)
        {
            Assert.Null(NemesisLetters.EndingNews(f, end));
        }
        else
        {
            Assert.Contains(news, NemesisLetters.EndingNews(f, end));
        }
    }

    [Fact]
    public void Facts_FallBackWhenThePlotIsMissingSome()
    {
        var f = NemesisLetters.Facts.From(new Plot { Protagonist = ProtagonistSlot.ForPawn("p", "Mara") }, "C");

        Assert.Equal(("the raider", "the wastes"), (f.Nemesis, f.Place));
        Assert.DoesNotContain("  ", NemesisLetters.Sighting(f).Text);
    }
}
