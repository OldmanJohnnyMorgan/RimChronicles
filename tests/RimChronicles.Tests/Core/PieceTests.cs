using System.Xml.Linq;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PieceTests
{
    private static PieceThing One(string def, int x, int z, CellKind kind = CellKind.PassThrough) =>
        new() { Def = def, At = new Cell(x, z), Rotatable = false, Kind = kind };

    private static PieceSource Source(int sx, int sz, IEnumerable<PieceThing> things, params Side[] doors) => new()
    {
        Name = "test",
        SizeX = sx,
        SizeZ = sz,
        Things = things.ToList(),
        Meta = new PieceMeta { Tags = ["Test"], DoorSides = doors },
    };

    // ---- the repo's pieces

    public static TheoryData<string> PieceNames => new(PieceXml.Load().Select(p => p.Name));

    [Theory]
    [MemberData(nameof(PieceNames))]
    public void EveryPiece_FitsItsSizeAndDoorsAtEveryRotation(string name)
    {
        var piece = PieceXml.Load().Single(p => p.Name == name);
        Assert.Empty(PieceCheck.Problems(piece));
    }

    [Fact]
    public void ThePiecesFile_HasTheStarterSet_WithUniqueNames()
    {
        var names = PieceXml.Load().Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.StartsWith("RimChronicles_Piece_", n));
        Assert.Superset(new HashSet<string>
        {
            "RimChronicles_Piece_SandbagLine", "RimChronicles_Piece_SandbagCorner", "RimChronicles_Piece_MortarPit",
            "RimChronicles_Piece_TurretNest", "RimChronicles_Piece_Barracks",
        }, names.ToHashSet());
    }

    [Fact]
    public void MortarPit_LooksLikeTheSpec()
    {
        var pit = PieceSpec.Build(PieceXml.Load().Single(p => p.Name == "RimChronicles_Piece_MortarPit"), Side.North);
        Assert.Equal("ooooo\no.ooo\no.ooo\no...o\noo.oo", pit.Picture()); // the 2×2 mortar sits against the east sandbags
        Assert.Equal([Side.South], pit.DoorSides);
    }

    [Fact]
    public void Barracks_IsAWalledRoomWithADoorAndThreeBeds()
    {
        var barracks = PieceSpec.Build(PieceXml.Load().Single(p => p.Name == "RimChronicles_Piece_Barracks"), Side.North);
        Assert.Equal("#######\n#o.o.o#\n#o.o.o#\n#.....#\n#o....#\n###D###", barracks.Picture());
    }

    [Fact]
    public void FootprintsMatchTheGame()
    {
        var root = "/workspaces/reference/data/Core/Defs";
        if (!Directory.Exists(root))
        {
            return; // the game's XML is only in the codespace, never in the repo or CI
        }

        var defs = new Dictionary<string, XElement>();
        var files = Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Repo.Path("Defs"), "*.xml", SearchOption.AllDirectories)); // the mod's own, after the game's
        foreach (var file in files)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (var def in doc.Root!.Elements("ThingDef"))
            {
                if ((def.Element("defName")?.Value ?? def.Attribute("Name")?.Value) is { } key)
                {
                    defs[key] = def;
                }
            }
        }

        string? Get(XElement? def, string path)
        {
            for (var i = 0; def is not null && i < 12; i++)
            {
                var node = path.Split('/').Aggregate((XElement?)def, (e, p) => e?.Element(p));
                if (node is not null)
                {
                    return node.Value;
                }

                def = def.Attribute("ParentName") is { } parent && defs.TryGetValue(parent.Value, out var p) ? p : null;
            }

            return null;
        }

        foreach (var (name, fp) in PieceXml.Footprints)
        {
            var def = defs[name];
            var size = Get(def, "size") ?? "(1,1)";
            Assert.Equal($"({fp.SizeX},{fp.SizeZ})", size.Replace(" ", ""));
            Assert.Equal(fp.Rotatable, !string.Equals(Get(def, "rotatable"), "false", StringComparison.OrdinalIgnoreCase));
            var passability = Get(def, "passability");
            var kind = name == "Door" ? CellKind.Door : passability == "Impassable" ? CellKind.Wall
                : Get(def, "category") == "Item" ? CellKind.Floor : CellKind.PassThrough;
            Assert.Equal(fp.Kind, kind);
        }
    }

    // ---- rotation, against vanilla's prefab placement

    [Fact]
    public void Rotation_TurnsClockwise_FromTheCornerVanillaCountsFrom()
    {
        // A 3×2 piece with a marker in its south-west corner and a door on its north side.
        var source = Source(3, 2, [One("Marker", 0, 0)], Side.North);
        var east = PieceSpec.Build(source, Side.East);
        Assert.Equal((2, 3), (east.SizeX, east.SizeZ));
        Assert.Equal("o.\n..\n..", east.Picture()); // south-west goes to north-west
        Assert.Equal([Side.East], east.DoorSides);

        var south = PieceSpec.Build(source, Side.South);
        Assert.Equal("..o\n...", south.Picture()); // to north-east
        Assert.Equal([Side.South], south.DoorSides);

        var west = PieceSpec.Build(source, Side.West);
        Assert.Equal("..\n..\n.o", west.Picture()); // to south-east
        Assert.Equal([Side.West], west.DoorSides);
    }

    [Fact]
    public void Rotation_FourQuarterTurnsOfSingleCells_ComeBackToTheStart()
    {
        var source = Source(4, 3, [One("a", 0, 0), One("b", 3, 1, CellKind.Wall), One("c", 1, 2, CellKind.Door)], Side.South);
        var north = PieceSpec.Build(source, Side.North).Picture();
        var turned = source;
        for (var i = 0; i < 4; i++)
        {
            var spec = PieceSpec.Build(turned, Side.East);
            turned = turned with
            {
                SizeX = spec.SizeX,
                SizeZ = spec.SizeZ,
                Things = Enumerable.Range(0, spec.SizeX).SelectMany(x => Enumerable.Range(0, spec.SizeZ).Select(z => (x, z)))
                    .Where(c => spec[c.x, c.z] != CellKind.Empty).Select(c => One("t", c.x, c.z, spec[c.x, c.z])).ToList(),
            };
        }

        Assert.Equal(north, PieceSpec.Build(turned, Side.North).Picture());
    }

    [Fact]
    public void Rotation_ARotatableEvenSizedThing_ShiftsAsVanillaShiftsIt()
    {
        // GenAdj.OccupiedRect for a 2×2 at (0,0): north covers (0..1, 0..1); east shifts z by -1; south both; west x.
        var mortar = new PieceThing { Def = "m", At = new Cell(0, 0), SizeX = 2, SizeZ = 2, Rotatable = true };
        Assert.Equal([new(0, 0), new(0, 1), new(1, 0), new(1, 1)], PieceRotation.Rect(new Cell(0, 0), Side.North, 2, 2).ToList());
        Assert.Equal([new(0, -1), new(0, 0), new(1, -1), new(1, 0)], PieceRotation.Rect(new Cell(0, 0), Side.East, 2, 2).ToList());
        Assert.Equal([new(-1, -1), new(-1, 0), new(0, -1), new(0, 0)], PieceRotation.Rect(new Cell(0, 0), Side.South, 2, 2).ToList());
        Assert.Equal([new(-1, 0), new(-1, 1), new(0, 0), new(0, 1)], PieceRotation.Rect(new Cell(0, 0), Side.West, 2, 2).ToList());
        Assert.Equal(4, PieceRotation.Occupied(mortar, Side.East).Count());
    }

    [Fact]
    public void Rotation_ABedTurnsWithThePiece()
    {
        var bed = new PieceThing { Def = "Bed", At = new Cell(0, 0), SizeX = 1, SizeZ = 2, Rotatable = true };
        var source = Source(1, 2, [bed], Side.South);
        Assert.Equal("o\no", PieceSpec.Build(source, Side.North).Picture());
        Assert.Equal("oo", PieceSpec.Build(source, Side.East).Picture());
        Assert.Empty(PieceSpec.Build(source, Side.South).Outside);
        Assert.Empty(PieceSpec.Build(source, Side.West).Outside);
    }

    // ---- the checks

    [Fact]
    public void Check_FindsAThingOutsideTheSize()
    {
        var problems = PieceCheck.Problems(Source(3, 3, [One("Sandbags", 3, 0)], Side.South));
        Assert.Contains(problems, p => p.Contains("Sandbags at") && p.Contains("outside"));
    }

    [Fact]
    public void Check_FindsAWalledDoorSideWithoutADoor_AndAnAllWallSide()
    {
        var walls = Enumerable.Range(0, 3).Select(x => One("Wall", x, 0, CellKind.Wall)).ToList();
        Assert.Contains(PieceCheck.Problems(Source(3, 2, walls, Side.South)), p => p.Contains("all wall"));

        walls.RemoveAt(1);
        Assert.Contains(PieceCheck.Problems(Source(3, 2, walls, Side.South)), p => p.Contains("walled but has no door"));

        walls.Add(One("Door", 1, 0, CellKind.Door));
        Assert.Empty(PieceCheck.Problems(Source(3, 2, walls, Side.South)));
    }

    [Fact]
    public void Check_FindsOverlaps_MissingDoorsAndTags_BadOrder_AndUnflaggedPower()
    {
        var source = Source(3, 3, [One("Wall", 1, 1, CellKind.Wall), One("Sandbags", 1, 1)]) with
        {
            Meta = new PieceMeta { BuildOrder = 9, ThreatCost = -1 },
        };
        var problems = PieceCheck.Problems(source);
        Assert.Contains(problems, p => p.Contains("overlap"));
        Assert.Contains(problems, p => p.Contains("no door sides"));
        Assert.Contains(problems, p => p.Contains("no tags"));
        Assert.Contains(problems, p => p.Contains("build order 9"));
        Assert.Contains(problems, p => p.Contains("can't be negative"));

        var powered = Source(3, 3, [new PieceThing { Def = "Turret", At = new Cell(1, 1), Rotatable = false, UsesPower = true }], Side.South);
        Assert.Contains(PieceCheck.Problems(powered), p => p.Contains("needsPower must be true"));
    }

    [Fact]
    public void Check_RefusesANonRotatableThingThatDoesntFaceNorth()
    {
        var odd = new PieceThing { Def = "Odd", At = new Cell(1, 1), Rotatable = false, PlacingRot = Side.South };
        Assert.Contains(PieceCheck.Problems(Source(3, 3, [odd], Side.South)), p => p.Contains("doesn't face north"));
    }

    [Fact]
    public void Check_ABadSizeStopsAtOnce()
    {
        Assert.Single(PieceCheck.Problems(Source(0, 3, [], Side.South)));
    }
}
