namespace RimChronicles.Core.Crisis.Pieces;

/// <summary>
/// How RimWorld 1.7 places a prefab's things when it turns the prefab, restated on plain data so the planner's
/// footprints match what spawns (crisis C2; see <c>docs/ENGINE_NOTES.md</c>, "Crisis survey"). It mirrors
/// <c>PrefabUtility.GetRoot</c> / <c>GetAdjustedLocalPosition</c> / <c>GetAdjustedThingLocalPosition</c> and
/// <c>GenAdj.OccupiedRect</c> / <c>AdjustForRotation</c> with North as the reference rotation. Coordinates are relative
/// to the turned footprint's south-west corner.
/// </summary>
public static class PieceRotation
{
    public static Side Turn(Side side, Side by) => (Side)(((int)side + (int)by) % 4);

    public static bool Horizontal(Side side) => side is Side.East or Side.West;

    public static (int X, int Z) Turned(int sizeX, int sizeZ, Side rotation) => Horizontal(rotation) ? (sizeZ, sizeX) : (sizeX, sizeZ);

    /// <summary>The footprint corner vanilla counts from: south-west for North, north-west for East, north-east for
    /// South, south-east for West (<c>PrefabUtility.GetRoot</c>).</summary>
    public static Cell Root(int turnedX, int turnedZ, Side rotation) => rotation switch
    {
        Side.East => new Cell(0, turnedZ - 1),
        Side.South => new Cell(turnedX - 1, turnedZ - 1),
        Side.West => new Cell(turnedX - 1, 0),
        _ => new Cell(0, 0),
    };

    /// <summary>A prefab cell turned with the prefab, relative to <see cref="Root"/> (<c>GetAdjustedLocalPosition</c>).</summary>
    public static Cell Local(Cell c, Side rotation) => rotation switch
    {
        Side.East => new Cell(c.Z, -c.X),
        Side.South => new Cell(-c.X, -c.Z),
        Side.West => new Cell(-c.Z, c.X),
        _ => c,
    };

    /// <summary>The cells a thing covers in the turned piece, relative to <see cref="Root"/>.</summary>
    public static IEnumerable<Cell> Occupied(PieceThing thing, Side rotation)
    {
        var center = Local(thing.At, rotation);
        Side facing;
        if (thing.Rotatable)
        {
            facing = Turn(rotation, (Side)(((thing.RelativeTurns % 4) + 4) % 4));
        }
        else
        {
            // Non-rotatable things keep their placing rotation; vanilla shifts an even-sized one so it stays in place.
            center = Shift(center, thing.SizeX, thing.SizeZ, rotation);
            facing = Side.North;
        }

        return Rect(center, facing, thing.SizeX, thing.SizeZ);
    }

    /// <summary><c>GenAdj.OccupiedRect(center, rot, size)</c>.</summary>
    public static IEnumerable<Cell> Rect(Cell center, Side facing, int sizeX, int sizeZ)
    {
        var c = Shift(center, sizeX, sizeZ, facing);
        var (sx, sz) = Turned(sizeX, sizeZ, facing);
        var minX = c.X - (sx - 1) / 2;
        var minZ = c.Z - (sz - 1) / 2;
        for (var x = minX; x < minX + sx; x++)
        {
            for (var z = minZ; z < minZ + sz; z++)
            {
                yield return new Cell(x, z);
            }
        }
    }

    /// <summary><c>GenAdj.AdjustForRotation(ref center, ref size, Rot4.North, rot)</c>: an even side moves the
    /// center by one cell, depending on the rotation.</summary>
    private static Cell Shift(Cell center, int sizeX, int sizeZ, Side rotation)
    {
        if (sizeX == 1 && sizeZ == 1)
        {
            return center;
        }

        var (sx, sz) = Turned(sizeX, sizeZ, rotation);
        var (dx, dz) = rotation switch
        {
            Side.East => (0, -1),
            Side.South => (-1, -1),
            Side.West => (-1, 0),
            _ => (0, 0),
        };
        return new Cell(center.X + (sx % 2 == 0 ? dx : 0), center.Z + (sz % 2 == 0 ? dz : 0));
    }
}
