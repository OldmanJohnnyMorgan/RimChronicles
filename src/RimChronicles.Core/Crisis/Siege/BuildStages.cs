using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Core.Crisis.Siege;

/// <summary>What finishing a siege stage gives the Empire (crisis spec §5.5).</summary>
public enum SiegeUnlock
{
    Camp,
    Bombardment,
    OrbitalStrikes,
    ShieldAndRelay,
    Reinforcements,
}

/// <summary>One stage of the camp: its number (a piece's <c>buildOrder</c>), name and what it unlocks.</summary>
public sealed record SiegeStageSpec(int Number, string Name, SiegeUnlock Unlock, string Built);

/// <summary>
/// The siege camp's five stages (crisis spec §5.5). Each stage's pieces are placed as blueprints only once every
/// blueprint and frame of the stage before is gone (built, or wrecked by the colony): the player's chance to strike
/// while the camp is weak.
/// </summary>
public static class BuildStages
{
    public const int First = 1;
    public const int Last = 5;

    public static readonly IReadOnlyList<SiegeStageSpec> All =
    [
        new(1, "sandbag lines and tents", SiegeUnlock.Camp, "The Imperial siege camp has its sandbag lines up."),
        new(2, "mortar pits", SiegeUnlock.Bombardment, "The Imperial camp's mortar pits are built: the shelling starts now."),
        new(3, "a herald uplink", SiegeUnlock.OrbitalStrikes, "The Imperial camp's uplink is up: it can call orbital strikes without a herald."),
        new(4, "a shield emitter and psychic relay", SiegeUnlock.ShieldAndRelay, "The Imperial camp's shield and psychic relay are up: shells won't reach it, and its psykers recover faster."),
        new(5, "a drop-pod beacon", SiegeUnlock.Reinforcements, "The Imperial camp's drop-pod beacon is lit: reinforcements will land there."),
    ];

    public static SiegeStageSpec Get(int number) => All[Math.Clamp(number, First, Last) - 1];

    /// <summary>The stage to be on: the next one when nothing of the current stage is left to build, else the same.
    /// Stage 0 means nothing is placed yet. After the last stage it stays there.</summary>
    public static int Next(int current, int pendingInCurrent) =>
        current < First ? First : pendingInCurrent > 0 || current >= Last ? current : current + 1;

    /// <summary>True when the last stage is placed and fully built (or wrecked): nothing more to build.</summary>
    public static bool Done(int current, int pendingInCurrent) => current >= Last && pendingInCurrent == 0;
}

/// <summary>A place for a piece in the camp, in the camp's own frame: x to the right, z toward the colony, cells
/// counted from the camp's centre.</summary>
public sealed record CampSlot(int Stage, string Tag, Cell Min, Side Rotation);

/// <summary>A piece placed in the camp, in map directions: its south-west corner relative to the camp's centre and its
/// rotation. The game stamps it as blueprints at <c>spot + Min</c>.</summary>
public sealed record CampPlacement(int Stage, PieceSource Piece, Cell Min, Side Rotation, int SizeX, int SizeZ)
{
    public IEnumerable<Cell> Cells() =>
        Enumerable.Range(Min.X, SizeX).SelectMany(x => Enumerable.Range(Min.Z, SizeZ).Select(z => new Cell(x, z)));
}

/// <summary>
/// The camp's layout (crisis spec §5.5): a sandbag line facing the colony with lines on both flanks and tents behind;
/// mortar pits behind the line, open to the rear; the uplink, shield and relay, and beacon further back. The same
/// layout turns to face the colony on any side. Each slot takes the first piece (by name) with its tag built in its
/// stage, so new pieces can replace the starter ones from XML.
/// </summary>
public static class CampLayout
{
    /// <summary>Cells from the centre to the camp's farthest corner.</summary>
    public const int Radius = 17;

    public static readonly IReadOnlyList<CampSlot> Slots =
    [
        new(1, "Line", new Cell(-8, 6), Side.North),
        new(1, "Line", new Cell(-2, 6), Side.North),
        new(1, "Line", new Cell(4, 6), Side.North),
        new(1, "Line", new Cell(-10, 0), Side.East),
        new(1, "Line", new Cell(10, 0), Side.East),
        new(1, "Barracks", new Cell(-3, -13), Side.North),
        new(2, "Artillery", new Cell(-8, 0), Side.North),
        new(2, "Artillery", new Cell(3, 0), Side.North),
        new(3, "Uplink", new Cell(-2, -5), Side.North),
        new(4, "Relay", new Cell(-2, 0), Side.North),
        new(5, "Beacon", new Cell(5, -7), Side.North),
    ];

    /// <summary>The camp facing <paramref name="facing"/> with these pieces. A slot with no fitting piece is left out.</summary>
    public static IReadOnlyList<CampPlacement> Plan(IEnumerable<PieceSource> pieces, Side facing)
    {
        var byName = pieces.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        var placements = new List<CampPlacement>();
        foreach (var slot in Slots)
        {
            if (byName.FirstOrDefault(p => p.Meta.BuildOrder == slot.Stage && p.Meta.Tags.Contains(slot.Tag)) is not { } piece)
            {
                continue;
            }

            var (lx, lz) = PieceRotation.Turned(piece.SizeX, piece.SizeZ, slot.Rotation);
            var a = Turn(slot.Min, facing);
            var b = Turn(new Cell(slot.Min.X + lx - 1, slot.Min.Z + lz - 1), facing);
            var min = new Cell(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z));
            var rotation = PieceRotation.Turn(slot.Rotation, facing);
            var (sx, sz) = PieceRotation.Turned(piece.SizeX, piece.SizeZ, rotation);
            placements.Add(new CampPlacement(slot.Stage, piece, min, rotation, sx, sz));
        }

        return placements;
    }

    /// <summary>A cell of the camp's frame in map directions: the camp's front (+z) turned to face <paramref name="facing"/>.</summary>
    public static Cell Turn(Cell c, Side facing) => facing switch
    {
        Side.East => new Cell(c.Z, -c.X),
        Side.South => new Cell(-c.X, -c.Z),
        Side.West => new Cell(-c.Z, c.X),
        _ => c,
    };
}
