using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class SiegeTests
{
    private static SpotFeatures Spot(int x = 0, float distance = 45, float margin = 20, float cover = 0.2f, float? entrance = null,
        int flanks = 0, int mortars = 0, float ground = 0.9f, bool reachable = true, bool inColony = false) => new()
    {
        Cell = new Cell(x, 0),
        DistanceToColony = distance,
        TurretMargin = margin,
        CoverShare = cover,
        EntranceAngle = entrance,
        OpenFlanks = flanks,
        ColonyMortarsInReach = mortars,
        BuildableShare = ground,
        Reachable = reachable,
        InColony = inColony,
    };

    // ---- spot scoring

    [Fact]
    public void Score_RulesOutSpotsTheCampCantUse()
    {
        Assert.True(SpotScorer.Score(Spot()).Ok);
        Assert.Contains("turret", SpotScorer.Score(Spot(margin: 1)).Rejected);
        Assert.Contains("too close", SpotScorer.Score(Spot(distance: 30)).Rejected); // inside its own mortars' minimum range
        Assert.Contains("too far", SpotScorer.Score(Spot(distance: 95)).Rejected);
        Assert.Contains("path", SpotScorer.Score(Spot(reachable: false)).Rejected);
        Assert.Contains("inside", SpotScorer.Score(Spot(inColony: true)).Rejected);
        Assert.Contains("ground", SpotScorer.Score(Spot(ground: 0.4f)).Rejected);
    }

    [Fact]
    public void Score_PrefersCover_Closeness_TheProbedEntrance_AndCoveredFlanks()
    {
        float S(SpotFeatures s) => SpotScorer.Score(s).Total;
        Assert.True(S(Spot(cover: 0.3f)) > S(Spot(cover: 0.05f)));
        Assert.True(S(Spot(distance: 40)) > S(Spot(distance: 70)));
        Assert.True(S(Spot(entrance: 10)) > S(Spot(entrance: 80)));
        Assert.True(S(Spot(entrance: 10)) > S(Spot())); // facing a known door beats knowing none
        Assert.True(S(Spot(flanks: 0)) > S(Spot(flanks: 1)));
        Assert.True(S(Spot(flanks: 1)) > S(Spot(flanks: 2)));
        Assert.True(S(Spot(mortars: 0)) > S(Spot(mortars: 2)));
    }

    [Fact]
    public void Score_CoverCountsInFullFromTheThreshold_AndMortarsAreCapped()
    {
        Assert.Equal(SpotScorer.Score(Spot(cover: 0.35f)).Total, SpotScorer.Score(Spot(cover: 0.9f)).Total, 3);
        Assert.Equal(SpotScorer.Score(Spot(mortars: 3)).Total, SpotScorer.Score(Spot(mortars: 9)).Total, 3);
    }

    [Fact]
    public void Rank_BestFirst_RuledOutLast_TiesByCell_AndBestIsNullWhenNothingQualifies()
    {
        var ranked = SpotScorer.Rank([Spot(x: 3, margin: 0), Spot(x: 2), Spot(x: 1), Spot(x: 4, cover: 0.4f)]);
        Assert.Equal([4, 1, 2, 3], ranked.Select(s => s.Spot.Cell.X));
        Assert.False(ranked[^1].Ok);
        Assert.Null(SpotScorer.Best([Spot(margin: 0), Spot(reachable: false)]));
        Assert.Contains("ruled out", ranked[^1].ToString());
        Assert.Contains("cover", ranked[0].ToString());
    }

    [Theory]
    [InlineData(0, 0, 0, 40, Side.North)]
    [InlineData(0, 0, 40, 5, Side.East)]
    [InlineData(0, 0, 3, -40, Side.South)]
    [InlineData(10, 10, -30, 0, Side.West)]
    public void Facing_TheCampFrontPointsAtTheColony(int cx, int cz, int colonyX, int colonyZ, Side expected) =>
        Assert.Equal(expected, SpotScorer.Facing(new Cell(cx, cz), new Cell(colonyX, colonyZ)));

    // ---- build stages

    [Fact]
    public void Stages_StartAtOne_MoveOnOnlyWhenNothingIsLeftToBuild_AndStopAtFive()
    {
        Assert.Equal(1, BuildStages.Next(0, 0));
        Assert.Equal(1, BuildStages.Next(1, 3));
        Assert.Equal(2, BuildStages.Next(1, 0));
        Assert.Equal(5, BuildStages.Next(5, 0));
        Assert.False(BuildStages.Done(4, 0));
        Assert.False(BuildStages.Done(5, 1));
        Assert.True(BuildStages.Done(5, 0));
        Assert.Equal(5, BuildStages.All.Count);
        Assert.Equal([SiegeUnlock.Camp, SiegeUnlock.Bombardment, SiegeUnlock.OrbitalStrikes, SiegeUnlock.ShieldAndRelay, SiegeUnlock.Reinforcements],
            BuildStages.All.Select(s => s.Unlock));
    }

    /// <summary>Tech spec §6 row 5: stage N+1 blueprints never exist before stage N is built. A camp is played out with
    /// builders finishing one piece per tick; the next stage's pieces must never be placed while any are left.</summary>
    [Fact]
    public void Stages_TheNextStagesBlueprintsNeverExistBeforeThisOneIsBuilt()
    {
        var plan = CampLayout.Plan(PieceXml.For("empire"), Side.North);
        var stage = 0;
        var pending = new List<CampPlacement>();
        var placedStages = new List<int>();
        for (var tick = 0; tick < 100 && !BuildStages.Done(stage, pending.Count); tick++)
        {
            var next = BuildStages.Next(stage, pending.Count);
            if (next != stage)
            {
                Assert.Empty(pending);
                stage = next;
                pending = plan.Where(p => p.Stage == stage).ToList();
                placedStages.Add(stage);
            }
            else if (pending.Count > 0)
            {
                pending.RemoveAt(0); // one piece built
            }
        }

        Assert.Equal([1, 2, 3, 4, 5], placedStages);
    }

    // ---- camp layout

    public static TheoryData<Side> Facings => new(Enum.GetValues<Side>());

    [Theory]
    [MemberData(nameof(Facings))]
    public void Layout_EveryStageHasItsPieces_NoneOverlap_AllWithinTheRadius(Side facing)
    {
        var plan = CampLayout.Plan(PieceXml.For("empire"), facing);
        Assert.Equal(CampLayout.Slots.Count, plan.Count); // the repo has a piece for every slot
        foreach (var stage in Enumerable.Range(BuildStages.First, BuildStages.Last))
        {
            Assert.Contains(plan, p => p.Stage == stage);
        }

        Assert.All(plan, p => Assert.Equal(p.Stage, p.Piece.Meta.BuildOrder));
        var cells = plan.SelectMany(p => p.Cells()).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, c => Assert.True(Math.Max(Math.Abs(c.X), Math.Abs(c.Z)) <= CampLayout.Radius, $"{c} is outside the camp"));
        Assert.All(plan, p =>
        {
            var spec = PieceSpec.Build(p.Piece, p.Rotation);
            Assert.Equal((spec.SizeX, spec.SizeZ), (p.SizeX, p.SizeZ));
        });
    }

    [Theory]
    [MemberData(nameof(Facings))]
    public void Layout_TheSandbagsFaceTheColony_MortarsBehindThem_OpenToTheRear(Side facing)
    {
        var plan = CampLayout.Plan(PieceXml.For("empire"), facing);
        var toward = CampLayout.Turn(new Cell(0, 1), facing);
        float Ahead(CampPlacement p) => p.Cells().Max(c => (c.X * toward.X) + (c.Z * toward.Z));
        var lines = plan.Where(p => p.Piece.Meta.Tags.Contains("Line")).Max(Ahead);
        Assert.All(plan.Where(p => !p.Piece.Meta.Tags.Contains("Line")), p => Assert.True(Ahead(p) < lines, $"{p.Piece.Name} is ahead of the sandbags"));
        var rear = PieceRotation.Turn(Side.South, facing);
        Assert.All(plan.Where(p => p.Piece.Meta.Tags.Contains("Artillery")),
            p => Assert.Equal([rear], PieceSpec.Build(p.Piece, p.Rotation).DoorSides));
    }

    [Fact]
    public void Layout_ASlotWithNoPieceIsLeftOut()
    {
        var onlyLines = PieceXml.For("empire").Where(p => p.Meta.Tags.Contains("Line")).ToList();
        var plan = CampLayout.Plan(onlyLines, Side.North);
        Assert.All(plan, p => Assert.Equal(1, p.Stage));
        Assert.Equal(CampLayout.Slots.Count(s => s.Tag == "Line"), plan.Count);
    }

    [Fact]
    public void Layout_TurningIsAQuarterTurnClockwise()
    {
        var c = new Cell(2, 5);
        Assert.Equal(c, CampLayout.Turn(c, Side.North));
        Assert.Equal(new Cell(5, -2), CampLayout.Turn(c, Side.East));
        Assert.Equal(new Cell(-2, -5), CampLayout.Turn(c, Side.South));
        Assert.Equal(new Cell(-5, 2), CampLayout.Turn(c, Side.West));
    }

    // ---- goals

    [Fact]
    public void Goals_OnlyThoseTheColonyGivesATargetFor()
    {
        Assert.Equal([SiegeGoal.BreakWalls, SiegeGoal.Starve, SiegeGoal.KillLeader], SiegeGoals.Allowed(new SiegeTargets(true, true, true)).Select(g => g.Goal));
        Assert.Equal([SiegeGoal.Starve], SiegeGoals.Allowed(new SiegeTargets(false, true, false)).Select(g => g.Goal));
        Assert.Equal(SiegeGoal.None, SiegeGoals.Pick(SiegeGoals.Allowed(new SiegeTargets(false, false, false)), 0.5));
        Assert.All(SiegeGoals.All, g => Assert.StartsWith("Goal:", g.Id));
    }

    [Theory]
    [InlineData(0.0, SiegeGoal.BreakWalls)]
    [InlineData(0.49, SiegeGoal.BreakWalls)]
    [InlineData(0.5, SiegeGoal.Starve)]
    [InlineData(0.8, SiegeGoal.KillLeader)]
    [InlineData(1.0, SiegeGoal.KillLeader)]
    public void Goals_TheWeightedPick(double roll, SiegeGoal expected) =>
        Assert.Equal(expected, SiegeGoals.Pick(SiegeGoals.All, roll)); // weights 2 : 1 : 1

    [Theory]
    [InlineData("Goal:Starve", SiegeGoal.Starve)]
    [InlineData("KillLeader", SiegeGoal.KillLeader)]
    [InlineData("Goal:Nonsense", SiegeGoal.None)]
    [InlineData(null, SiegeGoal.None)]
    public void Goals_ParseTheirIds(string? text, SiegeGoal expected) => Assert.Equal(expected, SiegeGoals.Parse(text));

    [Fact]
    public void Goals_KillLeaderAimsTheStrikesAtTheLeader_OverATurret()
    {
        var turret = new StrikeCandidate { Cell = new Cell(0, 0), Kind = StrikeTargetKind.Turret, Sighted = true };
        var leader = new StrikeCandidate { Cell = new Cell(20, 0), Kind = StrikeTargetKind.Leader, Value = 1.5f, Sighted = true };
        Assert.Equal(StrikeTargetKind.Turret, StrikeRules.Pick([turret, leader], new StrikeState())!.Value.Target.Kind);
        Assert.Equal(StrikeTargetKind.Leader, StrikeRules.Pick([turret, leader], new StrikeState(), SiegeGoal.KillLeader)!.Value.Target.Kind);
    }

    // ---- builders, breaking the siege, waves

    [Fact]
    public void Builders_AShare_AtLeastTwo_OnePerMortar_NoMoreThanCanBuild()
    {
        Assert.Equal(4, SiegeRules.Builders(10, 10, 2));
        Assert.Equal(2, SiegeRules.Builders(3, 3, 0));
        Assert.Equal(3, SiegeRules.Builders(4, 4, 3));
        Assert.Equal(1, SiegeRules.Builders(10, 1, 2));
    }

    [Fact]
    public void BuildersFalling_AttacksWhileStrong_LeavesWhenSpent()
    {
        Assert.Equal(AfterBuilders.Hold, SiegeRules.AfterBuildersFall(6, 3, 0.2f)); // half left still holds
        Assert.Equal(AfterBuilders.Assault, SiegeRules.AfterBuildersFall(6, 2, 0.2f));
        Assert.Equal(AfterBuilders.Assault, SiegeRules.AfterBuildersFall(3, 1, 0.2f));
        Assert.Equal(AfterBuilders.Retreat, SiegeRules.AfterBuildersFall(6, 1, 0.6f));
        Assert.Equal(AfterBuilders.Hold, SiegeRules.AfterBuildersFall(0, 0, 0.9f)); // no builders ever: nothing to lose
    }

    [Fact]
    public void Waves_OnlyWithABeacon_AReserve_TheIntervalPassed_AndUnderTheCap()
    {
        Assert.True(SiegeRules.WaveDue(true, 0, 900, SiegeRules.WaveTicks));
        Assert.False(SiegeRules.WaveDue(false, 0, 900, SiegeRules.WaveTicks));
        Assert.False(SiegeRules.WaveDue(true, 0, 0, SiegeRules.WaveTicks));
        Assert.False(SiegeRules.WaveDue(true, 0, 900, SiegeRules.WaveTicks - 1));
        Assert.False(SiegeRules.WaveDue(true, SiegeRules.MaxWaves, 900, SiegeRules.WaveTicks));
        Assert.Equal(300, SiegeRules.WavePoints(900, 0));
        Assert.Equal(900, SiegeRules.WavePoints(900, SiegeRules.MaxWaves - 1));
        Assert.Equal(0, SiegeRules.WavePoints(900, SiegeRules.MaxWaves));
    }

    // ---- the siege as an operation

    [Fact]
    public void Profile_WalksIn_DigsIn_ThenAttacks_AndCanAlwaysLeave()
    {
        var siege = OperationProfiles.For(CrisisOperations.SiegeWithBombardment);
        Assert.Same(OperationProfiles.Siege, siege);
        Assert.Equal(PhaseKind.Travel, siege.Find(siege.Start)!.Kind);
        Assert.True(siege.RetreatsOnLosses());
        var dig = siege.Phases.Single(p => p.Kind == PhaseKind.Siege);
        var toAssault = dig.Links.Where(l => siege.Find(l.To)!.Kind == PhaseKind.Assault).ToList();
        Assert.Contains(toAssault, l => l.Trigger == PhaseTrigger.BuildersLost);
        Assert.Contains(toAssault, l => l.Trigger == PhaseTrigger.TimeUp && l.Value == SiegeRules.SiegeTicks);
        Assert.All(toAssault, l => Assert.Equal(OperationOutcome.Running, l.Outcome));

        // Losses are checked before the builders, so a spent siege leaves rather than attacking.
        var losses = dig.Links.ToList().FindIndex(l => l.Trigger == PhaseTrigger.LossesOver);
        var builders = dig.Links.ToList().FindIndex(l => l.Trigger == PhaseTrigger.BuildersLost);
        Assert.True(losses < builders);
        Assert.True(siege.ReserveShare > 0); // the beacon's waves
    }

    [Fact]
    public void Mix_PraetoriansToBuild_AHeraldFromStage3()
    {
        var force = ForceMix.Build(ForceMix.For(CrisisOperations.SiegeWithBombardment), CrisisStage.War, 4000, e => e.Role == UnitRole.Adept ? 160 : 150);
        Assert.True(force.Count(e => e.Role == UnitRole.Praetorian) >= force.Count / 2);
        Assert.Contains(force, e => e.Role == UnitRole.Herald);
        Assert.Contains(force, e => e.Specialty == Specialty.Control);
    }
}
