using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.SilentPress;
using RimChronicles.Core.Snapshots;

namespace RimChronicles.Tests.Core;

public class SilentPressTests
{
    private static Plot NewPlot()
    {
        var plot = new Plot { Template = SilentPressArc.TemplateId, Beat = PlotBeat.Hook, BeatDay = 10 };
        plot.SetFact(SilentPressArc.ReporterName, "Kael Dunmore");
        plot.SetFact(SilentPressArc.Outlet, "the Rim Dispatch");
        plot.SetFact(SilentPressArc.ReporterFaction, "Tola");
        return plot;
    }

    [Fact]
    public void Steps_GoFromCluesToTheEnvoyToAnAnswer()
    {
        var plot = NewPlot();
        Assert.Equal(SilentPressStep.TraderClue, SilentPressArc.NextStep(plot));

        plot.Beat = PlotBeat.Complication;
        plot.SetFact(SilentPressArc.Clues, "1");
        Assert.Equal(SilentPressStep.VisitorClue, SilentPressArc.NextStep(plot));
        plot.SetFact(SilentPressArc.Clues, "2");
        Assert.Equal(SilentPressStep.Envoy, SilentPressArc.NextStep(plot));

        plot.Beat = PlotBeat.Choice;
        Assert.Equal(SilentPressStep.Refused, SilentPressArc.NextStep(plot)); // unanswered counts as a refusal
        plot.SetFact(SilentPressArc.Answer, SilentPressArc.AnswerHaggle);
        Assert.Equal(SilentPressStep.Reinstated, SilentPressArc.NextStep(plot));
        plot.SetFact(SilentPressArc.Answer, SilentPressArc.AnswerRefuse);
        plot.SetFact("refused", "yes");
        Assert.Equal(SilentPressStep.Reoffer, SilentPressArc.NextStep(plot));
        plot.SetFact(SilentPressArc.Answer, SilentPressArc.AnswerPay);
        Assert.Equal(SilentPressStep.Reinstated, SilentPressArc.NextStep(plot));
    }

    [Theory]
    [InlineData(5000f, 300)]
    [InlineData(48000f, 950)]
    [InlineData(900000f, 3000)]
    public void Price_ScalesWithWealthWithinLimits(float wealth, int price) => Assert.Equal(price, SilentPressArc.Price(wealth));

    [Fact]
    public void Haggling_PaysSixtyPercent() => Assert.Equal(570, SilentPressArc.HagglePrice(950));

    [Fact]
    public void Reinstate_BringsACoolReporterWhoWarmsUpAfterAFewBulletins()
    {
        var desk = new NewsDesk();
        desk.NewReporter("Kael Dunmore", "Tola", 0, 20);
        desk.Plan(0, 3, false);
        desk.Silence(5);

        desk.Reinstate(today: 30, inDays: 2, coldBulletins: 2, coldPersona: SilentPressLetters.ColdPersona("New Arrivals"));
        Assert.True(desk.ReplacementDue(32));
        desk.NewReporter("Mira Holt", "Tola", 32, 20);
        Assert.Contains("never joke about the colony", desk.Reporter.ToInfo().Persona);

        for (var day = 32; desk.Reporter.ColdBulletinsLeft > 0; day += 3)
        {
            desk.Plan(day, 3, false);
            desk.SetPending(new Bulletin { Text = "Cold." });
            desk.Deliver(day, 3);
        }

        Assert.Equal(WorldSummarizerDefaults(), desk.Reporter.ToInfo().Persona);
    }

    [Fact]
    public void Reinstate_OnlyAfterASilencing()
    {
        var desk = new NewsDesk();
        desk.Reinstate(1, 1, 2, "cold");

        Assert.Equal(ReporterStatus.Active, desk.Reporter.Status);
    }

    [Fact]
    public void Letters_KeepTheSilenceUnexplainedUntilTheEnvoy()
    {
        var f = SilentPressLetters.Facts.From(NewPlot(), "New Arrivals");

        Assert.Contains("isn't sending anyone out to New Arrivals anymore", SilentPressLetters.TraderClue(f).Text);
        Assert.DoesNotContain("Kael", SilentPressLetters.TraderClue(f).Text);
        Assert.Contains("murdered a journalist", SilentPressLetters.VisitorClue(f).Text);
        var envoy = SilentPressLetters.Envoy(f, 950, 570).Text;
        Assert.Contains("950 silver", envoy);
        Assert.Contains("570", envoy);
        Assert.Contains("-20 goodwill", SilentPressLetters.Refused(f, 20).Text);
        Assert.Equal("The Rim Dispatch: an envoy", SilentPressLetters.Envoy(f, 1, 1).Label);
    }

    private static string WorldSummarizerDefaults() => RimChronicles.Core.Summaries.WorldSummarizer.DefaultReporter.Persona;
}
