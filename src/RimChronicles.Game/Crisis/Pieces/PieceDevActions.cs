using LudeonTK;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Pieces;

/// <summary>Dev Mode tools for crisis pieces (crisis C2): turn, stamp built or as Imperial blueprints at the mouse (the
/// mouse cell is the footprint's south-west corner), and check every piece. Stamping built also checks that what
/// spawned covers exactly the cells the planner expects.</summary>
public static class PieceDevActions
{
    private static Rot4 _rotation = Rot4.North;

    [DebugAction("Rim Chronicles", "Crisis: turn pieces", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Turn()
    {
        _rotation.Rotate(RotationDirection.Clockwise);
        Messages.Message($"Rim Chronicles: pieces now stamp facing {_rotation.ToStringHuman()}.", MessageTypeDefOf.NeutralEvent, historical: false);
    }

    [DebugAction("Rim Chronicles", "Crisis: stamp piece", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Stamp() => Nodes(blueprint: false);

    [DebugAction("Rim Chronicles", "Crisis: stamp piece as Imperial blueprints", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> StampBlueprints() => Nodes(blueprint: true);

    [DebugAction("Rim Chronicles", "Crisis: check pieces", allowedGameStates = AllowedGameStates.Playing)]
    public static void Check()
    {
        var lines = new List<string> { $"{PieceLibrary.All.Count} pieces loaded." };
        if (PieceLibrary.Problems.Count > 0)
        {
            lines.Add("Left out:");
            lines.AddRange(PieceLibrary.Problems.Select(p => "  " + p));
        }

        foreach (var (prefab, source) in PieceLibrary.All)
        {
            var spec = PieceSpec.Build(source, Side.North);
            lines.Add("");
            lines.Add($"{prefab.defName} ({source.SizeX}×{source.SizeZ}; tags {string.Join(", ", source.Meta.Tags)}; doors {string.Join(", ", spec.DoorSides)}; "
                + $"build order {source.Meta.BuildOrder}; threat {source.Meta.ThreatCost:0}; defense {source.Meta.DefenseValue}{(source.Meta.NeedsPower ? "; needs power" : "")})");
            lines.Add(spec.Picture());
        }

        Log.Message("[Rim Chronicles] Pieces:\n" + string.Join("\n", lines));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    private static List<DebugActionNode> Nodes(bool blueprint) => PieceLibrary.All
        .Select(p => new DebugActionNode(p.Prefab.defName, DebugActionType.ToolMap, () => StampAtMouse(p.Prefab, p.Source, blueprint)))
        .ToList();

    private static void StampAtMouse(PrefabDef prefab, PieceSource source, bool blueprint)
    {
        var map = Find.CurrentMap;
        var min = UI.MouseCell();
        if (map is null || !min.InBounds(map))
        {
            return;
        }

        if (blueprint)
        {
            var faction = EmpireDriver.EmpireFaction ?? Find.FactionManager.RandomEnemyFaction();
            var placed = new List<Blueprint_Build>();
            var ok = faction is not null && PieceStamper.Blueprints(prefab, map, min, _rotation, faction, StuffPalette.Imperial, placed);
            Report(ok ? $"{placed.Count} blueprints for {faction!.Name}." : "it doesn't fit there (or there's no enemy faction).", ok);
            return;
        }

        var spawned = new List<Thing>();
        if (!PieceStamper.Instant(prefab, map, min, _rotation, null, StuffPalette.Imperial, spawned))
        {
            Report("it doesn't fit there.", false);
            return;
        }

        // The planner's footprint against what really spawned, both relative to the footprint's corner.
        var rect = PieceStamper.Footprint(prefab, min, _rotation);
        var spec = PieceSpec.Build(source, (Side)_rotation.AsInt);
        var expected = Enumerable.Range(0, spec.SizeX).SelectMany(x => Enumerable.Range(0, spec.SizeZ).Select(z => new Cell(x, z)))
            .Where(c => spec[c] > CellKind.Floor).ToHashSet();
        var actual = spawned.Where(t => t.def.category == ThingCategory.Building)
            .SelectMany(t => t.OccupiedRect().Cells).Select(c => new Cell(c.x - rect.minX, c.z - rect.minZ)).ToHashSet();
        if (expected.SetEquals(actual))
        {
            Report($"{spawned.Count} things; the footprint matches the planner's.", true);
        }
        else
        {
            Report($"the footprint differs from the planner's: only planned {string.Join(" ", expected.Except(actual))}; only spawned {string.Join(" ", actual.Except(expected))}.", false);
        }
    }

    private static void Report(string text, bool ok)
    {
        Messages.Message($"Rim Chronicles: {text}", ok ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, historical: false);
        if (!ok)
        {
            Log.Warning($"[Rim Chronicles] Stamp piece: {text}");
        }
    }
}
