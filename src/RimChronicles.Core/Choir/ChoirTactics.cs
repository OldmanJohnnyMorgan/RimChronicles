using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Strikes;

namespace RimChronicles.Core.Choir;

/// <summary>
/// The Choir's commander AI (War in Heaven tech spec §4.3, W7): a mechanitor stands back behind its mechs, on the far
/// side from the enemy, where it can still see them; when an enemy gets close it falls back. Its mechs are the threat,
/// and reaching the mechanitor is the counterplay (killing it shuts its mechs down). Plain rules on a
/// <see cref="BattleView"/>; the game moves the pawn.
/// </summary>
public static class CommanderTactics
{
    /// <summary>How far behind its mechs' centre a commander stands.</summary>
    public const float Standoff = 7f;

    /// <summary>An enemy this close means the commander falls back.</summary>
    public const float ThreatRange = 7f;

    /// <summary>How far a threatened commander falls back from the nearest enemy.</summary>
    public const float FallBack = 12f;

    /// <summary>A commander never strays further than this from its mechs (vanilla's mech command range is about 25).</summary>
    public const float MaxFromMechs = 20f;

    /// <summary>How far around the ideal spot it looks for an open cell.</summary>
    public const int SearchRadius = 5;

    /// <summary>True when a standing enemy (turrets included) is within <see cref="ThreatRange"/> of the commander, or in melee.</summary>
    public static bool Threatened(BattleView view, BattleUnit commander) =>
        commander.MeleeEnemies > 0 || view.Enemies(commander).Any(e => BattleView.Dist(e.Pos, commander.Pos) <= ThreatRange);

    /// <summary>
    /// Where <paramref name="commander"/> should stand, or null to hold where it is: with mechs left, behind their centre
    /// (away from the nearest enemy), in sight of them, and never closer to an enemy than <see cref="ThreatRange"/> when it
    /// can help it; threatened, it falls back from the nearest enemy, staying in range of its mechs. With no mechs left,
    /// it falls back toward <paramref name="rally"/>.
    /// </summary>
    public static Cell? Position(BattleView view, BattleUnit commander, IReadOnlyCollection<int> mechIds, Cell rally)
    {
        var mechs = view.Units.Where(u => mechIds.Contains(u.Id) && u.Fighting).ToList();
        var enemies = view.Enemies(commander).ToList();
        if (mechs.Count == 0)
        {
            return BattleView.Dist(commander.Pos, rally) <= SearchRadius ? null : Best(view, rally, rally, enemies, null);
        }

        var front = new Cell((int)Math.Round(mechs.Average(m => m.Pos.X)), (int)Math.Round(mechs.Average(m => m.Pos.Z)));
        var threat = enemies.OrderBy(e => BattleView.Dist(e.Pos, front)).FirstOrDefault();
        if (threat is null)
        {
            return BattleView.Dist(commander.Pos, front) <= Standoff ? null : Best(view, Toward(front, commander.Pos, Standoff), front, enemies, front);
        }

        Cell ideal;
        if (Threatened(view, commander))
        {
            var near = enemies.OrderBy(e => BattleView.Dist(e.Pos, commander.Pos)).First();
            ideal = Away(commander.Pos, near.Pos, FallBack);
            if (BattleView.Dist(ideal, front) > MaxFromMechs)
            {
                ideal = Toward(front, ideal, MaxFromMechs);
            }
        }
        else
        {
            ideal = Away(front, threat.Pos, Standoff);
        }

        var best = Best(view, ideal, ideal, enemies, front);
        return best is { } cell && BattleView.Dist(cell, commander.Pos) < 2 ? null : best;
    }

    /// <summary>The open cell near <paramref name="ideal"/> that's nearest it, preferring cells out of the enemy's
    /// reach and, with mechs, in sight of their centre.</summary>
    private static Cell? Best(BattleView view, Cell ideal, Cell centre, IReadOnlyList<BattleUnit> enemies, Cell? front) =>
        view.OpenAround(centre, 0, SearchRadius)
            .Select(c => (Cell: c, Score: BattleView.Dist(c, ideal)
                + (enemies.Any(e => BattleView.Dist(e.Pos, c) <= ThreatRange) ? 20 : 0)
                + (front is { } f && !view.Sight(c, f) ? 10 : 0)))
            .OrderBy(x => x.Score).ThenBy(x => x.Cell.X).ThenBy(x => x.Cell.Z)
            .Select(x => (Cell?)x.Cell)
            .FirstOrDefault();

    /// <summary><paramref name="distance"/> cells from <paramref name="from"/>, directly away from <paramref name="enemy"/>.</summary>
    public static Cell Away(Cell from, Cell enemy, float distance)
    {
        var dx = from.X - enemy.X;
        var dz = from.Z - enemy.Z;
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        return length < 0.01f ? from : new Cell(from.X + (int)Math.Round(dx / length * distance), from.Z + (int)Math.Round(dz / length * distance));
    }

    /// <summary>The point <paramref name="distance"/> cells from <paramref name="from"/> toward <paramref name="to"/> (or
    /// <paramref name="to"/> if it's nearer).</summary>
    public static Cell Toward(Cell from, Cell to, float distance)
    {
        var length = BattleView.Dist(from, to);
        return length <= distance || length < 0.01f
            ? to
            : new Cell(from.X + (int)Math.Round((to.X - from.X) / length * distance), from.Z + (int)Math.Round((to.Z - from.Z) / length * distance));
    }
}

/// <summary>
/// The Cantor's cluster drop (War in Heaven tech spec §4.3, the orbital strike's mirror, fairness rule 1): a living
/// Cantor who can see a target (or a standing signal spire) calls a few mechs down in drop pods near it. The landing
/// spot is marked for 6–10 seconds first, on open sky only, and the drop is called off if the Cantor falls or loses
/// sight. A cooldown and a per-raid cap from the budget keep it in check; the targets are chosen as for strikes.
/// </summary>
public static class ClusterDropRules
{
    /// <summary>Between drops on one map: about a minute of game time.</summary>
    public const int CooldownTicks = 3600;

    public const int MinCap = 1;
    public const int MaxCap = 4;

    /// <summary>Budget points per drop in a raid.</summary>
    public const float BudgetPerDrop = 1800f;

    /// <summary>The share of <see cref="BudgetPerDrop"/> each drop's mechs are worth.</summary>
    public const float DropShare = 0.35f;

    /// <summary>A Cantor sees this far.</summary>
    public const float Sight = 55f;

    /// <summary>The pods land this far from the target: close enough to threaten it, never on it.</summary>
    public const float MinLanding = 6f;

    public const float MaxLanding = 14f;

    public static int CapFor(float budget) => Math.Clamp((int)Math.Round(budget / BudgetPerDrop), MinCap, MaxCap);

    /// <summary>What one drop's mechs are worth.</summary>
    public static float Points(float budget) => Math.Max(150f, budget / CapFor(budget) * DropShare);

    /// <summary>Null if a drop may be called now, else why not.</summary>
    public static string? Blocked(StrikeState state, bool cantorUp, bool spireStanding, bool dropsDown, int tick)
    {
        if (dropsDown)
        {
            return "the colony knocked out the Choir's signal spire";
        }

        if (!cantorUp && !spireStanding)
        {
            return "no Cantor or signal spire to call a drop";
        }

        if (state.Cap > 0 && state.StrikesThisRaid >= state.Cap)
        {
            return $"this raid's {state.Cap} cluster drops are spent";
        }

        if (state.LastStrikeTick >= 0 && tick - state.LastStrikeTick < CooldownTicks)
        {
            return $"cooling down ({(CooldownTicks - (tick - state.LastStrikeTick)) / StrikeRules.TicksPerSecond} s)";
        }

        return null;
    }

    /// <summary>The target to drop near: as a strike would choose it (turrets, clustered colonists, power), never under
    /// overhead mountain or where the Choir already stands.</summary>
    public static StrikeCandidate? Target(IEnumerable<StrikeCandidate> candidates, StrikeState state) =>
        StrikeRules.Pick(candidates, state)?.Target;

    /// <summary>Where the pods land near the target: open sky only (fairness rule 3), standable, not fogged, between
    /// <see cref="MinLanding"/> and <see cref="MaxLanding"/> cells away.</summary>
    public static Cell? Landing(Cell target, IEnumerable<DropCell> cells) => Tactics.DropSpot(target, cells, MinLanding, MaxLanding);
}

/// <summary>Where mechs burrow up (War in Heaven tech spec §4.3, the drop pod's mirror, fairness rule 3).</summary>
public static class Burrowing
{
    /// <summary>Vanilla's tunnel takes 26–30 seconds to break the surface, with a rumble and dust: the warning.</summary>
    public const int MinDelayTicks = 1560;

    public const int MaxDelayTicks = 1800;

    /// <summary>
    /// A cell to burrow up in, near <paramref name="target"/>: never under overhead mountain (fairness rule 3), standable
    /// and not fogged, between <paramref name="minDist"/> and <paramref name="maxDist"/> cells away; nearest first. Unlike
    /// a drop pod, a constructed roof doesn't stop it.
    /// </summary>
    public static Cell? Spot(Cell target, IEnumerable<DropCell> cells, float minDist = 3, float maxDist = 12) =>
        cells.Where(c => c.Standable && !c.Fogged && c.Roof != RoofKind.Thick)
            .Select(c => (c.Cell, D: BattleView.Dist(c.Cell, target)))
            .Where(x => x.D >= minDist && x.D <= maxDist)
            .OrderBy(x => x.D).ThenBy(x => x.Cell.X).ThenBy(x => x.Cell.Z)
            .Select(x => (Cell?)x.Cell)
            .FirstOrDefault();

    /// <summary>Up to <paramref name="count"/> different cells, each at least two apart, so tunnels don't stack.</summary>
    public static IReadOnlyList<Cell> Spots(Cell target, IReadOnlyList<DropCell> cells, int count, float minDist = 3, float maxDist = 12)
    {
        var picked = new List<Cell>();
        var left = cells.ToList();
        while (picked.Count < count && Spot(target, left, minDist, maxDist) is { } spot)
        {
            picked.Add(spot);
            left.RemoveAll(c => BattleView.Dist(c.Cell, spot) < 2);
        }

        return picked;
    }
}

/// <summary>
/// The Hum (War in Heaven tech spec §4.3, the psychic assault's mirror): while a Choir resonator stands on the map,
/// colonists within its reach who can feel it work slower and feel worse. Destroying the resonators ends it. A
/// resonator also falls silent by itself after <see cref="MaxTicks"/>, so a hidden one can't hum forever.
/// </summary>
public static class HumRules
{
    /// <summary>How far a resonator's hum carries.</summary>
    public const float Radius = 70f;

    /// <summary>How often the hum is refreshed on each colonist; the effect lasts a little longer than this.</summary>
    public const int RefreshTicks = 250;

    public const int EffectTicks = 600;

    /// <summary>A resonator hums for at most two days.</summary>
    public const int MaxTicks = 120000;

    /// <summary>The pods land this far from the colony's centre: out of the way, but not out of reach.</summary>
    public const float MinDrop = 18f;

    public const float MaxDrop = 34f;

    /// <summary>Resonators per operation, by stage: one, then two from open war.</summary>
    public static int Resonators(CrisisStage stage) => stage >= CrisisStage.War ? 2 : 1;

    /// <summary>The hum's strength on a colonist: none without psychic sensitivity; stronger above normal.</summary>
    public static float Severity(float psychicSensitivity) => psychicSensitivity <= 0.01f ? 0 : Math.Clamp(psychicSensitivity, 0.25f, 2f);

    public static bool Reaches(Cell resonator, Cell pawn) => BattleView.Dist(resonator, pawn) <= Radius;
}
