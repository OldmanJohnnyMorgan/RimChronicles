using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.Crisis.Units;
using Power = RimChronicles.Core.Powers.Power;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The Hollow Choir's sites and cluster siege (War in Heaven W8): its own pieces, kept apart from the Empire's,
/// planned by the same planners.</summary>
public class ChoirSiteTests
{
    private static readonly IReadOnlyList<PieceSource> ChoirPieces = PieceXml.For(Power.Choir.Id);
    private static readonly IReadOnlyList<PieceSource> EmpirePieces = PieceXml.For(Power.Empire.Id);

    private static IReadOnlyList<SiteArchetype> Sites => SiteXml.Load(Power.Choir.Id);

    public static TheoryData<string> Archetypes => new(SiteXml.Load(Power.Choir.Id).Select(a => a.Name));

    // ---- pieces belong to a power, or to both

    [Fact]
    public void Pieces_TheEmpireNeverTakesTheChoirs_NorTheChoirTheEmpires()
    {
        Assert.DoesNotContain(EmpirePieces, p => p.Meta.Power == Power.Choir.Id);
        Assert.DoesNotContain(ChoirPieces, p => p.Meta.Power == Power.Empire.Id);
        Assert.Contains(ChoirPieces, p => p.Meta.Power == Power.Choir.Id);
        Assert.Contains(ChoirPieces, p => p.Meta.Power is null); // the shared barracks, power yard, sandbags
        Assert.All(PieceXml.Load(), p => Assert.True(p.Meta.Power is null || p.Meta.Power == Power.Empire.Id || p.Meta.Power == Power.Choir.Id, p.Name));
    }

    [Fact]
    public void ChoirPieces_UseTheChoirsOwnBuildings()
    {
        var things = ChoirPieces.Where(p => p.Meta.Power == Power.Choir.Id).SelectMany(p => p.Things).Select(t => t.Def).ToHashSet();
        Assert.Contains("RimChronicles_SignalSpire", things);
        Assert.Contains("RimChronicles_ChoirResonator", things);
        Assert.Contains("Turret_AutoMortar", things);
        Assert.DoesNotContain(ChoirPieces.SelectMany(p => p.Things), t => t.Def is "RimChronicles_ImperialUplink" or "RimChronicles_PsychicRelay" or "RimChronicles_ImperialDropBeacon");
    }

    // ---- sites: the same rules as the Empire's (fairness rule 4: the objective is always reachable)

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void FiftySeeds_EachChoirSitePassesTheValidators(string name)
    {
        var a = Sites.Single(s => s.Name == name);
        var firstTry = 0;
        for (var id = 1; id <= 50; id++)
        {
            var plan = SitePlanner.PlanValid(a, ChoirPieces, (a.MinBudget + a.MaxBudget) / 2, id, out var attempts);
            Assert.False(plan.Fallback, $"seed {id} fell back");
            Assert.Empty(SiteValidator.Problems(plan));
            firstTry += attempts == 1 ? 1 : 0;
        }

        Assert.True(firstTry >= 45, $"only {firstTry}/50 pass on the first seed");
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void EachChoirSite_HasItsObjectiveRoom_AndNoImperialPiece(string name)
    {
        var a = Sites.Single(s => s.Name == name);
        var plan = SitePlanner.PlanValid(a, ChoirPieces, a.MaxBudget, 7, out _);
        Assert.NotNull(plan.Objective);
        Assert.Contains(a.ObjectiveTag, plan.Objective!.Piece.Meta.Tags);
        Assert.DoesNotContain(plan.Pieces, p => p.Piece.Meta.Power == Power.Empire.Id);
    }

    [Fact]
    public void TheResonatorAndSpireSites_HaveTheirOwnObjectives()
    {
        Assert.Equal("ResonatorChamber", Sites.Single(s => s.Name == "Resonator").ObjectiveTag);
        Assert.Equal("SpireStation", Sites.Single(s => s.Name == "SignalSpire").ObjectiveTag);
        Assert.Equal(CrisisCapability.Psykers, Sites.Single(s => s.Name == "Resonator").Capability);
        Assert.Equal(CrisisCapability.Strikes, Sites.Single(s => s.Name == "SignalSpire").Capability);
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void EveryChoirGarrison_HasAMechanitor_AndMechs(string name)
    {
        foreach (var stage in new[] { CrisisStage.Reclamation, CrisisStage.War })
        {
            var force = ForceMix.Build(ChoirMixes.ForSite(name), stage, 1500, _ => 100);
            Assert.Contains(force, e => ChoirRoster.IsMechanitor(e.Role));
            Assert.Contains(force, e => e.Role == UnitRole.Mech);
        }
    }

    // ---- the cluster siege

    [Theory]
    [InlineData(Side.North)]
    [InlineData(Side.East)]
    [InlineData(Side.South)]
    [InlineData(Side.West)]
    public void TheClusterCamp_FillsEverySlot_WithTheChoirsPieces_AndNothingOverlaps(Side facing)
    {
        var plan = CampLayout.Plan(ChoirPieces, facing);
        Assert.Equal(CampLayout.Slots.Count, plan.Count);
        Assert.All(plan.Where(p => p.Stage >= 2), p => Assert.Equal(Power.Choir.Id, p.Piece.Meta.Power));
        var cells = plan.SelectMany(p => p.Cells()).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    [Fact]
    public void TheImperialCamp_IsUnchanged()
    {
        var plan = CampLayout.Plan(EmpirePieces, Side.North);
        Assert.DoesNotContain(plan, p => p.Piece.Meta.Power == Power.Choir.Id);
        Assert.Contains(plan, p => p.Piece.Name == "RimChronicles_Piece_SiegeUplink");
    }

    [Fact]
    public void TheCluster_GrowsOnATimer_WhileAMechanitorHoldsIt()
    {
        Assert.Equal(BuildStages.First, ClusterSiegeRules.Next(0, 0, mechanitorAtCamp: false)); // it lands at once
        Assert.Equal(1, ClusterSiegeRules.Next(1, ClusterSiegeRules.StageTicks - 1, true));
        Assert.Equal(2, ClusterSiegeRules.Next(1, ClusterSiegeRules.StageTicks, true));
        Assert.Equal(1, ClusterSiegeRules.Next(1, ClusterSiegeRules.StageTicks * 3, false)); // no mechanitor: no growth
        Assert.Equal(BuildStages.Last, ClusterSiegeRules.Next(BuildStages.Last, ClusterSiegeRules.StageTicks * 3, true));
        Assert.True(ClusterSiegeRules.StageTicks * (BuildStages.Last - 1) < SiegeRules.SiegeTicks); // it can finish before it attacks
        Assert.All(Enumerable.Range(1, 5), s => Assert.NotEmpty(ClusterSiegeRules.Landed(s)));
    }

    [Fact]
    public void TheClusterSiege_HasMechanitorsToHoldIt_AndRetreatsAfterLosses()
    {
        var force = ForceMix.Build(ChoirMixes.ClusterSiege, CrisisStage.War, 3000, _ => 100);
        Assert.True(force.Count(e => ChoirRoster.IsMechanitor(e.Role)) >= 2);
        Assert.True(OperationProfiles.For(CrisisOperations.ClusterSiege).RetreatsOnLosses());
        Assert.Equal(OperationProfiles.Siege.Phases, OperationProfiles.For(CrisisOperations.ClusterSiege).Phases);
    }
}
