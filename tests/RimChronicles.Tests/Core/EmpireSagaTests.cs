using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class EmpireSagaTests
{
    [Fact]
    public void Triggers_WaitForTheColonyToSettle()
    {
        var saga = new PowerSaga { SiteDestroyed = "Ashgrove" };
        Assert.Empty(saga.Triggers(new PowerSigns(1_000_000, "Ada"), Power.Empire.EarliestWakeDay - 1));
        Assert.Equal(3, saga.Triggers(new PowerSigns(1_000_000, "Ada"), Power.Empire.EarliestWakeDay).Count);
    }

    [Fact]
    public void Triggers_ListEveryPresentSign_HeaviestForTheMostPointed()
    {
        var saga = new PowerSaga { SiteDestroyed = "Ashgrove", TitleRefusedBy = "Ada" };
        var options = saga.Triggers(new PowerSigns(PowerSaga.WealthThreshold, "Bram"), 50);

        Assert.Equal(
            ["Awaken:SiteDestroyed", "Awaken:TitleRefused", "Awaken:ImperialColonist", "Awaken:Wealth"],
            options.Select(o => o.Id));
        Assert.Equal(options.OrderByDescending(o => o.Weight).Select(o => o.Id), options.Select(o => o.Id));
    }

    [Fact]
    public void Triggers_TheDeadlineWakesAQuietEmpire()
    {
        var saga = new PowerSaga();
        Assert.Empty(saga.Triggers(PowerSigns.None, Power.Empire.DeadlineDay - 1));
        Assert.Equal(AwakeningTrigger.Deadline, Assert.Single(saga.Triggers(PowerSigns.None, Power.Empire.DeadlineDay)).Step);
    }

    [Fact]
    public void TheEmpire_IsTheOpeningHook_OnTheHookDay()
    {
        var saga = new PowerSaga();
        Assert.Empty(saga.Triggers(PowerSigns.None, PowerSaga.HookDay - 1));
        Assert.Equal(AwakeningTrigger.Deadline, Assert.Single(saga.Triggers(PowerSigns.None, PowerSaga.HookDay)).Step);
        Assert.Equal(AwakeningTrigger.Wealth, Assert.Single(saga.Triggers(new PowerSigns(PowerSaga.WealthThreshold, null), PowerSaga.HookDay)).Step);
    }

    [Fact]
    public void TheChoir_WakesLater_UnlessItRunsAlone()
    {
        var saga = new PowerSaga { Power = Power.Choir };
        Assert.Empty(saga.Triggers(new PowerSigns(0, "Mira"), PowerSaga.LateEarliestDay - 1));
        Assert.Single(saga.Triggers(new PowerSigns(0, "Mira"), PowerSaga.LateEarliestDay));
        Assert.Empty(saga.Triggers(PowerSigns.None, PowerSaga.LateDeadlineDay - 1));
        Assert.Single(saga.Triggers(PowerSigns.None, PowerSaga.LateDeadlineDay));

        // Alone (no Royalty), it takes the hook day, and its signs count from then too.
        Assert.Equal(AwakeningTrigger.Deadline, Assert.Single(saga.Triggers(PowerSigns.None, PowerSaga.HookDay, PowerSaga.HookDay)).Step);
        Assert.Equal(AwakeningTrigger.MechanitorColonist, Assert.Single(saga.Triggers(new PowerSigns(0, "Mira"), PowerSaga.HookDay, PowerSaga.HookDay)).Step);
    }

    [Fact]
    public void Triggers_NoneOnceAwake()
    {
        var saga = new PowerSaga { SiteDestroyed = "Ashgrove" };
        saga.Awaken(AwakeningTrigger.SiteDestroyed, "Ashgrove", 40, 30, "Wyrmfold");
        Assert.Empty(saga.Triggers(PowerSigns.None, 400));
    }

    [Fact]
    public void Awaken_StirsForTheGivenDays_ThenWakes()
    {
        var saga = new PowerSaga();
        var ev = saga.Awaken(AwakeningTrigger.Wealth, "152000", 40, 25, "Wyrmfold");

        Assert.Equal(PowerState.Stirring, saga.State);
        Assert.Equal(EmpireText.StirringNews, ev.NewsText);
        Assert.Null(ev.LetterLabel);
        Assert.Contains("Wyrmfold's wealth (152000 silver)", saga.Log[^1]);
        Assert.False(saga.StirringOver(64));
        Assert.True(saga.StirringOver(65));

        var woke = saga.Wake(65, "the Sovereign Empire");
        Assert.Equal(PowerState.Awakened, saga.State);
        Assert.Equal(65, saga.StateDay);
        Assert.Equal("The Empire awakens", woke.LetterLabel);
        Assert.False(saga.StirringOver(100));
    }

    [Fact]
    public void Rumour_ComesEveryFewDaysWhileStirring()
    {
        var saga = new PowerSaga();
        Assert.Null(saga.Rumour(10, "the Empire", ["the Ochre Hand"], _ => 0, 6)); // dormant

        saga.Awaken(AwakeningTrigger.Dev, "", 40, 30, "Wyrmfold");
        Assert.Null(saga.Rumour(40 + PowerSaga.MinRumourGapDays - 1, "the Empire", ["the Ochre Hand"], _ => 0, 6));

        var rumour = saga.Rumour(45, "the Empire", ["the Ochre Hand"], n => n - 1, 6);
        Assert.Equal("the Empire demanded tribute from the Ochre Hand", rumour?.NewsText);
        Assert.Null(saga.Rumour(50, "the Empire", ["the Ochre Hand"], _ => 0, 6));
        Assert.NotNull(saga.Rumour(51, "the Empire", ["the Ochre Hand"], _ => 0, 6));
    }

    [Fact]
    public void Expose_RoundTrips_AndToleratesAnOldSave()
    {
        var saga = new PowerSaga { TitleRefusedBy = "Ada" };
        saga.Awaken(AwakeningTrigger.TitleRefused, "Ada", 40, 30, "Wyrmfold");
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        saga.Expose(saving);

        var loaded = new PowerSaga();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        Assert.Equal(PowerState.Stirring, loaded.State);
        Assert.Equal(70, loaded.NextStateDay);
        Assert.Equal(AwakeningTrigger.TitleRefused, loaded.Trigger);
        Assert.Equal("Ada", loaded.TitleRefusedBy);
        Assert.Equal(saga.Log, loaded.Log);

        var fresh = new PowerSaga();
        fresh.Expose(new DictionaryArchive(ArchiveMode.Loading));
        Assert.Equal(PowerState.Dormant, fresh.State);
        Assert.Equal(-1, fresh.NextStateDay);
        Assert.Equal("", fresh.SiteDestroyed);
    }
}
