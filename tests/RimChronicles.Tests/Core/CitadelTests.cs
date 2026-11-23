using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class CitadelTests
{
    private static readonly IReadOnlyList<PieceSource> Pieces = PieceXml.Load();

    /// <summary>The done-when: validators pass on 20 seeds, with every mix of relays, uplinks and the side entrance.</summary>
    [Fact]
    public void TwentySeeds_PassEveryRingsValidators_AndTheWholeMapChecks()
    {
        for (var id = 1; id <= 20; id++)
        {
            var options = new CitadelOptions { SideEntrance = id % 2 == 0, Relays = id % 3, Uplinks = id % 2 };
            var plan = CitadelPlanner.Plan(Pieces, options, id);
            Assert.False(plan.Fallback, $"seed {id} fell back");
            Assert.Empty(CitadelPlanner.Problems(plan));
        }
    }

    [Theory]
    [InlineData(225)]
    [InlineData(275)]
    [InlineData(350)]
    public void EveryMapSize_Fits(int size)
    {
        var plan = CitadelPlanner.Plan(Pieces, new CitadelOptions { MapSize = size }, 3);
        Assert.Equal(size, plan.Options.MapSize);
        Assert.Empty(CitadelPlanner.Problems(plan));
        Assert.All(plan.Rings, r => Assert.True(r.At.X + r.Plan.Grid.Width <= size && r.At.Z + r.Plan.Grid.Height <= size));
    }

    [Fact]
    public void MapSizes_AreClamped()
    {
        Assert.Equal(CitadelRings.MinMapSize, CitadelPlanner.Plan(Pieces, new CitadelOptions { MapSize = 100 }, 1).Options.MapSize);
        Assert.Equal(CitadelRings.MaxMapSize, CitadelPlanner.Plan(Pieces, new CitadelOptions { MapSize = 999 }, 1).Options.MapSize);
    }

    [Fact]
    public void TheRings_Nest_OuterWorksCourtyardKeep()
    {
        var plan = CitadelPlanner.Plan(Pieces, new CitadelOptions(), 5);
        var outer = plan.Outer.RingOnMap;
        var courtyard = plan.Courtyard.RingOnMap;
        var keep = plan.Keep.RingOnMap;
        Assert.True(outer.X < courtyard.X && courtyard.MaxX < outer.MaxX);
        Assert.True(courtyard.X < keep.X && keep.MaxX < courtyard.MaxX);
        Assert.Equal(2, plan.RingOf(keep.Centre));
        Assert.Equal(1, plan.RingOf(new Cell(courtyard.X + 1, courtyard.Z + 1)));
        Assert.Equal(0, plan.RingOf(new Cell(outer.X + 1, outer.Z + 1)));
        Assert.Equal(-1, plan.RingOf(new Cell(0, 0)));

        // No outer works piece or wall lands in the courtyard, and none of the courtyard's in the keep.
        foreach (var (inner, outerRing) in new[] { (plan.Courtyard, plan.Outer), (plan.Keep, plan.Courtyard) })
        {
            var area = new Rect(inner.At.X, inner.At.Z, inner.Plan.Grid.Width, inner.Plan.Grid.Height);
            Assert.DoesNotContain(outerRing.Plan.Pieces.SelectMany(p => p.Cells()).Select(outerRing.ToMap), area.Contains);
            Assert.DoesNotContain(outerRing.Plan.Walls.Concat(outerRing.Plan.Conduits).Select(outerRing.ToMap), area.Contains);
            Assert.DoesNotContain(outerRing.Plan.DefenderSpots.Select(outerRing.ToMap), area.Contains);
        }
    }

    [Fact]
    public void TheKeep_HoldsTheThroneRoom_AndTheCourtyardItsRelaysAndUplinks()
    {
        var plan = CitadelPlanner.Plan(Pieces, new CitadelOptions { Relays = 2, Uplinks = 1 }, 8);
        Assert.True(plan.Throne!.Has("Throne"));
        Assert.Equal(2, plan.Relays.Count());
        Assert.Single(plan.Courtyard.Plan.Pieces, p => p.Has(CitadelRings.UplinkTag));
        Assert.Contains(plan.Keep.Plan.Pieces, p => p.Has("Records"));
        Assert.Contains(plan.Outer.Plan.Pieces, p => p.Has("Artillery"));
        Assert.Equal(3, plan.Outer.Plan.Gates.Count);
    }

    [Fact]
    public void DestroyedSites_TakeRelaysAndUplinksFromTheCourtyard()
    {
        Assert.Equal(2, CitadelRings.Relays(0));
        Assert.Equal(1, CitadelRings.Relays(1));
        Assert.Equal(0, CitadelRings.Relays(5));
        Assert.Equal(0, CitadelRings.Uplinks(1));
        var none = CitadelPlanner.Plan(Pieces, new CitadelOptions { Relays = 0, Uplinks = 0 }, 4);
        Assert.Empty(none.Relays);
        Assert.DoesNotContain(none.Courtyard.Plan.Pieces, p => p.Has(CitadelRings.UplinkTag));
        Assert.Empty(CitadelPlanner.Problems(none));
    }

    [Fact]
    public void TheSideEntrance_OpensTheKeepsFarSide_AndOnlyWithIntel()
    {
        for (var id = 1; id <= 10; id++)
        {
            var hidden = CitadelPlanner.Plan(Pieces, new CitadelOptions(), id);
            Assert.Null(hidden.SideGate);
            Assert.Single(hidden.Keep.Plan.Gates);

            var known = CitadelPlanner.Plan(Pieces, new CitadelOptions { SideEntrance = true }, id);
            Assert.Equal(2, known.Keep.Plan.Gates.Count);
            var side = known.Keep.Plan.Gates[1];
            Assert.Equal(0, SitePlanner.FromEntry(side, known.Keep.Plan.Ring, PieceRotation.Turn(known.Keep.Plan.Entry, Side.South)));
            Assert.Equal(known.Keep.ToMap(side), known.SideGate);
            var grid = known.Combined();
            Assert.Equal(CellKind.Door, grid[known.SideGate!.Value]);
        }
    }

    [Fact]
    public void Validators_CatchAThroneRoomShutAway()
    {
        var plan = CitadelPlanner.Plan(Pieces, new CitadelOptions(), 2);
        foreach (var gate in plan.Keep.Plan.Gates)
        {
            plan.Keep.Plan.Grid[gate] = CellKind.Wall;
        }

        Assert.Contains(CitadelPlanner.Problems(plan), p => p.Contains("throne room can't be reached"));
    }

    [Fact]
    public void TheSameCitadel_EveryTime()
    {
        var a = CitadelPlanner.Plan(Pieces, new CitadelOptions(), 11);
        var b = CitadelPlanner.Plan(Pieces, new CitadelOptions(), 11);
        Assert.Equal(a.Rings.Select(r => r.Plan.Picture()), b.Rings.Select(r => r.Plan.Picture()));
    }

    [Fact]
    public void Garrisons_StayNearSixty_AndEachRingHasItsKind()
    {
        Assert.InRange(CitadelRings.MaxDefenders.Sum(), 50, 65);
        Assert.Equal(1f, CitadelRings.Shares.Sum(), 3);
        for (var ring = 0; ring < 3; ring++)
        {
            var force = ForceMix.Build(ForceMix.Citadel(ring), CrisisStage.Finale, 100000, e => 150, CitadelRings.MaxDefenders[ring]);
            Assert.Equal(CitadelRings.MaxDefenders[ring], force.Count);
            Assert.Contains(force, e => e.Role == UnitRole.Praetorian);
        }

        Assert.Contains(ForceMix.Citadel(2), e => e.Specialty == Specialty.Mind);
        Assert.Contains(ForceMix.Citadel(1), e => e.Role == UnitRole.Herald);
    }
}
