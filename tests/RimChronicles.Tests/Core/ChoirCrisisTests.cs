using RimChronicles.Core.Crisis;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The Hollow Choir's crisis vocabulary (War in Heaven W7–W8): its own operations and sites, kept apart from
/// the Empire's.</summary>
public class ChoirCrisisTests
{
    private static CrisisContext Context(Power power, CrisisStage stage, bool captive = false) => new()
    {
        Stage = stage,
        State = new CrisisState(),
        Built = CrisisOperations.For(power).Select(o => o.Step).ToHashSet(),
        HoldsCaptive = captive,
        Power = power,
    };

    private static Plot NewPlot() => new PlotBook().Start(new PlotTemplate { Id = Power.Choir.CrisisTemplateId, CountsTowardCap = false },
        new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 100, 3, "crisis");

    [Theory]
    [InlineData(CrisisStage.Probing, new[] { "Operation:ProbingSwarm", "Wait" })]
    [InlineData(CrisisStage.Reclamation, new[] { "Operation:ProbingSwarm", "Operation:TunnelBreach", "Operation:TheHum", "Wait" })]
    [InlineData(CrisisStage.War, new[] { "Operation:ProbingSwarm", "Operation:TunnelBreach", "Operation:TheHum", "Operation:ClusterSiege", "Operation:Burrowing", "Wait" })]
    public void TheChoirsCrisis_OffersItsOwnOperationsByStage(CrisisStage stage, string[] expected) =>
        Assert.Equal(expected, CrisisArc.Transitions(NewPlot(), Context(Power.Choir, stage), 100).Select(o => o.Id));

    [Fact]
    public void RetrievalAndTheFinale_FollowTheSameRules()
    {
        Assert.Contains("Operation:Retrieval", CrisisArc.Transitions(NewPlot(), Context(Power.Choir, CrisisStage.Reclamation, captive: true), 100).Select(o => o.Id));
        Assert.Contains("Operation:FinalChorus", CrisisArc.Transitions(NewPlot(), Context(Power.Choir, CrisisStage.Finale), 100).Select(o => o.Id));
        Assert.DoesNotContain("Operation:FinalAssault", CrisisArc.Transitions(NewPlot(), Context(Power.Choir, CrisisStage.Finale), 100).Select(o => o.Id));
    }

    [Fact]
    public void TheEmpiresCrisis_IsUnchanged()
    {
        Assert.Same(CrisisOperations.All, CrisisOperations.For(Power.Empire));
        Assert.Equal(new CrisisContext { Stage = CrisisStage.Probing, State = new CrisisState(), Built = new HashSet<string>() }.Power, Power.Empire);
        Assert.Empty(CrisisOperations.Choir.Select(o => o.Step).Intersect(CrisisOperations.All.Select(o => o.Step)).Except([CrisisOperations.Retrieval]));
    }

    [Fact]
    public void EveryChoirOperation_HasAChoirDescription() =>
        Assert.All(CrisisOperations.Choir, o => Assert.Matches("Choir|mechs|First Voice", o.Desc));

    [Fact]
    public void TheSites_AreKeptApartByPower()
    {
        var defs = Repo.Path("Defs", "SiteArchetypeDefs");
        Assert.Equal(["ForwardOutpost", "OrbitalUplink", "PsychicRelay", "ReclaimedHolding"], SiteArchetypeXml.Load(defs).Select(s => s.Name).Order());
        var choir = SiteArchetypeXml.Load(defs, Power.Choir.Id);
        Assert.Equal(["ChoirHolding", "ClusterOutpost", "Resonator", "SignalSpire"], choir.Select(s => s.Name).Order());
        Assert.All(choir, s => Assert.Equal("choir", s.Power));
        Assert.True(choir.Single(s => s.Name == "ChoirHolding").NeedsHolding);
        Assert.All(choir, s => Assert.Contains("Choir", s.Desc, StringComparison.OrdinalIgnoreCase));
    }
}
