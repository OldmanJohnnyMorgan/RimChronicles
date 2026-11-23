using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Core.Crisis.Sites;

/// <summary>A rectangle of cells: its south-west corner and size.</summary>
public readonly record struct Rect(int X, int Z, int W, int H)
{
    public int MaxX => X + W - 1;
    public int MaxZ => Z + H - 1;
    public Cell Centre => new(X + (W / 2), Z + (H / 2));

    public bool Contains(Cell c) => c.X >= X && c.X <= MaxX && c.Z >= Z && c.Z <= MaxZ;

    public Rect Inset(int by) => new(X + by, Z + by, W - (2 * by), H - (2 * by));

    public IEnumerable<Cell> Cells()
    {
        for (var x = X; x < X + W; x++)
        {
            for (var z = Z; z < Z + H; z++)
            {
                yield return new Cell(x, z);
            }
        }
    }

    /// <summary>True when the two share a stretch of edge (touching, not overlapping).</summary>
    public bool Touches(Rect o)
    {
        var xOverlap = Math.Min(MaxX, o.MaxX) - Math.Max(X, o.X) >= 0;
        var zOverlap = Math.Min(MaxZ, o.MaxZ) - Math.Max(Z, o.Z) >= 0;
        return (xOverlap && (MaxZ + 1 == o.Z || o.MaxZ + 1 == Z)) || (zOverlap && (MaxX + 1 == o.X || o.MaxX + 1 == X));
    }

    public override string ToString() => $"[{X},{Z} {W}x{H}]";
}

/// <summary>
/// A site's plan as cells (tech spec §3.2): what fills each cell, which piece owns it, and where conduits run. Cell
/// (0, 0) is the south-west corner. The planner and the validators work on this; the game spawns from it.
/// </summary>
public sealed class CellGrid
{
    private readonly CellKind[,] _kinds;
    private readonly int[,] _owner;
    private readonly bool[,] _conduit;

    public CellGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _kinds = new CellKind[width, height];
        _owner = new int[width, height];
        _conduit = new bool[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                _owner[x, z] = -1;
            }
        }
    }

    public int Width { get; }
    public int Height { get; }

    public bool InBounds(Cell c) => c.X >= 0 && c.Z >= 0 && c.X < Width && c.Z < Height;

    public CellKind this[Cell c]
    {
        get => _kinds[c.X, c.Z];
        set => _kinds[c.X, c.Z] = value;
    }

    /// <summary>The piece (its index in the plan) whose footprint covers the cell, or -1.</summary>
    public int Owner(Cell c) => _owner[c.X, c.Z];

    public bool Conduit(Cell c) => _conduit[c.X, c.Z];

    public void SetConduit(Cell c) => _conduit[c.X, c.Z] = true;

    /// <summary>True when the piece's footprint at <paramref name="min"/> is on the grid, inside <paramref name="area"/>,
    /// and on empty ground no other piece owns.</summary>
    public bool CanPlace(PieceSpec spec, Cell min, Rect area)
    {
        for (var x = 0; x < spec.SizeX; x++)
        {
            for (var z = 0; z < spec.SizeZ; z++)
            {
                var c = new Cell(min.X + x, min.Z + z);
                if (!area.Contains(c) || !InBounds(c) || _owner[c.X, c.Z] >= 0 || _kinds[c.X, c.Z] != CellKind.Empty)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void Place(PieceSpec spec, Cell min, int index)
    {
        for (var x = 0; x < spec.SizeX; x++)
        {
            for (var z = 0; z < spec.SizeZ; z++)
            {
                var c = new Cell(min.X + x, min.Z + z);
                _owner[c.X, c.Z] = index;
                _kinds[c.X, c.Z] = spec[x, z];
            }
        }
    }

    public static readonly Cell[] Neighbours = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    /// <summary>Every cell reachable from <paramref name="starts"/> through cells <paramref name="open"/> allows, four ways.</summary>
    public bool[,] Flood(IEnumerable<Cell> starts, Func<Cell, bool> open)
    {
        var seen = new bool[Width, Height];
        var queue = new Queue<Cell>();
        foreach (var s in starts.Where(s => InBounds(s) && open(s)))
        {
            if (!seen[s.X, s.Z])
            {
                seen[s.X, s.Z] = true;
                queue.Enqueue(s);
            }
        }

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in Neighbours)
            {
                var n = c + d;
                if (InBounds(n) && !seen[n.X, n.Z] && open(n))
                {
                    seen[n.X, n.Z] = true;
                    queue.Enqueue(n);
                }
            }
        }

        return seen;
    }

    /// <summary>The shortest four-way path from any start to any goal through open cells, both ends included; null if none.</summary>
    public List<Cell>? Path(IReadOnlyCollection<Cell> starts, IReadOnlySet<Cell> goals, Func<Cell, bool> open)
    {
        var from = new Dictionary<Cell, Cell?>();
        var queue = new Queue<Cell>();
        foreach (var s in starts.Where(InBounds))
        {
            if (from.TryAdd(s, null))
            {
                queue.Enqueue(s);
            }
        }

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (goals.Contains(c))
            {
                var path = new List<Cell>();
                for (Cell? at = c; at is { } a; at = from[a])
                {
                    path.Add(a);
                }

                path.Reverse();
                return path;
            }

            foreach (var d in Neighbours)
            {
                var n = c + d;
                if (InBounds(n) && !from.ContainsKey(n) && (open(n) || goals.Contains(n)))
                {
                    from[n] = c;
                    queue.Enqueue(n);
                }
            }
        }

        return null;
    }

    /// <summary>The cells along the grid's outer edge.</summary>
    public IEnumerable<Cell> Border()
    {
        for (var x = 0; x < Width; x++)
        {
            yield return new Cell(x, 0);
            yield return new Cell(x, Height - 1);
        }

        for (var z = 1; z < Height - 1; z++)
        {
            yield return new Cell(0, z);
            yield return new Cell(Width - 1, z);
        }
    }
}
