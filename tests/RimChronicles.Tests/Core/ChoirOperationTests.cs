using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;

namespace RimChronicles.Tests.Core;

/// <summary>The Hollow Choir's crisis in game (War in Heaven W7): its forces, the commander AI, cluster drops,
/// burrowing and the Hum, as plain rules.</summary>
public class ChoirOperationTests
{
    public static TheoryData<string> Operations => new(CrisisOperations.Choir.Select(o => o.Step));

    private static readonly Func<string, bool> AllExist = _ => true;

    // ---- shapes: fairness rule 6, as for the Empire

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryChoirOperation_ButTheFinalChorus_RetreatsAfterLosses(string operation)
    {
        var profile = OperationProfiles.For(operation);
        Assert.Equal(operation, profile.Operation);
        Assert.Equal(operation != CrisisOperations.FinalChorus, profile.RetreatsOnLosses());
        Assert.All(profile.Reachable().Where(p => p.Kind != PhaseKind.Retreat),
            p => Assert.Contains(p.Links, l => l.Trigger == PhaseTrigger.Abandoned && profile.Find(l.To)!.Kind == PhaseKind.Retreat));
    }

    [Fact]
    public void ChoirOperations_MirrorTheImperialShapes()
    {
        Assert.Equal(OperationProfiles.ProbingRaid.Phases, OperationProfiles.For(CrisisOperations.ProbingSwarm).Phases);
        Assert.Equal(OperationProfiles.Decapitation.Phases, OperationProfiles.For(CrisisOperations.Burrowing).Phases);
        Assert.Equal(OperationProfiles.FinalAssault.Phases, OperationProfiles.For(CrisisOperations.FinalChorus).Phases);
        Assert.Equal(PhaseKind.Assault, OperationProfiles.For(CrisisOperations.TunnelBreach).Find("assault")!.Kind);
        Assert.Equal(CrisisOperations.ProbingRaid, OperationProfiles.ProbingRaid.Operation); // the Empire's unchanged
    }

    // ---- forces

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryChoirMix_BringsAMechanitor_AndMechs_AtEveryStageItRuns(string operation)
    {
        var spec = CrisisOperations.Find(operation, Power.Choir)!;
        foreach (var stage in new[] { CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale }.Where(s => s >= spec.MinStage))
        {
            var force = ForceMix.Build(ChoirMixes.For(operation), stage, 2500, _ => 100);
            Assert.Contains(force, e => ChoirRoster.IsMechanitor(e.Role));
            Assert.Contains(force, e => e.Role == UnitRole.Mech && e.Mech != MechClass.None);
            Assert.True(force.Count(e => e.Role == UnitRole.Mech) > force.Count(e => ChoirRoster.IsMechanitor(e.Role)));
        }
    }

    [Fact]
    public void BossMechs_OnlyInTheFinale()
    {
        Assert.DoesNotContain(ForceMix.Build(ChoirMixes.FinalChorus, CrisisStage.War, 5000, _ => 100), e => e.Mech == MechClass.Boss);
        Assert.Contains(ForceMix.Build(ChoirMixes.FinalChorus, CrisisStage.Finale, 5000, _ => 100), e => e.Mech == MechClass.Boss);
    }

    [Fact]
    public void Roster_PicksByStage_AndFallsBackWhenAKindIsMissing()
    {
        Assert.All(Enumerable.Range(0, 20), i =>
            Assert.Contains(ChoirRoster.Pick(MechClass.Light, CrisisStage.Probing, i / 20.0, AllExist), new[] { "Mech_Militor", "Mech_Scorcher", "Mech_Pikeman" }));
        Assert.Equal("Mech_Lancer", ChoirRoster.Pick(MechClass.Tunneler, CrisisStage.Probing, 0.5, d => d != "Mech_Tunneler"));
        Assert.Equal("Mech_CentipedeGunner", ChoirRoster.Pick(MechClass.Boss, CrisisStage.Finale, 0, d => !d.StartsWith("Mech_W") && !d.StartsWith("Mech_D") && !d.StartsWith("Mech_A")));
        Assert.Null(ChoirRoster.Pick(MechClass.Line, CrisisStage.War, 0.5, _ => false));
        Assert.Equal(ChoirRoster.Pick(MechClass.Line, CrisisStage.War, 0.42, AllExist), ChoirRoster.Pick(MechClass.Line, CrisisStage.War, 0.42, AllExist));
    }

    [Fact]
    public void Assign_EachMechanitorTakesItsShare_TheRestFollowTheStrongest()
    {
        var owners = ChoirRoster.Assign([UnitRole.Chorister, UnitRole.Choirmaster], 16);
        Assert.Equal(8 + 3, owners.Count(o => o == 1)); // the Choirmaster's 8, plus the 3 left over after the Chorister's 5
        Assert.Equal(5, owners.Count(o => o == 0));
        Assert.All(ChoirRoster.Assign([], 3), o => Assert.Equal(-1, o));
        Assert.Empty(ChoirRoster.Assign([UnitRole.Cantor], 0));
    }

    // ---- the commander AI

    private static BattleUnit Unit(int id, BattleSide side, int x, int z, UnitRole? role = null, int melee = 0) =>
        new() { Id = id, Side = side, Pos = new Cell(x, z), Role = role, MeleeEnemies = melee, Ranged = true, WeaponRange = 25 };

    private static BattleView View(IEnumerable<BattleUnit> units, ISet<Cell>? walls = null)
    {
        var list = units.ToList();
        walls ??= new HashSet<Cell>();
        bool Open(Cell c) => Math.Abs(c.X) <= 40 && Math.Abs(c.Z) <= 40 && !walls.Contains(c) && list.All(u => u.Pos != c);
        bool Sight(Cell a, Cell b)
        {
            var steps = (int)Math.Ceiling(BattleView.Dist(a, b));
            for (var i = 1; i < steps; i++)
            {
                var c = new Cell(a.X + (int)Math.Round((b.X - a.X) * (double)i / steps), a.Z + (int)Math.Round((b.Z - a.Z) * (double)i / steps));
                if (walls.Contains(c))
                {
                    return false;
                }
            }

            return true;
        }

        return new BattleView(list, Open, Sight);
    }

    [Fact]
    public void Commander_StandsBehindItsMechs_AwayFromTheEnemy()
    {
        // Mechs at x 0, the colony to the east at x 20: the commander belongs west of its mechs.
        var commander = Unit(1, BattleSide.Empire, 3, 0, UnitRole.Chorister);
        var units = new[] { commander, Unit(2, BattleSide.Empire, 0, 1, UnitRole.Mech), Unit(3, BattleSide.Empire, 0, -1, UnitRole.Mech), Unit(9, BattleSide.Colony, 20, 0) };
        var spot = CommanderTactics.Position(View(units), commander, [2, 3], new Cell(-30, 0));
        Assert.NotNull(spot);
        Assert.True(spot!.Value.X < 0, $"expected west of the mechs, got {spot}");
        Assert.True(BattleView.Dist(spot.Value, new Cell(0, 0)) <= CommanderTactics.Standoff + CommanderTactics.SearchRadius);
    }

    [Fact]
    public void Commander_FallsBack_WhenAnEnemyGetsClose()
    {
        var commander = Unit(1, BattleSide.Empire, -6, 0, UnitRole.Chorister);
        var units = new[] { commander, Unit(2, BattleSide.Empire, 0, 0, UnitRole.Mech), Unit(9, BattleSide.Colony, -9, 2) };
        var view = View(units);
        Assert.True(CommanderTactics.Threatened(view, commander));
        var spot = CommanderTactics.Position(view, commander, [2], new Cell(-30, 0))!.Value;
        Assert.True(BattleView.Dist(spot, new Cell(-9, 2)) > BattleView.Dist(commander.Pos, new Cell(-9, 2)));
        Assert.True(BattleView.Dist(spot, new Cell(0, 0)) <= CommanderTactics.MaxFromMechs + CommanderTactics.SearchRadius);
    }

    [Fact]
    public void Commander_KeepsSightOfItsMechs_WhenItCan()
    {
        // A wall west of the mechs blocks the straight line back; the commander picks a cell that still sees them.
        var walls = Enumerable.Range(-3, 7).Select(z => new Cell(-4, z)).ToHashSet();
        var commander = Unit(1, BattleSide.Empire, 2, 6, UnitRole.Chorister);
        var units = new[] { commander, Unit(2, BattleSide.Empire, 0, 0, UnitRole.Mech), Unit(9, BattleSide.Colony, 20, 0) };
        var view = View(units, walls);
        var spot = CommanderTactics.Position(view, commander, [2], new Cell(-30, 0))!.Value;
        Assert.True(view.Sight(spot, new Cell(0, 0)));
    }

    [Fact]
    public void Commander_WithNoMechsLeft_FallsBackToTheRally()
    {
        var commander = Unit(1, BattleSide.Empire, 0, 0, UnitRole.Chorister);
        var spot = CommanderTactics.Position(View([commander, Unit(9, BattleSide.Colony, 15, 0)]), commander, [2], new Cell(-25, 0))!.Value;
        Assert.True(BattleView.Dist(spot, new Cell(-25, 0)) <= CommanderTactics.SearchRadius);
        Assert.Null(CommanderTactics.Position(View([Unit(1, BattleSide.Empire, -25, 0)]), Unit(1, BattleSide.Empire, -25, 0), [], new Cell(-25, 0)));
    }

    // ---- cluster drops (fairness rule 1)

    [Fact]
    public void ClusterDrop_NeedsACantorOrSpire_AndRespectsTheCapAndCooldown()
    {
        var state = new StrikeState { Cap = 2 };
        Assert.NotNull(ClusterDropRules.Blocked(state, cantorUp: false, spireStanding: false, dropsDown: false, tick: 10000));
        Assert.NotNull(ClusterDropRules.Blocked(state, cantorUp: true, spireStanding: false, dropsDown: true, tick: 10000));
        Assert.Null(ClusterDropRules.Blocked(state, cantorUp: true, spireStanding: false, dropsDown: false, tick: 10000));
        state.Struck(new Cell(0, 0), 10000);
        Assert.NotNull(ClusterDropRules.Blocked(state, true, false, false, 10000 + ClusterDropRules.CooldownTicks - 1));
        Assert.Null(ClusterDropRules.Blocked(state, true, false, false, 10000 + ClusterDropRules.CooldownTicks));
        state.Struck(new Cell(30, 30), 20000);
        Assert.Contains("spent", ClusterDropRules.Blocked(state, true, false, false, 90000));
    }

    [Fact]
    public void ClusterDrop_CapAndPointsScaleWithTheBudget()
    {
        Assert.Equal(ClusterDropRules.MinCap, ClusterDropRules.CapFor(100));
        Assert.Equal(ClusterDropRules.MaxCap, ClusterDropRules.CapFor(100000));
        Assert.True(ClusterDropRules.Points(9000) > ClusterDropRules.Points(1000));
    }

    [Fact]
    public void ClusterDrop_LandsOnOpenSky_NeverOnTheTarget()
    {
        var target = new Cell(0, 0);
        var cells = new[]
        {
            new DropCell(new Cell(2, 0), RoofKind.None, true),
            new DropCell(new Cell(7, 0), RoofKind.Constructed, true),
            new DropCell(new Cell(9, 0), RoofKind.None, true),
        };
        Assert.Equal(new Cell(9, 0), ClusterDropRules.Landing(target, cells));
        Assert.Null(ClusterDropRules.Landing(target, cells.Take(2)));
    }

    [Fact]
    public void ClusterDrop_TargetsLikeAStrike_NeverUnderMountain()
    {
        var turret = new StrikeCandidate { Cell = new Cell(5, 5), Kind = StrikeTargetKind.Turret, Sighted = true };
        var mountain = new StrikeCandidate { Cell = new Cell(20, 20), Kind = StrikeTargetKind.Turret, Value = 3, Sighted = true, ThickRoof = true };
        Assert.Equal(turret, ClusterDropRules.Target([turret, mountain], new StrikeState()));
        Assert.Null(ClusterDropRules.Target([turret with { Sighted = false }], new StrikeState()));
    }

    // ---- burrowing (fairness rule 3)

    [Fact]
    public void Burrowing_NeverUnderOverheadMountain_ButThroughAFloorIsFine()
    {
        var target = new Cell(0, 0);
        var cells = new[]
        {
            new DropCell(new Cell(3, 0), RoofKind.Thick, true),
            new DropCell(new Cell(4, 0), RoofKind.Constructed, true),
            new DropCell(new Cell(5, 0), RoofKind.None, true, Fogged: true),
        };
        Assert.Equal(new Cell(4, 0), Burrowing.Spot(target, cells));
        Assert.Null(Burrowing.Spot(target, cells.Where(c => c.Roof == RoofKind.Thick)));
    }

    [Fact]
    public void Burrowing_SpreadsItsTunnels()
    {
        var cells = Enumerable.Range(3, 10).Select(x => new DropCell(new Cell(x, 0), RoofKind.None, true)).ToList();
        var spots = Burrowing.Spots(new Cell(0, 0), cells, 3);
        Assert.Equal(3, spots.Count);
        Assert.All(spots, a => Assert.All(spots.Where(b => b != a), b => Assert.True(BattleView.Dist(a, b) >= 2)));
    }

    // ---- the Hum

    [Fact]
    public void TheHum_SparesThePsychicallyDeaf_AndGrowsWithSensitivity()
    {
        Assert.Equal(0, HumRules.Severity(0));
        Assert.True(HumRules.Severity(1.5f) > HumRules.Severity(1f));
        Assert.True(HumRules.Reaches(new Cell(0, 0), new Cell(40, 40)));
        Assert.False(HumRules.Reaches(new Cell(0, 0), new Cell(60, 60)));
        Assert.Equal(1, HumRules.Resonators(CrisisStage.Reclamation));
        Assert.Equal(2, HumRules.Resonators(CrisisStage.War));
    }

    // ---- stages and text

    [Fact]
    public void Stages_NameTheChoir_AndTheEmpiresWordingIsUnchanged()
    {
        var saga = new PowerSaga { Power = Power.Choir };
        Assert.Equal("the Hollow Choir isn't awake", CrisisStages.Explain(new CrisisInputs { Saga = saga, Power = Power.Choir }).Why);
        Assert.Equal("the Empire isn't awake", CrisisStages.Explain(new CrisisInputs { Saga = new PowerSaga() }).Why);
    }

    [Fact]
    public void ChoirStageText_AnnouncesEveryRise_AndTellsHowToFightIt()
    {
        foreach (var stage in new[] { CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale })
        {
            Assert.NotEmpty(CrisisText.ChoirStageNews(stage, stage - 1, "The Hollow Choir", "Wyrmfold"));
            Assert.Contains("Wyrmfold", CrisisText.ChoirStageLetter(stage, stage - 1, "The Hollow Choir", "Wyrmfold")!.Value.Text);
        }

        Assert.Contains("mechanitor", CrisisText.ChoirStageLetter(CrisisStage.Probing, CrisisStage.None, "The Hollow Choir", "Wyrmfold")!.Value.Text);
        Assert.Null(CrisisText.ChoirStageLetter(CrisisStage.Probing, CrisisStage.War, "The Hollow Choir", "Wyrmfold"));
    }
}
