using RimChronicles.Core.Choir;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The Hollow Choir's plot and rank ladder (War in Heaven W4b, spec D13).</summary>
public class ChoirPlotTests
{
    private static (PlotBook Book, Plot Plot, PowerSaga Saga) NewPlot()
    {
        var book = new PlotBook();
        var plot = book.Start(new PlotTemplate { Id = Power.Choir.PlotTemplateId, CountsTowardCap = false },
            new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 40, 3, "stirring");
        var saga = new PowerSaga { Power = Power.Choir };
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        return (book, plot, saga);
    }

    [Fact]
    public void TheChoirsPlot_UsesItsOwnOptions()
    {
        var (book, plot, saga) = NewPlot();
        Assert.Equal(["Act1:ChoirEnvoy"], PowerArc.Transitions(plot, saga, 41).Select(o => o.Id));
        book.Advance(plot, PlotBeat.Hook, 43, 6, "envoy");
        saga.Wake(60, "The Hollow Choir");
        Assert.Equal(["Act2:TheChoirMarches"], PowerArc.Transitions(plot, saga, 61).Select(o => o.Id));
        book.Advance(plot, PlotBeat.Complication, 62, 6, "marches");
        plot.SetFact(PowerArc.Stance, PowerStance.Ally);
        Assert.Equal(["Choir:EscortEnvoy", "Choir:ConquestJob", "Wait"], PowerArc.Transitions(plot, saga, 70).Select(o => o.Id));
        Assert.All(PowerArc.Transitions(plot, saga, 70).Where(o => o.Id != "Wait"), o => Assert.Contains("Choir", o.Desc));
    }

    [Fact]
    public void TheChoirsEndings_AreItsOwn()
    {
        var (book, plot, saga) = NewPlot();
        book.Advance(plot, PlotBeat.Choice, 100, 6, "act 3");
        PowerArc.StartJob(plot, PowerStep.Governorship, "Ada", 100);
        plot.SetFact(PowerArc.Answer, PowerArc.Accept);
        Assert.Equal(ChoirEnding.Choral, PowerArc.FinishJob(plot, Power.Choir).Ending);
        Assert.Equal(["Ending:Choral"], PowerArc.Transitions(plot, saga, 101).Select(o => o.Id));

        var (book2, plot2, _) = NewPlot();
        book2.Advance(plot2, PlotBeat.Choice, 100, 6, "act 3");
        PowerArc.StartJob(plot2, PowerStep.StormCapital, "Cathedral", 100);
        plot2.SetFact(QuestLink.FactFor(QuestTemplates.Sabotage), QuestOutcome.Success);
        Assert.Equal(ChoirEnding.Silenced, PowerArc.FinishJob(plot2, Power.Choir).Ending);
    }

    [Fact]
    public void TheEmpiresEndings_AreUnchanged()
    {
        Assert.Equal(global::RimChronicles.Core.Empire.EmpireEnding.Imperial, Power.Empire.ServiceEnding);
        Assert.Equal(global::RimChronicles.Core.Empire.EmpireEnding.Resistance, Power.Empire.DefeatEnding);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(12, 2)]
    [InlineData(24, 3)]
    [InlineData(100, 4)]
    public void Favor_GivesARank(int favor, int rank) => Assert.Equal(rank, ChoirRanks.RankFor(favor));

    [Fact]
    public void Service_BuildsOneColonistTowardTheOffer()
    {
        var ranks = new ChoirRanks();
        (string, string)[] colony = [("p2", "Bo"), ("p1", "Ada")];
        Assert.Equal("Chorister", ranks.GrantForJob(colony)); // 4: ties go to the first id, p1
        Assert.Null(ranks.GrantForJob(colony)); // 8
        Assert.Equal("", ranks.HighRankHolder(["p1", "p2"]));
        Assert.Equal("Choirmaster", ranks.GrantForJob(colony)); // 12: the same colonist, who has the most
        Assert.Equal("Ada", ranks.HighRankHolder(["p1", "p2"]));
        Assert.Equal("", ranks.HighRankHolder(["p2"])); // Ada has left the colony
        Assert.Equal(ChoirRanks.HighRank, ranks.RankOf("p1"));
    }

    [Fact]
    public void Ranks_SaveAndLoad()
    {
        var ranks = new ChoirRanks();
        ranks.GrantForJob([("p1", "Ada")], 12);
        var save = new DictionaryArchive(ArchiveMode.Saving);
        save.Deep(ref ranks, "choirRanks");
        ChoirRanks loaded = null!;
        new DictionaryArchive(ArchiveMode.Loading, save.Data).Deep(ref loaded, "choirRanks");
        Assert.Equal("Ada", loaded.HighRankHolder(["p1"]));
    }

    [Fact]
    public void TheChronicle_TitlesTheChoirsStory()
    {
        var (_, plot, _) = NewPlot();
        Assert.Equal("The Hollow Choir", PlotChronicle.Title(plot));
    }
}
