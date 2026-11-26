using System.Diagnostics;
using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Pieces;
using RimChronicles.Game.Crisis.Siege;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>Which settlements' maps are the Citadel, and its map generator. Main thread only.</summary>
public static class CitadelMaps
{
    /// <summary>Damage keep buildings take while a courtyard relay stands (crisis spec §4.5: "heavily reduced").</summary>
    public const float KeepShieldFactor = 0.1f;

    public static MapGeneratorDef? Generator => DefDatabase<MapGeneratorDef>.GetNamedSilentFail("RimChronicles_Citadel");

    /// <summary>Settlements Dev Mode turned into Citadels, with the options to use.</summary>
    public static readonly Dictionary<int, CitadelOptions> Dev = [];

    /// <summary>The Empire's Citadel or, since War in Heaven W9, the Hollow Choir's Cathedral: both use this generator.</summary>
    public static bool IsCitadel(MapParent? parent) => IsImperialCitadel(parent) || IsCathedral(parent);

    /// <summary>The Choir's capital becomes the Cathedral once its crisis is at war (or the finale) and the Choir is
    /// hostile; a settlement whose map was generated as the Cathedral stays one.</summary>
    public static bool IsCathedral(MapParent? parent)
    {
        if (parent is not Settlement settlement || Generator is null || !Choir.ChoirCrisisDriver.Available
            || Choir.ChoirCrisisDriver.Faction is not { } choir || settlement.Faction != choir || RimChroniclesGameComponent.Current is not { } component)
        {
            return false;
        }

        if (Dev.ContainsKey(settlement.ID) || component.ChoirCrisis.CitadelSettlementId == settlement.ID)
        {
            return true;
        }

        return choir.HostileTo(Faction.OfPlayer) && Choir.ChoirCrisisDriver.Stage(component, choir).Stage >= CrisisStage.War
            && EmpireBeats.Capital(choir) == settlement;
    }

    /// <summary>The capital becomes the Citadel once the crisis is at war (or the finale) and the Empire is hostile; a
    /// settlement whose map was generated as the Citadel stays one.</summary>
    public static bool IsImperialCitadel(MapParent? parent)
    {
        if (parent is not Settlement settlement || Generator is null || !CrisisDriver.Available
            || EmpireDriver.EmpireFaction is not { } empire || settlement.Faction != empire || RimChroniclesGameComponent.Current is not { } component)
        {
            return false;
        }

        if (Dev.ContainsKey(settlement.ID) || component.Crisis.CitadelSettlementId == settlement.ID)
        {
            return true;
        }

        return empire.HostileTo(Faction.OfPlayer) && CrisisDriver.Stage(component, empire).Stage >= CrisisStage.War
            && EmpireBeats.Capital(empire) == settlement;
    }

    /// <summary>The Citadel's options from the crisis: courtyard relays and uplinks left, the side entrance, the budget.
    /// For the Cathedral, the Choir's: resonator and spire sites destroyed, and its budget.</summary>
    public static CitadelOptions Options(int mapSize, int settlementId, bool cathedral = false)
    {
        if (Dev.TryGetValue(settlementId, out var dev))
        {
            return dev with { MapSize = mapSize };
        }

        var component = RimChroniclesGameComponent.Current;
        var state = cathedral ? component?.ChoirCrisis : component?.Crisis;
        var budget = component is null ? 12000f
            : 2 * (cathedral ? Choir.ChoirCrisisDriver.Budget(component, CrisisStage.Finale, ColonyStrengthReader.Read())
                : CrisisDriver.Budget(component, CrisisStage.Finale, ColonyStrengthReader.Read()));
        return new CitadelOptions
        {
            Power = cathedral ? Core.Powers.Power.Choir.Id : Core.Powers.Power.Empire.Id,
            MapSize = mapSize,
            Relays = CitadelRings.Relays(state?.RelaySitesDestroyed ?? 0),
            Uplinks = CitadelRings.Uplinks(state?.UplinkSitesDestroyed ?? 0),
            SideEntrance = state?.KeepEntranceKnown ?? false,
            Budget = budget,
        };
    }
}

/// <summary>
/// Generates the Citadel (crisis C12, spec §4.5): plans the three rings with Core's <see cref="CitadelPlanner"/>, seeded
/// from the settlement, stamps them outermost first, and spawns the outer garrison (and the inner ones, unless the
/// lazy-rings setting holds them back). <see cref="MapComponent_RcCitadel"/> runs the rest.
/// </summary>
public sealed class GenStep_RcCitadel : GenStep
{
    public override int SeedPart => 734190553;

    public override void Generate(Map map, GenStepParams parms)
    {
        // The Hollow Choir's Cathedral (War in Heaven W9) when the settlement is the Choir's, else the Imperial Citadel.
        var cathedral = map.Parent?.Faction is { } owner && owner == Choir.ChoirCrisisDriver.Faction;
        var power = cathedral ? Core.Powers.Power.Choir : Core.Powers.Power.Empire;
        if (Powers.PowerFactions.Of(power) is not { } empire)
        {
            Log.Error($"[Rim Chronicles] The {(cathedral ? "Cathedral needs the Hollow Choir's" : "Citadel needs the Empire")} faction; nothing generated.");
            return;
        }

        var clock = Stopwatch.StartNew();
        var id = map.Parent?.ID ?? map.Tile.tileId;
        var options = CitadelMaps.Options(Math.Min(map.Size.x, map.Size.z), id, cathedral) with { Power = power.Id };
        var plan = CitadelPlanner.Plan(PieceLibrary.SourcesFor(power.Id), options, id);
        var problems = CitadelPlanner.Problems(plan);
        if (problems.Count > 0)
        {
            Log.Warning($"[Rim Chronicles] Citadel plan problems: {string.Join("; ", problems)}");
        }

        var planned = clock.ElapsedMilliseconds;
        foreach (var ring in plan.Rings)
        {
            SiteStamper.Stamp(map, ring.Plan, new IntVec3(ring.At.X, 0, ring.At.Z), null, $"{(cathedral ? "Cathedral" : "Citadel")} {ring.Name}", empire,
                roads: ring.Index == 0, StuffPalette.ForPower(power.Id));
        }

        var component = MapComponent_RcCitadel.For(map)!;
        component.Set(plan, empire);
        var lazy = RimChroniclesMod.Settings.CitadelLazyRings;
        foreach (var ring in plan.Rings)
        {
            var garrison = new PendingGarrison(ring, plan);
            if (lazy && ring.Index > 0)
            {
                component.Hold(garrison);
            }
            else
            {
                component.Spawn(garrison, empire);
            }
        }

        if (RimChroniclesGameComponent.Current is { } game)
        {
            (cathedral ? game.ChoirCrisis : game.Crisis).CitadelSettlementId = id;
        }

        Log.Message($"[Rim Chronicles] {(cathedral ? "Cathedral" : "Citadel")} generated in {clock.ElapsedMilliseconds} ms (plan {planned} ms): map {options.MapSize}, "
            + $"{plan.Relays.Count()} courtyard relay(s), {options.Uplinks} uplink(s), side entrance {(plan.SideGate is null ? "hidden" : "known")}, "
            + $"budget {options.Budget:0}, pieces {string.Join("/", plan.Rings.Select(r => r.Plan.Pieces.Count))}{(lazy ? ", inner garrisons held back" : "")}.");
    }
}

/// <summary>A ring's garrison: its points, where it stands, and the ring it holds. Kept for later with lazy rings.</summary>
public sealed class PendingGarrison : IExposable
{
    public int ring;
    public float points;
    public List<IntVec3> spots = [];
    public IntVec3 centre;
    public float radius;

    /// <summary>Where the High Stellarch stands, in the keep's throne room (C13); invalid for other rings.</summary>
    public IntVec3 leader = IntVec3.Invalid;

    public PendingGarrison()
    {
    }

    public PendingGarrison(CitadelRing r, CitadelPlan plan)
    {
        ring = r.Index;
        points = r.Plan.DefenderPoints;
        spots = r.Plan.DefenderSpots.Select(c => r.ToMap(c)).Select(c => new IntVec3(c.X, 0, c.Z)).ToList();
        var c = r.RingOnMap.Centre;
        centre = new IntVec3(c.X, 0, c.Z);
        radius = Math.Max(r.RingOnMap.W, r.RingOnMap.H) / 2f;
        if (r.Index == 2 && plan.Throne is { } throne)
        {
            var room = throne.Cells().Where(cell => r.Plan.Grid[cell] is Core.Crisis.Pieces.CellKind.Empty or Core.Crisis.Pieces.CellKind.Floor)
                .OrderBy(cell => Math.Abs(cell.X - throne.Bounds.Centre.X) + Math.Abs(cell.Z - throne.Bounds.Centre.Z)).ToList();
            if (room.Count > 0)
            {
                var at = r.ToMap(room[0]);
                leader = new IntVec3(at.X, 0, at.Z);
            }
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref ring, "ring");
        Scribe_Values.Look(ref points, "points");
        Scribe_Collections.Look(ref spots, "spots", LookMode.Value);
        Scribe_Values.Look(ref centre, "centre");
        Scribe_Values.Look(ref radius, "radius");
        Scribe_Values.Look(ref leader, "leader", IntVec3.Invalid);
        spots ??= [];
    }
}

/// <summary>
/// A ring's garrison lord: it holds its ring; the courtyard's moves out to meet the colonists when they enter the outer
/// works (crisis spec §4.5: "defenders from the next ring inward move up"). The keep's honor guard never leaves.
/// </summary>
public sealed class LordJob_RcGarrison : LordJob
{
    public const string ReinforceMemo = "RcReinforce";

    public IntVec3 centre;
    public float radius;
    public int ring;

    public LordJob_RcGarrison()
    {
    }

    public LordJob_RcGarrison(IntVec3 centre, float radius, int ring)
    {
        this.centre = centre;
        this.radius = radius;
        this.ring = ring;
    }

    public bool HoldsFast => ring >= 2;

    /// <summary>No vanilla panic flee: a garrison holds its ring (or answers a reinforce memo) until it falls.</summary>
    public override bool AddFleeToil => false;

    public override StateGraph CreateGraph()
    {
        var graph = new StateGraph();
        var hold = new LordToil_DefendPoint(centre, radius, Math.Min(radius, 12f));
        graph.AddToil(hold);
        graph.StartingToil = hold;
        if (!HoldsFast)
        {
            var reinforce = new LordToil_RcAssault();
            graph.AddToil(reinforce);
            var move = new Transition(hold, reinforce);
            move.AddTrigger(new Trigger_Memo(ReinforceMemo));
            graph.AddTransition(move);
        }

        return graph;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref centre, "centre");
        Scribe_Values.Look(ref radius, "radius");
        Scribe_Values.Look(ref ring, "ring");
    }
}

/// <summary>
/// The Citadel on its map (crisis C12): the rings, the keep's shield and the courtyard relays that hold it up, which
/// ring the colonists have reached, and the garrisons held back by the lazy-rings setting. Saved with the map.
/// </summary>
public sealed class MapComponent_RcCitadel(Map map) : MapComponent(map)
{
    private const int CheckTicks = 250;
    private const float PsyfocusPerCheck = 0.02f;

    private bool _active;
    private List<CellRect> _rings = [];
    private List<Thing> _relays = [];
    private bool _shieldUp;
    private List<bool> _reached = [false, false, false];
    private List<Lord> _lords = [];
    private List<PendingGarrison> _held = [];
    private bool _welcomed;
    private bool _sideGate;

    /// <summary>Whose (War in Heaven W9): the Empire's Citadel, or the Choir's Cathedral. Null in saves from before W9:
    /// those are the Empire's.</summary>
    private Faction? _faction;

    private bool _cathedral;

    /// <summary>The Cathedral's chain of command: <c>_mechs[i]</c> is commanded by <c>_commanders[i]</c>.</summary>
    private List<Pawn?> _mechs = [];

    private List<Pawn?> _commanders = [];

    public bool Cathedral => _cathedral;

    private Faction? Owner => _faction ?? EmpireDriver.EmpireFaction;

    private string[] Names => _cathedral ? CitadelRings.ChoirNames : CitadelRings.Names;

    public bool Active => _active;
    public bool ShieldUp => _shieldUp;
    public IReadOnlyList<Thing> Relays => _relays;
    public IReadOnlyList<CellRect> Rings => _rings;

    public static MapComponent_RcCitadel? For(Map map) => map.GetComponent<MapComponent_RcCitadel>();

    public void Set(CitadelPlan plan, Faction empire)
    {
        _active = true;
        _faction = empire;
        _cathedral = plan.Options.IsCathedral;
        _rings = plan.Rings.Select(r => new CellRect(r.RingOnMap.X, r.RingOnMap.Z, r.RingOnMap.W, r.RingOnMap.H)).ToList();
        _relays = (_cathedral ? Choir.ChoirHum.ResonatorDef : SiegeDefs.Relay) is { } relay
            ? map.listerBuildings.allBuildingsNonColonist.Where(b => b.def == relay && b.Faction == empire && _rings[1].Contains(b.Position)).Cast<Thing>().ToList()
            : [];
        _shieldUp = _relays.Count > 0;
        _sideGate = plan.SideGate is not null;
    }

    public void Hold(PendingGarrison garrison) => _held.Add(garrison);

    /// <summary>Spawns a ring's garrison at its spots, under a <see cref="LordJob_RcGarrison"/>.</summary>
    public void Spawn(PendingGarrison g, Faction empire)
    {
        if (_cathedral)
        {
            SpawnChoir(g, empire);
            return;
        }

        var pawns = OperationSpawner.Force(ForceMix.Citadel(g.ring), CrisisStage.Finale, g.points, empire, CitadelRings.MaxDefenders[Math.Clamp(g.ring, 0, 2)]);
        if (pawns.Count == 0)
        {
            return;
        }

        var spots = g.spots.Where(c => c.InBounds(map) && c.Standable(map)).ToList();
        for (var i = 0; i < pawns.Count; i++)
        {
            GenSpawn.Spawn(pawns[i], CellFinder.RandomClosewalkCellNear(spots.Count > 0 ? spots[i % spots.Count] : g.centre, map, 2), map);
        }

        // The keep's honor guard is led by the High Stellarch, in the throne room (C13).
        if (g.ring == 2 && Finale.Stellarch.Ready(empire) is { } stellarch)
        {
            GenSpawn.Spawn(stellarch, g.leader.IsValid && g.leader.InBounds(map) ? g.leader : g.centre, map);
            pawns.Add(stellarch);
        }

        var lord = LordMaker.MakeNewLord(empire, new LordJob_RcGarrison(g.centre, g.radius, g.ring), map, pawns);
        while (_lords.Count <= g.ring)
        {
            _lords.Add(null!);
        }

        _lords[g.ring] = lord;
    }

    /// <summary>A Cathedral ring's garrison (W9): the ring's Choir mix under its mechanitors; the sanctum's bossgroup is
    /// commanded by the First Voice, who stands in the sanctum. A fallen commander's mechs shut down.</summary>
    private void SpawnChoir(PendingGarrison g, Faction choir)
    {
        var parts = Choir.ChoirForce.Build(Core.Choir.ChoirMixes.Cathedral(g.ring), CrisisStage.Finale, g.points, choir, CitadelRings.MaxDefenders[Math.Clamp(g.ring, 0, 2)]);
        Pawn? voice = null;
        if (g.ring == 2 && Choir.FirstVoice.Ready(choir) is { } leader)
        {
            voice = leader;
            parts.Mechanitors.Insert(0, leader);
            parts.Links.Clear();
            Choir.ChoirForce.Link(parts, parts.Mechs);
        }

        if (parts.Count == 0)
        {
            return;
        }

        var spots = g.spots.Where(c => c.InBounds(map) && c.Standable(map)).ToList();
        var pawns = parts.All;
        for (var i = 0; i < pawns.Count; i++)
        {
            var at = pawns[i] == voice && g.leader.IsValid && g.leader.InBounds(map) ? g.leader
                : CellFinder.RandomClosewalkCellNear(spots.Count > 0 ? spots[i % spots.Count] : g.centre, map, 2);
            GenSpawn.Spawn(pawns[i], at, map);
        }

        foreach (var (mech, commander) in parts.Links)
        {
            _mechs.Add(mech);
            _commanders.Add(commander);
        }

        var lord = LordMaker.MakeNewLord(choir, new LordJob_RcGarrison(g.centre, g.radius, g.ring), map, pawns);
        while (_lords.Count <= g.ring)
        {
            _lords.Add(null!);
        }

        _lords[g.ring] = lord;
    }

    /// <summary>A Cathedral mechanitor is dead, downed or captured: its mechs shut down where they stand.</summary>
    private void SilenceFallenCommanders()
    {
        var silenced = new List<Pawn>();
        for (var i = _mechs.Count - 1; i >= 0; i--)
        {
            if (_commanders[i] is { Dead: false, Downed: false, IsPrisoner: false, Spawned: true })
            {
                continue;
            }

            if (_mechs[i] is { Dead: false, Spawned: true } mech && mech.Map == map)
            {
                mech.TryGetComp<CompMechanoid>()?.Deactivate();
                silenced.Add(mech);
            }

            _mechs.RemoveAt(i);
            _commanders.RemoveAt(i);
        }

        if (silenced.Count > 0)
        {
            Messages.Message($"With their mechanitor down, {silenced.Count} of the Choir's mechs fall silent. Any colonist can dismantle them.",
                new LookTargets(silenced), MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>The ring a cell is in (2 keep, 1 courtyard, 0 outer works), or -1.</summary>
    public int RingOf(IntVec3 c)
    {
        for (var i = _rings.Count - 1; i >= 0; i--)
        {
            if (_rings[i].Contains(c))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A keep building, shielded while a courtyard relay stands (fairness rule 8: the relay is reachable).</summary>
    public bool Shields(Building building) =>
        _active && _shieldUp && _rings.Count > 2 && _rings[2].Contains(building.Position) && building.Faction == Owner;

    public override void MapComponentTick()
    {
        if (_active && _mechs.Count > 0 && Find.TickManager.TicksGame % 60 == 0)
        {
            SilenceFallenCommanders();
        }

        if (!_active || Find.TickManager.TicksGame % CheckTicks != 0)
        {
            return;
        }

        var colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed).ToList();
        if (!_welcomed && colonists.Count > 0)
        {
            _welcomed = true;
            Welcome();
        }

        if (_shieldUp && _relays.All(r => r is null || r.Destroyed) && _cathedral)
        {
            _shieldUp = false;
            StoryLetters.ChoirCrisis("The sanctum's shield is down",
                "The last resonator in the Cathedral's cloister is destroyed, and its hum with it. The sanctum's walls and doors are no longer shielded.",
                LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(_rings[2].CenterCell, map)));
        }

        if (_shieldUp && _relays.All(r => r is null || r.Destroyed))
        {
            _shieldUp = false;
            StoryLetters.Crisis("The keep's shield is down",
                "The last psychic relay in the Citadel's courtyard is destroyed. The keep's walls and doors are no longer shielded, and the psykers "
                + "inside have lost their link.", LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(_rings[2].CenterCell, map)));
        }

        if (_shieldUp && !_cathedral)
        {
            Empower();
        }

        var deepest = colonists.Select(p => RingOf(p.Position)).DefaultIfEmpty(-1).Max();
        for (var ring = 0; ring <= deepest && ring < _reached.Count; ring++)
        {
            if (!_reached[ring])
            {
                _reached[ring] = true;
                Reached(ring);
            }
        }
    }

    /// <summary>Colonists reached a ring: the next ring in comes to life (lazy rings), and the courtyard's garrison moves
    /// up to meet them when they enter the outer works.</summary>
    private void Reached(int ring)
    {
        if (Owner is { } empire)
        {
            foreach (var held in _held.Where(h => h.ring == ring + 1).ToList())
            {
                _held.Remove(held);
                Spawn(held, empire);
            }
        }

        if (ring + 1 < _lords.Count && _lords[ring + 1] is { } next && next.LordJob is LordJob_RcGarrison { HoldsFast: false })
        {
            next.ReceiveMemo(LordJob_RcGarrison.ReinforceMemo);
            Messages.Message($"The {(_cathedral ? "Cathedral" : "Citadel")}'s {Names[ring + 1]} garrison is moving out to meet you.", MessageTypeDefOf.ThreatBig);
        }
    }

    /// <summary>Imperial psykers inside the shielded keep keep their psyfocus up and their link to the relays.</summary>
    private void Empower()
    {
        var link = SiegeDefs.RelayLink;
        foreach (var pawn in map.mapPawns.SpawnedPawnsInFaction(EmpireDriver.EmpireFaction!).Where(p => p.HasPsylink && _rings[2].Contains(p.Position)))
        {
            pawn.psychicEntropy?.OffsetPsyfocusDirectly(PsyfocusPerCheck);
            if (link is not null)
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(link) is HediffWithComps had)
                {
                    had.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                }
                else
                {
                    pawn.health.AddHediff(link);
                }
            }
        }
    }

    private void Welcome()
    {
        if (_cathedral)
        {
            WelcomeCathedral();
            return;
        }

        var relays = _relays.Count;
        var text = "The Imperial Citadel: three rings of walls. The outer works hold trenches, turret nests and mortar pits; the courtyard "
            + "holds barracks, an armory and the Empire's psychic relays and uplinks; the keep holds the throne room.\n\n"
            + (relays > 0
                ? $"While any of the courtyard's {relays} psychic relay{(relays == 1 ? "" : "s")} stands, the keep's walls and doors are shielded and its psykers "
                  + "are strengthened. Destroy the relays first."
                : "The colony's raids on the Empire's relay sites left the courtyard without a relay: the keep is unshielded.")
            + (_sideGate ? "\n\nThe stolen Citadel plans show a side entrance into the keep, on the far side from its main gate." : "")
            + "\n\nWhen you enter the outer works, the courtyard's garrison will come out to meet you. The keep's guard never leaves the keep.";
        StoryLetters.Crisis("The Imperial Citadel", text, LetterDefOf.NeutralEvent, new LookTargets(new TargetInfo(_rings[2].CenterCell, map)));
    }

    private void WelcomeCathedral()
    {
        var resonators = _relays.Count;
        var text = "The Hollow Choir's Cathedral: three rings of walls. The outer works hold its mech line, charge-turret nests and auto-mortars; "
            + "the cloister holds its barracks, signal spires and the resonators; the sanctum holds the First Voice and its bossgroup guard.\n\n"
            + (resonators > 0
                ? $"While any of the cloister's {resonators} resonator{(resonators == 1 ? "" : "s")} stands, the sanctum's walls and doors are shielded, and "
                  + "everyone who can feel it hears the hum. Destroy the resonators first."
                : "The colony's raids on the Choir's resonators left the cloister without one: the sanctum is unshielded, and there is no hum.")
            + "\n\nThe Choir's mechs follow their mechanitors: bring a mechanitor down and its mechs fall silent. Bring the First Voice down and "
            + "the bossgroup falls with it.\n\nWhen you enter the outer works, the cloister's garrison will come out to meet you. The sanctum's guard "
            + "never leaves the sanctum.";
        StoryLetters.ChoirCrisis("The Cathedral", text, LetterDefOf.NeutralEvent, new LookTargets(new TargetInfo(_rings[2].CenterCell, map)));
    }

    public override void ExposeData()
    {
        Scribe_References.Look(ref _faction, "rcCitadelFaction");
        Scribe_Values.Look(ref _cathedral, "rcCathedral");
        Scribe_Collections.Look(ref _mechs, "rcCathedralMechs", LookMode.Reference);
        Scribe_Collections.Look(ref _commanders, "rcCathedralCommanders", LookMode.Reference);
        Scribe_Values.Look(ref _active, "rcCitadel");
        Scribe_Collections.Look(ref _rings, "rcCitadelRings", LookMode.Value);
        Scribe_Collections.Look(ref _relays, "rcCitadelRelays", LookMode.Reference);
        Scribe_Values.Look(ref _shieldUp, "rcCitadelShield");
        Scribe_Collections.Look(ref _reached, "rcCitadelReached", LookMode.Value);
        Scribe_Collections.Look(ref _lords, "rcCitadelLords", LookMode.Reference);
        Scribe_Collections.Look(ref _held, "rcCitadelHeld", LookMode.Deep);
        Scribe_Values.Look(ref _welcomed, "rcCitadelWelcomed");
        Scribe_Values.Look(ref _sideGate, "rcCitadelSideGate");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _rings ??= [];
            _relays ??= [];
            _relays.RemoveAll(r => r is null);
            _reached ??= [false, false, false];
            while (_reached.Count < 3)
            {
                _reached.Add(false);
            }

            _lords ??= [];
            _held ??= [];
            _mechs ??= [];
            _commanders ??= [];
            if (_mechs.Count != _commanders.Count)
            {
                _mechs.Clear();
                _commanders.Clear();
            }
        }
    }

    // ---- Dev Mode

    /// <summary>Makes an Imperial settlement near the colony that generates as the Citadel, generates it, goes there,
    /// and reports how long generation took (C12's done-when: under 30 s on the PC).</summary>
    [DebugAction("Rim Chronicles", "Crisis: generate Citadel", allowedGameStates = AllowedGameStates.PlayingOnWorld | AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Generate() =>
    [
        new("standard (2 relays, 1 uplink)", DebugActionType.Action, () => Make(new CitadelOptions())),
        new("side entrance known", DebugActionType.Action, () => Make(new CitadelOptions { SideEntrance = true })),
        new("after 1 relay and 1 uplink site destroyed", DebugActionType.Action, () => Make(new CitadelOptions { Relays = 1, Uplinks = 0 })),
        new("no relays left (keep unshielded)", DebugActionType.Action, () => Make(new CitadelOptions { Relays = 0, Uplinks = 0 })),
    ];

    /// <summary>The Hollow Choir's Cathedral (War in Heaven W9), made as the Citadel is.</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: generate Cathedral", allowedGameStates = AllowedGameStates.PlayingOnWorld | AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> GenerateCathedral() =>
    [
        new("standard (2 resonators, 1 spire)", DebugActionType.Action, () => Make(new CitadelOptions { Power = Core.Powers.Power.Choir.Id })),
        new("after 1 resonator and 1 spire site destroyed", DebugActionType.Action,
            () => Make(new CitadelOptions { Power = Core.Powers.Power.Choir.Id, Relays = 1, Uplinks = 0 })),
        new("no resonators left (sanctum unshielded)", DebugActionType.Action,
            () => Make(new CitadelOptions { Power = Core.Powers.Power.Choir.Id, Relays = 0, Uplinks = 0 })),
    ];

    private static void Make(CitadelOptions options)
    {
        var power = options.IsCathedral ? Core.Powers.Power.Choir : Core.Powers.Power.Empire;
        if (Powers.PowerFactions.Of(power) is not { } empire || CitadelMaps.Generator is null)
        {
            Messages.Message(options.IsCathedral
                ? "Rim Chronicles: the Cathedral needs Biotech, Royalty and the Hollow Choir awake (Hollow Choir: wake now)."
                : "Rim Chronicles: the Citadel needs Royalty and the Empire faction.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        PlanetTile? home = Find.AnyPlayerHomeMap?.Tile;
        if (!(home is { } h ? TileFinder.TryFindNewSiteTile(out var tile, h, 2, 10) : TileFinder.TryFindNewSiteTile(out tile, 2, 10)))
        {
            Messages.Message("Rim Chronicles: no free tile nearby.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
        settlement.SetFaction(empire);
        settlement.Tile = tile;
        settlement.Name = options.IsCathedral ? "Cathedral (test)" : "Citadel (test)";
        Find.WorldObjects.Add(settlement);
        var budget = RimChroniclesGameComponent.Current is { } c ? 2 * CrisisDriver.Budget(c, CrisisStage.Finale, ColonyStrengthReader.Read()) : 12000f;
        CitadelMaps.Dev[settlement.ID] = options with { Budget = budget };
        var size = RimChroniclesMod.Settings.CitadelMapSize;
        var clock = Stopwatch.StartNew();
        var map = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, new IntVec3(size, 1, size), null);
        var seconds = clock.ElapsedMilliseconds / 1000f;
        Current.Game.CurrentMap = map;
        Find.CameraDriver.JumpToCurrentMapLoc(map.Center);
        Messages.Message($"Rim Chronicles: {(options.IsCathedral ? "Cathedral" : "Citadel")} generated in {seconds:0.0} s ({size}×{size}). The log has the details.",
            seconds < 30 ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.NegativeEvent, historical: false);
    }

    /// <summary>Plans 20 Citadels (no map) and reports the validators and planning time.</summary>
    [DebugAction("Rim Chronicles", "Crisis: batch Citadels", allowedGameStates = AllowedGameStates.Playing)]
    public static void Batch()
    {
        var pieces = PieceLibrary.SourcesFor(Core.Powers.Power.Empire.Id);
        var lines = new List<string>();
        var clock = Stopwatch.StartNew();
        var passed = 0;
        for (var id = 1; id <= 20; id++)
        {
            var plan = CitadelPlanner.Plan(pieces, new CitadelOptions { MapSize = RimChroniclesMod.Settings.CitadelMapSize, SideEntrance = id % 2 == 0, Relays = id % 3 }, id);
            var problems = CitadelPlanner.Problems(plan);
            if (problems.Count == 0 && !plan.Fallback)
            {
                passed++;
            }
            else
            {
                lines.Add($"  seed {id}: {(plan.Fallback ? "fallback; " : "")}{string.Join("; ", problems)}");
            }
        }

        lines.Insert(0, $"Citadels: {passed}/20 pass every validator, planned in {clock.ElapsedMilliseconds / 20} ms each on average.");
        Log.Message("[Rim Chronicles] " + string.Join("\n", lines));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    /// <summary>The Citadel on the current map: shield, relays, rings reached, held garrisons.</summary>
    [DebugAction("Rim Chronicles", "Crisis: Citadel state", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Show()
    {
        if (Find.CurrentMap is not { } map || For(map) is not { Active: true } c)
        {
            Messages.Message("Rim Chronicles: this map isn't the Citadel.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var lines = new List<string>
        {
            $"Shield: {(c._shieldUp ? "up" : "down")}; relays standing {c._relays.Count(r => r is { Destroyed: false })} of {c._relays.Count}.",
            $"Rings reached: {string.Join(", ", c._reached.Select((r, i) => $"{CitadelRings.Names[i]} {(r ? "yes" : "no")}"))}.",
            $"Garrisons: {string.Join(", ", c._lords.Select((l, i) => $"{CitadelRings.Names[i]} {(l is null ? "none" : $"{l.ownedPawns.Count} ({l.CurLordToil?.GetType().Name})")}"))}; "
                + $"held back: {string.Join(", ", c._held.Select(h => CitadelRings.Names[h.ring]).DefaultIfEmpty("none"))}.",
            $"Side entrance: {(c._sideGate ? "yes" : "no")}.",
        };
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }
}
