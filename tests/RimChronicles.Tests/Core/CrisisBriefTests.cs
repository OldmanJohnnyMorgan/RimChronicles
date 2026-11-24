using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Captives;

namespace RimChronicles.Tests.Core;

public class CrisisBriefTests
{
    private static CrisisState State()
    {
        var state = new CrisisState();
        for (var day = 100; day <= 160; day += 12)
        {
            state.Started(CrisisOperations.ProbingRaid, CrisisStage.Probing, 800, day);
            state.Ended(OperationOutcome.Repelled);
        }

        state.Started(CrisisOperations.Breach, CrisisStage.Reclamation, 1800, 170);
        state.Hold(CrisisCapability.Strikes, 200);
        state.Hold(CrisisCapability.Psykers, 150); // already over by day 172
        state.Captured("p1", "Ortan", CaptiveRank.Officer, 120);
        state.Captured("p2", "Vell", CaptiveRank.Soldier, 121); // released since
        var sabe = state.Captured("p3", "Sabe", CaptiveRank.Psyker, 122);
        sabe.Interrogations = 1;
        state.Recruited("p3", 130);
        state.Intel.Add(new IntelRecord { Day = 125, Intel = "a", Text = "Old news.", ExpiresDay = 140 });
        state.Intel.Add(new IntelRecord { Day = 165, Intel = "b", Text = "A siege is coming.", ExpiresDay = 190 });
        return state;
    }

    [Fact]
    public void NoCrisis_NoBrief() =>
        Assert.Null(CrisisBrief.From(State(), CrisisStage.None, 172, _ => true));

    [Fact]
    public void TheBrief_KeepsTheLatestOperations_AndWhatStillHolds()
    {
        var brief = CrisisBrief.From(State(), CrisisStage.Reclamation, 172, id => id != "p2")!;
        Assert.Equal("Reclamation", brief.Stage);
        Assert.Equal(CrisisBrief.MaxOperations, brief.Operations.Count);
        Assert.Equal(("Breach", "running"), (brief.Operations[^1].Operation, brief.Operations[^1].Outcome));
        Assert.Equal("strikes", Assert.Single(brief.Disabled).Capability);
        Assert.Equal("Ortan", Assert.Single(brief.Captives).Name);      // Vell is gone; Sabe is a defector now
        Assert.Equal(("Sabe", "psyker"), (Assert.Single(brief.Defectors).Name, brief.Defectors[0].Rank));
        Assert.Equal("A siege is coming.", Assert.Single(brief.Intel));
    }

    [Fact]
    public void ADefectorWhoLeft_IsNotShown() =>
        Assert.Empty(CrisisBrief.From(State(), CrisisStage.War, 172, id => id != "p3")!.Defectors);
}
