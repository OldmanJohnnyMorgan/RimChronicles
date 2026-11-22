using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Units;

namespace RimChronicles.Core.Crisis.Battle;

/// <summary>Which side a unit is on, seen from the Empire.</summary>
public enum BattleSide
{
    /// <summary>The Empire's own units.</summary>
    Empire,

    /// <summary>The colony and anyone fighting for it.</summary>
    Colony,
}

/// <summary>Conditions the scorers check so they don't stack a cast on a target that already has it.</summary>
public static class BattleEffect
{
    public const string Painblock = "PsychicAnesthesia";
    public const string Invisible = "PsychicInvisibility";
    public const string Burden = "PsychicBurden";
    public const string Vertigo = "PsychicVertigo";
    public const string Blind = "PsychicBlindness";
    public const string Stunned = "Stunned";

    /// <summary>In a mental state (berserk, fleeing).</summary>
    public const string Mental = "Mental";
}

/// <summary>One unit in a fight, as plain data the game reads every 250 ticks (tech spec §4.3).</summary>
public sealed record BattleUnit
{
    public required int Id { get; init; }
    public required BattleSide Side { get; init; }
    public required Cell Pos { get; init; }

    public string Name { get; init; } = "";

    /// <summary>Its crisis role, or null for a colonist or anything else.</summary>
    public UnitRole? Role { get; init; }

    public bool IsTurret { get; init; }
    public bool Downed { get; init; }

    /// <summary>Overall health, 0 to 1.</summary>
    public float Health { get; init; } = 1;

    /// <summary>Pain, 0 to 1.</summary>
    public float Pain { get; init; }

    /// <summary>Damage per second of its weapon (or fists).</summary>
    public float Dps { get; init; }

    public bool Ranged { get; init; }
    public float WeaponRange { get; init; }

    /// <summary>Standing next to something that gives cover (sandbags, walls, rock).</summary>
    public bool InCover { get; init; }

    /// <summary>An armed enemy within its range has a line of sight to it.</summary>
    public bool UnderFire { get; init; }

    /// <summary>Any enemy can see it.</summary>
    public bool Seen { get; init; } = true;

    /// <summary>Tending someone right now (a medic).</summary>
    public bool Tending { get; init; }

    /// <summary>Enemies in melee reach (adjacent, standing).</summary>
    public int MeleeEnemies { get; init; }

    /// <summary>How much it matters, about 1 for a soldier or colonist, more for leaders.</summary>
    public float Value { get; init; } = 1;

    public float PsychicSensitivity { get; init; } = 1;

    /// <summary>Its <see cref="BattleEffect"/>s.</summary>
    public IReadOnlySet<string> Effects { get; init; } = new HashSet<string>();

    public bool Fighting => !Downed && !Effects.Contains(BattleEffect.Mental);

    public bool IsPsykerOrHerald => Role is UnitRole.Adept or UnitRole.Stellarch or UnitRole.Herald;
}

/// <summary>A psycast a psyker can cast right now (the game has already checked heat, psyfocus, level and cooldown).</summary>
public sealed record CastOption
{
    public required string Def { get; init; }

    /// <summary>How far from the caster it can target; 0 when it only targets around the caster.</summary>
    public float Range { get; init; }

    /// <summary>Its area, or 0 for a single target.</summary>
    public float Radius { get; init; }

    /// <summary>For skips: how far from the target its destination may be.</summary>
    public float DestRange { get; init; }

    /// <summary>The caster's neural heat after casting, as a share of its limit.</summary>
    public float HeatAfter { get; init; }

    public float PsyfocusCost { get; init; }

    /// <summary>A psychic effect: weaker on low psychic sensitivity, nothing on zero.</summary>
    public bool Psychic { get; init; }
}

/// <summary>
/// A fight as the psyker AI sees it (tech spec §4.3): every unit, plus the two map questions the scorers ask. Built by
/// the game once per lord every 250 ticks; scorers never touch the game.
/// </summary>
public sealed class BattleView
{
    public BattleView(IReadOnlyList<BattleUnit> units, Func<Cell, bool> open, Func<Cell, Cell, bool> sight)
    {
        Units = units;
        Open = open;
        Sight = sight;
    }

    public IReadOnlyList<BattleUnit> Units { get; }

    /// <summary>A cell a pawn could stand in, with no pawn or building in it.</summary>
    public Func<Cell, bool> Open { get; }

    /// <summary>A clear line of sight between two cells.</summary>
    public Func<Cell, Cell, bool> Sight { get; }

    public static float Dist(Cell a, Cell b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    public IEnumerable<BattleUnit> Enemies(BattleUnit of) => Units.Where(u => u.Side != of.Side && u.Fighting);

    public IEnumerable<BattleUnit> Allies(BattleUnit of) => Units.Where(u => u.Side == of.Side && u.Fighting);

    public IEnumerable<BattleUnit> Near(Cell c, float radius, Func<BattleUnit, bool> which) => Units.Where(u => which(u) && Dist(u.Pos, c) <= radius);

    /// <summary>Open cells in a ring around <paramref name="c"/>, nearest first.</summary>
    public IEnumerable<Cell> OpenAround(Cell c, int minRadius, int maxRadius)
    {
        var cells = new List<(Cell Cell, float D)>();
        for (var dx = -maxRadius; dx <= maxRadius; dx++)
        {
            for (var dz = -maxRadius; dz <= maxRadius; dz++)
            {
                var cell = new Cell(c.X + dx, c.Z + dz);
                var d = Dist(c, cell);
                if (d >= minRadius && d <= maxRadius + 0.01f && Open(cell))
                {
                    cells.Add((cell, d));
                }
            }
        }

        return cells.OrderBy(x => x.D).ThenBy(x => x.Cell.X).ThenBy(x => x.Cell.Z).Select(x => x.Cell);
    }

    public BattleUnit? Find(int id) => Units.FirstOrDefault(u => u.Id == id);
}
