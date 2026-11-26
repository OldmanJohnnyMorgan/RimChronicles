using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The Hollow Choir's Cathedral (War in Heaven W9): the Citadel's three rings with the Choir's rooms, its
/// resonators holding up the sanctum's shield, and the First Voice's bossgroup guard.</summary>
public class CathedralTests
{
    private static readonly IReadOnlyList<PieceSource> Pieces = PieceXml.For("choir");

    private static CitadelOptions Cathedral(int relays = CitadelRings.BaseRelays, int uplinks = CitadelRings.BaseUplinks) =>
        new() { Power = "choir", Relays = relays, Uplinks = uplinks };

    [Fact]
    public void TwentySeeds_PassEveryRingsValidators_AndTheWholeMapChecks()
    {
        for (var id = 1; id <= 20; id++)
        {
            var plan = CitadelPlanner.Plan(Pieces, Cathedral(id % 3, id % 2), id);
            Assert.False(plan.Fallback, $"seed {id} fell back");
            Assert.Empty(CitadelPlanner.Problems(plan));
        }
    }

    [Fact]
    public void TheSanctum_IsTheKeepsHeart_AndResonatorsHoldUpItsShield()
    {
        var plan = CitadelPlanner.Plan(Pieces, Cathedral(), 4);
        Assert.True(plan.Throne!.Has(CitadelRings.SanctumTag));
        Assert.Equal(2, plan.Relays.Count());
        Assert.All(plan.Relays, r => Assert.True(r.Has(CitadelRings.ChoirRelayTag)));
        Assert.Contains(plan.Courtyard.Plan.Pieces, p => p.Has(CitadelRings.ChoirUplinkTag));
    }

    [Fact]
    public void TheCathedral_TakesNoImperialPiece_AndTheCitadelNoChoirPiece()
    {
        var cathedral = CitadelPlanner.Plan(Pieces, Cathedral(), 9);
        Assert.DoesNotContain(cathedral.Rings.SelectMany(r => r.Plan.Pieces), p => p.Piece.Meta.Power == "empire");
        var citadel = CitadelPlanner.Plan(PieceXml.For("empire"), new CitadelOptions(), 9);
        Assert.DoesNotContain(citadel.Rings.SelectMany(r => r.Plan.Pieces), p => p.Piece.Meta.Power == "choir");
        Assert.True(citadel.Throne!.Has(CitadelRings.ThroneTag));
    }

    [Fact]
    public void ResonatorSitesDestroyed_LeaveTheSanctumUnshielded()
    {
        var plan = CitadelPlanner.Plan(Pieces, Cathedral(relays: CitadelRings.Relays(2)), 2);
        Assert.Empty(plan.Relays);
        Assert.Empty(CitadelPlanner.Problems(plan));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryRingsGarrison_HasACommander_AndTheSanctumABossgroup(int ring)
    {
        var force = ForceMix.Build(ChoirMixes.Cathedral(ring), CrisisStage.Finale, 4000, _ => 100);
        Assert.Contains(force, e => ChoirRoster.IsMechanitor(e.Role));
        Assert.Contains(force, e => e.Role == UnitRole.Mech);
        Assert.Equal(ring == 2, force.Any(e => e.Mech == MechClass.Boss));
    }

    [Fact]
    public void TheRingsHaveTheChoirsNames() =>
        Assert.Equal(["outer works", "cloister", "sanctum"], CitadelRings.NamesFor("choir"));
}
