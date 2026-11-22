using RimChronicles.Core.Empire;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Plots.Nemesis;

namespace RimChronicles.Tests.Core;

public class PromotionTests
{
    private static (PlotBook Book, Plot Plot) Nemesis()
    {
        var book = new PlotBook();
        var plot = book.Start(new PlotTemplate { Id = NemesisPlot.TemplateId }, ProtagonistSlot.ForPawn("p1", "Anouk"), 10, 0, "grudge");
        plot.SetFact(NemesisPlot.NemesisName, "Kael Dunmore");
        plot.SetFact(NemesisPlot.NemesisFaction, "the Ochre Hand");
        return (book, plot);
    }

    [Fact]
    public void Nemesis_TheDirectorMayPromoteThem_OnlyAfterTheSightingAndWhileTheEmpireIsOpen()
    {
        var (book, plot) = Nemesis();
        plot.SetFact(Promotion.EmpireOpen, "yes");
        plot.SetFact(Promotion.EmpireName, "the Sun Throne");
        Assert.DoesNotContain(NemesisArc.Transitions(plot), o => o.Step == nameof(NemesisStep.ImperialOfficer)); // no sighting yet

        book.Advance(plot, PlotBeat.Hook, 12, 5, "sighting");
        Assert.Contains(NemesisArc.Transitions(plot), o => o.Id == "Promote:KaelDunmoreJoinsTheEmpire");

        plot.SetFact(Promotion.EmpireOpen, "no");
        Assert.DoesNotContain(NemesisArc.Transitions(plot), o => o.Step == nameof(NemesisStep.ImperialOfficer));

        plot.SetFact(Promotion.EmpireOpen, "yes");
        book.Advance(plot, PlotBeat.Complication, 20, 5, "raid");
        Assert.Contains(NemesisArc.Transitions(plot), o => o.Step == nameof(NemesisStep.ImperialOfficer));

        plot.SetFact(Promotion.Fact, Promotion.ImperialOfficer);
        Assert.DoesNotContain(NemesisArc.Transitions(plot), o => o.Step == nameof(NemesisStep.ImperialOfficer)); // once only

        book.Advance(plot, PlotBeat.Choice, 25, 5, "choice");
        plot.SetFact(Promotion.Fact, "");
        Assert.False(Promotion.NemesisCanJoin(plot)); // too late
    }

    [Fact]
    public void Nemesis_AlreadyInTheEmpire_IsNotPromoted()
    {
        var (book, plot) = Nemesis();
        book.Advance(plot, PlotBeat.Hook, 12, 5, "sighting");
        plot.SetFact(Promotion.EmpireOpen, "yes");
        plot.SetFact(Promotion.EmpireName, "the Ochre Hand");
        Assert.False(Promotion.NemesisCanJoin(plot));
    }

    [Fact]
    public void LostFamily_TheRelativeIsCaught_WhenTheirFactionLosesASettlement_BeforeTheyAreTaken()
    {
        var book = new PlotBook();
        var plot = book.Start(new PlotTemplate { Id = LostFamilyArc.TemplateId }, ProtagonistSlot.ForPawn("p1", "Anouk"), 10, 0, "kin");
        plot.SetFact(LostFamilyArc.KinFaction, "the Ochre Hand");
        Assert.False(Promotion.KinCaughtBy(plot, "the Ochre Hand")); // no letter yet

        book.Advance(plot, PlotBeat.Hook, 12, 5, "letter");
        Assert.False(Promotion.KinCaughtBy(plot, "the Starling Tribe"));
        Assert.True(Promotion.KinCaughtBy(plot, "the Ochre Hand"));

        plot.SetFact(LostFamilyArc.Captor, "the Sun Throne");
        Assert.False(Promotion.KinCaughtBy(plot, "the Ochre Hand"));

        // Once caught, the arc carries on to the rescue as usual, with the Empire as captor.
        book.Advance(plot, PlotBeat.Complication, 30, 5, "caught");
        Assert.Equal(["Choice:RescueKin", "Wait"], LostFamilyArc.Transitions(plot).Select(o => o.Id));
    }
}
