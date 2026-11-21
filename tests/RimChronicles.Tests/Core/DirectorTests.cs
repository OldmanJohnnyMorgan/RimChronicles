using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Core.Snapshots;
using RimChronicles.Llm;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class DirectorTests
{
    private static readonly PlotTemplate Template = new() { Id = NemesisPlot.TemplateId };

    private static Plot NemesisPlotNamed(string name)
    {
        var book = new PlotBook();
        var plot = book.Start(Template, ProtagonistSlot.ForPawn("p1", "Mara"), 0, 0, "seed");
        plot.SetFact(NemesisPlot.NemesisName, name);
        return plot;
    }

    [Fact]
    public void PickWeighted_FollowsTheWeights()
    {
        PlotTransition[] options = [new("A", "A", 3), new("B", "B", 1)];

        Assert.Equal("A", Director.PickWeighted(options, 0.0).Id);
        Assert.Equal("A", Director.PickWeighted(options, 0.74).Id);
        Assert.Equal("B", Director.PickWeighted(options, 0.76).Id);
        Assert.Equal("B", Director.PickWeighted(options, 1.0).Id);
        Assert.Equal("A", Director.PickWeighted([new("A", "A", 0), new("B", "B", 0)], 0.9).Id);
    }

    [Fact]
    public void Nemesis_FirstSightingNeedsNoDecision()
    {
        var options = NemesisArc.Transitions(NemesisPlotNamed("'Hatchet' Imre"));

        Assert.Equal("Hook:HatchetImreSighted", Assert.Single(options).Id);
    }

    [Fact]
    public void Nemesis_AfterTheSighting_OffersARaidAnotherSightingOrAWait()
    {
        var plot = NemesisPlotNamed("Vex");
        new PlotBook().Advance(plot, PlotBeat.Hook, 3, 5, "sighted");
        plot.SetFact(NemesisArc.Sightings, "1");

        Assert.Equal(["Complication:VexLeadsARaid", "Hook:VexSightedAgain", "Wait"], NemesisArc.Transitions(plot).Select(o => o.Id));

        PlotBook.Hold(plot, 8, 5, "a quiet stretch");
        plot.SetFact(NemesisArc.Sightings, "2");
        Assert.Equal(["Complication:VexLeadsARaid"], NemesisArc.Transitions(plot).Select(o => o.Id));
    }

    [Fact]
    public void Nemesis_AfterTheRaid_OffersTheChoiceOrAWait_ThenFollowsTheAnswer()
    {
        var plot = NemesisPlotNamed("Vex");
        var book = new PlotBook();
        book.Advance(plot, PlotBeat.Complication, 3, 5, "raid");

        Assert.Equal(["Choice:BountyOrWaitForVex", "Wait"], NemesisArc.Transitions(plot).Select(o => o.Id));

        book.Advance(plot, PlotBeat.Choice, 9, 5, "choice");
        plot.SetFact(NemesisArc.Choice, NemesisArc.ChoiceBounty);
        var forced = Assert.Single(NemesisArc.Transitions(plot));
        Assert.Equal(nameof(NemesisStep.BountyKill), forced.Step);
    }

    [Fact]
    public void Hold_DelaysThePlotWithoutFiringABeat()
    {
        var book = new PlotBook();
        var plot = NemesisPlotNamed("Vex");

        PlotBook.Hold(plot, 10, 6, "the colony needs a pause");

        Assert.Equal((16, -1), (plot.NextBeatDay, book.LastBeatDay));
        Assert.True(PlotBook.HasWaitedThisBeat(plot));
        Assert.EndsWith("waited: the colony needs a pause", plot.Log[^1]);
    }

    private static WorldSnapshot Snapshot(params string[] options) => SnapshotJson.Load(Repo.Path("harness", "snapshots", "snap-07-mid-strained.json"))
        with { AllowedTransitions = options };

    [Fact]
    public async Task Build_ParsesTheModelsChoiceAndReason()
    {
        var provider = new FakeChatProvider(FakeChatProvider.Answer("""{"choice": "Complication:VexLeadsARaid", "reason": "Vex has been seen twice."}"""));
        var (definition, _) = Director.Build(PromptLibrary.Load(Repo.Prompts), Snapshot("Complication:VexLeadsARaid", "Wait"), ModelTier.Recommended,
            grammar: true, new DelegateFallback<DirectorChoice>(() => new DirectorChoice("Wait", "fallback")));

        var outcome = await new LlmTaskRunner(provider).RunAsync(definition, CancellationToken.None);

        Assert.Equal(OutcomeSource.Model, outcome.Source);
        Assert.Equal(new DirectorChoice("Complication:VexLeadsARaid", "Vex has been seen twice."), outcome.Value);
        Assert.Contains("Complication:VexLeadsARaid", provider.Requests[0].User);
        Assert.Contains("\"enum\"", definition.SchemaOverride!.Schema.ToJsonString());
    }

    [Fact]
    public async Task Build_FallsBackWhenTheModelPicksSomethingNotAllowed()
    {
        var bad = FakeChatProvider.Answer("""{"choice": "Resolution:VexDies", "reason": "Because."}""");
        var (definition, _) = Director.Build(PromptLibrary.Load(Repo.Prompts), Snapshot("Complication:VexLeadsARaid", "Wait"), ModelTier.Recommended,
            grammar: false, new DelegateFallback<DirectorChoice>(() => new DirectorChoice("Wait", "picked by weight")));

        var outcome = await new LlmTaskRunner(new FakeChatProvider(bad, bad)).RunAsync(definition, CancellationToken.None);

        Assert.Equal((OutcomeSource.Template, "Wait"), (outcome.Source, outcome.Value.Choice));
    }

    [Fact]
    public void Decisions_RoundTripAndAreCapped()
    {
        var book = new PlotBook();
        for (var i = 0; i < PlotBook.MaxDecisionsKept + 5; i++)
        {
            book.Record(new DirectorDecision { Day = i, PlotId = "nemesis-1", Choice = "Wait", Reason = $"r{i}", Source = "Random" });
        }

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        book.Expose(saving);
        var loaded = new PlotBook();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal(PlotBook.MaxDecisionsKept, loaded.Decisions.Count);
        Assert.Equal(5, loaded.Decisions[0].Day);
        Assert.Equal("Day 204, nemesis-1: Wait (Random). r204", loaded.Decisions[^1].ToString());
    }

    [Fact]
    public void Nemesis_HuntPath_OffersTheQuestWaitsOnItThenFollowsItsOutcome()
    {
        var plot = NemesisPlotNamed("Vex");
        var book = new PlotBook();
        book.Active.Add(plot);
        book.Advance(plot, PlotBeat.Choice, 9, 5, "choice");
        plot.SetFact(NemesisArc.Choice, NemesisArc.ChoiceHunt);

        Assert.Equal(nameof(NemesisStep.Hunt), Assert.Single(NemesisArc.Transitions(plot)).Step);

        book.LinkQuest(plot, 7, QuestTemplates.Hunt, 10);
        Assert.Empty(NemesisArc.Transitions(plot)); // the quest decides

        book.QuestEnded(book.Quests[0], QuestOutcome.Fail, 20);
        Assert.Equal(nameof(NemesisStep.SecondRaid), Assert.Single(NemesisArc.Transitions(plot)).Step);

        plot.SetFact(NemesisArc.SecondRaidDone, "yes");
        Assert.Equal(nameof(NemesisStep.Vanished), Assert.Single(NemesisArc.Transitions(plot)).Step);
    }

    [Fact]
    public void Nemesis_ChoiceLetter_NamesAllThreeOptions()
    {
        var plot = NemesisPlotNamed("Vex");
        plot.SetFact(NemesisPlot.VictimName, "Dorin");
        plot.SetFact(NemesisArc.Place, "Kelso");
        var text = NemesisLetters.Choice(NemesisLetters.Facts.From(plot, "C"), 300).Text;

        Assert.Contains("300 silver", text);
        Assert.Contains("go after Vex in person, to the camp near Kelso", text);
        Assert.Contains("the colony can wait", text);
    }
}
