using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class StrikeTests
{
    private static StrikeCandidate Target(int x, StrikeTargetKind kind = StrikeTargetKind.Turret, int colonists = 0, int allies = 0,
        bool sighted = true, bool roof = false, float value = 1, int alliesNearBeam = 0) =>
        new() { Cell = new Cell(x, 0), Kind = kind, Colonists = colonists, Allies = allies, Sighted = sighted, ThickRoof = roof, Value = value, AlliesNearBeam = alliesNearBeam };

    // ---- fairness rule 1: a source, a telegraph, a cooldown, a cap

    [Fact]
    public void Blocked_WithoutAHeraldOrUplink()
    {
        Assert.NotNull(StrikeRules.Blocked(new StrikeState(), heraldUp: false, uplinkStanding: false, strikesDown: false, tick: 1000));
        Assert.Null(StrikeRules.Blocked(new StrikeState(), heraldUp: true, uplinkStanding: false, strikesDown: false, tick: 1000));
        Assert.Null(StrikeRules.Blocked(new StrikeState(), heraldUp: false, uplinkStanding: true, strikesDown: false, tick: 1000));
    }

    [Fact]
    public void Blocked_WhileTheColonyHasKnockedOutTheUplink_EvenWithAHerald()
    {
        Assert.Contains("uplink", StrikeRules.Blocked(new StrikeState(), true, true, strikesDown: true, tick: 1000));
    }

    [Fact]
    public void Blocked_DuringTheCooldown_AndOnceTheCapIsSpent()
    {
        var state = new StrikeState { Cap = 2 };
        state.Struck(new Cell(0, 0), 1000);
        Assert.Contains("cooling down", StrikeRules.Blocked(state, true, false, false, 1000 + StrikeRules.CooldownTicks - 1));
        Assert.Null(StrikeRules.Blocked(state, true, false, false, 1000 + StrikeRules.CooldownTicks));

        state.Struck(new Cell(20, 0), 5000);
        Assert.Contains("spent", StrikeRules.Blocked(state, true, false, false, 99_999));
        state.Reset();
        Assert.Null(StrikeRules.Blocked(state, true, false, false, 99_999));
    }

    [Fact]
    public void TheTelegraph_IsSixToTenSeconds()
    {
        Assert.Equal(6 * 60, StrikeRules.DelayTicks(0));
        Assert.Equal(8 * 60, StrikeRules.DelayTicks(StrikeRules.DefaultDelaySeconds));
        Assert.Equal(10 * 60, StrikeRules.DelayTicks(60));
    }

    [Fact]
    public void TheCap_ComesFromTheBudget()
    {
        Assert.Equal(StrikeRules.MinCap, StrikeRules.CapFor(0));
        Assert.Equal(3, StrikeRules.CapFor(3500));
        Assert.Equal(StrikeRules.MaxCap, StrikeRules.CapFor(1_000_000));
    }

    // ---- targeting

    [Fact]
    public void Pick_PrefersTurretsAndClustersAndPower_OverWalls()
    {
        var pick = StrikeRules.Pick([Target(0, StrikeTargetKind.Wall, value: 3), Target(20, StrikeTargetKind.Turret)], new StrikeState());
        Assert.Equal(StrikeTargetKind.Turret, pick!.Value.Target.Kind);
        Assert.Equal(StrikeKind.Shell, pick.Value.Kind);

        var cluster = StrikeRules.Pick([Target(0, StrikeTargetKind.Turret), Target(20, StrikeTargetKind.Cluster, colonists: 3)], new StrikeState());
        Assert.Equal(StrikeTargetKind.Cluster, cluster!.Value.Target.Kind);
        Assert.Equal(StrikeKind.Salvo, cluster.Value.Kind);
    }

    [Fact]
    public void Pick_ALoneColonistIsNoCluster()
    {
        Assert.Null(StrikeRules.Pick([Target(0, StrikeTargetKind.Cluster, colonists: 1)], new StrikeState()));
    }

    [Fact]
    public void Pick_NeverWhereANeedsSightIsMissing_UnderMountain_OrOnItsOwnTroops()
    {
        Assert.Null(StrikeRules.Pick([Target(0, sighted: false)], new StrikeState()));
        Assert.Null(StrikeRules.Pick([Target(0, roof: true)], new StrikeState()));
        Assert.Null(StrikeRules.Pick([Target(0, allies: 1)], new StrikeState()));
        Assert.NotNull(StrikeRules.Pick([Target(0)], new StrikeState()));
    }

    [Fact]
    public void Pick_NeverTheSameSpotTwiceInARow()
    {
        var state = new StrikeState();
        state.Struck(new Cell(0, 0), 100);
        var pick = StrikeRules.Pick([Target(0, value: 5), Target(2, value: 5), Target(30, value: 1)], state);
        Assert.Equal(30, pick!.Value.Target.Cell.X);
    }

    [Fact]
    public void Pick_TheSiegeGoalShiftsTheAim()
    {
        var candidates = new[] { Target(0, StrikeTargetKind.Turret), Target(20, StrikeTargetKind.Food, value: 1.5f), Target(40, StrikeTargetKind.Leader, value: 1.4f) };
        Assert.Equal(StrikeTargetKind.Turret, StrikeRules.Pick(candidates, new StrikeState())!.Value.Target.Kind);
        Assert.Equal(StrikeTargetKind.Food, StrikeRules.Pick(candidates, new StrikeState(), SiegeGoal.Starve)!.Value.Target.Kind);
        Assert.Equal(StrikeTargetKind.Leader, StrikeRules.Pick(candidates, new StrikeState(), SiegeGoal.KillLeader)!.Value.Target.Kind);
    }

    [Fact]
    public void Kind_ABeamOnlyWhereNoImperialIsWithinItsFires()
    {
        Assert.Equal(StrikeKind.Beam, StrikeRules.KindFor(Target(0, StrikeTargetKind.Food)));
        Assert.Equal(StrikeKind.Salvo, StrikeRules.KindFor(Target(0, StrikeTargetKind.Food, alliesNearBeam: 1)));
        Assert.Equal(StrikeKind.Shell, StrikeRules.KindFor(Target(0, StrikeTargetKind.Power)));
    }

    // ---- saving

    private sealed class Holder : IArchivable
    {
        public StrikeState State = new();

        public void Expose(IArchive archive) => archive.Deep(ref State, "state");
    }

    [Fact]
    public void State_RoundTrips_AndAMissingOneLoadsFresh()
    {
        var holder = new Holder();
        holder.State.Cap = 3;
        holder.State.Struck(new Cell(7, 9), 4321);
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        holder.Expose(saving);

        var loaded = new Holder { State = null! };
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        Assert.Equal((3, 1, 4321, new Cell(7, 9)), (loaded.State.Cap, loaded.State.StrikesThisRaid, loaded.State.LastStrikeTick, loaded.State.LastCell!.Value));

        var fresh = new Holder { State = null! };
        fresh.Expose(new DictionaryArchive(ArchiveMode.Loading));
        Assert.Equal(-1, fresh.State.LastStrikeTick);
        Assert.Null(fresh.State.LastCell);
    }
}
