using RimChronicles.Core.Powers;
using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Captives;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.News;
using RimChronicles.Game.Crisis.Pieces;
using RimChronicles.Game.Crisis.Sites;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Crisis.Captives;

/// <summary>An intel item (crisis spec §5.6, XML): who can give it up, what it does, and when it goes stale.</summary>
public sealed class IntelDef : Def
{
    public List<CaptiveRank> ranks = [];
    public IntelEffect effect;
    public int expiryDays;
    public float weight = 1;

    public IntelSpec ToCore() => new(defName, ranks ?? [], effect, expiryDays, weight) { Label = label ?? defName };

    public static List<IntelSpec> All => DefDatabase<IntelDef>.AllDefsListForReading.Select(d => d.ToCore()).ToList();
}

/// <summary>
/// Captives, defectors and intel in the game (crisis C10): who's an Imperial captive and of what rank, recording
/// captures, recruitments, releases and harsh treatment, and applying intel (Core's <see cref="IntelEffects"/>, plus
/// the parts that need the world: revealing a site, counting a garrison). Main thread only.
/// </summary>
public static class CaptiveOps
{
    private const int RevealedSiteDays = 15;

    public static CrisisState? State => RimChroniclesGameComponent.Current?.Crisis;

    public static CaptiveRank RankOf(Pawn? pawn) => UnitKit.Unit(pawn?.kindDef) is { } unit ? CaptiveRules.RankOf(unit.role) : CaptiveRank.None;

    /// <summary>The colony holds an Imperial captive, or a defector still with the colony (Retrieval is allowed).</summary>
    public static bool HoldsAny() => Held().Any();

    /// <summary>Every captive and defector the colony holds, captives first, then by rank (highest first).</summary>
    public static IEnumerable<Pawn> Held()
    {
        var state = State;
        return Find.Maps.Where(m => m.IsPlayerHome)
            .SelectMany(m => m.mapPawns.AllPawnsSpawned)
            .Where(p => !p.Dead && ((p.IsPrisonerOfColony && RankOf(p) != CaptiveRank.None)
                || (p.IsColonist && state?.IsDefector(p.ThingID) == true)))
            .OrderByDescending(p => p.IsPrisonerOfColony).ThenByDescending(p => (int)RankOf(p)).ThenBy(p => p.thingIDNumber);
    }

    /// <summary>The record for an Imperial captive, made the first time it's needed.</summary>
    public static CaptiveRecord? Record(Pawn pawn) =>
        RankOf(pawn) is var rank && rank != CaptiveRank.None && State is { } state
            ? state.Captured(pawn.ThingID, pawn.LabelShort, rank, GenDate.DaysPassed)
            : null;

    public static void Captured(Pawn pawn)
    {
        if (Record(pawn) is not { } record)
        {
            return;
        }

        Messages.Message($"{pawn.LabelShort} is an Imperial {Rank(record.Rank)}. Tick \"interrogate (Imperial intel)\" in their prisoner tab to question them; "
            + "while they're held, the Empire may come for them.", pawn, MessageTypeDefOf.NeutralEvent);
    }

    /// <summary>A captive joined the colony: after interrogation, they're a defector (and the Empire hears of it).</summary>
    public static void Recruited(Pawn pawn)
    {
        if (State is not { } state || state.Recruited(pawn.ThingID, GenDate.DaysPassed) is not { } defector)
        {
            return;
        }

        var empire = EmpireDriver.EmpireFaction;
        StoryLetters.Crisis("An Imperial defects",
            $"{pawn.LabelShort}, once an Imperial {Rank(defector.Rank)}, has joined {Faction.OfPlayer.Name}. The Empire won't forget it: "
            + "while they're with the colony, it may send a strike force to take them back.",
            LetterDefOf.PositiveEvent, pawn, empire);
        RimChroniclesGameComponent.Current?.News.Add(new NewsEvent(GenDate.DaysPassed, Core.Powers.Power.Empire.NewsKind,
            $"An Imperial {Rank(defector.Rank)} defected to {Faction.OfPlayer.Name}", empire?.Name ?? ""));
    }

    /// <summary>A captive let go: a little goodwill and a cheaper next operation.</summary>
    public static void Released(Pawn pawn)
    {
        if (RankOf(pawn) == CaptiveRank.None || State is not { } state)
        {
            return;
        }

        state.Release();
        EmpireDriver.EmpireFaction?.TryAffectGoodwillWith(Faction.OfPlayer, CaptiveRules.ReleaseGoodwill, canSendMessage: true, canSendHostilityLetter: false);
        Messages.Message($"Word of {pawn.LabelShort}'s release reaches the Empire: its next operation will be a little lighter.", MessageTypeDefOf.PositiveEvent);
    }

    public static void Executed(Pawn pawn) => Harsh(pawn, s => s.Executed++);

    public static void Harvested(Pawn pawn) => Harsh(pawn, s => s.Harvested++);

    public static void Enslaved(Pawn pawn) => Harsh(pawn, s => s.Enslaved++);

    /// <summary>Executions, harvests and enslavements of Imperial captives are counted for the director (C11).</summary>
    private static void Harsh(Pawn pawn, Action<CrisisState> count)
    {
        if (RankOf(pawn) != CaptiveRank.None && State is { } state)
        {
            count(state);
        }
    }

    /// <summary>Applies an intel item and tells the player what it gave: Core's effect on the crisis, then a revealed
    /// site or a garrison count where the item calls for one.</summary>
    public static void Learn(IntelSpec intel, string source, LookTargets? look = null)
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var outcome = IntelEffects.Apply(intel, component.Crisis, CrisisDriver.Context(component), GenDate.DaysPassed, Rand.Value);
        var text = outcome.Text;
        if (outcome.RevealSite is { } archetype)
        {
            text += RevealSite(archetype, out var site) is { } where ? $" {where}" : " The site couldn't be found on the map.";
            if (site is not null)
            {
                look = new LookTargets(site);
            }
        }

        if (outcome.RevealGarrison)
        {
            text = Garrison() ?? "They gave up the size of an Imperial garrison, but the colony knows of no Imperial site to use it on.";
        }

        component.Crisis.Intel[^1].Text = text;
        StoryLetters.Crisis($"Intel: {intel.Label}", $"{source}\n\n{text}", LetterDefOf.PositiveEvent, look ?? LookTargets.Invalid);
    }

    /// <summary>Puts a site of the archetype on the world map near the colony, for <see cref="RevealedSiteDays"/> days.</summary>
    private static string? RevealSite(string archetype, out Site? site)
    {
        site = null;
        if (SiteOffers.Usable().FirstOrDefault(d => d.archetype == archetype) is not { sitePart: { } part } def
            || EmpireDriver.EmpireFaction is not { } empire || Find.AnyPlayerHomeMap is not { } home
            || !TileFinder.TryFindNewSiteTile(out var tile, home.Tile, 3, 12))
        {
            return null;
        }

        site = SiteMaker.MakeSite(part, tile, empire, threatPoints: StorytellerUtility.DefaultThreatPointsNow(home));
        site.GetComponent<TimeoutComp>()?.StartTimeout(RevealedSiteDays * GenDate.TicksPerDay);
        Find.WorldObjects.Add(site);
        return $"The {def.label} is marked on the world map, {Find.WorldGrid.ApproxDistanceInTiles(home.Tile, tile):0} tiles away. It will be moved in {RevealedSiteDays} days.";
    }

    /// <summary>The garrison of the nearest known Imperial site, planned as its map will be.</summary>
    private static string? Garrison()
    {
        var home = Find.AnyPlayerHomeMap;
        var sites = Find.WorldObjects.Sites.Where(s => !s.HasMap && s.Faction == EmpireDriver.EmpireFaction)
            .Select(s => (Site: s, Def: SiteOffers.Usable().FirstOrDefault(d => s.parts.Any(p => p.def == d.sitePart))))
            .Where(x => x.Def is not null)
            .OrderBy(x => home is null ? 0 : Find.WorldGrid.ApproxDistanceInTiles(home.Tile, x.Site.Tile))
            .ToList();
        if (sites.Count == 0)
        {
            return null;
        }

        var (site, def) = sites[0];
        var points = site.parts.FirstOrDefault(p => p.def == def!.sitePart)?.parms.threatPoints ?? def!.budget.min;
        var plan = SitePlanner.PlanValid(def!.ToCore(), PieceLibrary.All.Select(p => p.Source).ToList(), points, site.ID, out _);
        var soldiers = Math.Max(2, (int)Math.Round(plan.DefenderPoints / 150f));
        return $"The {def.label} ({site.Label}) is held by about {soldiers} troops, with {plan.Gates.Count} gate{(plan.Gates.Count == 1 ? "" : "s")} on its "
            + $"{plan.Entry.ToString().ToLowerInvariant()} side.";
    }

    public static string Rank(CaptiveRank rank) => rank switch
    {
        CaptiveRank.Soldier => "praetorian",
        CaptiveRank.Breacher => "breacher",
        CaptiveRank.Herald => "herald",
        CaptiveRank.Psyker => "adept",
        CaptiveRank.Officer => "officer",
        _ => "trooper",
    };

    // ---- Dev Mode (crisis spec §9: "RC: Spawn captive", "RC: Grant intel")

    /// <summary>Spawns a crisis unit of a chosen kind at the click as the colony's prisoner.</summary>
    [DebugAction("Rim Chronicles", "Crisis: spawn captive", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> SpawnCaptive() => UnitKit.Kinds.Select(kind => new DebugActionNode(kind.label ?? kind.defName,
        DebugActionType.ToolMap, () =>
        {
            if (EmpireDriver.EmpireFaction is not { } empire || Find.CurrentMap is not { } map || !UI.MouseCell().InBounds(map))
            {
                return;
            }

            var pawn = UnitKit.Generate(kind, empire, CrisisStage.War);
            GenSpawn.Spawn(pawn, UI.MouseCell(), map);
            pawn.guest.CapturedBy(Faction.OfPlayer);
        })).ToList();

    /// <summary>Applies a chosen intel item as if a captive had given it up.</summary>
    [DebugAction("Rim Chronicles", "Crisis: grant intel", allowedGameStates = AllowedGameStates.Playing)]
    public static List<DebugActionNode> GrantIntel() => DefDatabase<IntelDef>.AllDefsListForReading.Select(def =>
        new DebugActionNode(def.label ?? def.defName, DebugActionType.Action, () => Learn(def.ToCore(), "Granted from Dev Mode."))).ToList();
}
