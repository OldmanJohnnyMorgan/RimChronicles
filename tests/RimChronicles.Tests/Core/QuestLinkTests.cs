using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class QuestLinkTests
{
    private static readonly PlotTemplate Template = new() { Id = "Nemesis" };

    [Fact]
    public void LinkQuest_MarksThePlotPending()
    {
        var book = new PlotBook();
        var plot = book.Start(Template, ProtagonistSlot.ForPawn("p1", "Mara"), 0, 0, "seed");

        var link = book.LinkQuest(plot, questId: 42, "Hunt", today: 5);

        Assert.Equal(("pending", 42, "quest.Hunt"), (plot.Fact("quest.Hunt"), link.QuestId, link.FactKey));
        Assert.True(link.Pending);
        Assert.EndsWith("quest offered: Hunt", plot.Log[^1]);
    }

    [Theory]
    [InlineData(QuestOutcome.Success)]
    [InlineData(QuestOutcome.Fail)]
    [InlineData(QuestOutcome.Ignored)]
    public void QuestEnded_PutsTheOutcomeOnThePlotAndMakesItDue(string outcome)
    {
        var book = new PlotBook();
        var plot = book.Start(Template, ProtagonistSlot.ForPawn("p1", "Mara"), 0, 0, "seed");
        plot.NextBeatDay = 30;
        var link = book.LinkQuest(plot, 42, "Hunt", 5);

        Assert.Same(plot, book.QuestEnded(link, outcome, today: 12));

        Assert.Equal((outcome, 12), (plot.Fact("quest.Hunt"), plot.NextBeatDay));
        Assert.False(link.Pending);
    }

    [Fact]
    public void QuestEnded_ForAnEndedPlotOnlyRecordsTheOutcome()
    {
        var book = new PlotBook();
        var plot = book.Start(Template, ProtagonistSlot.ForPawn("p1", "Mara"), 0, 0, "seed");
        var link = book.LinkQuest(plot, 42, "Hunt", 5);
        book.End(plot, 6, "done");

        Assert.Null(book.QuestEnded(link, QuestOutcome.Success, 7));
        Assert.Equal(QuestOutcome.Success, link.Outcome);
    }

    [Fact]
    public void Links_RoundTripAndOldFinishedOnesArePruned()
    {
        var book = new PlotBook();
        var plot = book.Start(Template, ProtagonistSlot.ForPawn("p1", "Mara"), 0, 0, "seed");
        book.QuestEnded(book.LinkQuest(plot, 1, "Hunt", 0), QuestOutcome.Fail, 3);
        book.LinkQuest(plot, 2, "Rescue", 50);

        book.PruneQuests(today: 100);
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        book.Expose(saving);
        var loaded = new PlotBook();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        var link = Assert.Single(loaded.Quests);
        Assert.Equal((2, "Rescue", true), (link.QuestId, link.Template, link.Pending));
    }
}
