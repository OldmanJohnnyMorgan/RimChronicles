using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Core.Crisis.Operations;

/// <summary>How roofed a cell is, for drop pods.</summary>
public enum RoofKind
{
    None,
    Constructed,

    /// <summary>Overhead mountain.</summary>
    Thick,
}

/// <summary>A cell a drop pod could land on, as the game reads it.</summary>
public sealed record DropCell(Cell Cell, RoofKind Roof, bool Standable, bool Fogged = false);

/// <summary>Shared tactics for every operation (crisis spec §5.2) and the drop-pod rule (fairness rule 3).</summary>
public static class Tactics
{
    /// <summary>
    /// The squad's shared target: a colonist tending the wounded first (medics), then the one easiest to bring down
    /// (lowest health, most hurt), nearest the squad. Turrets and anyone already down are left to normal combat.
    /// </summary>
    public static BattleUnit? FocusTarget(BattleView view, BattleSide side, Cell squad, float maxRange = 45)
    {
        return view.Units
            .Where(u => u.Side != side && u.Fighting && !u.IsTurret && BattleView.Dist(u.Pos, squad) <= maxRange)
            .OrderByDescending(u => u.Tending)
            .ThenBy(u => u.Health - (0.5f * u.Pain) + (BattleView.Dist(u.Pos, squad) / 100))
            .ThenBy(u => u.Id)
            .FirstOrDefault();
    }

    /// <summary>Where a drop pod may land near <paramref name="target"/>: open sky only, never under a roof of any
    /// kind (so never in a room under overhead mountain, fairness rule 3), standable, not fogged, at least
    /// <paramref name="minDist"/> cells away. Nearest first.</summary>
    public static Cell? DropSpot(Cell target, IEnumerable<DropCell> cells, float minDist = 3, float maxDist = 12) =>
        cells.Where(c => c.Standable && !c.Fogged && c.Roof == RoofKind.None)
            .Select(c => (c.Cell, D: BattleView.Dist(c.Cell, target)))
            .Where(x => x.D >= minDist && x.D <= maxDist)
            .OrderBy(x => x.D).ThenBy(x => x.Cell.X).ThenBy(x => x.Cell.Z)
            .Select(x => (Cell?)x.Cell)
            .FirstOrDefault();

    /// <summary>Is it safe to carry a downed ally off: no standing enemy within <paramref name="radius"/>.</summary>
    public static bool SafeToCarry(BattleView view, BattleUnit downed, float radius = 10) =>
        !view.Units.Any(u => u.Side != downed.Side && u.Fighting && !u.IsTurret && BattleView.Dist(u.Pos, downed.Pos) <= radius);
}
