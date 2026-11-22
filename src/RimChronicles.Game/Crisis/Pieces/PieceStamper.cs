using RimChronicles.Core.Crisis.Pieces;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Pieces;

/// <summary>The materials a site or a raid builds with, in order of preference. A thing made from stuff takes the first
/// one it can be made of, or its default stuff.</summary>
public sealed class StuffPalette(params ThingDef?[] stuffs)
{
    private readonly List<ThingDef> _stuffs = stuffs.OfType<ThingDef>().ToList();

    /// <summary>Stone blocks, steel, plasteel, cloth: what an Imperial camp or outpost uses when nothing else is set.</summary>
    public static StuffPalette Imperial => new(
        DefDatabase<ThingDef>.GetNamedSilentFail("BlocksGranite"), ThingDefOf.Steel, ThingDefOf.Plasteel, ThingDefOf.Cloth);

    public ThingDef? For(ThingDef def)
    {
        if (!def.MadeFromStuff)
        {
            return null;
        }

        return _stuffs.FirstOrDefault(s => s.stuffProps?.CanMake(def) == true) ?? GenStuff.DefaultStuffFor(def);
    }
}

/// <summary>
/// Stamps a piece on a map, either built at once (the location generator) or as blueprints for a faction's pawns to
/// build (the siege planner). <paramref name="min"/> in each call is the footprint's south-west corner, as in the
/// planner's <see cref="PieceSpec"/>. Main thread only.
/// </summary>
public static class PieceStamper
{
    /// <summary>The <c>pos</c> vanilla's prefab calls take so the turned footprint's south-west corner lands on <paramref name="min"/>.</summary>
    public static IntVec3 PosFor(PrefabDef prefab, IntVec3 min, Rot4 rot)
    {
        var atZero = GenAdj.OccupiedRect(IntVec3.Zero, rot, prefab.size);
        return new IntVec3(min.x - atZero.minX, 0, min.z - atZero.minZ);
    }

    public static CellRect Footprint(PrefabDef prefab, IntVec3 min, Rot4 rot) => GenAdj.OccupiedRect(PosFor(prefab, min, rot), rot, prefab.size);

    /// <summary>Spawns the piece built. Returns false, spawning nothing, if any of it can't go there (out of bounds, or
    /// in the way of a building already there).</summary>
    public static bool Instant(PrefabDef prefab, Map map, IntVec3 min, Rot4 rot, Faction? faction, StuffPalette palette, List<Thing>? spawned = null)
    {
        var pos = PosFor(prefab, min, rot);
        if (!Footprint(prefab, min, rot).InBounds(map) || !PrefabUtility.CanSpawnPrefab(prefab, map, pos, rot, canWipeEdifices: false))
        {
            return false;
        }

        PrefabUtility.SpawnPrefab(prefab, map, pos, rot, faction, spawned, data =>
        {
            var def = data.def.IsBlueprint && data.def.entityDefToBuild is ThingDef built ? built : data.def;
            return def.MadeFromStuff ? Tuple.Create(def, palette.For(def) ?? data.stuff) : null;
        });
        return true;
    }

    /// <summary>
    /// Places the piece as blueprints owned by <paramref name="faction"/>, all or nothing. Vanilla's prefab blueprint
    /// mode always gives blueprints to the player (<c>PrefabUtility.SpawnThing</c> passes <c>Faction.OfPlayer</c>), so
    /// an Imperial siege camp places its own, at the cells vanilla's <c>PrefabUtility.GetThings</c> gives.
    /// </summary>
    public static bool Blueprints(PrefabDef prefab, Map map, IntVec3 min, Rot4 rot, Faction faction, StuffPalette palette, List<Blueprint_Build>? placed = null)
    {
        if (!Footprint(prefab, min, rot).InBounds(map))
        {
            return false;
        }

        var plan = new List<(ThingDef Def, IntVec3 Cell, Rot4 Rot, ThingDef? Stuff)>();
        foreach (var (data, cell, thingRot) in PrefabUtility.GetThings(prefab, PosFor(prefab, min, rot), rot))
        {
            var def = data.def.IsBlueprint && data.def.entityDefToBuild is ThingDef built ? built : data.def;
            var facing = def.rotatable ? thingRot : def.defaultPlacingRot;
            var stuff = palette.For(def) ?? data.stuff;
            if (def.blueprintDef is null || !GenConstruct.CanPlaceBlueprintAt(def, cell, facing, map, godMode: false, null, null, stuff).Accepted)
            {
                return false;
            }

            plan.Add((def, cell, facing, stuff));
        }

        foreach (var (def, cell, facing, stuff) in plan)
        {
            var blueprint = GenConstruct.PlaceBlueprintForBuild(def, cell, map, facing, faction, stuff);
            placed?.Add(blueprint);
        }

        return true;
    }
}
