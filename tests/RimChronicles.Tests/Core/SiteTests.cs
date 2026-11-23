using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class SiteTests
{
    private static readonly IReadOnlyList<PieceSource> Pieces = PieceXml.Load();

    public static TheoryData<string> Archetypes => new(SiteXml.Load().Select(a => a.Name));

    private static float Mid(SiteArchetype a) => (a.MinBudget + a.MaxBudget) / 2;

    // ---- the done-when: 50 seeds per archetype pass

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void FiftySeeds_EachPassesTheValidators_WithoutTheFallback(string name)
    {
        var a = SiteXml.Get(name);
        var firstTry = 0;
        for (var id = 1; id <= 50; id++)
        {
            var plan = SitePlanner.PlanValid(a, Pieces, Mid(a), id, out var attempts);
            Assert.False(plan.Fallback, $"seed {id} fell back");
            Assert.Empty(SiteValidator.Problems(plan));
            firstTry += attempts == 1 ? 1 : 0;
        }

        Assert.True(firstTry >= 45, $"only {firstTry}/50 pass on the first seed");
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void TheFallback_PassesTheValidators_AtEitherEndOfTheBudget(string name)
    {
        var a = SiteXml.Get(name);
        foreach (var points in new[] { a.MinBudget, a.MaxBudget, 10f, 99999f })
        {
            var plan = SitePlanner.Fallback(a, Pieces, points);
            Assert.True(plan.Fallback);
            Assert.Empty(SiteValidator.Problems(plan));
            Assert.Equal(Side.South, plan.Entry);
        }
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void TheSameSite_AlwaysGetsTheSameLayout(string name)
    {
        var a = SiteXml.Get(name);
        var one = SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(42, a.Name));
        var two = SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(42, a.Name));
        Assert.Equal(one.Picture(), two.Picture());
        var others = Enumerable.Range(1, 10).Select(id => SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(id, a.Name)).Picture()).Distinct().Count();
        Assert.True(others >= 8, $"only {others} different layouts from ten seeds");
    }

    // ---- the steps

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void TheObjective_IsInTheDeepestSlot_AndHasItsRoom(string name)
    {
        var a = SiteXml.Get(name);
        for (var id = 1; id <= 20; id++)
        {
            var plan = SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(id, a.Name));
            var objective = plan.Objective!;
            Assert.True(objective.Has(a.ObjectiveTag));
            var slot = plan.Slots.FindIndex(s => s.Contains(objective.Bounds.Centre));
            Assert.Equal(plan.SlotDepth.Max(), plan.SlotDepth[slot]);
        }
    }

    [Fact]
    public void Partition_SlotsTileTheArea_AndAreNeverTooSmall()
    {
        var area = new Rect(5, 5, 42, 38);
        var slots = SitePlanner.Partition(area, 11, new SeededRng(7));
        Assert.True(slots.Count >= 4);
        Assert.Equal(area.W * area.H, slots.Sum(s => s.W * s.H));
        Assert.Equal(area.W * area.H, slots.SelectMany(s => s.Cells()).Distinct().Count());
        Assert.All(slots, s => Assert.True(s.W >= 11 && s.H >= 11, $"{s} is under the minimum"));
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void Wrap_GatesOnTheEntrySide_CoverBesideThem_AndTheWayInStaysOpen(string name)
    {
        var a = SiteXml.Get(name);
        for (var id = 1; id <= 20; id++)
        {
            var plan = SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(id, a.Name));
            Assert.Equal(a.Gates, plan.Gates.Count);
            Assert.All(plan.Gates, g => Assert.Equal(0, SitePlanner.FromEntry(g, plan.Ring, plan.Entry)));
            Assert.Contains(plan.Pieces, p => p.Role == SiteRole.Defense && p.Has(a.DefenseTag));
            var reached = plan.Grid.Flood(plan.Grid.Border(), c => plan.Grid[c] != CellKind.Wall);
            Assert.All(plan.Gates, g => Assert.True(reached[g.X, g.Z]));
            Assert.All(plan.DefenderSpots, c => Assert.True(reached[c.X, c.Z], $"defender spot {c} is shut in"));
            Assert.NotEmpty(plan.DefenderSpots);
        }
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void Budget_PiecesStayWithinTheirShare_TheGarrisonGetsTheRest(string name)
    {
        var a = SiteXml.Get(name);
        foreach (var points in new[] { 0f, Mid(a), 1e6f })
        {
            var plan = SitePlanner.Plan(a, Pieces, points, SitePlanner.SeedFor(3, a.Name));
            Assert.InRange(plan.Budget, a.MinBudget, a.MaxBudget);
            Assert.True(plan.PieceCost <= (plan.Budget * a.PieceShare) + 0.01f);
            Assert.Equal(plan.Budget, plan.PieceCost + plan.DefenderPoints, 2);
        }
    }

    [Fact]
    public void Power_SitesWithTurretsHaveConduitsToThem()
    {
        var a = SiteXml.Get("OrbitalUplink");
        var withTurrets = Enumerable.Range(1, 30).Select(id => SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(id, a.Name)))
            .Where(p => p.Pieces.Any(x => x.Piece.Meta.NeedsPower && !x.Has("CommsRoom"))).ToList();
        Assert.NotEmpty(withTurrets);
        Assert.All(withTurrets, p => Assert.NotEmpty(p.Conduits));
        Assert.All(withTurrets, p => Assert.All(p.Conduits, c => Assert.NotEqual(CellKind.Wall, p.Grid[c])));
    }

    [Fact]
    public void Emplacements_TurnTheirSandbagsTowardTheWayIn()
    {
        var a = SiteXml.Get("OrbitalUplink");
        var checkedAny = false;
        for (var id = 1; id <= 30; id++)
        {
            var plan = SitePlanner.Plan(a, Pieces, Mid(a), SitePlanner.SeedFor(id, a.Name));
            foreach (var pit in plan.Pieces.Where(p => p.Has(SiteTags.Emplacement)))
            {
                checkedAny = true;
                Assert.DoesNotContain(plan.Entry, pit.Spec.DoorSides); // the open side never faces the gates' wall
            }
        }

        Assert.True(checkedAny);
    }

    // ---- the validators on hand-made plans

    private static readonly SiteArchetype Tiny = new()
    {
        Name = "Tiny", Width = 30, Height = 30, ObjectiveTag = "CommsRoom", MinBudget = 100, MaxBudget = 1000,
        Required = [new PieceRequest(SiteTags.Power)],
    };

    private static SitePlan Hand(bool gate = true, float budget = 500)
    {
        var plan = new SitePlan
        {
            Archetype = Tiny, Seed = 0, Grid = new CellGrid(30, 30), Ring = new Rect(2, 2, 26, 26), Entry = Side.South, Budget = budget,
        };
        foreach (var c in plan.Ring.Cells().Where(c => c.X == 2 || c.X == 27 || c.Z == 2 || c.Z == 27))
        {
            var isGate = gate && c == new Cell(15, 2);
            plan.Grid[c] = isGate ? CellKind.Door : CellKind.Wall;
            (isGate ? plan.Gates : plan.Walls).Add(c);
        }

        void Put(string name, int x, int z, string role)
        {
            var spec = PieceSpec.Build(Pieces.Single(p => p.Name == name), Side.North);
            plan.Grid.Place(spec, new Cell(x, z), plan.Pieces.Count);
            plan.Pieces.Add(new SitePlacement(plan.Pieces.Count, spec, new Cell(x, z), role));
        }

        Put("RimChronicles_Piece_CommandRoom", 10, 16, SiteRole.Objective);
        Put("RimChronicles_Piece_PowerYard", 5, 4, SiteRole.Required);
        return plan;
    }

    [Fact]
    public void Validators_AHandMadePlanWithAConduitPasses()
    {
        var plan = Hand();
        Assert.Contains(SiteValidator.Problems(plan), p => p.Contains("has no power"));
        foreach (var c in Enumerable.Range(9, 10).Select(z => new Cell(12, z)))
        {
            plan.Grid.SetConduit(c);
            plan.Conduits.Add(c);
        }

        Assert.Empty(SiteValidator.Problems(plan));
    }

    [Fact]
    public void Validators_CatchAShutSite_ASealedRoom_AMissingGenerator_AndTheBudget()
    {
        var shut = Hand(gate: false);
        var problems = SiteValidator.Problems(shut);
        Assert.Contains(problems, p => p.Contains("can't be reached from the map edge"));
        Assert.Contains(problems, p => p.Contains("sealed room"));
        Assert.Contains(problems, p => p == "no gate");

        var noGenerator = Hand();
        noGenerator.Pieces.RemoveAll(p => p.Has(SiteTags.Power));
        problems = SiteValidator.Problems(noGenerator);
        Assert.Contains(problems, p => p.Contains("no generator"));
        Assert.Contains(problems, p => p.StartsWith("required Power"));

        Assert.Contains(SiteValidator.Problems(Hand(budget: 5000)), p => p.StartsWith("budget"));
        Assert.Contains(SiteValidator.Problems(Hand(budget: 10)), p => p.StartsWith("budget"));
    }

    [Fact]
    public void Validators_ASealedPieceMayBeShut()
    {
        var vault = Pieces.Single(p => p.Name == "RimChronicles_Piece_Barracks") with
        {
            Name = "Vault", // the barracks with its door walled up
            Things = Pieces.Single(p => p.Name == "RimChronicles_Piece_Barracks").Things.Select(t => t.Def == "Door" ? t with { Kind = CellKind.Wall } : t).ToList(),
        };
        var plan = Hand();
        foreach (var c in Enumerable.Range(9, 10).Select(z => new Cell(12, z)))
        {
            plan.Grid.SetConduit(c);
            plan.Conduits.Add(c);
        }

        var spec = PieceSpec.Build(vault with { Meta = vault.Meta with { Tags = ["Room"] } }, Side.North);
        plan.Grid.Place(spec, new Cell(18, 5), plan.Pieces.Count);
        plan.Pieces.Add(new SitePlacement(plan.Pieces.Count, spec, new Cell(18, 5), SiteRole.Optional));
        Assert.Contains(SiteValidator.Problems(plan), p => p.Contains("sealed room"));

        var sealedSpec = PieceSpec.Build(vault with { Meta = vault.Meta with { Tags = ["Room", SiteTags.Sealed] } }, Side.North);
        plan.Pieces[^1] = new SitePlacement(plan.Pieces.Count - 1, sealedSpec, new Cell(18, 5), SiteRole.Optional);
        Assert.Empty(SiteValidator.Problems(plan));
    }

    [Fact]
    public void PowerCells_FollowThePieceAsItTurns()
    {
        var nest = Pieces.Single(p => p.Name == "RimChronicles_Piece_TurretNest");
        foreach (var side in Enum.GetValues<Side>())
        {
            var spec = PieceSpec.Build(nest, side);
            Assert.Single(spec.PowerCells);
            Assert.Equal(CellKind.PassThrough, spec[spec.PowerCells[0]]);
            Assert.Equal(new Cell(2, 2), spec.PowerCells[0]); // the 1×1 turret stays in the middle of the 5×5 nest
        }
    }

    // ---- which site, and who guards it

    [Fact]
    public void Archetypes_OutpostsFromTheStart_RelaysAndUplinksFromReclamation()
    {
        var all = SiteXml.Load();
        Assert.Empty(SiteArchetypes.Allowed(all, CrisisStage.None));
        Assert.Equal(["ForwardOutpost"], SiteArchetypes.Allowed(all, CrisisStage.Probing).Select(a => a.Name));
        Assert.Equal(["ForwardOutpost", "OrbitalUplink", "PsychicRelay"], SiteArchetypes.Allowed(all, CrisisStage.Reclamation).Select(a => a.Name));
        Assert.Null(SiteArchetypes.Pick([], 0.5));
        Assert.Equal("ForwardOutpost", SiteArchetypes.Pick(SiteArchetypes.Allowed(all, CrisisStage.War), 0.0)!.Name);
        Assert.Equal("PsychicRelay", SiteArchetypes.Pick(SiteArchetypes.Allowed(all, CrisisStage.War), 0.99)!.Name);
        Assert.All(all, a => Assert.StartsWith("Site:", a.Id));
        Assert.Equal(CrisisCapability.Psykers, SiteXml.Get("PsychicRelay").Capability);
        Assert.Equal(CrisisCapability.Strikes, SiteXml.Get("OrbitalUplink").Capability);
        Assert.Null(SiteXml.Get("ForwardOutpost").Capability);
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void Garrisons_PraetoriansAndWhatTheSiteGuards(string name)
    {
        var mix = ForceMix.ForSite(name);
        Assert.Contains(mix, e => e.Role == UnitRole.Praetorian);
        var force = ForceMix.Build(mix, CrisisStage.War, 3000, e => 150);
        Assert.True(force.Count >= 10);
        if (name == "PsychicRelay")
        {
            Assert.Contains(force, e => e.Role == UnitRole.Adept);
        }

        if (name == "OrbitalUplink")
        {
            Assert.Contains(force, e => e.Role == UnitRole.Herald);
        }
    }
}
