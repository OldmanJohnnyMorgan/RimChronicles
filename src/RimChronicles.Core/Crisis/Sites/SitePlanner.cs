using RimChronicles.Core.Crisis.Pieces;

namespace RimChronicles.Core.Crisis.Sites;

/// <summary>What a piece is for in a site.</summary>
public static class SiteRole
{
    public const string Objective = "objective";
    public const string Required = "required";
    public const string Optional = "optional";
    public const string Defense = "defense";
}

/// <summary>A piece in a site plan: which, where (its footprint's south-west corner), turned how, and why.</summary>
public sealed record SitePlacement(int Index, PieceSpec Spec, Cell Min, string Role)
{
    public PieceSource Piece => Spec.Source;
    public Side Rotation => Spec.Rotation;
    public Rect Bounds => new(Min.X, Min.Z, Spec.SizeX, Spec.SizeZ);

    public IEnumerable<Cell> Cells() => Bounds.Cells();

    public IEnumerable<Cell> PowerCells() => Spec.PowerCells.Select(c => Min + c);

    public bool Has(string tag) => Piece.Meta.Tags.Contains(tag);
}

/// <summary>
/// A site's plan (tech spec §3.2): the grid, the pieces, the wall ring and its gates, the conduits, where defenders
/// stand and the budget split. The game stamps it with the grid's (0, 0) at a map cell.
/// </summary>
public sealed class SitePlan
{
    public required SiteArchetype Archetype { get; init; }
    public required ulong Seed { get; init; }
    public required CellGrid Grid { get; init; }

    /// <summary>The wall ring: its perimeter cells are wall, except the gates.</summary>
    public required Rect Ring { get; init; }

    /// <summary>A rectangle left free for an inner ring planned on its own (the Citadel), or null.</summary>
    public Rect? Hole { get; init; }

    /// <summary>The side the gates are on: where the player is expected to come from.</summary>
    public required Side Entry { get; init; }

    public required float Budget { get; init; }
    public bool Fallback { get; init; }

    public List<Rect> Slots { get; } = [];

    /// <summary>Each slot's depth from the entry side in the connection graph (0: on the entry side).</summary>
    public List<int> SlotDepth { get; } = [];
    public List<SitePlacement> Pieces { get; } = [];
    public List<Cell> Walls { get; } = [];
    public List<Cell> Gates { get; } = [];
    public List<Cell> Conduits { get; } = [];
    public List<Cell> DefenderSpots { get; } = [];

    /// <summary>What the planner couldn't do (a required piece with no room, a conduit with no path).</summary>
    public List<string> Notes { get; } = [];

    public SitePlacement? Objective => Pieces.FirstOrDefault(p => p.Role == SiteRole.Objective);

    /// <summary>The threat cost of every piece placed.</summary>
    public float PieceCost => Pieces.Sum(p => p.Piece.Meta.ThreatCost);

    /// <summary>What's left of the budget for the garrison.</summary>
    public float DefenderPoints => Math.Max(0, Budget - PieceCost);

    /// <summary>A text picture, north at the top: <c>#</c> wall, <c>D</c> door or gate, <c>o</c> a building you can
    /// pass, <c>_</c> floor, <c>+</c> conduit on open ground, <c>,</c> open ground inside a piece, <c>.</c> open ground,
    /// <c>O</c> the objective piece's buildings.</summary>
    public string Picture()
    {
        var objective = Objective?.Index ?? -1;
        var rows = new List<string>();
        for (var z = Grid.Height - 1; z >= 0; z--)
        {
            rows.Add(new string(Enumerable.Range(0, Grid.Width).Select(x =>
            {
                var c = new Cell(x, z);
                return Grid[c] switch
                {
                    CellKind.Wall => '#',
                    CellKind.Door => 'D',
                    CellKind.PassThrough => Grid.Owner(c) == objective ? 'O' : 'o',
                    CellKind.Floor => '_',
                    _ => Grid.Conduit(c) ? '+' : Grid.Owner(c) >= 0 ? ',' : '.',
                };
            }).ToArray()));
        }

        return string.Join("\n", rows);
    }

    /// <summary>The slots area: inside the ring, with a lane along the walls.</summary>
    public Rect SlotArea => Ring.Inset(1 + SitePlanner.Lane);
}

/// <summary>
/// Plans a site (crisis spec §4.2, tech spec §3.2), seeded so one site always gets the same layout: partition the build
/// area into slots (BSP), connect them (a spanning tree plus a few loops) with the objective deepest from the entry
/// side, fill them with pieces by tag (objective, then required, then weighted optional ones within the budget), and
/// wrap it all in a wall with gates, cover inside the gates, and conduits from the generator to every piece that needs
/// power. <see cref="SiteValidator"/> checks the result; <see cref="Fallback"/> is the fixed layout used after
/// <see cref="MaxAttempts"/> failures.
/// </summary>
public static class SitePlanner
{
    /// <summary>Open ground between the grid's edge and the wall ring.</summary>
    public const int Margin = 2;

    /// <summary>The open lane inside the walls, around the slots.</summary>
    public const int Lane = 2;

    public const int MaxAttempts = 5;

    /// <summary>How far inside a gate its cover goes, and how wide a lane in front of it stays clear.</summary>
    private const int CoverDepth = 4;
    private const int GateLane = 1;

    /// <summary>The seed for attempt <paramref name="attempt"/> of a site.</summary>
    public static ulong SeedFor(long siteId, string archetype, int attempt = 0) =>
        SeededRng.Seed(siteId, archetype) ^ unchecked((ulong)attempt * 0x9E3779B97F4A7C15UL);

    /// <summary>The first plan, of up to <see cref="MaxAttempts"/> seeds, that passes the validators; else the fallback.</summary>
    public static SitePlan PlanValid(SiteArchetype archetype, IReadOnlyList<PieceSource> pieces, float points, long siteId, out int attempts, Rect? hole = null)
    {
        for (attempts = 1; attempts <= MaxAttempts; attempts++)
        {
            var plan = Plan(archetype, pieces, points, SeedFor(siteId, archetype.Name, attempts - 1), hole);
            if (SiteValidator.Problems(plan).Count == 0)
            {
                return plan;
            }
        }

        return Fallback(archetype, pieces, points, hole);
    }

    /// <summary>Plans a site. With <paramref name="hole"/>, that rectangle stays free for an inner ring, and slots mostly
    /// inside it are dropped.</summary>
    public static SitePlan Plan(SiteArchetype archetype, IReadOnlyList<PieceSource> pieces, float points, ulong seed, Rect? hole = null)
    {
        var rng = new SeededRng(seed);
        var entry = (Side)rng.Next(4);
        var plan = Start(archetype, points, seed, entry, fallback: false, hole);
        var area = plan.SlotArea;
        var slots = Partition(area, MinSlot(archetype, pieces), rng);
        if (hole is { } h)
        {
            slots = slots.Where(s => s.Cells().Count(h.Contains) * 2 < s.W * s.H).ToList();
        }

        plan.Slots.AddRange(slots);
        var (parent, depth) = Connect(slots, area, entry, rng);
        plan.SlotDepth.AddRange(depth);

        var objectiveSlot = Enumerable.Range(0, slots.Count)
            .OrderByDescending(i => depth[i])
            .ThenByDescending(i => FromEntry(slots[i].Centre, area, entry))
            .ThenBy(i => i)
            .First();
        var used = new HashSet<int> { objectiveSlot };
        Place(plan, Pick(pieces, archetype.ObjectiveTag, rng), slots[objectiveSlot], Toward(slots, objectiveSlot, parent, entry), SiteRole.Objective, centred: true,
            $"objective ({archetype.ObjectiveTag})");

        foreach (var request in archetype.Required)
        {
            for (var n = 0; n < request.Count; n++)
            {
                var piece = Pick(pieces, request.Tag, rng);
                var free = Enumerable.Range(0, slots.Count).Where(i => !used.Contains(i)).OrderByDescending(i => depth[i]).ThenBy(i => i).ToList();
                var placed = false;
                foreach (var i in free.Concat(Enumerable.Range(0, slots.Count).Where(used.Contains)))
                {
                    if (Place(plan, piece, slots[i], Toward(slots, i, parent, entry), SiteRole.Required, centred: true, null))
                    {
                        used.Add(i);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    plan.Notes.Add($"no room for required {request.Tag}");
                }
            }
        }

        var counts = archetype.Optional.ToDictionary(o => o, _ => 0);
        foreach (var i in Shuffled(Enumerable.Range(0, slots.Count).Where(i => !used.Contains(i)).ToList(), rng))
        {
            if (rng.NextDouble() >= archetype.OptionalChance)
            {
                continue;
            }

            var open = archetype.Optional.Where(o => counts[o] < o.Max && pieces.Any(p => p.Meta.Tags.Contains(o.Tag)
                && plan.PieceCost + p.Meta.ThreatCost <= plan.Budget * archetype.PieceShare)).ToList();
            if (WeightedPick(open, rng) is not { } request)
            {
                continue;
            }

            var piece = Pick(pieces, request.Tag, rng, p => plan.PieceCost + p.Meta.ThreatCost <= plan.Budget * archetype.PieceShare);
            if (piece is not null && Place(plan, piece, slots[i], Toward(slots, i, parent, entry), SiteRole.Optional, centred: true, null))
            {
                counts[request]++;
                used.Add(i);
            }
        }

        Wrap(plan, pieces, rng);
        return plan;
    }

    /// <summary>
    /// The archetype's fixed layout, used when every seeded plan failed (crisis spec §4.2 step 6): one open courtyard,
    /// gates on the south side, the objective as deep as it fits and the rest placed row by row behind it.
    /// </summary>
    public static SitePlan Fallback(SiteArchetype archetype, IReadOnlyList<PieceSource> pieces, float points, Rect? hole = null)
    {
        var rng = new SeededRng(0);
        var plan = Start(archetype, points, 0, Side.South, fallback: true, hole);
        var area = plan.SlotArea;
        plan.Slots.Add(area);
        plan.SlotDepth.Add(0);
        Place(plan, First(pieces, archetype.ObjectiveTag), area, Side.South, SiteRole.Objective, centred: false, $"objective ({archetype.ObjectiveTag})");
        foreach (var request in archetype.Required)
        {
            for (var n = 0; n < request.Count; n++)
            {
                Place(plan, First(pieces, request.Tag), area, Side.South, SiteRole.Required, centred: false, $"required {request.Tag}");
            }
        }

        Wrap(plan, pieces, rng);
        return plan;
    }

    private static SitePlan Start(SiteArchetype archetype, float points, ulong seed, Side entry, bool fallback, Rect? hole)
    {
        var grid = new CellGrid(archetype.Width, archetype.Height);
        if (hole is { } h)
        {
            grid.Reserve(h);
        }

        return new SitePlan
        {
            Archetype = archetype,
            Seed = seed,
            Grid = grid,
            Ring = new Rect(Margin, Margin, archetype.Width - (2 * Margin), archetype.Height - (2 * Margin)),
            Entry = entry,
            Budget = archetype.Budget(points),
            Fallback = fallback,
            Hole = hole,
        };
    }

    /// <summary>The smallest slot: the largest objective or required piece and a one-cell lane on each side.</summary>
    public static int MinSlot(SiteArchetype archetype, IEnumerable<PieceSource> pieces)
    {
        var tags = archetype.Required.Select(r => r.Tag).Append(archetype.ObjectiveTag).ToHashSet();
        var sizes = pieces.Where(p => p.Meta.Tags.Any(tags.Contains)).Select(p => Math.Max(p.SizeX, p.SizeZ)).DefaultIfEmpty(5);
        return sizes.Max() + 2;
    }

    // ---- 1. partition

    public static List<Rect> Partition(Rect area, int min, SeededRng rng)
    {
        var leaves = new List<Rect>();

        void Split(Rect r)
        {
            var canV = r.W >= 2 * min;
            var canH = r.H >= 2 * min;
            // Below three slots across, a split may stop early for a bigger slot; the whole area always splits once.
            if ((!canV && !canH) || (r != area && r.W < 3 * min && r.H < 3 * min && rng.NextDouble() < 0.3))
            {
                leaves.Add(r);
                return;
            }

            var vertical = canV && (!canH || r.W > r.H || (r.W == r.H && rng.Next(2) == 0));
            if (vertical)
            {
                var at = rng.Range(min, r.W - min);
                Split(new Rect(r.X, r.Z, at, r.H));
                Split(new Rect(r.X + at, r.Z, r.W - at, r.H));
            }
            else
            {
                var at = rng.Range(min, r.H - min);
                Split(new Rect(r.X, r.Z, r.W, at));
                Split(new Rect(r.X, r.Z + at, r.W, r.H - at));
            }
        }

        Split(area);
        return leaves;
    }

    // ---- 2. connect

    /// <summary>A spanning tree over touching slots, grown from the slots on the entry side, plus up to two loops; then
    /// each slot's parent (toward the entry) and depth.</summary>
    public static (int[] Parent, int[] Depth) Connect(List<Rect> slots, Rect area, Side entry, SeededRng rng)
    {
        var n = slots.Count;
        var edges = new List<(int A, int B)>();
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (slots[i].Touches(slots[j]))
                {
                    edges.Add((i, j));
                }
            }
        }

        var entrySlots = Enumerable.Range(0, n).Where(i => OnEntrySide(slots[i], area, entry)).ToList();
        var inTree = new HashSet<int>(entrySlots);
        var chosen = new List<(int, int)>();
        while (true)
        {
            var frontier = edges.Where(e => inTree.Contains(e.A) != inTree.Contains(e.B)).ToList();
            if (frontier.Count == 0)
            {
                break;
            }

            var e = frontier[rng.Next(frontier.Count)];
            chosen.Add(e);
            inTree.Add(e.A);
            inTree.Add(e.B);
        }

        var spare = edges.Except(chosen).ToList();
        for (var loops = 0; loops < 2 && spare.Count > 0; loops++)
        {
            var e = spare[rng.Next(spare.Count)];
            chosen.Add(e);
            spare.Remove(e);
        }

        var parent = Enumerable.Repeat(-1, n).ToArray();
        var depth = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var queue = new Queue<int>();
        foreach (var s in entrySlots)
        {
            depth[s] = 0;
            queue.Enqueue(s);
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var (a, b) in chosen)
            {
                var next = a == at ? b : b == at ? a : -1;
                if (next >= 0 && depth[next] == int.MaxValue)
                {
                    depth[next] = depth[at] + 1;
                    parent[next] = at;
                    queue.Enqueue(next);
                }
            }
        }

        for (var i = 0; i < n; i++)
        {
            if (depth[i] == int.MaxValue)
            {
                depth[i] = 0; // a slot cut off from the tree counts as by the entry
            }
        }

        return (parent, depth);
    }

    private static bool OnEntrySide(Rect slot, Rect area, Side entry) => entry switch
    {
        Side.South => slot.Z == area.Z,
        Side.North => slot.MaxZ == area.MaxZ,
        Side.West => slot.X == area.X,
        _ => slot.MaxX == area.MaxX,
    };

    /// <summary>How far a cell is from the entry side of <paramref name="area"/>.</summary>
    public static int FromEntry(Cell c, Rect area, Side entry) => entry switch
    {
        Side.South => c.Z - area.Z,
        Side.North => area.MaxZ - c.Z,
        Side.West => c.X - area.X,
        _ => area.MaxX - c.X,
    };

    /// <summary>The side of a slot that faces its parent (toward the entry); the entry side for a slot with none.</summary>
    private static Side Toward(List<Rect> slots, int slot, int[] parent, Side entry)
    {
        if (parent[slot] < 0)
        {
            return entry;
        }

        var a = slots[slot].Centre;
        var b = slots[parent[slot]].Centre;
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        return Math.Abs(dx) > Math.Abs(dz) ? (dx > 0 ? Side.East : Side.West) : (dz > 0 ? Side.North : Side.South);
    }

    // ---- 3. fill

    private static PieceSource? Pick(IReadOnlyList<PieceSource> pieces, string tag, SeededRng rng, Func<PieceSource, bool>? ok = null)
    {
        var fits = pieces.Where(p => p.Meta.Tags.Contains(tag) && (ok is null || ok(p))).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        return fits.Count == 0 ? null : fits[rng.Next(fits.Count)];
    }

    private static PieceSource? First(IReadOnlyList<PieceSource> pieces, string tag) =>
        pieces.Where(p => p.Meta.Tags.Contains(tag)).OrderBy(p => p.Name, StringComparer.Ordinal).FirstOrDefault();

    private static PieceRequest? WeightedPick(IReadOnlyList<PieceRequest> requests, SeededRng rng)
    {
        var total = requests.Sum(r => r.Weight);
        if (total <= 0)
        {
            return null;
        }

        var at = rng.NextDouble() * total;
        foreach (var r in requests)
        {
            if ((at -= r.Weight) < 0)
            {
                return r;
            }
        }

        return requests[^1];
    }

    private static List<int> Shuffled(List<int> list, SeededRng rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    /// <summary>The piece's rotations, those with a door facing <paramref name="face"/> first. An emplacement (a
    /// sandbagged mortar or turret, open at the back) turns its opening away instead, so its sandbags face the way in.</summary>
    private static IEnumerable<PieceSpec> Turnings(PieceSource piece, Side face)
    {
        if (piece.Meta.Tags.Contains(SiteTags.Emplacement))
        {
            face = PieceRotation.Turn(face, Side.South);
        }

        return Enum.GetValues<Side>().Select(r => PieceSpec.Build(piece, r))
            .OrderByDescending(s => s.DoorSides.Contains(face)).ThenBy(s => (int)s.Rotation);
    }

    /// <summary>Places a piece in <paramref name="region"/> with a free lane round it: centred (a slot), or as far from
    /// the entry as it fits (the fallback's courtyard). Notes <paramref name="failure"/> when it can't.</summary>
    private static bool Place(SitePlan plan, PieceSource? piece, Rect region, Side face, string role, bool centred, string? failure)
    {
        if (piece is not null)
        {
            var inner = region.Inset(1);
            var area = plan.SlotArea;
            foreach (var spec in Turnings(piece, face))
            {
                if (spec.SizeX > inner.W || spec.SizeZ > inner.H)
                {
                    continue;
                }

                var mins = Enumerable.Range(inner.X, inner.W - spec.SizeX + 1)
                    .SelectMany(x => Enumerable.Range(inner.Z, inner.H - spec.SizeZ + 1).Select(z => new Cell(x, z)));
                mins = centred
                    ? mins.OrderBy(m => Dist2(new Cell(m.X + (spec.SizeX / 2), m.Z + (spec.SizeZ / 2)), region.Centre)).ThenBy(m => m.X).ThenBy(m => m.Z)
                    : mins.OrderByDescending(m => FromEntry(new Cell(m.X, m.Z + spec.SizeZ - 1), area, plan.Entry))
                        .ThenBy(m => Math.Abs(m.X + (spec.SizeX / 2) - region.Centre.X)).ThenBy(m => m.X);
                foreach (var min in mins)
                {
                    if (plan.Grid.CanPlace(spec, min, inner) && Clear(plan, spec, min))
                    {
                        Add(plan, spec, min, role);
                        return true;
                    }
                }
            }
        }

        if (failure is not null)
        {
            plan.Notes.Add($"no room for {failure}");
        }

        return false;
    }

    /// <summary>A piece keeps one free cell from every other piece, so lanes stay open between them.</summary>
    private static bool Clear(SitePlan plan, PieceSpec spec, Cell min)
    {
        var around = new Rect(min.X - 1, min.Z - 1, spec.SizeX + 2, spec.SizeZ + 2);
        return around.Cells().Where(plan.Grid.InBounds).All(c => plan.Grid.Owner(c) == -1);
    }

    private static void Add(SitePlan plan, PieceSpec spec, Cell min, string role)
    {
        var index = plan.Pieces.Count;
        plan.Grid.Place(spec, min, index);
        plan.Pieces.Add(new SitePlacement(index, spec, min, role));
    }

    private static int Dist2(Cell a, Cell b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));

    // ---- 4. wrap

    private static void Wrap(SitePlan plan, IReadOnlyList<PieceSource> pieces, SeededRng rng)
    {
        var ring = plan.Ring;
        var grid = plan.Grid;
        var gates = GatePositions(ring, plan.Entry, Math.Clamp(plan.Archetype.Gates, 1, 3)).ToHashSet();
        foreach (var c in ring.Cells().Where(c => c.X == ring.X || c.X == ring.MaxX || c.Z == ring.Z || c.Z == ring.MaxZ))
        {
            if (gates.Contains(c))
            {
                grid[c] = CellKind.Door;
                plan.Gates.Add(c);
            }
            else
            {
                grid[c] = CellKind.Wall;
                plan.Walls.Add(c);
            }
        }

        // Cover inside each gate, beside the lane in, never in it.
        var inward = CampInward(plan.Entry);
        foreach (var gate in plan.Gates)
        {
            for (var n = 0; n < 2; n++)
            {
                if (Pick(pieces, plan.Archetype.DefenseTag, rng, p => plan.PieceCost + p.Meta.ThreatCost <= plan.Budget * plan.Archetype.PieceShare) is not { } piece)
                {
                    break;
                }

                var spec = PieceSpec.Build(piece, plan.Entry);
                var target = new Cell(gate.X + (inward.X * CoverDepth), gate.Z + (inward.Z * CoverDepth));
                var inside = ring.Inset(1);
                var near = new Rect(target.X - 10, target.Z - 10, 21, 21);
                var spot = near.Cells().Where(inside.Contains)
                    .Where(m => plan.Grid.CanPlace(spec, m, inside) && !InGateLane(spec, m, plan.Gates, inward) && Clear(plan, spec, m))
                    .OrderBy(m => Dist2(new Cell(m.X + (spec.SizeX / 2), m.Z + (spec.SizeZ / 2)), target)).ThenBy(m => m.X).ThenBy(m => m.Z)
                    .Cast<Cell?>().FirstOrDefault();
                if (spot is not { } min)
                {
                    break;
                }

                Add(plan, spec, min, SiteRole.Defense);
            }
        }

        Conduits(plan);
        Defenders(plan);
    }

    /// <summary>Gates spread evenly along the entry side of the ring.</summary>
    public static IEnumerable<Cell> GatePositions(Rect ring, Side entry, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            yield return entry switch
            {
                Side.South => new Cell(ring.X + (ring.W * i / (count + 1)), ring.Z),
                Side.North => new Cell(ring.X + (ring.W * i / (count + 1)), ring.MaxZ),
                Side.West => new Cell(ring.X, ring.Z + (ring.H * i / (count + 1))),
                _ => new Cell(ring.MaxX, ring.Z + (ring.H * i / (count + 1))),
            };
        }
    }

    /// <summary>The direction from the entry wall into the site.</summary>
    private static Cell CampInward(Side entry) => entry switch
    {
        Side.South => new Cell(0, 1),
        Side.North => new Cell(0, -1),
        Side.West => new Cell(1, 0),
        _ => new Cell(-1, 0),
    };

    private static bool InGateLane(PieceSpec spec, Cell min, IEnumerable<Cell> gates, Cell inward)
    {
        var rect = new Rect(min.X, min.Z, spec.SizeX, spec.SizeZ);
        foreach (var gate in gates)
        {
            for (var d = 0; d <= CoverDepth + 3; d++)
            {
                for (var side = -GateLane; side <= GateLane; side++)
                {
                    var c = inward.X != 0
                        ? new Cell(gate.X + (inward.X * d), gate.Z + side)
                        : new Cell(gate.X + side, gate.Z + (inward.Z * d));
                    if (rect.Contains(c))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Conduits from the generator piece to each piece that needs power, along cells that aren't walls or doors. As in
    /// vanilla (<c>PowerConnectionMaker</c>: a building wires itself to a conduit up to 6 cells away, through walls), a
    /// conduit only has to come within <see cref="SiteValidator.PowerReach"/> of a powered thing.
    /// </summary>
    private static void Conduits(SitePlan plan)
    {
        var consumers = plan.Pieces.Where(p => p.Piece.Meta.NeedsPower).ToList();
        if (consumers.Count == 0)
        {
            return;
        }

        var generator = plan.Pieces.FirstOrDefault(p => p.Has(SiteTags.Power));
        if (generator is null)
        {
            plan.Notes.Add("pieces need power but there's no generator");
            return;
        }

        var inside = plan.Ring.Inset(1);
        bool Open(Cell c) => inside.Contains(c) && !plan.Grid.IsReserved(c) && plan.Grid[c] is not (CellKind.Wall or CellKind.Door);
        var starts = SiteValidator.InReach(generator.PowerCells(), plan.Grid).Where(Open).ToHashSet();
        foreach (var consumer in consumers)
        {
            var goals = SiteValidator.InReach(consumer.PowerCells(), plan.Grid).Where(Open).ToHashSet();
            var path = plan.Grid.Path(starts.Concat(plan.Conduits).Distinct().ToList(), goals, Open);
            if (path is null)
            {
                plan.Notes.Add($"no conduit path to {consumer.Piece.Name}");
                continue;
            }

            foreach (var c in path.Where(c => !plan.Grid.Conduit(c)))
            {
                plan.Grid.SetConduit(c);
                plan.Conduits.Add(c);
            }
        }
    }

    /// <summary>The cell nearest <paramref name="from"/> (ring by ring outward, then by position) that <paramref name="ok"/>
    /// allows, within <paramref name="radius"/> cells either way.</summary>
    private static Cell? Nearest(Cell from, Func<Cell, bool> ok, int radius)
    {
        for (var r = 0; r <= radius; r++)
        {
            var ring = new Rect(from.X - r, from.Z - r, (2 * r) + 1, (2 * r) + 1).Cells()
                .Where(c => Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Z - from.Z)) == r && ok(c))
                .OrderBy(c => Dist2(c, from)).ThenBy(c => c.X).ThenBy(c => c.Z).ToList();
            if (ring.Count > 0)
            {
                return ring[0];
            }
        }

        return null;
    }

    /// <summary>Opens a gate in the middle of the ring's <paramref name="side"/> wall (the Citadel keep's side entrance,
    /// from intel). Returns the gate, or null if that side already has one.</summary>
    public static Cell? AddGate(SitePlan plan, Side side)
    {
        var gate = GatePositions(plan.Ring, side, 1).First();
        if (plan.Gates.Contains(gate))
        {
            return null;
        }

        plan.Grid[gate] = CellKind.Door;
        plan.Walls.Remove(gate);
        plan.Gates.Add(gate);
        return gate;
    }

    /// <summary>Where the garrison stands: the open cell nearest each slot's centre, a few in front of each gate, and
    /// inside the barracks.</summary>
    private static void Defenders(SitePlan plan)
    {
        var inside = plan.Ring.Inset(1);
        bool Open(Cell c) => inside.Contains(c) && !plan.Grid.IsReserved(c) && plan.Grid[c] is CellKind.Empty or CellKind.Floor;
        var spots = new List<Cell>();
        foreach (var slot in plan.Slots)
        {
            if (Nearest(slot.Centre, Open, 12) is { } c)
            {
                spots.Add(c);
            }
        }

        var inward = CampInward(plan.Entry);
        foreach (var gate in plan.Gates)
        {
            spots.AddRange(Enumerable.Range(2, 3).Select(d => new Cell(gate.X + (inward.X * d), gate.Z + (inward.Z * d))).Where(Open));
        }

        foreach (var barracks in plan.Pieces.Where(p => p.Has(SiteTags.Barracks)))
        {
            spots.AddRange(barracks.Cells().Where(Open).Take(3));
        }

        plan.DefenderSpots.AddRange(spots.Distinct());
    }
}

/// <summary>Piece tags the site planner and validators look for.</summary>
public static class SiteTags
{
    public const string Power = "Power";
    public const string Barracks = "Barracks";

    /// <summary>A sandbagged emplacement open at the back (mortar pit, turret nest).</summary>
    public const string Emplacement = "Emplacement";

    /// <summary>A room meant to be entered only by breaching (a vault): the sealed-room validator lets it be.</summary>
    public const string Sealed = "Sealed";
}

/// <summary>
/// The site validators (crisis spec §4.3, fairness rule 4): the objective is reachable from the map edge, every piece
/// that needs power is joined to a generator, no room is sealed unless its piece says so, the required pieces are
/// there, and the budget is within the archetype's range.
/// </summary>
public static class SiteValidator
{
    /// <summary>How far (in cells, either way) a conduit may be from a powered thing's cell for the thing to wire into
    /// it. Vanilla connects within 6 of the thing's position; 4 from any of its cells stays inside that for things up
    /// to 5 cells across.</summary>
    public const int PowerReach = 4;

    /// <summary>The grid cells within <see cref="PowerReach"/> of any of <paramref name="cells"/>.</summary>
    public static IEnumerable<Cell> InReach(IEnumerable<Cell> cells, CellGrid grid) => cells
        .SelectMany(c => new Rect(c.X - PowerReach, c.Z - PowerReach, (2 * PowerReach) + 1, (2 * PowerReach) + 1).Cells())
        .Where(grid.InBounds).Distinct();

    /// <summary>Numbers each run of touching conduits (0 for no conduit).</summary>
    private static int[,] Networks(SitePlan plan)
    {
        var grid = plan.Grid;
        var net = new int[grid.Width, grid.Height];
        var next = 0;
        foreach (var c in plan.Conduits.Where(c => net[c.X, c.Z] == 0))
        {
            next++;
            var seen = grid.Flood([c], grid.Conduit);
            for (var x = 0; x < grid.Width; x++)
            {
                for (var z = 0; z < grid.Height; z++)
                {
                    if (seen[x, z])
                    {
                        net[x, z] = next;
                    }
                }
            }
        }

        return net;
    }

    private static IEnumerable<int> Nets(SitePlacement piece, CellGrid grid, int[,] net) =>
        InReach(piece.PowerCells(), grid).Select(c => net[c.X, c.Z]).Where(n => n > 0).Distinct();

    public static IReadOnlyList<string> Problems(SitePlan plan)
    {
        var problems = new List<string>();
        var grid = plan.Grid;
        var a = plan.Archetype;
        if (plan.Objective is not { } objective)
        {
            problems.Add($"no objective piece ({a.ObjectiveTag})");
        }
        else if (!objective.Has(a.ObjectiveTag))
        {
            problems.Add($"the objective piece {objective.Piece.Name} isn't tagged {a.ObjectiveTag}");
        }

        foreach (var request in a.Required)
        {
            var have = plan.Pieces.Count(p => p.Role == SiteRole.Required && p.Has(request.Tag));
            if (have < request.Count)
            {
                problems.Add($"required {request.Tag}: {have} of {request.Count}");
            }
        }

        if (plan.Gates.Count == 0)
        {
            problems.Add("no gate");
        }

        // Reachability: from every edge cell, through anything but walls (doors open).
        var reached = grid.Flood(grid.Border(), c => grid[c] != CellKind.Wall);
        if (plan.Objective is { } goal && !goal.Cells().Any(c => grid[c] != CellKind.Wall && reached[c.X, c.Z]))
        {
            problems.Add($"the objective {goal.Piece.Name} can't be reached from the map edge");
        }

        foreach (var piece in plan.Pieces.Where(p => !p.Has(SiteTags.Sealed)))
        {
            if (piece.Cells().Any(c => grid[c] != CellKind.Wall && !reached[c.X, c.Z]))
            {
                problems.Add($"{piece.Piece.Name} at {piece.Min} has a sealed room");
            }
        }

        // Power: a consumer and a generator wire into the same run of conduits (each within reach of one of its cells).
        var consumers = plan.Pieces.Where(p => p.Piece.Meta.NeedsPower).ToList();
        if (consumers.Count > 0)
        {
            var generators = plan.Pieces.Where(p => p.Has(SiteTags.Power)).ToList();
            if (generators.Count == 0)
            {
                problems.Add("pieces need power but there's no generator");
            }
            else
            {
                var net = Networks(plan);
                var live = generators.SelectMany(g => Nets(g, plan.Grid, net)).ToHashSet();
                foreach (var consumer in consumers.Where(c => !Nets(c, plan.Grid, net).Any(live.Contains)))
                {
                    problems.Add($"{consumer.Piece.Name} at {consumer.Min} has no power");
                }
            }
        }

        var total = plan.PieceCost + plan.DefenderPoints;
        if (total < a.MinBudget - 0.5f || total > a.MaxBudget + 0.5f)
        {
            problems.Add($"budget {total:0} is outside {a.MinBudget:0}–{a.MaxBudget:0}");
        }

        return problems;
    }
}
