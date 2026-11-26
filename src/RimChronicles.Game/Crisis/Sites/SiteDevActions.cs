using LudeonTK;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Game.Crisis.Pieces;
using RimChronicles.Game.Empire;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>Dev Mode tools for Imperial sites (crisis spec §9: "RC: Generate site", "RC: Batch sites").</summary>
public static class SiteDevActions
{
    private const int BatchSeeds = 50;
    private static readonly long[] Seeds = [1, 2, 3, 4, 5];

    /// <summary>Makes a site of an archetype near the colony with a chosen seed, generates its map and goes there.</summary>
    [DebugAction("Rim Chronicles", "Crisis: generate site", allowedGameStates = AllowedGameStates.PlayingOnWorld | AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Generate() => SiteOffers.Usable(Core.Powers.Power.Empire.Id).Concat(SiteOffers.Usable(Core.Powers.Power.Choir.Id)).Select(def =>
    {
        var node = new DebugActionNode(def.archetype);
        foreach (var seed in Seeds.Cast<long?>().Append(null))
        {
            node.AddChild(new DebugActionNode(seed is { } s ? $"seed {s}" : "the site's own seed", DebugActionType.Action, () => Make(def, seed)));
        }

        return node;
    }).ToList();

    private static void Make(SiteArchetypeDef def, long? seed)
    {
        if (Powers.PowerFactions.Of(def.power == Core.Powers.Power.Choir.Id ? Core.Powers.Power.Choir : Core.Powers.Power.Empire) is not { } empire
            || def.sitePart is null)
        {
            Messages.Message(def.power == Core.Powers.Power.Choir.Id
                ? "Rim Chronicles: Choir sites need the Hollow Choir awake (Hollow Choir: wake now)."
                : "Rim Chronicles: sites need Royalty and the Empire faction.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        PlanetTile? home = Find.AnyPlayerHomeMap?.Tile;
        var found = home is { } h ? TileFinder.TryFindNewSiteTile(out var tile, h, 2, 10) : TileFinder.TryFindNewSiteTile(out tile, 2, 10);
        if (!found)
        {
            Messages.Message("Rim Chronicles: no free tile for a site nearby.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var site = SiteMaker.MakeSite(def.sitePart, tile, empire, threatPoints: (def.budget.min + def.budget.max) / 2);
        Find.WorldObjects.Add(site);
        GenStep_RcSite.NextSeed = seed;
        var map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, Find.World.info.initialMapSize, null);
        Current.Game.CurrentMap = map;
        Find.CameraDriver.JumpToCurrentMapLoc(map.Center);
        var record = MapComponent_RcSite.For(map);
        Messages.Message($"Rim Chronicles: {def.label}, seed {record?.Seed}, {record?.Objectives.Count ?? 0} objective(s). The log has the details.",
            MessageTypeDefOf.NeutralEvent, historical: false);
    }

    /// <summary>Plans 50 seeds of each archetype (no map) and reports how many pass the validators first time, which
    /// fell back to the fixed layout, what failed, and how often each piece was used.</summary>
    [DebugAction("Rim Chronicles", "Crisis: batch sites", allowedGameStates = AllowedGameStates.Playing)]
    public static void Batch()
    {
        var lines = new List<string>();
        foreach (var def in SiteOffers.Usable(Core.Powers.Power.Empire.Id).Concat(SiteOffers.Usable(Core.Powers.Power.Choir.Id)))
        {
            lines.AddRange(BatchReport(def.ToCore(), PieceLibrary.SourcesFor(def.power), BatchSeeds));
            lines.Add("");
        }

        if (lines.Count == 0)
        {
            lines.Add("No site archetypes loaded (they need Royalty).");
        }

        Log.Message("[Rim Chronicles] Site batch:\n" + string.Join("\n", lines));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    /// <summary>The batch report for one archetype, as text lines.</summary>
    public static List<string> BatchReport(SiteArchetype archetype, IReadOnlyList<PieceSource> pieces, int seeds)
    {
        var firstTry = 0;
        var fallbacks = 0;
        var problems = new Dictionary<string, int>();
        var usage = new Dictionary<string, int>();
        for (var id = 1; id <= seeds; id++)
        {
            var first = SitePlanner.Plan(archetype, pieces, (archetype.MinBudget + archetype.MaxBudget) / 2, SitePlanner.SeedFor(id, archetype.Name));
            var found = SiteValidator.Problems(first);
            if (found.Count == 0)
            {
                firstTry++;
            }

            foreach (var p in found)
            {
                var key = p.Split(" at ")[0];
                problems[key] = problems.GetValueOrDefault(key) + 1;
            }

            var plan = SitePlanner.PlanValid(archetype, pieces, (archetype.MinBudget + archetype.MaxBudget) / 2, id, out _);
            if (plan.Fallback)
            {
                fallbacks++;
            }

            foreach (var piece in plan.Pieces)
            {
                usage[piece.Piece.Name] = usage.GetValueOrDefault(piece.Piece.Name) + 1;
            }
        }

        var lines = new List<string>
        {
            $"{archetype.Id}: {firstTry}/{seeds} pass on the first seed; {seeds - fallbacks}/{seeds} pass within {SitePlanner.MaxAttempts}; {fallbacks} fall back to the fixed layout.",
        };
        lines.AddRange(problems.OrderByDescending(p => p.Value).Select(p => $"  first-seed problem x{p.Value}: {p.Key}"));
        lines.Add("  pieces used: " + string.Join(", ", usage.OrderByDescending(u => u.Value).Select(u => $"{u.Key.Replace("RimChronicles_Piece_", "")} {u.Value}")));
        return lines;
    }
}
