using System.Xml.Linq;
using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Psykers;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PsykerTests
{
    private const int CasterId = 1;
    private static int _nextId = 100;

    private static BattleUnit Caster(int x = 0, int z = 0) =>
        new() { Id = CasterId, Side = BattleSide.Empire, Pos = new Cell(x, z), Name = "adept", Role = UnitRole.Adept };

    private static BattleUnit Colonist(int x, int z, float dps = 5, bool cover = false, bool ranged = true, string? name = null) =>
        new() { Id = _nextId++, Side = BattleSide.Colony, Pos = new Cell(x, z), Name = name ?? $"colonist{x},{z}", Dps = dps, InCover = cover, Ranged = ranged, WeaponRange = 25 };

    private static BattleUnit Ally(int x, int z, UnitRole role = UnitRole.Praetorian) =>
        new() { Id = _nextId++, Side = BattleSide.Empire, Pos = new Cell(x, z), Name = $"{role}{x},{z}", Role = role };

    /// <summary>An open 60×60 field (cells from -30 to 30), clear sight unless walls block the straight line.</summary>
    private static BattleView View(IEnumerable<BattleUnit> units, ISet<Cell>? walls = null)
    {
        var list = units.ToList();
        walls ??= new HashSet<Cell>();
        bool Open(Cell c) => Math.Abs(c.X) <= 30 && Math.Abs(c.Z) <= 30 && !walls.Contains(c) && list.All(u => u.Pos != c);
        bool Sight(Cell a, Cell b)
        {
            var steps = (int)Math.Ceiling(BattleView.Dist(a, b));
            for (var i = 1; i < steps; i++)
            {
                var c = new Cell(a.X + (int)Math.Round((b.X - a.X) * i / (double)steps), a.Z + (int)Math.Round((b.Z - a.Z) * i / (double)steps));
                if (walls.Contains(c))
                {
                    return false;
                }
            }

            return true;
        }

        return new BattleView(list, Open, Sight);
    }

    private static CastOption Cast(string def, float range = 25, float radius = 0, float heatAfter = 0.2f, bool psychic = true, float destRange = 0) =>
        new() { Def = def, Range = range, Radius = radius, HeatAfter = heatAfter, Psychic = psychic, DestRange = destRange };

    private static ScoredCast? Score(IPsycastScorer scorer, BattleView view, CastOption cast) => scorer.Score(view, view.Find(CasterId)!, cast);

    // ---- scorers

    [Fact]
    public void Stun_TheMostDangerousShooterInRange_NeverAStunnedOrPsychicallyDeafOne()
    {
        var weak = Colonist(5, 0, dps: 3);
        var strong = Colonist(6, 0, dps: 12);
        var far = Colonist(40, 0, dps: 30);
        var view = View([Caster(), weak, strong, far]);
        Assert.Equal(strong.Id, Score(new StunShooter(), view, Cast("Stun"))!.Target.UnitId);

        var stunned = strong with { Effects = new HashSet<string> { BattleEffect.Stunned } };
        Assert.Equal(weak.Id, Score(new StunShooter(), View([Caster(), weak, stunned, far]), Cast("Stun"))!.Target.UnitId);

        var deaf = strong with { PsychicSensitivity = 0 };
        Assert.Equal(weak.Id, Score(new StunShooter(), View([Caster(), weak, deaf]), Cast("Stun"))!.Target.UnitId);
    }

    [Fact]
    public void Stun_NotThroughAWall()
    {
        var shooter = Colonist(5, 0, dps: 10);
        var view = View([Caster(), shooter], new HashSet<Cell> { new(2, 0), new(3, 0) });
        Assert.Null(Score(new StunShooter(), view, Cast("Stun")));
    }

    [Fact]
    public void Pulse_NeedsThreeColonists_AndAtMostOneAlly()
    {
        var three = new[] { Colonist(10, 0), Colonist(11, 0), Colonist(10, 1) };
        var scored = Score(new PulseCluster(), View([Caster(), .. three]), Cast("VertigoPulse", radius: 3.9f));
        Assert.NotNull(scored);
        Assert.Contains(scored!.Target.Cell, three.Select(c => c.Pos));

        Assert.Null(Score(new PulseCluster(), View([Caster(), Colonist(10, 0), Colonist(11, 0)]), Cast("VertigoPulse", radius: 3.9f)));
        Assert.Null(Score(new PulseCluster(), View([Caster(), .. three, Ally(10, 2), Ally(11, 1)]), Cast("VertigoPulse", radius: 3.9f)));
    }

    [Fact]
    public void Pulse_DoesntRecastOnColonistsAlreadyAffected()
    {
        var dizzy = new HashSet<string> { BattleEffect.Vertigo };
        var three = new[] { Colonist(10, 0) with { Effects = dizzy }, Colonist(11, 0) with { Effects = dizzy }, Colonist(10, 1) };
        Assert.Null(Score(new PulseCluster(), View([Caster(), .. three]), Cast("VertigoPulse", radius: 3.9f)));
        Assert.NotNull(Score(new PulseCluster(), View([Caster(), .. three]), Cast("BlindingPulse", radius: 3.9f)));
    }

    [Fact]
    public void Skip_PullsAColonistInCoverOutNextToTwoPraetorians()
    {
        var target = Colonist(12, 0, cover: true);
        var p1 = Ally(2, 5);
        var p2 = Ally(3, 5);
        var scored = Score(new SkipOutOfCover(), View([Caster(), target, p1, p2]), Cast("Skip", range: 27.9f, psychic: false, destRange: 27.9f));
        Assert.NotNull(scored);
        Assert.Equal(target.Id, scored!.Target.UnitId);
        var dest = scored.Target.Dest!.Value;
        Assert.True(BattleView.Dist(dest, p1.Pos) <= 2.5f && BattleView.Dist(dest, p2.Pos) <= 2.5f);
        Assert.NotEqual(p1.Pos, dest);
    }

    [Fact]
    public void Skip_NeedsTwoPraetorians_AndADestinationInRangeOfTheTarget()
    {
        var target = Colonist(12, 0, cover: true);
        Assert.Null(Score(new SkipOutOfCover(), View([Caster(), target, Ally(2, 5)]), Cast("Skip", psychic: false)));
        Assert.Null(Score(new SkipOutOfCover(), View([Caster(), target, Ally(-20, 5), Ally(-21, 5)]), Cast("Skip", range: 27.9f, psychic: false, destRange: 10)));
    }

    [Fact]
    public void Skip_AnAllyOutOfALosingMelee()
    {
        var ally = Ally(5, 0) with { MeleeEnemies = 2 };
        var scored = Score(new SkipAllyOutOfMelee(), View([Caster(), ally, Colonist(6, 0), Colonist(5, 1), Ally(-8, 0)]), Cast("Skip", psychic: false, destRange: 27.9f));
        Assert.Equal(ally.Id, scored!.Target.UnitId);
        Assert.True(BattleView.Dist(scored.Target.Dest!.Value, new Cell(6, 0)) >= 6);

        Assert.Null(Score(new SkipAllyOutOfMelee(), View([Caster(), Ally(5, 0) with { MeleeEnemies = 1 }, Colonist(6, 0)]), Cast("Skip", psychic: false)));
    }

    [Fact]
    public void ChaosSkip_AColonistInAGroup_AndMassChaosSkipNeedsThree()
    {
        var group = new[] { Colonist(10, 0, cover: true), Colonist(11, 1), Colonist(12, 0) };
        Assert.NotNull(Score(new ChaosSkipCluster(), View([Caster(), .. group]), Cast("ChaosSkip", psychic: false)));
        Assert.Null(Score(new ChaosSkipCluster(), View([Caster(), Colonist(10, 0), Colonist(11, 1)]), Cast("ChaosSkip", psychic: false)));

        Assert.NotNull(Score(new ChaosSkipCluster(), View([Caster(), .. group]), Cast("MassChaosSkip", radius: 7.9f, psychic: false)));
        Assert.Null(Score(new ChaosSkipCluster(), View([Caster(), .. group.Take(2)]), Cast("MassChaosSkip", radius: 7.9f, psychic: false)));
    }

    [Fact]
    public void Beckon_OnlyAShooterInCover()
    {
        var inCover = Colonist(10, 0, dps: 6, cover: true);
        var open = Colonist(9, 0, dps: 20);
        Assert.Equal(inCover.Id, Score(new BeckonShooter(), View([Caster(), inCover, open]), Cast("Beckon", range: 19.9f))!.Target.UnitId);
        Assert.Null(Score(new BeckonShooter(), View([Caster(), open]), Cast("Beckon", range: 19.9f)));
    }

    [Fact]
    public void Burden_TheColonistClosingOnAHerald()
    {
        var herald = Ally(10, 10, UnitRole.Herald);
        var close = Colonist(12, 10);
        var away = Colonist(-10, -10);
        Assert.Equal(close.Id, Score(new BurdenCloser(), View([Caster(), herald, close, away]), Cast("Burden", range: 29.9f))!.Target.UnitId);
        Assert.Null(Score(new BurdenCloser(), View([Caster(), herald, away]), Cast("Burden", range: 29.9f)));
    }

    [Fact]
    public void Berserk_AColonistWithTwoOthersNearby()
    {
        var group = new[] { Colonist(10, 0), Colonist(12, 0), Colonist(11, 2) };
        Assert.NotNull(Score(new BerserkInGroup(), View([Caster(), .. group]), Cast("Berserk", range: 19.9f)));
        Assert.Null(Score(new BerserkInGroup(), View([Caster(), Colonist(10, 0), Colonist(20, 0)]), Cast("Berserk", range: 19.9f)));
    }

    [Fact]
    public void Painblock_TheMostValuableAllyInPain_NotOneAlreadyPainblocked()
    {
        var hurt = Ally(3, 0) with { Pain = 0.5f };
        var herald = Ally(4, 0, UnitRole.Herald) with { Pain = 0.4f, Value = 2 };
        Assert.Equal(herald.Id, Score(new PainblockAlly(), View([Caster(), hurt, herald]), Cast("Painblock", psychic: true))!.Target.UnitId);

        var blocked = herald with { Effects = new HashSet<string> { BattleEffect.Painblock } };
        Assert.Equal(hurt.Id, Score(new PainblockAlly(), View([Caster(), hurt, blocked]), Cast("Painblock"))!.Target.UnitId);
        Assert.Null(Score(new PainblockAlly(), View([Caster(), Ally(3, 0) with { Pain = 0.1f }]), Cast("Painblock")));
    }

    [Fact]
    public void Chunkskip_CoverNextToAPinnedAlly_OnTheEnemySide()
    {
        var pinned = Ally(5, 0) with { UnderFire = true };
        var shooter = Colonist(15, 0);
        var scored = Score(new ChunkCover(), View([Caster(), pinned, shooter]), Cast("Chunkskip", psychic: false));
        Assert.NotNull(scored);
        Assert.True(BattleView.Dist(scored!.Target.Cell, pinned.Pos) <= 2);
        Assert.True(scored.Target.Cell.X > pinned.Pos.X);
        Assert.Null(Score(new ChunkCover(), View([Caster(), pinned with { InCover = true }, shooter]), Cast("Chunkskip", psychic: false)));
    }

    [Fact]
    public void Wallraise_BetweenATurretAndTheAllyItsShooting()
    {
        var ally = Ally(5, 0) with { UnderFire = true };
        var turret = new BattleUnit { Id = _nextId++, Side = BattleSide.Colony, Pos = new Cell(15, 0), IsTurret = true, Ranged = true, WeaponRange = 25, Name = "turret" };
        var scored = Score(new WallraiseLane(), View([Caster(), ally, turret]), Cast("Wallraise", psychic: false));
        Assert.NotNull(scored);
        Assert.InRange(scored!.Target.Cell.X, 6, 9);
        Assert.Equal(0, scored.Target.Cell.Z);
        Assert.Null(Score(new WallraiseLane(), View([Caster(), ally]), Cast("Wallraise", psychic: false)));
    }

    [Fact]
    public void Smokepop_AndBulletShield_CountTheAlliesUnderFire()
    {
        var exposed = new[] { Ally(5, 0) with { UnderFire = true }, Ally(6, 1) with { UnderFire = true }, Ally(5, 2) with { UnderFire = true } };
        Assert.NotNull(Score(new SmokeAdvance(), View([Caster(), .. exposed.Take(2), Colonist(20, 0)]), Cast("Smokepop", radius: 3, psychic: false)));
        Assert.Null(Score(new SmokeAdvance(), View([Caster(), exposed[0], Colonist(20, 0)]), Cast("Smokepop", radius: 3, psychic: false)));

        Assert.NotNull(Score(new ShieldGroup(), View([Caster(), .. exposed, Colonist(20, 0)]), Cast("BulletShield", radius: 4.9f, psychic: false)));
        Assert.Null(Score(new ShieldGroup(), View([Caster(), .. exposed.Take(2), Colonist(20, 0)]), Cast("BulletShield", radius: 4.9f, psychic: false)));
    }

    [Fact]
    public void Invisibility_AHeraldTheColonyHasntSeen()
    {
        var herald = Ally(5, 0, UnitRole.Herald) with { Seen = false };
        var scored = Score(new InvisibleFlank(), View([Caster(), herald, Colonist(30, 0)]), Cast("Invisibility", range: 19.9f));
        Assert.Equal(herald.Id, scored!.Target.UnitId);
        Assert.Null(Score(new InvisibleFlank(), View([Caster(), herald with { Seen = true }, Colonist(30, 0)]), Cast("Invisibility", range: 19.9f)));
    }

    // ---- the brain

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<IPsycastScorer>> Scorers = new Dictionary<string, IReadOnlyList<IPsycastScorer>>
    {
        ["Stun"] = [new StunShooter()],
        ["VertigoPulse"] = [new PulseCluster()],
    };

    private static IReadOnlyList<IPsycastScorer> For(string psycast) => Scorers.TryGetValue(psycast, out var list) ? list : [];

    [Fact]
    public void Brain_PicksTheBestCast()
    {
        var view = View([Caster(), Colonist(10, 0, dps: 4), Colonist(11, 0, dps: 4), Colonist(10, 1, dps: 4), Colonist(11, 1, dps: 4)]);
        var decision = PsykerBrain.Choose(view, view.Find(CasterId)!, [Cast("Stun"), Cast("VertigoPulse", radius: 3.9f)], For, 1);
        Assert.Equal("VertigoPulse", decision.Cast!.Psycast); // four colonists in the area beat one stun
        Assert.Equal(2, decision.Candidates.Count);
    }

    [Fact]
    public void Brain_HoldsWithNothingCastable_OrNoGoodTarget()
    {
        var view = View([Caster(), Colonist(10, 0)]);
        var none = PsykerBrain.Choose(view, view.Find(CasterId)!, [], For, 1);
        Assert.Null(none.Cast);
        Assert.StartsWith("hold", none.Why);

        var noTarget = PsykerBrain.Choose(View([Caster()]), Caster(), [Cast("Stun")], For, 1);
        Assert.Null(noTarget.Cast);
    }

    [Fact]
    public void Brain_ObeysHeatAndPsyfocus()
    {
        var view = View([Caster(), Colonist(10, 0, dps: 10)]);
        var caster = view.Find(CasterId)!;
        Assert.Null(PsykerBrain.Choose(view, caster, [Cast("Stun", heatAfter: 1.0f)], For, 1).Cast);
        Assert.Null(PsykerBrain.Choose(view, caster, [Cast("Stun") with { PsyfocusCost = 0.05f }], For, 0.01f).Cast);

        var cool = PsykerBrain.Choose(view, caster, [Cast("Stun", heatAfter: 0.3f)], For, 1).Cast!;
        var hot = PsykerBrain.Choose(view, caster, [Cast("Stun", heatAfter: 0.95f)], For, 1).Candidates[0];
        Assert.True(hot.Utility < cool.Utility);
    }

    [Fact]
    public void Brain_NeverCastsAPsycastWithNoScorer()
    {
        var view = View([Caster(), Colonist(10, 0, dps: 10)]);
        Assert.Null(PsykerBrain.Choose(view, view.Find(CasterId)!, [Cast("Neuroquake"), Cast("Farskip")], For, 1).Cast);
    }

    [Fact]
    public void Brain_ANearlyOverheatedPsykerHoldsForAWeakCast()
    {
        var view = View([Caster(), Colonist(10, 0, dps: 1)]);
        var decision = PsykerBrain.Choose(view, view.Find(CasterId)!, [Cast("Stun", heatAfter: 0.99f)], For, 1);
        Assert.Null(decision.Cast);
        Assert.Contains("hold", decision.Why);
    }

    // ---- the defs

    private static List<(string Scorer, List<string> Psycasts)> ScorerDefs() =>
        XDocument.Load(Repo.Path("Defs", "ScorerDefs", "RimChronicles_Scorers.xml")).Root!.Elements()
            .Select(e => (e.Element("scorer")!.Value, e.Element("psycasts")!.Elements("li").Select(li => li.Value).ToList()))
            .ToList();

    [Fact]
    public void ScorerDefs_NameKnownScorers_AndUseEveryScorer()
    {
        var defs = ScorerDefs();
        Assert.All(defs, d => Assert.NotNull(PsykerScorers.Find(d.Scorer)));
        Assert.Equal(PsykerScorers.All.Select(s => s.Id).OrderBy(s => s), defs.Select(d => d.Scorer).Distinct().OrderBy(s => s));
        Assert.Equal(PsykerScorers.All.Count, PsykerScorers.All.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void ScorerDefs_CoverEveryKitPsycastButFarskip_AndNoBannedOne()
    {
        var scored = ScorerDefs().SelectMany(d => d.Psycasts).ToHashSet();
        var kits = new[] { PsycastKits.Skip, PsycastKits.Control, PsycastKits.Mind, PsycastKits.Ward, PsycastKits.Stellarch }
            .SelectMany(k => k).Select(k => k.Def).Distinct();
        Assert.Equal(["Farskip"], kits.Where(k => !scored.Contains(k)).ToList()); // Farskip's escape comes with C13
        Assert.Empty(scored.Intersect(PsycastKits.RequiredBans));
    }
}
