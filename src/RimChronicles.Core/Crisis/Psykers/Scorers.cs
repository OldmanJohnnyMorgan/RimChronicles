using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Units;

namespace RimChronicles.Core.Crisis.Psykers;

/// <summary>What a cast is aimed at: a unit, or a cell. Skips also have a destination.</summary>
public sealed record CastTarget(int? UnitId, Cell Cell, Cell? Dest = null);

/// <summary>A scored cast: which psycast, at what, how good, and why (for the debug view).</summary>
public sealed record ScoredCast(string Psycast, string Scorer, float Utility, CastTarget Target, string Why);

/// <summary>
/// Scores one psycast for one caster in one fight (crisis spec §5.3.1): a utility and a target, or nothing. Scorers
/// are plain rules on a <see cref="BattleView"/>, tested on fixtures. Which psycasts a scorer handles is set in XML
/// (<c>ScorerDef</c>); a psycast with no scorer is never cast.
/// </summary>
public interface IPsycastScorer
{
    /// <summary>The id a <c>ScorerDef</c> names.</summary>
    string Id { get; }

    ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast);
}

/// <summary>Every scorer, by id.</summary>
public static class PsykerScorers
{
    public static readonly IReadOnlyList<IPsycastScorer> All =
    [
        new SkipOutOfCover(), new SkipAllyOutOfMelee(), new ChaosSkipCluster(), new BeckonShooter(), new StunShooter(),
        new BurdenCloser(), new PulseCluster(), new BerserkInGroup(), new PainblockAlly(), new ChunkCover(),
        new WallraiseLane(), new SmokeAdvance(), new ShieldGroup(), new InvisibleFlank(),
    ];

    public static IPsycastScorer? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    // ---- shared helpers

    /// <summary>In range of the caster (or around the caster, for casts with no range) and in sight.</summary>
    internal static bool Reach(BattleView view, BattleUnit caster, CastOption cast, Cell at) =>
        (cast.Range <= 0 ? BattleView.Dist(caster.Pos, at) <= Math.Max(cast.Radius, 1) : BattleView.Dist(caster.Pos, at) <= cast.Range)
        && view.Sight(caster.Pos, at);

    /// <summary>A psychic cast does nothing to a target with no psychic sensitivity.</summary>
    internal static bool Affects(CastOption cast, BattleUnit target) => !cast.Psychic || target.PsychicSensitivity > 0.1f;

    internal static float Sensitivity(CastOption cast, BattleUnit target) => cast.Psychic ? Math.Clamp(target.PsychicSensitivity, 0, 2) : 1;

    /// <summary>Living enemy pawns (not turrets) the cast can reach and affect.</summary>
    internal static IEnumerable<BattleUnit> TargetPawns(BattleView view, BattleUnit caster, CastOption cast) =>
        view.Enemies(caster).Where(e => !e.IsTurret && Affects(cast, e) && Reach(view, caster, cast, e.Pos));

    /// <summary>The best cell to centre an area cast on: an enemy's cell with the most enemies and the fewest allies in
    /// the radius.</summary>
    internal static (Cell Cell, int Enemies, int Allies)? BestArea(BattleView view, BattleUnit caster, CastOption cast, string? skipEffect)
    {
        var radius = Math.Max(cast.Radius, 1.5f);
        (Cell, int, int)? best = null;
        var bestScore = float.MinValue;
        foreach (var centre in view.Enemies(caster).Where(e => !e.IsTurret && Reach(view, caster, cast, e.Pos)))
        {
            var enemies = view.Near(centre.Pos, radius, u => u.Side != caster.Side && u.Fighting && !u.IsTurret && Affects(cast, u)
                && (skipEffect is null || !u.Effects.Contains(skipEffect))).Count();
            var allies = view.Near(centre.Pos, radius, u => u.Side == caster.Side && !u.Downed).Count();
            var score = enemies - (2f * allies);
            if (score > bestScore)
            {
                bestScore = score;
                best = (centre.Pos, enemies, allies);
            }
        }

        return best;
    }

    internal static Cell Centroid(IEnumerable<BattleUnit> units)
    {
        var list = units.ToList();
        return new Cell((int)Math.Round(list.Average(u => u.Pos.X)), (int)Math.Round(list.Average(u => u.Pos.Z)));
    }
}

/// <summary>Skip: the colonist in the best cover, or with the best weapon, out next to two or more Praetorians.</summary>
public sealed class SkipOutOfCover : IPsycastScorer
{
    public string Id => "SkipOutOfCover";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var praetorians = view.Allies(caster).Where(a => a.Role == UnitRole.Praetorian).ToList();
        if (praetorians.Count < 2)
        {
            return null;
        }

        ScoredCast? best = null;
        foreach (var target in PsykerScorers.TargetPawns(view, caster, cast).Where(e => e.InCover || e.Dps >= 8))
        {
            foreach (var praetorian in praetorians)
            {
                var found = view.OpenAround(praetorian.Pos, 1, 2)
                    .Where(c => view.Near(c, 2.5f, u => u.Side == caster.Side && u.Role == UnitRole.Praetorian && u.Fighting).Count() >= 2
                        && (cast.DestRange <= 0 || BattleView.Dist(target.Pos, c) <= cast.DestRange) && view.Sight(target.Pos, c))
                    .Select(c => (Cell?)c).FirstOrDefault();
                if (found is not { } dest)
                {
                    continue;
                }

                var utility = 4 + (target.Dps / 10) + (target.InCover ? 2 : 0) + target.Value;
                if (best is null || utility > best.Utility)
                {
                    best = new ScoredCast(cast.Def, Id, utility, new CastTarget(target.Id, target.Pos, dest),
                        $"skip {target.Name} out of {(target.InCover ? "cover" : "the line")} next to Praetorians");
                }
            }
        }

        return best;
    }
}

/// <summary>Skip: an ally losing a melee (two or more enemies on it, or one while badly hurt) out to safety near allies.</summary>
public sealed class SkipAllyOutOfMelee : IPsycastScorer
{
    public string Id => "SkipAllyOutOfMelee";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var enemies = view.Enemies(caster).ToList();
        foreach (var ally in view.Allies(caster).Where(a => !a.IsTurret && (a.MeleeEnemies >= 2 || (a.MeleeEnemies >= 1 && a.Health < 0.5f)))
                     .Where(a => PsykerScorers.Reach(view, caster, cast, a.Pos)).OrderByDescending(a => a.Value))
        {
            var dest = view.OpenAround(ally.Pos, 6, 10)
                .Where(c => (cast.DestRange <= 0 || BattleView.Dist(ally.Pos, c) <= cast.DestRange) && view.Sight(ally.Pos, c)
                    && enemies.All(e => BattleView.Dist(e.Pos, c) >= 6))
                .OrderByDescending(c => view.Near(c, 5, u => u.Side == caster.Side && u.Fighting).Count())
                .Select(c => (Cell?)c).FirstOrDefault();
            if (dest is { } cell)
            {
                return new ScoredCast(cast.Def, Id, 5 + ally.Value + ally.MeleeEnemies, new CastTarget(ally.Id, ally.Pos, cell),
                    $"skip {ally.Name} out of a losing melee");
            }
        }

        return null;
    }
}

/// <summary>Chaos Skip: a colonist holding a chokepoint with others (two more within 4 cells), best if in cover. With a
/// radius (Mass Chaos Skip), three or more colonists in the area and at most one ally.</summary>
public sealed class ChaosSkipCluster : IPsycastScorer
{
    public string Id => "ChaosSkipCluster";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        if (cast.Radius > 0)
        {
            if (PsykerScorers.BestArea(view, caster, cast, null) is not { Enemies: >= 3, Allies: <= 1 } area)
            {
                return null;
            }

            return new ScoredCast(cast.Def, Id, 2 * area.Enemies, new CastTarget(null, area.Cell), $"scatter {area.Enemies} colonists");
        }

        var best = PsykerScorers.TargetPawns(view, caster, cast)
            .Select(e => (Unit: e, Others: view.Near(e.Pos, 4, u => u.Side == e.Side && u.Id != e.Id && u.Fighting && !u.IsTurret).Count()))
            .Where(x => x.Others >= 2)
            .OrderByDescending(x => x.Others + (x.Unit.InCover ? 1 : 0)).ThenBy(x => x.Unit.Id)
            .FirstOrDefault();
        return best.Unit is null
            ? null
            : new ScoredCast(cast.Def, Id, 3 + best.Others + (best.Unit.InCover ? 1 : 0), new CastTarget(best.Unit.Id, best.Unit.Pos),
                $"chaos-skip {best.Unit.Name} out of a group of {best.Others + 1}");
    }
}

/// <summary>Beckon: a colonist shooting from cover, pulled out toward the caster.</summary>
public sealed class BeckonShooter : IPsycastScorer
{
    public string Id => "BeckonShooter";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var target = PsykerScorers.TargetPawns(view, caster, cast).Where(e => e.Ranged && e.InCover)
            .OrderByDescending(e => e.Dps).ThenBy(e => e.Id).FirstOrDefault();
        if (target is null)
        {
            return null;
        }

        var escort = view.Near(caster.Pos, 4, u => u.Side == caster.Side && u.Role == UnitRole.Praetorian && u.Fighting).Count();
        return new ScoredCast(cast.Def, Id, (3 + (target.Dps / 10) + (0.5f * escort)) * PsykerScorers.Sensitivity(cast, target),
            new CastTarget(target.Id, target.Pos), $"beckon {target.Name} out of cover");
    }
}

/// <summary>Stun: the colonist with the most dangerous weapon in range.</summary>
public sealed class StunShooter : IPsycastScorer
{
    public string Id => "StunShooter";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var target = PsykerScorers.TargetPawns(view, caster, cast).Where(e => !e.Effects.Contains(BattleEffect.Stunned))
            .OrderByDescending(e => e.Dps * PsykerScorers.Sensitivity(cast, e)).ThenBy(e => e.Id).FirstOrDefault();
        return target is null || target.Dps <= 0
            ? null
            : new ScoredCast(cast.Def, Id, (2 + (target.Dps / 8)) * PsykerScorers.Sensitivity(cast, target), new CastTarget(target.Id, target.Pos),
                $"stun {target.Name}, the most dangerous shooter in range");
    }
}

/// <summary>Burden: a colonist closing on a psyker or Herald.</summary>
public sealed class BurdenCloser : IPsycastScorer
{
    public string Id => "BurdenCloser";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var protectees = view.Allies(caster).Where(a => a.IsPsykerOrHerald).ToList();
        var best = PsykerScorers.TargetPawns(view, caster, cast).Where(e => !e.Effects.Contains(BattleEffect.Burden))
            .Select(e => (Unit: e, D: protectees.Count == 0 ? float.MaxValue : protectees.Min(p => BattleView.Dist(p.Pos, e.Pos))))
            .Where(x => x.D <= 8)
            .OrderBy(x => x.D).ThenBy(x => x.Unit.Id)
            .FirstOrDefault();
        return best.Unit is null
            ? null
            : new ScoredCast(cast.Def, Id, (3 + ((8 - best.D) / 3)) * PsykerScorers.Sensitivity(cast, best.Unit), new CastTarget(best.Unit.Id, best.Unit.Pos),
                $"burden {best.Unit.Name}, {best.D:0} cells from a psyker or Herald");
    }
}

/// <summary>Vertigo, Blinding and Berserk Pulse: three or more colonists in the radius and at most one ally.</summary>
public sealed class PulseCluster : IPsycastScorer
{
    public string Id => "PulseCluster";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var effect = cast.Def switch
        {
            "VertigoPulse" => BattleEffect.Vertigo,
            "BlindingPulse" => BattleEffect.Blind,
            _ => BattleEffect.Mental,
        };
        if (PsykerScorers.BestArea(view, caster, cast, effect) is not { Enemies: >= 3, Allies: <= 1 } area)
        {
            return null;
        }

        return new ScoredCast(cast.Def, Id, (1.5f * area.Enemies) - (2 * area.Allies), new CastTarget(null, area.Cell),
            $"pulse {area.Enemies} colonists ({area.Allies} allies in the area)");
    }
}

/// <summary>Berserk: a colonist standing with two or more others, so they turn on each other.</summary>
public sealed class BerserkInGroup : IPsycastScorer
{
    public string Id => "BerserkInGroup";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var best = PsykerScorers.TargetPawns(view, caster, cast).Where(e => e.Dps > 0)
            .Select(e => (Unit: e, Others: view.Near(e.Pos, 5, u => u.Side == e.Side && u.Id != e.Id && u.Fighting && !u.IsTurret).Count()))
            .Where(x => x.Others >= 2)
            .OrderByDescending(x => x.Others + (x.Unit.Dps / 10)).ThenBy(x => x.Unit.Id)
            .FirstOrDefault();
        return best.Unit is null
            ? null
            : new ScoredCast(cast.Def, Id, (4 + best.Others) * PsykerScorers.Sensitivity(cast, best.Unit), new CastTarget(best.Unit.Id, best.Unit.Pos),
                $"berserk {best.Unit.Name} among {best.Others} others");
    }
}

/// <summary>Painblock: the most valuable ally in pain and still fighting.</summary>
public sealed class PainblockAlly : IPsycastScorer
{
    public string Id => "PainblockAlly";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var ally = view.Allies(caster).Where(a => !a.IsTurret && a.Pain >= 0.3f && !a.Effects.Contains(BattleEffect.Painblock)
                && PsykerScorers.Reach(view, caster, cast, a.Pos))
            .OrderByDescending(a => a.Value + a.Pain).ThenBy(a => a.Id).FirstOrDefault();
        return ally is null
            ? null
            : new ScoredCast(cast.Def, Id, 2 + ally.Value + (3 * ally.Pain), new CastTarget(ally.Id, ally.Pos), $"painblock {ally.Name}");
    }
}

/// <summary>Chunkskip: rock chunks as cover next to an ally pinned in the open, on the enemy's side.</summary>
public sealed class ChunkCover : IPsycastScorer
{
    public string Id => "ChunkCover";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var enemies = view.Enemies(caster).Where(e => e.Ranged).ToList();
        if (enemies.Count == 0)
        {
            return null;
        }

        foreach (var ally in view.Allies(caster).Where(a => !a.IsTurret && a.UnderFire && !a.InCover).OrderByDescending(a => a.Value))
        {
            var threat = enemies.OrderBy(e => BattleView.Dist(e.Pos, ally.Pos)).First();
            var cell = view.OpenAround(ally.Pos, 1, 2).OrderBy(c => BattleView.Dist(c, threat.Pos)).Select(c => (Cell?)c).FirstOrDefault();
            if (cell is { } at && PsykerScorers.Reach(view, caster, cast, at))
            {
                return new ScoredCast(cast.Def, Id, 2.5f + ally.Value, new CastTarget(null, at), $"chunks as cover for {ally.Name}");
            }
        }

        return null;
    }
}

/// <summary>Wallraise: a wall across the line a colony turret is firing along at an ally.</summary>
public sealed class WallraiseLane : IPsycastScorer
{
    public string Id => "WallraiseLane";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var turrets = view.Enemies(caster).Where(e => e.IsTurret).ToList();
        foreach (var ally in view.Allies(caster).Where(a => !a.IsTurret && a.UnderFire).OrderByDescending(a => a.Value))
        {
            var turret = turrets.Where(t => BattleView.Dist(t.Pos, ally.Pos) <= Math.Max(t.WeaponRange, 10) && view.Sight(t.Pos, ally.Pos))
                .OrderBy(t => BattleView.Dist(t.Pos, ally.Pos)).FirstOrDefault();
            if (turret is null)
            {
                continue;
            }

            var d = BattleView.Dist(ally.Pos, turret.Pos);
            var step = Math.Min(3f, d / 2);
            var at = new Cell(
                ally.Pos.X + (int)Math.Round((turret.Pos.X - ally.Pos.X) * step / d),
                ally.Pos.Z + (int)Math.Round((turret.Pos.Z - ally.Pos.Z) * step / d));
            if (view.Open(at) && PsykerScorers.Reach(view, caster, cast, at))
            {
                var covered = view.Near(at, 4, u => u.Side == caster.Side && u.UnderFire && u.Fighting).Count();
                return new ScoredCast(cast.Def, Id, 4 + covered, new CastTarget(null, at), $"wall off {turret.Name}'s line on {ally.Name}");
            }
        }

        return null;
    }
}

/// <summary>Smokepop: cover for two or more allies crossing open ground under fire.</summary>
public sealed class SmokeAdvance : IPsycastScorer
{
    public string Id => "SmokeAdvance";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var exposed = view.Allies(caster).Where(a => !a.IsTurret && a.UnderFire && !a.InCover).ToList();
        if (exposed.Count < 2)
        {
            return null;
        }

        var radius = cast.Radius > 0 ? cast.Radius : 3;
        var best = exposed.Select(a => (Centre: a, Count: exposed.Count(o => BattleView.Dist(o.Pos, a.Pos) <= radius)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Centre.Id).First();
        return best.Count < 2 || !PsykerScorers.Reach(view, caster, cast, best.Centre.Pos)
            ? null
            : new ScoredCast(cast.Def, Id, 2 * best.Count, new CastTarget(null, best.Centre.Pos), $"smoke over {best.Count} exposed allies");
    }
}

/// <summary>Bullet shield: three or more allies under ranged fire in its radius.</summary>
public sealed class ShieldGroup : IPsycastScorer
{
    public string Id => "ShieldGroup";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var radius = cast.Radius > 0 ? cast.Radius : 4.9f;
        var underFire = view.Allies(caster).Where(a => !a.IsTurret && a.UnderFire).ToList();
        var best = underFire.Select(a => (Centre: a, Count: underFire.Count(o => BattleView.Dist(o.Pos, a.Pos) <= radius)))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Centre.Id).FirstOrDefault();
        return best.Centre is null || best.Count < 3 || !PsykerScorers.Reach(view, caster, cast, best.Centre.Pos)
            ? null
            : new ScoredCast(cast.Def, Id, 2.5f * best.Count, new CastTarget(null, best.Centre.Pos), $"shield {best.Count} allies under fire");
    }
}

/// <summary>Invisibility: a Herald, or the adept itself, before any enemy has seen it, with enemies near enough to flank.</summary>
public sealed class InvisibleFlank : IPsycastScorer
{
    public string Id => "InvisibleFlank";

    public ScoredCast? Score(BattleView view, BattleUnit caster, CastOption cast)
    {
        var enemies = view.Enemies(caster).ToList();
        var target = view.Allies(caster)
            .Where(a => (a.Role == UnitRole.Herald || a.Id == caster.Id) && !a.Seen && !a.Effects.Contains(BattleEffect.Invisible)
                && enemies.Any(e => BattleView.Dist(e.Pos, a.Pos) <= 35) && (a.Id == caster.Id || PsykerScorers.Reach(view, caster, cast, a.Pos)))
            .OrderByDescending(a => a.Role == UnitRole.Herald).ThenBy(a => a.Id).FirstOrDefault();
        return target is null
            ? null
            : new ScoredCast(cast.Def, Id, target.Role == UnitRole.Herald ? 5 : 3, new CastTarget(target.Id, target.Pos),
                $"hide {target.Name} before the colony sees it");
    }
}
