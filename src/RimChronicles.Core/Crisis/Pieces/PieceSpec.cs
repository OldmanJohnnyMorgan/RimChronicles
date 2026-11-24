namespace RimChronicles.Core.Crisis.Pieces;

/// <summary>A rotation in quarter turns, numbered as RimWorld's <c>Rot4</c> (North 0, East 1, South 2, West 3). A piece
/// side rotates with the piece: the North side of a piece turned East faces East.</summary>
public enum Side
{
    North,
    East,
    South,
    West,
}

/// <summary>What fills a cell of a piece, for the planner and the validators. Ordered: when two things share a cell, the
/// higher kind wins.</summary>
public enum CellKind
{
    /// <summary>Nothing: open ground.</summary>
    Empty,

    /// <summary>Floor laid by the piece.</summary>
    Floor,

    /// <summary>A building pawns can pass through but not stand in (sandbags, turrets, furniture).</summary>
    PassThrough,

    Door,

    /// <summary>Impassable: walls and other impassable buildings.</summary>
    Wall,
}

public readonly record struct Cell(int X, int Z)
{
    public static Cell operator +(Cell a, Cell b) => new(a.X + b.X, a.Z + b.Z);

    public override string ToString() => $"({X},{Z})";
}

/// <summary>One thing in a piece, as plain data: where the prefab puts it (unrotated) and what the game says about its
/// def.</summary>
public sealed record PieceThing
{
    public required string Def { get; init; }

    /// <summary>The thing's position in the prefab, before the piece is rotated.</summary>
    public required Cell At { get; init; }

    public int SizeX { get; init; } = 1;
    public int SizeZ { get; init; } = 1;

    /// <summary>Vanilla <c>ThingDef.rotatable</c>.</summary>
    public bool Rotatable { get; init; } = true;

    /// <summary>The prefab's <c>relativeRotation</c>, in quarter turns clockwise.</summary>
    public int RelativeTurns { get; init; }

    public CellKind Kind { get; init; } = CellKind.PassThrough;

    /// <summary>The thing draws or stores power.</summary>
    public bool UsesPower { get; init; }

    /// <summary>For a non-rotatable thing, its <c>defaultPlacingRot</c>. Only North is supported (the check says so).</summary>
    public Side PlacingRot { get; init; } = Side.North;
}

/// <summary>What a piece's <c>PieceExtension</c> says about it (tech spec §3.1).</summary>
public sealed record PieceMeta
{
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The sides it connects on, unrotated.</summary>
    public IReadOnlyList<Side> DoorSides { get; init; } = [];

    public bool NeedsPower { get; init; }
    public int DefenseValue { get; init; }
    public float ThreatCost { get; init; }

    /// <summary>For sieges: the stage it is built in (1–5, crisis spec §5.5).</summary>
    public int BuildOrder { get; init; } = 1;
}

/// <summary>A piece as the prefab defines it: unrotated, with its things and floor cells.</summary>
public sealed record PieceSource
{
    public required string Name { get; init; }
    public required int SizeX { get; init; }
    public required int SizeZ { get; init; }
    public IReadOnlyList<PieceThing> Things { get; init; } = [];
    public IReadOnlyList<Cell> Floor { get; init; } = [];
    public PieceMeta Meta { get; init; } = new();
}

/// <summary>
/// A piece at one rotation, as the site planner and the siege planner see it: its footprint, a kind per cell and the
/// sides it connects on (crisis spec §3). Cell (0, 0) is the footprint's south-west corner.
/// </summary>
public sealed class PieceSpec
{
    private readonly CellKind[,] _cells;

    private PieceSpec(PieceSource source, Side rotation, CellKind[,] cells, IReadOnlyList<Side> doorSides, IReadOnlyList<string> outside,
        IReadOnlyList<Cell> powerCells)
    {
        Source = source;
        Rotation = rotation;
        _cells = cells;
        DoorSides = doorSides;
        Outside = outside;
        PowerCells = powerCells;
    }

    public PieceSource Source { get; }
    public string Name => Source.Name;
    public PieceMeta Meta => Source.Meta;
    public Side Rotation { get; }
    public int SizeX => _cells.GetLength(0);
    public int SizeZ => _cells.GetLength(1);
    public IReadOnlyList<Side> DoorSides { get; }

    /// <summary>Things whose cells fall outside the footprint at this rotation (a broken piece; empty when it's fine).</summary>
    public IReadOnlyList<string> Outside { get; }

    /// <summary>Cells holding a thing that draws, makes or stores power, in footprint coordinates: where a conduit
    /// connects it.</summary>
    public IReadOnlyList<Cell> PowerCells { get; }

    public CellKind this[int x, int z] => _cells[x, z];

    public CellKind this[Cell c] => _cells[c.X, c.Z];

    public static bool Passable(CellKind kind) => kind != CellKind.Wall;

    /// <summary>The cells along one side of the footprint, west to east or south to north.</summary>
    public IEnumerable<Cell> Edge(Side side) => side switch
    {
        Side.North => Enumerable.Range(0, SizeX).Select(x => new Cell(x, SizeZ - 1)),
        Side.South => Enumerable.Range(0, SizeX).Select(x => new Cell(x, 0)),
        Side.East => Enumerable.Range(0, SizeZ).Select(z => new Cell(SizeX - 1, z)),
        _ => Enumerable.Range(0, SizeZ).Select(z => new Cell(0, z)),
    };

    /// <summary>A text picture, north at the top: <c>.</c> empty, <c>_</c> floor, <c>o</c> pass-through, <c>D</c> door, <c>#</c> wall.</summary>
    public string Picture()
    {
        var rows = new List<string>();
        for (var z = SizeZ - 1; z >= 0; z--)
        {
            rows.Add(new string(Enumerable.Range(0, SizeX).Select(x => _cells[x, z] switch
            {
                CellKind.Floor => '_',
                CellKind.PassThrough => 'o',
                CellKind.Door => 'D',
                CellKind.Wall => '#',
                _ => '.',
            }).ToArray()));
        }

        return string.Join("\n", rows);
    }

    /// <summary>The piece turned to <paramref name="rotation"/>, with every thing placed as RimWorld's
    /// <c>PrefabUtility.SpawnPrefab</c> places it (see <see cref="PieceRotation"/>).</summary>
    public static PieceSpec Build(PieceSource source, Side rotation)
    {
        var (sizeX, sizeZ) = PieceRotation.Turned(source.SizeX, source.SizeZ, rotation);
        var cells = new CellKind[Math.Max(0, sizeX), Math.Max(0, sizeZ)];
        var outside = new List<string>();
        var root = PieceRotation.Root(sizeX, sizeZ, rotation);

        void Mark(Cell c, CellKind kind, string what)
        {
            if (c.X < 0 || c.Z < 0 || c.X >= sizeX || c.Z >= sizeZ)
            {
                outside.Add($"{what} at {c}");
                return;
            }

            if (kind > cells[c.X, c.Z])
            {
                cells[c.X, c.Z] = kind;
            }
        }

        foreach (var floor in source.Floor)
        {
            Mark(root + PieceRotation.Local(floor, rotation), CellKind.Floor, "floor");
        }

        var power = new List<Cell>();
        foreach (var thing in source.Things)
        {
            foreach (var cell in PieceRotation.Occupied(thing, rotation))
            {
                Mark(root + cell, thing.Kind, thing.Def);
                if (thing.UsesPower)
                {
                    power.Add(root + cell);
                }
            }
        }

        var doors = source.Meta.DoorSides.Select(s => PieceRotation.Turn(s, rotation)).Distinct().OrderBy(s => s).ToList();
        return new PieceSpec(source, rotation, cells, doors, outside, power.Distinct().ToList());
    }
}
