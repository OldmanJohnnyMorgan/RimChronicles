using RimChronicles.Core.Powers;

namespace RimChronicles.Tests.Core;

/// <summary>Either power can wake first, and wakes the other when it attacks; the ship launch and the Royal Ascent wake
/// one at once (War in Heaven W5, spec §4–§5).</summary>
public class CrossWakingTests
{
    private static PowerSaga Awake(Power power)
    {
        var saga = new PowerSaga { Power = power };
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        saga.Wake(60, power.Id);
        return saga;
    }

    [Fact]
    public void WakingAtOnce_SkipsTheStirring_AndKeepsWhatWokeIt()
    {
        var choir = new PowerSaga { Power = Power.Choir };
        var ev = choir.WakeAtOnce(AwakeningTrigger.RoyalAscent, "", 120, "The Hollow Choir", "Wyrmfold");

        Assert.NotNull(ev);
        Assert.Equal(PowerState.Awakened, choir.State);
        Assert.Equal(AwakeningTrigger.RoyalAscent, choir.Trigger);
        Assert.Equal(120 + PowerExpansion.FirstAttackDays, choir.NextExpansionDay);
        Assert.Contains("Royal Ascent", choir.Log[0]);
        Assert.Equal("The Hollow Choir awakens", ev!.LetterLabel);
    }

    [Fact]
    public void WakingAtOnce_FromStirring_KeepsTheFirstTrigger_AndDoesNothingOnceAwake()
    {
        var empire = new PowerSaga();
        empire.Awaken(AwakeningTrigger.Wealth, "160000", 40, 30, "Wyrmfold");
        Assert.NotNull(empire.WakeAtOnce(AwakeningTrigger.ShipLaunch, "", 50, "the Empire", "Wyrmfold"));
        Assert.Equal(AwakeningTrigger.Wealth, empire.Trigger);
        Assert.Equal(PowerState.Awakened, empire.State);
        Assert.Null(empire.WakeAtOnce(AwakeningTrigger.ShipLaunch, "", 51, "the Empire", "Wyrmfold"));
    }

    [Fact]
    public void ARivalWakes_OnlyOnceThePowerHasAttacked()
    {
        var empire = Awake(Power.Empire);
        var choir = new PowerSaga { Power = Power.Choir };

        Assert.False(CrossWaking.RivalShouldWake(empire, CrossWaking.HasAttacked(empire), choir)); // awake, but no attack yet
        empire.Expanded(new ExpansionTarget("1", "Ashgrove", "The Free Rim", 10, 2), ExpansionResult.Conquered, 70, 20, "the Empire");
        Assert.True(CrossWaking.RivalShouldWake(empire, CrossWaking.HasAttacked(empire), choir));

        choir.Awaken(AwakeningTrigger.Dev, "", 71, 20, "Wyrmfold");
        Assert.True(CrossWaking.RivalShouldWake(empire, true, choir)); // stirring still counts as asleep
        choir.Wake(72, "the Choir");
        Assert.False(CrossWaking.RivalShouldWake(empire, true, choir));
    }

    [Fact]
    public void AnyKindOfAttack_Counts()
    {
        var choir = Awake(Power.Choir);
        Assert.False(CrossWaking.HasAttacked(choir));
        Assert.True(CrossWaking.HasAttacked(choir, crisisOperations: true));
        choir.NextShipRaidDay = 80; // a raid against the Royal Ascent
        Assert.True(CrossWaking.HasAttacked(choir));
    }

    [Fact]
    public void AnEndedStory_WakesNoOne()
    {
        var empire = Awake(Power.Empire);
        empire.Ending = "Imperial";
        Assert.False(CrossWaking.RivalShouldWake(empire, true, new PowerSaga { Power = Power.Choir }));
    }

    [Fact]
    public void TheTriggers_ExplainThemselves()
    {
        Assert.Contains("Hollow Choir", Power.Empire.Text.TriggerWhy(AwakeningTrigger.Rival, "", "Wyrmfold"));
        Assert.Contains("ship", Power.Empire.Text.TriggerWhy(AwakeningTrigger.ShipLaunch, "", "Wyrmfold"));
        Assert.Contains("Royal Ascent", Power.Choir.Text.TriggerWhy(AwakeningTrigger.RoyalAscent, "", "Wyrmfold"));
        Assert.Contains("Empire", Power.Choir.Text.TriggerWhy(AwakeningTrigger.Rival, "", "Wyrmfold"));
    }
}
