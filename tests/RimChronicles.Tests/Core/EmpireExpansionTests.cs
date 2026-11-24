using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class EmpireExpansionTests
{
    private static ExpansionTarget T(string name, float distance, int settlements = 3) => new(name, name, "the Ochre Hand", distance, settlements);

    [Fact]
    public void PickTarget_PrefersNearAndWeak_WithinRange()
    {
        var near = T("Near", 5);
        var far = T("Far", 50);
        var outOfRange = T("Beyond", PowerExpansion.RangeTiles + 1, 1);
        Assert.True(PowerExpansion.Weight(near) > PowerExpansion.Weight(far));
        Assert.True(PowerExpansion.Weight(T("Weak", 20, 1)) > PowerExpansion.Weight(T("Strong", 20, 5)));

        var picks = Enumerable.Range(0, 100).Select(i => PowerExpansion.PickTarget([near, far, outOfRange], i / 100.0)!.Name).ToList();
        Assert.DoesNotContain("Beyond", picks);
        Assert.True(picks.Count(p => p == "Near") > picks.Count(p => p == "Far"));
    }

    [Fact]
    public void PickTarget_TakesTheNearestWhenNoneInRange_AndNullWhenNone()
    {
        Assert.Equal("B", PowerExpansion.PickTarget([T("A", 90), T("B", 70)], 0.9)!.Name);
        Assert.Null(PowerExpansion.PickTarget([], 0.5));
    }

    [Fact]
    public void PickResult_StrongerOwnersHoldMoreOften()
    {
        var weak = T("W", 10, 1);
        var strong = T("S", 10, 6);
        Assert.Equal(0.20, PowerExpansion.RepelChance(weak), 3);
        Assert.Equal(0.45, PowerExpansion.RepelChance(strong), 3);

        Assert.Equal(ExpansionResult.Repelled, PowerExpansion.PickResult(weak, 0.1));
        Assert.Equal(ExpansionResult.Razed, PowerExpansion.PickResult(weak, 0.3));
        Assert.Equal(ExpansionResult.Conquered, PowerExpansion.PickResult(weak, 0.5));
        Assert.Equal(ExpansionResult.Repelled, PowerExpansion.PickResult(strong, 0.4));
    }

    [Fact]
    public void Saga_ExpandsOnlyWhenAwake_OnItsTimer()
    {
        var saga = new PowerSaga();
        Assert.False(saga.ExpansionDue(500));
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        Assert.False(saga.ExpansionDue(500));

        saga.Wake(60, "the Empire");
        Assert.False(saga.ExpansionDue(60 + PowerExpansion.FirstAttackDays - 1));
        Assert.True(saga.ExpansionDue(60 + PowerExpansion.FirstAttackDays));

        var ev = saga.Expanded(T("Marrow Hold", 12), ExpansionResult.Conquered, 67, 20, "the Empire");
        Assert.Equal("the Empire took Marrow Hold from the Ochre Hand", ev.NewsText);
        Assert.Equal(87, saga.NextExpansionDay);
        saga.Expanded(T("Ashgrove", 12), ExpansionResult.Repelled, 87, 20, "the Empire");
        Assert.Equal((1, 1), (saga.Conquests, saga.Defeats));
    }

    [Fact]
    public void Saga_SavesExpansions()
    {
        var saga = new PowerSaga();
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        saga.Wake(60, "the Empire");
        saga.Expanded(T("Marrow Hold", 12), ExpansionResult.Razed, 67, 20, "the Empire");
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        saga.Expose(saving);

        var loaded = new PowerSaga();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        Assert.Equal(87, loaded.NextExpansionDay);
        var record = Assert.Single(loaded.Expansions);
        Assert.Equal(("Marrow Hold", ExpansionResult.Razed), (record.Settlement, record.Result));
    }
}
