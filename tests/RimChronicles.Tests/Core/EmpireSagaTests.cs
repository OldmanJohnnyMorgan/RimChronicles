using RimChronicles.Core.Empire;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class EmpireSagaTests
{
    [Fact]
    public void Triggers_WaitForTheColonyToSettle()
    {
        var saga = new EmpireSaga { SiteDestroyed = "Ashgrove" };
        Assert.Empty(saga.Triggers(new EmpireSigns(1_000_000, "Ada"), EmpireSaga.EarliestAwakeningDay - 1));
        Assert.Equal(3, saga.Triggers(new EmpireSigns(1_000_000, "Ada"), EmpireSaga.EarliestAwakeningDay).Count);
    }

    [Fact]
    public void Triggers_ListEveryPresentSign_HeaviestForTheMostPointed()
    {
        var saga = new EmpireSaga { SiteDestroyed = "Ashgrove", TitleRefusedBy = "Ada" };
        var options = saga.Triggers(new EmpireSigns(EmpireSaga.WealthThreshold, "Bram"), 50);

        Assert.Equal(
            ["Awaken:SiteDestroyed", "Awaken:TitleRefused", "Awaken:ImperialColonist", "Awaken:Wealth"],
            options.Select(o => o.Id));
        Assert.Equal(options.OrderByDescending(o => o.Weight).Select(o => o.Id), options.Select(o => o.Id));
    }

    [Fact]
    public void Triggers_TheDeadlineWakesAQuietEmpire()
    {
        var saga = new EmpireSaga();
        Assert.Empty(saga.Triggers(EmpireSigns.None, EmpireSaga.DeadlineDay - 1));
        Assert.Equal(AwakeningTrigger.Deadline, Assert.Single(saga.Triggers(EmpireSigns.None, EmpireSaga.DeadlineDay)).Step);
    }

    [Fact]
    public void Triggers_NoneOnceAwake()
    {
        var saga = new EmpireSaga { SiteDestroyed = "Ashgrove" };
        saga.Awaken(AwakeningTrigger.SiteDestroyed, "Ashgrove", 40, 30, "Wyrmfold");
        Assert.Empty(saga.Triggers(EmpireSigns.None, 400));
    }

    [Fact]
    public void Awaken_StirsForTheGivenDays_ThenWakes()
    {
        var saga = new EmpireSaga();
        var ev = saga.Awaken(AwakeningTrigger.Wealth, "152000", 40, 25, "Wyrmfold");

        Assert.Equal(EmpireState.Stirring, saga.State);
        Assert.Equal(EmpireText.StirringNews, ev.NewsText);
        Assert.Null(ev.LetterLabel);
        Assert.Contains("Wyrmfold's wealth (152000 silver)", saga.Log[^1]);
        Assert.False(saga.StirringOver(64));
        Assert.True(saga.StirringOver(65));

        var woke = saga.Wake(65, "the Sovereign Empire");
        Assert.Equal(EmpireState.Awakened, saga.State);
        Assert.Equal(65, saga.StateDay);
        Assert.Equal("The Empire awakens", woke.LetterLabel);
        Assert.False(saga.StirringOver(100));
    }

    [Fact]
    public void Rumour_ComesEveryFewDaysWhileStirring()
    {
        var saga = new EmpireSaga();
        Assert.Null(saga.Rumour(10, "the Empire", ["the Ochre Hand"], _ => 0, 6)); // dormant

        saga.Awaken(AwakeningTrigger.Dev, "", 40, 30, "Wyrmfold");
        Assert.Null(saga.Rumour(40 + EmpireSaga.MinRumourGapDays - 1, "the Empire", ["the Ochre Hand"], _ => 0, 6));

        var rumour = saga.Rumour(45, "the Empire", ["the Ochre Hand"], n => n - 1, 6);
        Assert.Equal("the Empire demanded tribute from the Ochre Hand", rumour?.NewsText);
        Assert.Null(saga.Rumour(50, "the Empire", ["the Ochre Hand"], _ => 0, 6));
        Assert.NotNull(saga.Rumour(51, "the Empire", ["the Ochre Hand"], _ => 0, 6));
    }

    [Fact]
    public void Expose_RoundTrips_AndToleratesAnOldSave()
    {
        var saga = new EmpireSaga { TitleRefusedBy = "Ada" };
        saga.Awaken(AwakeningTrigger.TitleRefused, "Ada", 40, 30, "Wyrmfold");
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        saga.Expose(saving);

        var loaded = new EmpireSaga();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        Assert.Equal(EmpireState.Stirring, loaded.State);
        Assert.Equal(70, loaded.NextStateDay);
        Assert.Equal(AwakeningTrigger.TitleRefused, loaded.Trigger);
        Assert.Equal("Ada", loaded.TitleRefusedBy);
        Assert.Equal(saga.Log, loaded.Log);

        var fresh = new EmpireSaga();
        fresh.Expose(new DictionaryArchive(ArchiveMode.Loading));
        Assert.Equal(EmpireState.Dormant, fresh.State);
        Assert.Equal(-1, fresh.NextStateDay);
        Assert.Equal("", fresh.SiteDestroyed);
    }
}
