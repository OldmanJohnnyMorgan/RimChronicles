using RimChronicles.Core.Crisis.Pieces;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Pieces;

/// <summary>
/// Every crisis piece (a <c>PrefabDef</c> with a <see cref="PieceExtension"/>), turned into plain data for the
/// planners, and checked once after the defs load (crisis C2). A piece with problems is logged and left out, so a
/// broken piece never reaches a map.
/// </summary>
[StaticConstructorOnStartup]
public static class PieceLibrary
{
    private static List<(PrefabDef Prefab, PieceSource Source)>? _pieces;
    private static List<string> _problems = [];

    static PieceLibrary()
    {
        Load();
        foreach (var problem in _problems)
        {
            Log.Warning($"[Rim Chronicles] Piece left out: {problem}");
        }
    }

    /// <summary>The pieces that passed their checks.</summary>
    public static IReadOnlyList<(PrefabDef Prefab, PieceSource Source)> All => _pieces ?? Load();

    /// <summary>What the startup check found (empty when every piece is fine).</summary>
    public static IReadOnlyList<string> Problems
    {
        get
        {
            _ = All;
            return _problems;
        }
    }

    /// <summary>The pieces a power may use (War in Heaven W8): its own and the shared ones.</summary>
    public static IReadOnlyList<(PrefabDef Prefab, PieceSource Source)> For(string power) =>
        All.Where(p => p.Source.Meta.Power is null || p.Source.Meta.Power == power).ToList();

    /// <summary>The plain pieces a power may use, for the planners.</summary>
    public static List<PieceSource> SourcesFor(string power) => For(power).Select(p => p.Source).ToList();

    public static PieceSource? Find(PrefabDef prefab) => All.FirstOrDefault(p => p.Prefab == prefab).Source;

    private static List<(PrefabDef, PieceSource)> Load()
    {
        var pieces = new List<(PrefabDef, PieceSource)>();
        var problems = new List<string>();
        foreach (var prefab in DefDatabase<PrefabDef>.AllDefsListForReading.Where(p => p.HasModExtension<PieceExtension>()))
        {
            var source = ToSource(prefab);
            var found = PieceCheck.Problems(source);
            if (found.Count == 0)
            {
                pieces.Add((prefab, source));
            }

            problems.AddRange(found);
        }

        _problems = problems;
        _pieces = pieces;
        return pieces;
    }

    /// <summary>The prefab as plain data: each thing with its def's size, rotation rules, kind and power use.</summary>
    public static PieceSource ToSource(PrefabDef prefab)
    {
        var things = new List<PieceThing>();
        foreach (var (data, cell) in prefab.GetThings())
        {
            var def = data.def.IsBlueprint && data.def.entityDefToBuild is ThingDef built ? built : data.def;
            things.Add(new PieceThing
            {
                Def = def.defName,
                At = new Cell(cell.x, cell.z),
                SizeX = def.size.x,
                SizeZ = def.size.z,
                Rotatable = def.rotatable,
                RelativeTurns = (int)data.relativeRotation,
                Kind = KindOf(def),
                UsesPower = def.ConnectToPower,
                PlacingRot = (Side)def.defaultPlacingRot.AsInt,
            });
        }

        return new PieceSource
        {
            Name = prefab.defName,
            SizeX = prefab.size.x,
            SizeZ = prefab.size.z,
            Things = things,
            Floor = prefab.GetTerrain().Select(t => new Cell(t.cell.x, t.cell.z)).ToList(),
            Meta = prefab.GetModExtension<PieceExtension>().ToCore(),
        };
    }

    public static CellKind KindOf(ThingDef def) => def.IsDoor
        ? CellKind.Door
        : def.passability switch
        {
            Traversability.Impassable => CellKind.Wall,
            Traversability.PassThroughOnly => CellKind.PassThrough,
            _ => def.category == ThingCategory.Building ? CellKind.PassThrough : CellKind.Floor,
        };
}
