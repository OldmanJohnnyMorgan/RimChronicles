using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Plots;

namespace RimChronicles.Tests.Core;

public class EmpireArcTests
{
    private static readonly PlotTemplate Template = new() { Id = Power.Empire.PlotTemplateId, CountsTowardCap = false };

    private static (PlotBook Book, Plot Plot, PowerSaga Saga) NewPlot()
    {
        var book = new PlotBook();
        var plot = book.Start(Template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 40, 3, "stirring");
        var saga = new PowerSaga();
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        return (book, plot, saga);
    }

    private static IEnumerable<string> Ids(Plot plot, PowerSaga saga, int today = 100) => PowerArc.Transitions(plot, saga, today).Select(o => o.Id);

    [Fact]
    public void Act1_TheEnvoyComes_ThenAct2WaitsForTheAwakening()
    {
        var (book, plot, saga) = NewPlot();
        Assert.Equal(["Act1:ImperialEnvoy"], Ids(plot, saga));

        book.Advance(plot, PlotBeat.Hook, 43, 6, "envoy");
        Assert.Empty(Ids(plot, saga)); // still stirring
        saga.Wake(60, "the Empire");
        Assert.Equal(["Act2:TheEmpireMarches"], Ids(plot, saga));
    }

    [Theory]
    [InlineData(PowerStance.Ally, new[] { "Empire:EscortEnvoy", "Empire:ConquestJob", "Wait" })]
    [InlineData(PowerStance.Oppose, new[] { "Empire:DefendAlly", "Empire:SabotageImperial", "Wait" })]
    [InlineData(PowerStance.Neutral, new[] { "Empire:TributeDemand", "Wait" })]
    [InlineData("", new[] { "Empire:TributeDemand", "Wait" })] // no answer to the envoy: neutral
    public void Act2_OffersJobsThatFitTheStance(string stance, string[] expected)
    {
        var (book, plot, saga) = NewPlot();
        plot.SetFact(PowerArc.Stance, stance);
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        Assert.Equal(expected, Ids(plot, saga));
    }

    [Fact]
    public void Act2_AQuestJobWaitsForItsOutcome_ThenItsAftermath_AndMayWaitAgain()
    {
        var (book, plot, saga) = NewPlot();
        plot.SetFact(PowerArc.Stance, PowerStance.Oppose);
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        PlotBook.Hold(plot, 60, 5, "quiet");
        Assert.DoesNotContain("Wait", Ids(plot, saga));

        PowerArc.StartJob(plot, PowerStep.DefendAlly, "Marrow Hold");
        var link = book.LinkQuest(plot, 7, QuestTemplates.DefendAlly, 66);
        Assert.Empty(Ids(plot, saga));

        book.QuestEnded(link, QuestOutcome.Success, 70);
        Assert.Equal(["Act2:Aftermath:DefendAlly"], Ids(plot, saga));

        var result = PowerArc.FinishJob(plot);
        Assert.Equal(new PowerArc.JobResult(PowerStep.DefendAlly, "Marrow Hold", true, null), result);
        Assert.Equal(1, PowerArc.Count(plot, PowerArc.Wins));
        Assert.Contains("Wait", Ids(plot, saga)); // the job cleared the wait marker
    }

    [Theory]
    [InlineData(PowerArc.TributePaid, true, null)]
    [InlineData(PowerArc.TributeRefused, false, PowerStance.Oppose)]
    [InlineData(PowerArc.TributeFealty, true, PowerStance.Ally)]
    public void Tribute_TheAnswerMayChangeTheStance(string answer, bool success, string? stance)
    {
        var (book, plot, saga) = NewPlot();
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        PowerArc.StartJob(plot, PowerStep.Tribute, "", 70);
        Assert.Empty(Ids(plot, saga, 71));

        plot.SetFact(PowerArc.Answer, answer);
        Assert.Equal(["Act2:Aftermath:Tribute"], Ids(plot, saga, 71));
        var result = PowerArc.FinishJob(plot);
        Assert.Equal((success, stance), (result.Success, result.NewStance));
        Assert.Equal(stance ?? PowerStance.Neutral, PowerArc.StanceOf(plot));
    }

    [Fact]
    public void Tribute_UnansweredByTheDeadline_CountsAsIgnored()
    {
        var (book, plot, saga) = NewPlot();
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        PowerArc.StartJob(plot, PowerStep.Tribute, "", 70);
        Assert.Empty(Ids(plot, saga, 70 + PowerArc.LetterTimeoutDays - 1));
        Assert.Equal(["Act2:Aftermath:Tribute"], Ids(plot, saga, 70 + PowerArc.LetterTimeoutDays));

        var result = PowerArc.FinishJob(plot);
        Assert.False(result.Success);
        Assert.Null(result.NewStance);
    }

    [Fact]
    public void Act3_OpensAfterEnoughExpansionsOrJobs()
    {
        var (book, plot, saga) = NewPlot();
        saga.Wake(60, "the Empire");
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        plot.SetFact(PowerArc.Wins, "2");
        plot.SetFact(PowerArc.Losses, "1");
        Assert.Equal(["Act3:Reckoning"], Ids(plot, saga));

        plot.SetFact(PowerArc.Wins, "0");
        plot.SetFact(PowerArc.Losses, "0");
        for (var i = 0; i < PowerArc.ExpansionsForReckoning; i++)
        {
            saga.Expanded(new ExpansionTarget($"s{i}", $"S{i}", "the Ochre Hand", 10, 3), ExpansionResult.Conquered, 70 + i, 15, "the Empire");
        }

        Assert.Equal(["Act3:Reckoning"], Ids(plot, saga));
        book.Advance(plot, PlotBeat.Choice, 90, 6, "reckoning");
        Assert.Equal(["Act3:FinalDemand", "Wait"], Ids(plot, saga)); // neutral, and the Empire not broken
    }

    [Fact]
    public void ReckoningScore_CountsConquestsAndTheColonysPart()
    {
        var (_, plot, saga) = NewPlot();
        saga.Wake(60, "the Empire");
        saga.Expanded(new ExpansionTarget("a", "A", "the Ochre Hand", 10, 3), ExpansionResult.Conquered, 70, 15, "the Empire");
        saga.RecordAssisted("B", "the Ochre Hand", ExpansionResult.Repelled, 80, "siege broken");
        plot.SetFact(PowerArc.Wins, "2");

        plot.SetFact(PowerArc.Stance, PowerStance.Ally);
        Assert.Equal(2, PowerArc.ReckoningScore(plot, saga));
        plot.SetFact(PowerArc.Stance, PowerStance.Oppose);
        Assert.Equal(-2, PowerArc.ReckoningScore(plot, saga));

        saga.Reckon(false, 90);
        Assert.Equal(PowerState.Broken, saga.State);
        Assert.False(saga.ExpansionDue(500));
    }
}

public class EmpireAct3Tests
{
    private static readonly PlotTemplate Template = new() { Id = Power.Empire.PlotTemplateId, CountsTowardCap = false };

    private static (PlotBook Book, Plot Plot, PowerSaga Saga) InAct3(string stance, bool ascendant)
    {
        var book = new PlotBook();
        var plot = book.Start(Template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 40, 3, "stirring");
        var saga = new PowerSaga();
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        saga.Wake(60, "the Empire");
        saga.Reckon(ascendant, 100);
        plot.SetFact(PowerArc.Stance, stance);
        book.Advance(plot, PlotBeat.Choice, 100, 6, "reckoning");
        return (book, plot, saga);
    }

    private static IEnumerable<string> Ids(Plot plot, PowerSaga saga, int today = 110) => PowerArc.Transitions(plot, saga, today).Select(o => o.Id);

    [Theory]
    [InlineData(PowerStance.Oppose, true, new[] { "Act3:StormTheCapital", "Wait" })]
    [InlineData(PowerStance.Oppose, false, new[] { "Act3:StormTheCapital", "Act3:ForceATreaty", "Wait" })]
    [InlineData(PowerStance.Neutral, true, new[] { "Act3:FinalDemand", "Wait" })]
    [InlineData(PowerStance.Neutral, false, new[] { "Act3:ForceATreaty", "Wait" })]
    [InlineData(PowerStance.Ally, true, new[] { "Empire:EscortEnvoy", "Empire:ConquestJob", "Wait" })]
    public void Act3_OpensThePathsThatFitTheStanceAndTheEmpire(string stance, bool ascendant, string[] expected)
    {
        var (_, plot, saga) = InAct3(stance, ascendant);
        Assert.Equal(expected, Ids(plot, saga));
    }

    [Fact]
    public void Ally_WithAHighTitle_IsOfferedTheGovernorship_AndAcceptingIsTheImperialEnding()
    {
        var (_, plot, saga) = InAct3(PowerStance.Ally, true);
        saga.HighTitleHolder = "Cassia";
        Assert.Equal("Act3:OfferGovernorship", Ids(plot, saga).First());

        PowerArc.StartJob(plot, PowerStep.Governorship, "Cassia", 110);
        Assert.Empty(Ids(plot, saga, 111));
        plot.SetFact(PowerArc.Answer, PowerArc.Accept);
        Assert.Equal(["Act3:Aftermath:Governorship"], Ids(plot, saga, 111));

        Assert.Equal(EmpireEnding.Imperial, PowerArc.FinishJob(plot).Ending);
        Assert.Equal(["Ending:Imperial"], Ids(plot, saga, 112));
    }

    [Fact]
    public void TakingTheCapital_IsTheResistanceEnding_AndFailingCountsAsALoss()
    {
        var (book, plot, saga) = InAct3(PowerStance.Oppose, true);
        PowerArc.StartJob(plot, PowerStep.StormCapital, "Solace", 110);
        var link = book.LinkQuest(plot, 3, QuestTemplates.Sabotage, 110);
        book.QuestEnded(link, QuestOutcome.Fail, 150);
        var failed = PowerArc.FinishJob(plot);
        Assert.Null(failed.Ending);
        Assert.Equal(1, PowerArc.Count(plot, PowerArc.Losses));

        PowerArc.StartJob(plot, PowerStep.StormCapital, "Solace", 160);
        link = book.LinkQuest(plot, 4, QuestTemplates.Sabotage, 160);
        book.QuestEnded(link, QuestOutcome.Success, 190);
        Assert.Equal(EmpireEnding.Resistance, PowerArc.FinishJob(plot).Ending);
        Assert.Equal(["Ending:Resistance"], Ids(plot, saga, 191));
    }

    [Theory]
    [InlineData(PowerStep.Treaty, PowerArc.Accept, EmpireEnding.Independence, null)]
    [InlineData(PowerStep.Treaty, PowerArc.Decline, null, PowerStance.Oppose)]
    [InlineData(PowerStep.Treaty, PowerArc.Ignored, null, null)]
    [InlineData(PowerStep.FinalDemand, PowerArc.Accept, null, PowerStance.Ally)]
    [InlineData(PowerStep.FinalDemand, PowerArc.Decline, null, PowerStance.Oppose)]
    [InlineData(PowerStep.FinalDemand, PowerArc.Ignored, null, PowerStance.Oppose)]
    public void Letters_LeadToAnEndingOrANewStance(string step, string answer, string? ending, string? stance)
    {
        var (_, plot, _) = InAct3(PowerStance.Neutral, false);
        PowerArc.StartJob(plot, step, "", 110);
        plot.SetFact(PowerArc.Answer, answer);
        var result = PowerArc.FinishJob(plot);
        Assert.Equal((ending, stance), (result.Ending, result.NewStance));
    }
}

public class TransitionDescriptionTests
{
    [Fact]
    public void EveryArcOption_HasADescription_ForDirectorV2()
    {
        var book = new PlotBook();
        var nemesis = book.Start(new PlotTemplate { Id = RimChronicles.Core.Plots.Nemesis.NemesisPlot.TemplateId }, ProtagonistSlot.ForPawn("p1", "Mara"), 10, 0, "grudge");
        nemesis.SetFact(RimChronicles.Core.Plots.Nemesis.NemesisPlot.NemesisName, "Vex");
        book.Advance(nemesis, PlotBeat.Hook, 12, 5, "sighting");
        var options = RimChronicles.Core.Plots.Nemesis.NemesisArc.Transitions(nemesis);
        Assert.All(options, o => Assert.False(string.IsNullOrEmpty(o.Desc), o.Id));
        Assert.Equal("Vex leads a raid on the colony", options.Single(o => o.Step == "Raid").Desc);
        Assert.Equal(Director.WaitDesc, options.Single(o => o.Id == Director.WaitId).Desc);

        var saga = new PowerSaga();
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        saga.Wake(60, "the Empire");
        var empire = book.Start(new PlotTemplate { Id = Power.Empire.PlotTemplateId, CountsTowardCap = false }, ProtagonistSlot.ForPawn("colony", "Wyrmfold"), 40, 3, "stirring");
        foreach (var stance in new[] { PowerStance.Ally, PowerStance.Oppose, PowerStance.Neutral })
        {
            empire.SetFact(PowerArc.Stance, stance);
            book.Advance(empire, PlotBeat.Complication, 60, 6, "marches");
            Assert.All(PowerArc.Transitions(empire, saga, 70), o => Assert.False(string.IsNullOrEmpty(o.Desc), o.Id));
        }
    }
}
