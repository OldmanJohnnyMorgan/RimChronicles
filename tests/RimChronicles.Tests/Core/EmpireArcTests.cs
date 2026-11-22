using RimChronicles.Core.Empire;
using RimChronicles.Core.Plots;

namespace RimChronicles.Tests.Core;

public class EmpireArcTests
{
    private static readonly PlotTemplate Template = new() { Id = EmpireArc.TemplateId, CountsTowardCap = false };

    private static (PlotBook Book, Plot Plot, EmpireSaga Saga) NewPlot()
    {
        var book = new PlotBook();
        var plot = book.Start(Template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 40, 3, "stirring");
        var saga = new EmpireSaga();
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        return (book, plot, saga);
    }

    private static IEnumerable<string> Ids(Plot plot, EmpireSaga saga, int today = 100) => EmpireArc.Transitions(plot, saga, today).Select(o => o.Id);

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
    [InlineData(EmpireStance.Ally, new[] { "Empire:EscortEnvoy", "Empire:ConquestJob", "Wait" })]
    [InlineData(EmpireStance.Oppose, new[] { "Empire:DefendAlly", "Empire:SabotageImperial", "Wait" })]
    [InlineData(EmpireStance.Neutral, new[] { "Empire:TributeDemand", "Wait" })]
    [InlineData("", new[] { "Empire:TributeDemand", "Wait" })] // no answer to the envoy: neutral
    public void Act2_OffersJobsThatFitTheStance(string stance, string[] expected)
    {
        var (book, plot, saga) = NewPlot();
        plot.SetFact(EmpireArc.Stance, stance);
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        Assert.Equal(expected, Ids(plot, saga));
    }

    [Fact]
    public void Act2_AQuestJobWaitsForItsOutcome_ThenItsAftermath_AndMayWaitAgain()
    {
        var (book, plot, saga) = NewPlot();
        plot.SetFact(EmpireArc.Stance, EmpireStance.Oppose);
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        PlotBook.Hold(plot, 60, 5, "quiet");
        Assert.DoesNotContain("Wait", Ids(plot, saga));

        EmpireArc.StartJob(plot, EmpireStep.DefendAlly, "Marrow Hold");
        var link = book.LinkQuest(plot, 7, QuestTemplates.DefendAlly, 66);
        Assert.Empty(Ids(plot, saga));

        book.QuestEnded(link, QuestOutcome.Success, 70);
        Assert.Equal(["Act2:Aftermath:DefendAlly"], Ids(plot, saga));

        var result = EmpireArc.FinishJob(plot);
        Assert.Equal(new EmpireArc.JobResult(EmpireStep.DefendAlly, "Marrow Hold", true, null), result);
        Assert.Equal(1, EmpireArc.Count(plot, EmpireArc.Wins));
        Assert.Contains("Wait", Ids(plot, saga)); // the job cleared the wait marker
    }

    [Theory]
    [InlineData(EmpireArc.TributePaid, true, null)]
    [InlineData(EmpireArc.TributeRefused, false, EmpireStance.Oppose)]
    [InlineData(EmpireArc.TributeFealty, true, EmpireStance.Ally)]
    public void Tribute_TheAnswerMayChangeTheStance(string answer, bool success, string? stance)
    {
        var (book, plot, saga) = NewPlot();
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        EmpireArc.StartJob(plot, EmpireStep.Tribute, "", 70);
        Assert.Empty(Ids(plot, saga, 71));

        plot.SetFact(EmpireArc.Answer, answer);
        Assert.Equal(["Act2:Aftermath:Tribute"], Ids(plot, saga, 71));
        var result = EmpireArc.FinishJob(plot);
        Assert.Equal((success, stance), (result.Success, result.NewStance));
        Assert.Equal(stance ?? EmpireStance.Neutral, EmpireArc.StanceOf(plot));
    }

    [Fact]
    public void Tribute_UnansweredByTheDeadline_CountsAsIgnored()
    {
        var (book, plot, saga) = NewPlot();
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        EmpireArc.StartJob(plot, EmpireStep.Tribute, "", 70);
        Assert.Empty(Ids(plot, saga, 70 + EmpireArc.LetterTimeoutDays - 1));
        Assert.Equal(["Act2:Aftermath:Tribute"], Ids(plot, saga, 70 + EmpireArc.LetterTimeoutDays));

        var result = EmpireArc.FinishJob(plot);
        Assert.False(result.Success);
        Assert.Null(result.NewStance);
    }

    [Fact]
    public void Act3_OpensAfterEnoughExpansionsOrJobs()
    {
        var (book, plot, saga) = NewPlot();
        saga.Wake(60, "the Empire");
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        plot.SetFact(EmpireArc.Wins, "2");
        plot.SetFact(EmpireArc.Losses, "1");
        Assert.Equal(["Act3:Reckoning"], Ids(plot, saga));

        plot.SetFact(EmpireArc.Wins, "0");
        plot.SetFact(EmpireArc.Losses, "0");
        for (var i = 0; i < EmpireArc.ExpansionsForReckoning; i++)
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
        plot.SetFact(EmpireArc.Wins, "2");

        plot.SetFact(EmpireArc.Stance, EmpireStance.Ally);
        Assert.Equal(2, EmpireArc.ReckoningScore(plot, saga));
        plot.SetFact(EmpireArc.Stance, EmpireStance.Oppose);
        Assert.Equal(-2, EmpireArc.ReckoningScore(plot, saga));

        saga.Reckon(false, 90);
        Assert.Equal(EmpireState.Broken, saga.State);
        Assert.False(saga.ExpansionDue(500));
    }
}

public class EmpireAct3Tests
{
    private static readonly PlotTemplate Template = new() { Id = EmpireArc.TemplateId, CountsTowardCap = false };

    private static (PlotBook Book, Plot Plot, EmpireSaga Saga) InAct3(string stance, bool ascendant)
    {
        var book = new PlotBook();
        var plot = book.Start(Template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 40, 3, "stirring");
        var saga = new EmpireSaga();
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        saga.Wake(60, "the Empire");
        saga.Reckon(ascendant, 100);
        plot.SetFact(EmpireArc.Stance, stance);
        book.Advance(plot, PlotBeat.Choice, 100, 6, "reckoning");
        return (book, plot, saga);
    }

    private static IEnumerable<string> Ids(Plot plot, EmpireSaga saga, int today = 110) => EmpireArc.Transitions(plot, saga, today).Select(o => o.Id);

    [Theory]
    [InlineData(EmpireStance.Oppose, true, new[] { "Act3:StormTheCapital", "Wait" })]
    [InlineData(EmpireStance.Oppose, false, new[] { "Act3:StormTheCapital", "Act3:ForceATreaty", "Wait" })]
    [InlineData(EmpireStance.Neutral, true, new[] { "Act3:FinalDemand", "Wait" })]
    [InlineData(EmpireStance.Neutral, false, new[] { "Act3:ForceATreaty", "Wait" })]
    [InlineData(EmpireStance.Ally, true, new[] { "Empire:EscortEnvoy", "Empire:ConquestJob", "Wait" })]
    public void Act3_OpensThePathsThatFitTheStanceAndTheEmpire(string stance, bool ascendant, string[] expected)
    {
        var (_, plot, saga) = InAct3(stance, ascendant);
        Assert.Equal(expected, Ids(plot, saga));
    }

    [Fact]
    public void Ally_WithAHighTitle_IsOfferedTheGovernorship_AndAcceptingIsTheImperialEnding()
    {
        var (_, plot, saga) = InAct3(EmpireStance.Ally, true);
        saga.HighTitleHolder = "Cassia";
        Assert.Equal("Act3:OfferGovernorship", Ids(plot, saga).First());

        EmpireArc.StartJob(plot, EmpireStep.Governorship, "Cassia", 110);
        Assert.Empty(Ids(plot, saga, 111));
        plot.SetFact(EmpireArc.Answer, EmpireArc.Accept);
        Assert.Equal(["Act3:Aftermath:Governorship"], Ids(plot, saga, 111));

        Assert.Equal(EmpireEnding.Imperial, EmpireArc.FinishJob(plot).Ending);
        Assert.Equal(["Ending:Imperial"], Ids(plot, saga, 112));
    }

    [Fact]
    public void TakingTheCapital_IsTheResistanceEnding_AndFailingCountsAsALoss()
    {
        var (book, plot, saga) = InAct3(EmpireStance.Oppose, true);
        EmpireArc.StartJob(plot, EmpireStep.StormCapital, "Solace", 110);
        var link = book.LinkQuest(plot, 3, QuestTemplates.Sabotage, 110);
        book.QuestEnded(link, QuestOutcome.Fail, 150);
        var failed = EmpireArc.FinishJob(plot);
        Assert.Null(failed.Ending);
        Assert.Equal(1, EmpireArc.Count(plot, EmpireArc.Losses));

        EmpireArc.StartJob(plot, EmpireStep.StormCapital, "Solace", 160);
        link = book.LinkQuest(plot, 4, QuestTemplates.Sabotage, 160);
        book.QuestEnded(link, QuestOutcome.Success, 190);
        Assert.Equal(EmpireEnding.Resistance, EmpireArc.FinishJob(plot).Ending);
        Assert.Equal(["Ending:Resistance"], Ids(plot, saga, 191));
    }

    [Theory]
    [InlineData(EmpireStep.Treaty, EmpireArc.Accept, EmpireEnding.Independence, null)]
    [InlineData(EmpireStep.Treaty, EmpireArc.Decline, null, EmpireStance.Oppose)]
    [InlineData(EmpireStep.Treaty, EmpireArc.Ignored, null, null)]
    [InlineData(EmpireStep.FinalDemand, EmpireArc.Accept, null, EmpireStance.Ally)]
    [InlineData(EmpireStep.FinalDemand, EmpireArc.Decline, null, EmpireStance.Oppose)]
    [InlineData(EmpireStep.FinalDemand, EmpireArc.Ignored, null, EmpireStance.Oppose)]
    public void Letters_LeadToAnEndingOrANewStance(string step, string answer, string? ending, string? stance)
    {
        var (_, plot, _) = InAct3(EmpireStance.Neutral, false);
        EmpireArc.StartJob(plot, step, "", 110);
        plot.SetFact(EmpireArc.Answer, answer);
        var result = EmpireArc.FinishJob(plot);
        Assert.Equal((ending, stance), (result.Ending, result.NewStance));
    }
}
