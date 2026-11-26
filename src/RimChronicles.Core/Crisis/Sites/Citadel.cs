using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Core.Crisis.Sites;

/// <summary>What shapes a Citadel (crisis spec §4.5): the map's size, the courtyard relays and uplinks left after the
/// colony's raids on relay and uplink sites, whether intel revealed the keep's side entrance, and the threat budget.</summary>
public sealed record CitadelOptions
{
    public int MapSize { get; init; } = CitadelRings.DefaultMapSize;
    public int Relays { get; init; } = CitadelRings.BaseRelays;
    public int Uplinks { get; init; } = CitadelRings.BaseUplinks;
    public bool SideEntrance { get; init; }
    public float Budget { get; init; } = 12000;

    /// <summary>Whose (War in Heaven W9): the Empire's Citadel, or the Hollow Choir's Cathedral, which plans the same
    /// three rings with its own rooms (<see cref="CitadelRings.RelayTagFor"/> and the rest).</summary>
    public string Power { get; init; } = Powers.Power.Empire.Id;

    public bool IsCathedral => Power == Powers.Power.Choir.Id;
}

/// <summary>One ring of the Citadel: which (0 outer works, 1 courtyard, 2 keep), its plan, and where its grid's (0, 0)
/// lies on the map.</summary>
public sealed record CitadelRing(int Index, string Name, SitePlan Plan, Cell At)
{
    public Cell ToMap(Cell c) => new(At.X + c.X, At.Z + c.Z);

    /// <summary>The ring's wall rectangle on the map.</summary>
    public Rect RingOnMap => new(At.X + Plan.Ring.X, At.Z + Plan.Ring.Z, Plan.Ring.W, Plan.Ring.H);
}

/// <summary>The Citadel's three rings (crisis spec §4.5), each a site plan, nested: the courtyard in the hole the outer
/// works leave, the keep in the courtyard's.</summary>
public sealed class CitadelPlan
{
    public required CitadelOptions Options { get; init; }
    public required IReadOnlyList<CitadelRing> Rings { get; init; }

    /// <summary>The keep's side entrance on the map, when intel revealed it.</summary>
    public Cell? SideGate { get; init; }

    public CitadelRing Outer => Rings[0];
    public CitadelRing Courtyard => Rings[1];
    public CitadelRing Keep => Rings[2];

    public bool Fallback => Rings.Any(r => r.Plan.Fallback);

    /// <summary>Which ring a map cell is in: 2 inside the keep's walls, 1 inside the courtyard's, 0 inside the outer
    /// works', -1 outside them all.</summary>
    public int RingOf(Cell c)
    {
        for (var i = Rings.Count - 1; i >= 0; i--)
        {
            if (Rings[i].RingOnMap.Contains(c))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The courtyard's relay chambers: while one stands, the keep is shielded (fairness rule 8).</summary>
    public IEnumerable<SitePlacement> Relays => Courtyard.Plan.Pieces.Where(p => p.Has(CitadelRings.RelayTagFor(Options.Power)));

    public SitePlacement? Throne => Keep.Plan.Objective;

    /// <summary>The whole map as one grid: each ring's cells where its plan has them (not the hole it leaves).</summary>
    public CellGrid Combined()
    {
        var size = Options.MapSize;
        var grid = new CellGrid(size, size);
        foreach (var ring in Rings)
        {
            var g = ring.Plan.Grid;
            for (var x = 0; x < g.Width; x++)
            {
                for (var z = 0; z < g.Height; z++)
                {
                    var c = new Cell(x, z);
                    var at = ring.ToMap(c);
                    if (!g.IsReserved(c) && grid.InBounds(at))
                    {
                        grid[at] = g[c];
                        if (g.Conduit(c))
                        {
                            grid.SetConduit(at);
                        }
                    }
                }
            }
        }

        return grid;
    }
}

/// <summary>The Citadel's rings as site archetypes, and its sizes. Numbers are first guesses for the balance pass (C14).</summary>
public static class CitadelRings
{
    public const int DefaultMapSize = 275;
    public const int MinMapSize = 225;
    public const int MaxMapSize = 350;

    /// <summary>Open ground between the map edge and the outer works.</summary>
    public const int EdgeMargin = 6;

    public const int CourtyardSize = 112;
    public const int KeepSize = 46;

    public const int BaseRelays = 2;
    public const int BaseUplinks = 1;

    public const string RelayTag = "RelayChamber";
    public const string UplinkTag = "UplinkStation";
    public const string ThroneTag = "Throne";

    // The Hollow Choir's Cathedral (War in Heaven W9): resonators hold up the sanctum's shield, signal spires stand in
    // the cloister, and the First Voice waits in the sanctum.
    public const string ChoirRelayTag = "ResonatorChamber";
    public const string ChoirUplinkTag = "SpireStation";
    public const string SanctumTag = "Sanctum";

    public static string RelayTagFor(string power) => power == Powers.Power.Choir.Id ? ChoirRelayTag : RelayTag;

    public static string UplinkTagFor(string power) => power == Powers.Power.Choir.Id ? ChoirUplinkTag : UplinkTag;

    /// <summary>The keep's objective room: the throne room, or the Cathedral's sanctum.</summary>
    public static string ThroneTagFor(string power) => power == Powers.Power.Choir.Id ? SanctumTag : ThroneTag;

    /// <summary>The Cathedral's rings, as the player reads them.</summary>
    public static readonly string[] ChoirNames = ["outer works", "cloister", "sanctum"];

    public static string[] NamesFor(string power) => power == Powers.Power.Choir.Id ? ChoirNames : Names;

    public static readonly float[] Shares = [0.4f, 0.35f, 0.25f];

    /// <summary>The most defenders each ring gets, so the whole Citadel stays near 60 (crisis spec §4.5).</summary>
    public static readonly int[] MaxDefenders = [24, 22, 16];

    public static readonly string[] Names = ["outer works", "courtyard", "keep"];

    /// <summary>Each relay or uplink site the colony destroyed takes one from the courtyard.</summary>
    public static int Relays(int destroyed) => Math.Max(0, BaseRelays - destroyed);

    public static int Uplinks(int destroyed) => Math.Max(0, BaseUplinks - destroyed);

    public static int OuterSize(int mapSize) => Math.Clamp(mapSize, MinMapSize, MaxMapSize) - (2 * EdgeMargin);

    public static SiteArchetype Outer(CitadelOptions o)
    {
        var size = OuterSize(o.MapSize);
        var budget = o.Budget * Shares[0];
        return new SiteArchetype
        {
            Name = "CitadelOuter", Width = size, Height = size, ObjectiveTag = SiteTags.Barracks,
            Required = [new(SiteTags.Power), new("Artillery", 2)],
            Optional = [new("Line", Weight: 4, Max: 24), new("Turret", Weight: 3, Max: 10), new("Artillery", Weight: 1, Max: 3), new(SiteTags.Barracks, Weight: 1, Max: 3)],
            Gates = 3, MinBudget = budget, MaxBudget = budget, PieceShare = 0.5f, OptionalChance = 0.5f,
        };
    }

    public static SiteArchetype Courtyard(CitadelOptions o)
    {
        var budget = o.Budget * Shares[1];
        var relay = RelayTagFor(o.Power);
        var required = new List<PieceRequest> { new(SiteTags.Barracks, 2), new(SiteTags.Power), new("Armory") };
        if (o.Relays > 1)
        {
            required.Add(new PieceRequest(relay, o.Relays - 1));
        }

        if (o.Uplinks > 0)
        {
            required.Add(new PieceRequest(UplinkTagFor(o.Power), o.Uplinks));
        }

        IReadOnlyList<PieceRequest> optional = o.IsCathedral
            ? [new("Turret", Weight: 2, Max: 4), new("Artillery", Weight: 1, Max: 2)]
            : [new("Turret", Weight: 2, Max: 4), new("Artillery", Weight: 1, Max: 2), new("Records", Weight: 1, Max: 1)];
        return new SiteArchetype
        {
            Name = "CitadelCourtyard", Width = CourtyardSize, Height = CourtyardSize,
            ObjectiveTag = o.Relays > 0 ? relay : "Armory",
            Required = o.Relays > 0 ? required : required.Where(r => r.Tag != "Armory").ToList(),
            Optional = optional,
            Gates = 2, MinBudget = budget, MaxBudget = budget, PieceShare = 0.3f, OptionalChance = 0.6f,
        };
    }

    public static SiteArchetype Keep(CitadelOptions o)
    {
        var budget = o.Budget * Shares[2];
        if (o.IsCathedral)
        {
            return new SiteArchetype
            {
                Name = "CathedralSanctum", Width = KeepSize, Height = KeepSize, ObjectiveTag = SanctumTag,
                Required = [new(SiteTags.Barracks)],
                Optional = [new("Armory", Weight: 1, Max: 1), new(SiteTags.Barracks, Weight: 1, Max: 1)],
                Gates = 1, MinBudget = budget, MaxBudget = budget, PieceShare = 0.15f, OptionalChance = 0.9f,
            };
        }

        return new SiteArchetype
        {
            Name = "CitadelKeep", Width = KeepSize, Height = KeepSize, ObjectiveTag = "Throne",
            Required = [new("Records")],
            Optional = [new("Vault", Weight: 2, Max: 1), new(SiteTags.Barracks, Weight: 1, Max: 1), new("Armory", Weight: 1, Max: 1)],
            Gates = 1, MinBudget = budget, MaxBudget = budget, PieceShare = 0.15f, OptionalChance = 0.9f,
        };
    }
}

/// <summary>
/// Plans the Citadel (crisis spec §4.5, tech spec §3.3): the site planner once per ring, outermost first, each inner
/// ring in the hole the ring outside it leaves. With the side entrance known, the keep gets a second gate on the far
/// side from its first. <see cref="Problems"/> runs each ring's validators and the whole-map checks.
/// </summary>
public static class CitadelPlanner
{
    public static CitadelPlan Plan(IReadOnlyList<PieceSource> pieces, CitadelOptions options, long seedId)
    {
        var outerSize = CitadelRings.OuterSize(options.MapSize);
        var options2 = options with { MapSize = outerSize + (2 * CitadelRings.EdgeMargin) };
        var outerAt = new Cell(CitadelRings.EdgeMargin, CitadelRings.EdgeMargin);
        var courtyardIn = (outerSize - CitadelRings.CourtyardSize) / 2;
        var keepIn = (CitadelRings.CourtyardSize - CitadelRings.KeepSize) / 2;
        var courtyardHole = new Rect(courtyardIn, courtyardIn, CitadelRings.CourtyardSize, CitadelRings.CourtyardSize);
        var keepHole = new Rect(keepIn, keepIn, CitadelRings.KeepSize, CitadelRings.KeepSize);

        var outer = SitePlanner.PlanValid(CitadelRings.Outer(options2), pieces, 0, seedId, out _, courtyardHole);
        var courtyard = SitePlanner.PlanValid(CitadelRings.Courtyard(options2), pieces, 0, seedId, out _, keepHole);
        var keep = SitePlanner.PlanValid(CitadelRings.Keep(options2), pieces, 0, seedId, out _);
        var courtyardAt = new Cell(outerAt.X + courtyardIn, outerAt.Z + courtyardIn);
        var keepAt = new Cell(courtyardAt.X + keepIn, courtyardAt.Z + keepIn);

        Cell? side = null;
        if (options.SideEntrance && SitePlanner.AddGate(keep, PieceRotation.Turn(keep.Entry, Side.South)) is { } gate)
        {
            side = new Cell(keepAt.X + gate.X, keepAt.Z + gate.Z);
        }

        return new CitadelPlan
        {
            Options = options2,
            Rings =
            [
                new CitadelRing(0, CitadelRings.Names[0], outer, outerAt),
                new CitadelRing(1, CitadelRings.Names[1], courtyard, courtyardAt),
                new CitadelRing(2, CitadelRings.Names[2], keep, keepAt),
            ],
            SideGate = side,
        };
    }

    /// <summary>Each ring's own validators, then over the whole map: the throne room and every courtyard relay are
    /// reachable from the map edge (fairness rules 4 and 8), and the rings nest without overlapping.</summary>
    public static IReadOnlyList<string> Problems(CitadelPlan plan)
    {
        var problems = plan.Rings.SelectMany(r => SiteValidator.Problems(r.Plan).Select(p => $"{r.Name}: {p}")).ToList();
        for (var i = 1; i < plan.Rings.Count; i++)
        {
            var inner = plan.Rings[i];
            var outer = plan.Rings[i - 1];
            var hole = outer.Plan.Hole is { } h ? new Rect(outer.At.X + h.X, outer.At.Z + h.Z, h.W, h.H) : (Rect?)null;
            if (hole is not { } on || on.X != inner.At.X || on.Z != inner.At.Z || on.W != inner.Plan.Grid.Width || on.H != inner.Plan.Grid.Height)
            {
                problems.Add($"the {inner.Name} doesn't sit in the hole the {outer.Name} leaves");
            }
        }

        var grid = plan.Combined();
        var reached = grid.Flood(grid.Border(), c => grid[c] != CellKind.Wall);
        bool Reached(CitadelRing ring, SitePlacement piece) =>
            piece.Cells().Select(ring.ToMap).Any(c => grid[c] != CellKind.Wall && reached[c.X, c.Z]);

        if (plan.Throne is not { } throne || !throne.Has(CitadelRings.ThroneTagFor(plan.Options.Power)))
        {
            problems.Add($"the keep has no {(plan.Options.IsCathedral ? "sanctum" : "throne room")}");
        }
        else if (!Reached(plan.Keep, throne))
        {
            problems.Add("the throne room can't be reached from the map edge");
        }

        foreach (var relay in plan.Relays.Where(r => !Reached(plan.Courtyard, r)))
        {
            problems.Add($"the courtyard relay at {plan.Courtyard.ToMap(relay.Min)} can't be reached from the map edge");
        }

        if (plan.Relays.Count() != plan.Options.Relays)
        {
            problems.Add($"the courtyard has {plan.Relays.Count()} relays, not {plan.Options.Relays}");
        }

        return problems;
    }
}
