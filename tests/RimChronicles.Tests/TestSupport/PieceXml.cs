using System.Globalization;
using System.Xml.Linq;
using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Tests.TestSupport;

/// <summary>
/// Reads the repo's crisis pieces (<c>Defs/PrefabDefs/</c>) into <see cref="PieceSource"/>s without RimWorld, the way
/// <c>PieceLibrary.ToSource</c> does in game. Thing sizes and kinds come from <see cref="Footprints"/>, copied from the
/// game's XML; a piece that uses a def not listed there fails, so the list must grow with the pieces.
/// </summary>
public static class PieceXml
{
    public sealed record Footprint(int SizeX, int SizeZ, bool Rotatable, CellKind Kind, bool UsesPower = false, Side PlacingRot = Side.North);

    /// <summary>From <c>Data/Core/Defs</c> (1.7) and the repo's <c>Defs/ThingDefs</c>: size, rotatable, passability, door, power. <see cref="FootprintsMatchTheGame"/>
    /// re-checks them when the game's XML is in <c>/workspaces/reference/data</c>.</summary>
    public static readonly IReadOnlyDictionary<string, Footprint> Footprints = new Dictionary<string, Footprint>
    {
        ["Sandbags"] = new(1, 1, false, CellKind.PassThrough),
        ["Barricade"] = new(1, 1, false, CellKind.PassThrough),
        ["Turret_Mortar"] = new(2, 2, true, CellKind.PassThrough),
        ["Turret_MiniTurret"] = new(1, 1, false, CellKind.PassThrough, UsesPower: true),
        ["Wall"] = new(1, 1, false, CellKind.Wall),
        ["Door"] = new(1, 1, false, CellKind.Door),
        ["Bed"] = new(1, 2, true, CellKind.PassThrough, PlacingRot: Side.South),
        ["TorchLamp"] = new(1, 1, false, CellKind.PassThrough),

        // The siege camp's own buildings (Defs/ThingDefs/RimChronicles_Siege.xml).
        ["RimChronicles_ImperialUplink"] = new(3, 2, true, CellKind.PassThrough),
        ["RimChronicles_ImperialShield"] = new(1, 1, false, CellKind.PassThrough),
        ["RimChronicles_PsychicRelay"] = new(2, 2, false, CellKind.PassThrough),
        ["RimChronicles_ImperialDropBeacon"] = new(1, 1, false, CellKind.PassThrough),
    };

    public static string PiecesFile => Repo.Path("Defs", "PrefabDefs", "RimChronicles_Pieces.xml");

    public static IReadOnlyList<PieceSource> Load(string? path = null) =>
        XDocument.Load(path ?? PiecesFile).Root!.Elements("PrefabDef").Select(Parse).ToList();

    public static PieceSource Parse(XElement prefab)
    {
        var name = prefab.Element("defName")?.Value ?? "?";
        var (sx, sz) = Pair(prefab.Element("size")!.Value);
        var things = new List<PieceThing>();
        foreach (var thing in prefab.Element("things")?.Elements() ?? [])
        {
            var def = thing.Name.LocalName;
            if (!Footprints.TryGetValue(def, out var fp))
            {
                throw new InvalidOperationException($"{name}: {def} isn't in PieceXml.Footprints; add its size and kind from the game's XML");
            }

            var turns = thing.Element("relativeRotation")?.Value switch
            {
                "Clockwise" => 1,
                "Opposite" => 2,
                "Counterclockwise" => 3,
                _ => 0,
            };
            foreach (var at in Cells(thing))
            {
                things.Add(new PieceThing
                {
                    Def = def, At = at, SizeX = fp.SizeX, SizeZ = fp.SizeZ, Rotatable = fp.Rotatable, RelativeTurns = turns,
                    Kind = fp.Kind, UsesPower = fp.UsesPower, PlacingRot = fp.PlacingRot,
                });
            }
        }

        var floor = (prefab.Element("terrain")?.Elements() ?? []).SelectMany(t => Rects(t.Element("rects"))).ToList();
        var ext = prefab.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value.EndsWith(".PieceExtension") == true);
        var meta = ext is null
            ? new PieceMeta()
            : new PieceMeta
            {
                Tags = ext.Element("tags")?.Elements("li").Select(e => e.Value).ToList() ?? [],
                DoorSides = ext.Element("doorSides")?.Elements("li").Select(e => Enum.Parse<Side>(e.Value, ignoreCase: true)).ToList() ?? [],
                NeedsPower = bool.TryParse(ext.Element("needsPower")?.Value, out var p) && p,
                DefenseValue = int.TryParse(ext.Element("defenseValue")?.Value, out var d) ? d : 0,
                ThreatCost = float.TryParse(ext.Element("threatCost")?.Value, CultureInfo.InvariantCulture, out var t) ? t : 0,
                BuildOrder = int.TryParse(ext.Element("buildOrder")?.Value, out var b) ? b : 1,
            };
        return new PieceSource { Name = name, SizeX = sx, SizeZ = sz, Things = things, Floor = floor, Meta = meta };
    }

    /// <summary>As <c>PrefabDef.GetThings</c>: every rect cell, then every listed position, else the one position (default (0,0,0)).</summary>
    private static IEnumerable<Cell> Cells(XElement thing)
    {
        var cells = Rects(thing.Element("rects")).ToList();
        cells.AddRange(thing.Element("positions")?.Elements("li").Select(e => Position(e.Value)) ?? []);
        if (cells.Count == 0)
        {
            cells.Add(thing.Element("position") is { } p ? Position(p.Value) : new Cell(0, 0));
        }

        return cells;
    }

    private static IEnumerable<Cell> Rects(XElement? rects)
    {
        foreach (var li in rects?.Elements("li") ?? [])
        {
            var n = Numbers(li.Value);
            for (var x = n[0]; x <= n[2]; x++)
            {
                for (var z = n[1]; z <= n[3]; z++)
                {
                    yield return new Cell(x, z);
                }
            }
        }
    }

    private static Cell Position(string text)
    {
        var n = Numbers(text);
        return new Cell(n[0], n[2]);
    }

    private static (int, int) Pair(string text)
    {
        var n = Numbers(text);
        return (n[0], n[1]);
    }

    private static int[] Numbers(string text) =>
        text.Trim().TrimStart('(').TrimEnd(')').Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
}
