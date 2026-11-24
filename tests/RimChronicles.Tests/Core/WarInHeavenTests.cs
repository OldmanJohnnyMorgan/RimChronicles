using RimChronicles.Core.Choir;
using RimChronicles.Core.Empire;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Core.War;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The War in Heaven (W10–W11, spec §7): when it starts, what the colony is asked to do, and how it ends.</summary>
public class WarInHeavenTests
{
    private static PowerSaga Awake(Power power)
    {
        var saga = new PowerSaga { Power = power };
        saga.WakeAtOnce(AwakeningTrigger.Dev, "", 60, power.Id, "Wyrmfold");
        return saga;
    }

    private static Plot NewPlot() => new PlotBook().Start(new PlotTemplate { Id = WarInHeaven.TemplateId, CountsTowardCap = false },
        new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 300, 3, "war");

    private static WarInHeaven Started(int front = 0)
    {
        var war = new WarInHeaven { Front = front };
        war.Start(300, "test");
        return war;
    }

    private static IEnumerable<string> Ids(Plot plot, WarContext c) => WarArc.Transitions(plot, c, 310).Select(o => o.Id);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(29, 0)]
    [InlineData(30, 0)]
    [InlineData(105, 0.015)]
    [InlineData(180, 0.03)]
    [InlineData(1000, 0.1)] // capped
    public void TheOdds_RiseWithTheLengthOfTheWar(int warDays, double odds) =>
        Assert.Equal(odds, WarInHeaven.Odds(warDays), 6);

    [Fact]
    public void Day600_StartsItForCertain()
    {
        Assert.True(WarInHeaven.StartsToday(600, 0, 0.99));
        Assert.False(WarInHeaven.StartsToday(599, 0, 0.0001));
        Assert.True(WarInHeaven.StartsToday(400, 180, 0.02));
        Assert.False(WarInHeaven.StartsToday(400, 180, 0.05));
    }

    [Fact]
    public void ItNeedsBothPowersAwake_AndTheColonyAtWar()
    {
        var war = new WarInHeaven();
        Assert.False(war.CanStart(Awake(Power.Empire), new PowerSaga { Power = Power.Choir }, colonyAtWar: true));
        Assert.False(war.CanStart(Awake(Power.Empire), Awake(Power.Choir), colonyAtWar: false));
        Assert.True(war.CanStart(Awake(Power.Empire), Awake(Power.Choir), colonyAtWar: true));
        war.Start(300, "day 600");
        Assert.False(war.CanStart(Awake(Power.Empire), Awake(Power.Choir), colonyAtWar: true));
    }

    [Fact]
    public void TheWarSince_IsTheEarliestStanceStillAgainstAPower()
    {
        var a = new Allegiance();
        Assert.Equal(-1, a.WarSince());
        a.Set(Power.Empire, PowerStance.Ally, Power.All, 50, AllegianceCause.Plot); // the Choir becomes its enemy on day 50
        Assert.Equal(50, a.WarSince());
        a.DeclareIndependence(Power.All, 90, AllegianceCause.Dev);
        Assert.Equal(50, a.WarSince());
    }

    [Fact]
    public void OnAPowersSide_TheColonyFightsItsRival_AndThePushOpensWithTheFront()
    {
        var c = new WarContext { Side = Power.Empire.Id, War = Started() };
        Assert.Equal(["War:DefendFront", "War:StrikeRival", "War:EscortConvoy", "War:SabotageRival", "Wait"], Ids(NewPlot(), c));
        var desc = WarArc.Transitions(NewPlot(), c, 310).First().Desc;
        Assert.Contains("the Empire", desc);
        Assert.Contains("the Hollow Choir", desc);

        var pushing = new WarContext { Side = Power.Choir.Id, War = Started(front: WarArc.FrontForPush) };
        Assert.Contains("War:FinalPush", Ids(NewPlot(), pushing));
        Assert.Contains("the Empire's capital", WarArc.Describe(WarStep.FinalPush, pushing));
    }

    [Fact]
    public void TheFinalPush_DestroysTheRival_ThenTheSideMakesItsOffer()
    {
        var war = Started(front: WarArc.FrontForPush);
        var c = new WarContext { Side = Power.Empire.Id, War = war };
        var plot = NewPlot();
        WarArc.StartJob(plot, WarStep.FinalPush, "the Cathedral", 310);
        Assert.Empty(Ids(plot, c)); // under way
        plot.SetFact(QuestLink.FactFor(QuestTemplates.Sabotage), QuestOutcome.Success);
        Assert.Equal(["War:Aftermath:FinalPush"], Ids(plot, c));
        var r = WarArc.FinishJob(plot, c);
        Assert.Same(Power.Choir, r.Destroys);
        war.Destroy(Power.Choir, 320, "its Cathedral fell");

        Assert.Equal(["War:VictoryOffer", "Wait"], Ids(plot, c));
        WarArc.StartJob(plot, WarStep.VictoryOffer, "", 320);
        plot.SetFact(PowerArc.Answer, PowerArc.Accept);
        Assert.Equal(EmpireEnding.Imperial, WarArc.FinishJob(plot, c).Ending);
        Assert.Equal(["Ending:Imperial"], Ids(plot, c));
    }

    [Fact]
    public void TheChoirsSide_EndsInChoralVictory()
    {
        var war = Started();
        war.Destroy(Power.Empire, 320, "its Citadel fell");
        var c = new WarContext { Side = Power.Choir.Id, War = war };
        var plot = NewPlot();
        WarArc.StartJob(plot, WarStep.VictoryOffer, "", 320);
        plot.SetFact(PowerArc.Answer, PowerArc.Accept);
        Assert.Equal(ChoirEnding.Choral, WarArc.FinishJob(plot, c).Ending);
    }

    [Fact]
    public void AnIndependentColony_FightsBoth_GetsATreatyAfterOne_AndTotalIndependenceAfterBoth()
    {
        var war = Started();
        var c = new WarContext { Side = Side.Independent, War = war };
        Assert.Equal(["War:StrikeEmpire", "War:SabotageEmpire", "War:StrikeChoir", "War:SabotageChoir", "Wait"], Ids(NewPlot(), c));

        war.Destroy(Power.Choir, 320, "its Cathedral fell");
        Assert.Equal(["War:TreatyOffer", "War:StrikeEmpire", "War:SabotageEmpire", "Wait"], Ids(NewPlot(), c));

        var refused = NewPlot();
        WarArc.StartJob(refused, WarStep.TreatyOffer, "", 321);
        refused.SetFact(PowerArc.Answer, PowerArc.Decline);
        Assert.Null(WarArc.FinishJob(refused, c).Ending); // the colony may say no and fight on (spec D9)

        var signed = NewPlot();
        WarArc.StartJob(signed, WarStep.TreatyOffer, "", 321);
        signed.SetFact(PowerArc.Answer, PowerArc.Accept);
        Assert.Equal(EmpireEnding.Independence, WarArc.FinishJob(signed, c).Ending);

        war.Destroy(Power.Empire, 400, "its Citadel fell");
        Assert.Equal([$"Ending:{WarEnding.IndependenceTotal}"], Ids(NewPlot(), c));
    }

    [Fact]
    public void JobsMoveTheFront()
    {
        var war = Started();
        var c = new WarContext { Side = Power.Empire.Id, War = war };
        var plot = NewPlot();
        WarArc.StartJob(plot, WarStep.DefendFront, "Ashgrove", 310);
        plot.SetFact(QuestLink.FactFor(QuestTemplates.DefendAlly), QuestOutcome.Success);
        WarArc.FinishJob(plot, c);
        WarArc.StartJob(plot, WarStep.StrikeRival, "Hollowmere", 320);
        plot.SetFact(QuestLink.FactFor(QuestTemplates.Sabotage), "failure");
        WarArc.FinishJob(plot, c);
        WarArc.StartJob(plot, WarStep.EscortConvoy, "Ashgrove", 330);
        plot.SetFact(QuestLink.FactFor(QuestTemplates.Escort), QuestOutcome.Success);
        WarArc.FinishJob(plot, c);
        Assert.Equal(1, war.Front);
    }

    [Fact]
    public void NothingIsOffered_BeforeItStarts() =>
        Assert.Empty(Ids(NewPlot(), new WarContext { Side = Power.Empire.Id, War = new WarInHeaven() }));

    [Fact]
    public void ItSavesAndLoads()
    {
        var war = Started(front: 3);
        war.Destroy(Power.Choir, 320, "its Cathedral fell");
        var save = new DictionaryArchive(ArchiveMode.Saving);
        save.Deep(ref war, "war");
        WarInHeaven loaded = null!;
        new DictionaryArchive(ArchiveMode.Loading, save.Data).Deep(ref loaded, "war");
        Assert.Equal((WarState.Started, 300, 3), (loaded.State, loaded.StartDay, loaded.Front));
        Assert.True(loaded.IsDestroyed(Power.Choir));

        WarInHeaven old = null!;
        new DictionaryArchive(ArchiveMode.Loading, []).Deep(ref old, "war");
        Assert.Equal(WarState.NotYet, old.State);
    }
}
