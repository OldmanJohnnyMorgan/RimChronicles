using LudeonTK;
using RimChronicles.Core.Choir;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Powers;

/// <summary>
/// The Hollow Choir's faction (War in Heaven W3, tech spec R2, R3). It isn't made at world creation: <see cref="Create"/>
/// makes it when the Choir wakes (W4 calls it), the way vanilla adds factions mid-game (<c>NewGeneratedFaction</c>, then
/// <c>FactionManager.Add</c>; ENGINE_NOTES, "War in Heaven survey"). It gets settlements near the colony and is the
/// Empire's enemy from the start. Needs Biotech. Main thread only.
/// </summary>
public static class ChoirFaction
{
    /// <summary>Settlements placed near the colony when the Choir appears, on top of the one vanilla places anywhere.</summary>
    public const int SettlementsNearColony = 4;

    /// <summary>How far from the colony those settlements go, in tiles.</summary>
    public const int MinSettlementTiles = 8;

    public const int MaxSettlementTiles = 30;

    public static FactionDef? Def => DefDatabase<FactionDef>.GetNamedSilentFail(ChoirIds.FactionDefName);

    /// <summary>True when the Choir can exist in this game: Biotech is active and its def loaded.</summary>
    public static bool Available => ModsConfig.BiotechActive && Def is not null;

    /// <summary>The Choir's faction once it has appeared, unless it was defeated.</summary>
    public static Faction? Existing => Def is { } def && Find.FactionManager.FirstFactionOfDef(def) is { defeated: false } f ? f : null;

    /// <summary>Makes the faction (or returns the one that exists): its leader, its settlements near
    /// <paramref name="home"/>, and enmity with the Empire.</summary>
    public static Faction? Create(PlanetTile home)
    {
        if (!Available)
        {
            return null;
        }

        if (Existing is { } existing)
        {
            return existing;
        }

        var faction = FactionGenerator.NewGeneratedFaction(Find.WorldGrid.Surface, new FactionGeneratorParms(Def!));
        Find.FactionManager.Add(faction);
        for (var i = 0; i < SettlementsNearColony; i++)
        {
            if (!home.Valid || !TileFinder.TryFindNewSiteTile(out var tile, home, MinSettlementTiles, MaxSettlementTiles))
            {
                break;
            }

            var settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            settlement.SetFaction(faction);
            settlement.Tile = tile;
            settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
            Find.WorldObjects.Add(settlement);
        }

        if (Faction.OfEmpire is { } empire)
        {
            MakeEnemies(faction, empire);
        }

        Log.Message($"[Rim Chronicles] The Hollow Choir appeared with {Find.WorldObjects.Settlements.Count(s => s.Faction == faction)} settlements.");
        return faction;
    }

    /// <summary>Sets two non-player factions hostile at -100, on both sides (as <c>TryMakeInitialRelationsWith</c>
    /// sets relations).</summary>
    private static void MakeEnemies(Faction a, Faction b)
    {
        foreach (var (from, to) in new[] { (a, b), (b, a) })
        {
            var relation = from.RelationWith(to, allowNull: true);
            if (relation is null)
            {
                from.TryMakeInitialRelationsWith(to);
                relation = from.RelationWith(to);
            }

            relation.baseGoodwill = -100;
            relation.kind = FactionRelationKind.Hostile;
        }
    }

    private static PlanetTile HomeTile => Find.AnyPlayerHomeMap?.Tile ?? PlanetTile.Invalid;

    [DebugAction("Rim Chronicles", "Hollow Choir: create faction", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevCreate()
    {
        if (!Available)
        {
            Messages.Message("Rim Chronicles: the Hollow Choir needs Biotech.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var faction = Create(HomeTile);
        Messages.Message($"Rim Chronicles: {faction?.Name ?? "no faction"} is on the map.", MessageTypeDefOf.NeutralEvent, historical: false);
    }

    [DebugAction("Rim Chronicles", "Hollow Choir: raid (1,500 points)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DevRaid()
    {
        var map = Find.CurrentMap;
        if (map is null || Existing is not { } faction)
        {
            Messages.Message("Rim Chronicles: no Hollow Choir yet (Hollow Choir: create faction).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var parms = new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, faction = faction, points = 1500f, tile = map.Tile };
        var pawns = PawnGroupMakerUtility.GeneratePawns(parms).ToList();
        if (pawns.Count == 0 || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return;
        }

        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
        }

        if (faction.HostileTo(Faction.OfPlayer))
        {
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, pawns);
        }
        else
        {
            LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(entry), map, pawns);
        }

        var mechs = pawns.Count(p => p.RaceProps.IsMechanoid);
        Messages.Message($"Rim Chronicles: a Choir group of {pawns.Count - mechs} mechanitors and {mechs} mechs arrived"
            + (faction.HostileTo(Faction.OfPlayer) ? " and is attacking." : " (not hostile: it holds its ground)."), new LookTargets(pawns), MessageTypeDefOf.ThreatBig, historical: false);
    }
}
