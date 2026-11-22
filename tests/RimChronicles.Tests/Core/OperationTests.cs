using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class OperationTests
{
    public static TheoryData<string> Operations => new(CrisisOperations.All.Select(o => o.Step));

    // ---- fairness rule 6: every operation can retreat

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryOperation_ButTheFinalAssault_RetreatsAfterLosses(string operation)
    {
        var profile = OperationProfiles.For(operation);
        Assert.Equal(operation != CrisisOperations.FinalAssault, profile.RetreatsOnLosses());
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryOperation_PullsOutWhenTheCrisisEnds_AndItsLinksLeadSomewhere(string operation)
    {
        var profile = OperationProfiles.For(operation);
        Assert.NotNull(profile.Find(profile.Start));
        Assert.All(profile.Phases.SelectMany(p => p.Links), l => Assert.NotNull(profile.Find(l.To)));
        Assert.All(profile.Reachable().Where(p => p.Kind != PhaseKind.Retreat),
            p => Assert.Contains(p.Links, l => l.Trigger == PhaseTrigger.Abandoned && profile.Find(l.To)!.Kind == PhaseKind.Retreat));
        Assert.Contains(profile.Reachable(), p => p.Kind == PhaseKind.Retreat);
    }

    [Fact]
    public void RetreatsOnLosses_CatchesAnAssaultWithNoWayOut()
    {
        var stuck = new OperationProfile("Test", "a",
        [
            new("a", PhaseKind.Assault, [new(PhaseTrigger.LossesOver, "b", 0.4f)]),
            new("b", PhaseKind.Assault, [new(PhaseTrigger.TimeUp, "a", 100)]),
            new("retreat", PhaseKind.Retreat, []),
        ]);
        Assert.False(stuck.RetreatsOnLosses());
    }

    [Fact]
    public void Outcomes_LossesAreRepelled_GoalsSucceed_AbandoningAborts_RegroupingRuns()
    {
        Assert.Equal(OperationOutcome.Repelled, new PhaseLink(PhaseTrigger.LossesOver, "x").Outcome);
        Assert.Equal(OperationOutcome.Succeeded, new PhaseLink(PhaseTrigger.TargetDown, "x").Outcome);
        Assert.Equal(OperationOutcome.Succeeded, new PhaseLink(PhaseTrigger.TimeUp, "x").Outcome); // a probe that lasted got its look
        Assert.Equal(OperationOutcome.Aborted, new PhaseLink(PhaseTrigger.Abandoned, "x").Outcome);
        var regroup = OperationProfiles.Standard(CrisisOperations.Breach).Find("regroup")!;
        Assert.Equal(OperationOutcome.Running, regroup.Links.Single(l => l.Trigger == PhaseTrigger.TimeUp).Outcome);
    }

    [Fact]
    public void Profiles_TheProbeIsSmallAndBrief_TheStandardHoldsAReserve()
    {
        Assert.True(OperationProfiles.ProbingRaid.ForceShare <= 0.5f);
        Assert.Equal(OperationProfiles.ProbeTicks, OperationProfiles.ProbingRaid.Find("probe")!.Links.Single(l => l.Trigger == PhaseTrigger.TimeUp).Value);
        var standard = OperationProfiles.Standard(CrisisOperations.Breach);
        Assert.True(standard.ForceShare + standard.ReserveShare <= 1.0001f);
        Assert.True(standard.ReserveShare > 0);
    }

    // ---- force mixes

    private static float Cost(MixEntry e) => e.Role switch
    {
        UnitRole.Praetorian => 150,
        UnitRole.Adept => 160,
        UnitRole.Herald => 90,
        UnitRole.Breacher => 110,
        _ => 400,
    };

    [Fact]
    public void Force_StaysWithinTheBudget_AndFollowsTheShares()
    {
        var force = ForceMix.Build(ForceMix.Decapitation, CrisisStage.War, 3000, Cost);
        Assert.True(force.Sum(Cost) <= 3000);
        Assert.True(force.Sum(Cost) > 3000 - 400);
        var praetorians = force.Count(e => e.Role == UnitRole.Praetorian);
        var adepts = force.Count(e => e.Role == UnitRole.Adept);
        Assert.InRange(adepts, praetorians - 1, praetorians + 1); // 3 : 1 + 1 + 1, within one as the budget runs out
        Assert.Contains(force, e => e.Specialty == Specialty.Skip);
    }

    [Fact]
    public void Force_LeavesOutUnitsTheStageDoesntHaveYet()
    {
        var early = ForceMix.Build(ForceMix.ProbingRaid, CrisisStage.Probing, 5000, Cost);
        Assert.All(early, e => Assert.Equal(UnitRole.Praetorian, e.Role));
        var war = ForceMix.Build(ForceMix.ProbingRaid, CrisisStage.War, 5000, Cost);
        Assert.Contains(war, e => e.Role == UnitRole.Herald);

        var mind = new[] { new MixEntry(UnitRole.Adept, Specialty.Mind, 1), new MixEntry(UnitRole.Praetorian, Specialty.None, 1) };
        Assert.DoesNotContain(ForceMix.Build(mind, CrisisStage.Reclamation, 2000, Cost), e => e.Specialty == Specialty.Mind);
        Assert.Contains(ForceMix.Build(mind, CrisisStage.War, 2000, Cost), e => e.Specialty == Specialty.Mind);
    }

    [Fact]
    public void Force_NeverFewerThanTwo_NeverMoreThanTheCap()
    {
        Assert.Equal(ForceMix.MinUnits, ForceMix.Build(ForceMix.ProbingRaid, CrisisStage.Probing, 10, Cost).Count);
        Assert.Equal(ForceMix.MaxUnits, ForceMix.Build(ForceMix.ProbingRaid, CrisisStage.Probing, 1_000_000, Cost).Count);
        Assert.Empty(ForceMix.Build([], CrisisStage.War, 5000, Cost));
    }

    [Fact]
    public void Force_IsTheSameEveryTime()
    {
        var a = ForceMix.Build(ForceMix.Assault, CrisisStage.War, 4321, Cost);
        var b = ForceMix.Build(ForceMix.Assault, CrisisStage.War, 4321, Cost);
        Assert.Equal(a, b);
    }

    [Fact]
    public void PsychicAssault_NoMindAdeptsBeforeStage3_ThenTheSpecRatio()
    {
        var reclamation = ForceMix.Build(ForceMix.PsychicAssault, CrisisStage.Reclamation, 4000, Cost);
        Assert.DoesNotContain(reclamation, e => e.Specialty == Specialty.Mind);
        Assert.Contains(reclamation, e => e.Specialty == Specialty.Control);
        Assert.Contains(reclamation, e => e.Specialty == Specialty.Skip);

        // Stage 3 at about 12 units' budget: 6 Praetorians to 2 Mind, 2 Control, 1 Ward, 1 Skip.
        var war = ForceMix.Build(ForceMix.PsychicAssault, CrisisStage.War, 12 * 155, Cost);
        int Count(Specialty s) => war.Count(e => e.Specialty == s);
        Assert.InRange(war.Count(e => e.Role == UnitRole.Praetorian), 5, 7);
        Assert.InRange(Count(Specialty.Mind), 1, 2);
        Assert.InRange(Count(Specialty.Control), 1, 2);
        Assert.True(war.Count(e => e.Role == UnitRole.Adept) >= war.Count(e => e.Role == UnitRole.Praetorian) - 1); // psyker-heavy
    }

    [Fact]
    public void Breach_BreachersLead_AndAHeraldFromStage3()
    {
        var reclamation = ForceMix.Build(ForceMix.Breach, CrisisStage.Reclamation, 3000, Cost);
        Assert.True(reclamation.Count(e => e.Role == UnitRole.Breacher) >= 2);
        Assert.DoesNotContain(reclamation, e => e.Role == UnitRole.Herald);
        Assert.Contains(ForceMix.Build(ForceMix.Breach, CrisisStage.War, 3000, Cost), e => e.Role == UnitRole.Herald);
    }

    [Fact]
    public void Breach_OpensWithABreachingPhase_ThenTheStandardShape()
    {
        var breach = OperationProfiles.For(CrisisOperations.Breach);
        Assert.Equal(PhaseKind.Breach, breach.Find(breach.Start)!.Kind);
        Assert.Equal(PhaseKind.Assault, breach.Find("second")!.Kind);
        Assert.True(breach.RetreatsOnLosses());
        Assert.Equal(PhaseKind.Assault, OperationProfiles.For(CrisisOperations.PsychicAssault).Find("assault")!.Kind);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryOperation_HasAMix_WithPraetorians(string operation)
    {
        Assert.Contains(ForceMix.For(operation), e => e.Role == UnitRole.Praetorian);
    }

    // ---- fairness rule 3: drop pods

    private static IEnumerable<DropCell> Field(Func<Cell, RoofKind> roof, Func<Cell, bool>? standable = null)
    {
        for (var x = -12; x <= 12; x++)
        {
            for (var z = -12; z <= 12; z++)
            {
                var c = new Cell(x, z);
                yield return new DropCell(c, roof(c), standable?.Invoke(c) ?? true);
            }
        }
    }

    [Fact]
    public void DropSpot_NeverUnderAnyRoof_NearestOpenSky()
    {
        // The target is in a roofed room 8×8 around it, under mountain in the middle.
        var spot = Tactics.DropSpot(new Cell(0, 0), Field(c => Math.Abs(c.X) <= 4 && Math.Abs(c.Z) <= 4
            ? (Math.Abs(c.X) <= 2 && Math.Abs(c.Z) <= 2 ? RoofKind.Thick : RoofKind.Constructed)
            : RoofKind.None));
        Assert.NotNull(spot);
        Assert.True(Math.Abs(spot!.Value.X) > 4 || Math.Abs(spot.Value.Z) > 4);
        Assert.InRange(BattleView.Dist(spot.Value, new Cell(0, 0)), 5, 6);
    }

    [Fact]
    public void DropSpot_NoneWhenEverythingNearIsRoofedOrBlocked()
    {
        Assert.Null(Tactics.DropSpot(new Cell(0, 0), Field(_ => RoofKind.Thick)));
        Assert.Null(Tactics.DropSpot(new Cell(0, 0), Field(_ => RoofKind.None, _ => false)));
    }

    [Fact]
    public void DropSpot_NotRightOnTopOfTheTarget()
    {
        var spot = Tactics.DropSpot(new Cell(0, 0), Field(_ => RoofKind.None));
        Assert.True(BattleView.Dist(spot!.Value, new Cell(0, 0)) >= 3);
    }

    // ---- focus fire and carrying the downed

    private static BattleUnit Colonist(int id, int x, float health = 1, bool tending = false, bool downed = false) =>
        new() { Id = id, Side = BattleSide.Colony, Pos = new Cell(x, 0), Health = health, Tending = tending, Downed = downed };

    private static BattleView View(params BattleUnit[] units) => new(units, _ => true, (_, _) => true);

    [Fact]
    public void FocusTarget_MedicsFirst_ThenTheEasiestToBringDown()
    {
        var view = View(Colonist(1, 10), Colonist(2, 12, health: 0.4f), Colonist(3, 20, tending: true));
        Assert.Equal(3, Tactics.FocusTarget(view, BattleSide.Empire, new Cell(0, 0))!.Id);
        var noMedic = View(Colonist(1, 10), Colonist(2, 12, health: 0.4f));
        Assert.Equal(2, Tactics.FocusTarget(noMedic, BattleSide.Empire, new Cell(0, 0))!.Id);
    }

    [Fact]
    public void FocusTarget_NotTheDowned_NotTurrets_NotOutOfRange()
    {
        var turret = new BattleUnit { Id = 9, Side = BattleSide.Colony, Pos = new Cell(5, 0), IsTurret = true, Health = 0.1f };
        var view = View(Colonist(1, 10, downed: true), turret, Colonist(2, 90));
        Assert.Null(Tactics.FocusTarget(view, BattleSide.Empire, new Cell(0, 0)));
    }

    [Fact]
    public void Carrying_OnlyWithNoColonistClose()
    {
        var downed = new BattleUnit { Id = 5, Side = BattleSide.Empire, Pos = new Cell(0, 0), Downed = true };
        Assert.False(Tactics.SafeToCarry(View(downed, Colonist(1, 6)), downed));
        Assert.True(Tactics.SafeToCarry(View(downed, Colonist(1, 25)), downed));
        Assert.True(Tactics.SafeToCarry(View(downed, Colonist(1, 6, downed: true)), downed));
    }

    // ---- what a probe learns

    private sealed class Holder : IArchivable
    {
        public CrisisState Crisis = new();

        public void Expose(IArchive archive) => archive.Deep(ref Crisis, "crisis");
    }

    [Fact]
    public void Probe_ItsEntrancesAreKept_NewestReplacingTheLast_AndSaved()
    {
        var holder = new Holder();
        holder.Crisis.Learned(Enumerable.Range(0, 10).Select(i => (i, i * 2)), 40);
        Assert.Equal(CrisisState.MaxEntrances, holder.Crisis.Entrances.Count);
        holder.Crisis.Learned([(5, 5)], 50);
        Assert.Single(holder.Crisis.Entrances);

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        holder.Expose(saving);
        var loaded = new Holder { Crisis = null! };
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        Assert.Equal((5, 5, 50), (loaded.Crisis.Entrances[0].X, loaded.Crisis.Entrances[0].Z, loaded.Crisis.EntrancesDay));
    }
}
