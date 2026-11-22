using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RimChronicles.Game.Crisis.Strikes;

/// <summary>A strike that has been called and is being telegraphed. Saved with the map.</summary>
public sealed class PendingStrike : IExposable
{
    public IntVec3 cell;
    public StrikeKind kind;
    public Pawn? herald;
    public int fireTick;

    /// <summary>Called from Dev Mode: no Herald needed.</summary>
    public bool dev;

    public void ExposeData()
    {
        Scribe_Values.Look(ref cell, "cell");
        Scribe_Values.Look(ref kind, "kind");
        Scribe_References.Look(ref herald, "herald");
        Scribe_Values.Look(ref fireTick, "fireTick");
        Scribe_Values.Look(ref dev, "dev");
    }
}

/// <summary>
/// Imperial orbital strikes on one map (crisis C5, spec §5.4). While a crisis raid is on the map, a Herald who is up
/// and can see a good target (or a standing uplink) calls a strike: the target is marked and a letter warns the player,
/// and the strike lands 6–10 seconds later, unless the source is lost first (fairness rule 1). Rules are in Core's
/// <see cref="StrikeRules"/>; this reads the map and spawns Royalty's strike things. Main thread only.
/// </summary>
public sealed class MapComponent_RcStrikes(Map map) : MapComponent(map)
{
    private const int CheckTicks = 60;
    private const int SourceCheckTicks = 30;
    private const float HeraldSight = 60f;

    /// <summary>ThingDefs that count as a standing uplink for the Empire (the siege uplink and Orbital Uplink sites add
    /// theirs in C8 and C9).</summary>
    public static readonly HashSet<ThingDef> Uplinks = [];

    private StrikeState _state = new();
    private List<PendingStrike> _pending = [];

    public StrikeState State => _state;
    public IReadOnlyList<PendingStrike> Pending => _pending;

    public static MapComponent_RcStrikes? For(Map map) => map.GetComponent<MapComponent_RcStrikes>();

    public override void MapComponentTick()
    {
        var tick = Find.TickManager.TicksGame;
        if (_pending.Count > 0 && tick % SourceCheckTicks == 0)
        {
            Telegraph(tick);
        }

        if (tick % CheckTicks != 0 || !CrisisDriver.Available || EmpireDriver.EmpireFaction is not { } empire)
        {
            return;
        }

        if (!RaidOnMap(empire))
        {
            _state.Reset();
            return;
        }

        var heralds = Heralds(empire);
        var uplink = UplinkStanding(empire);
        if (_state.Cap == 0 && (heralds.Count > 0 || uplink))
        {
            _state.Cap = StrikeRules.CapFor(RaidBudget());
        }

        if (_pending.Count > 0 || StrikeRules.Blocked(_state, heralds.Count > 0, uplink, StrikesDown(), tick) is not null)
        {
            return;
        }

        if (StrikeRules.Pick(Candidates(empire, heralds, uplink), _state) is { } pick)
        {
            var cell = new IntVec3(pick.Target.Cell.X, 0, pick.Target.Cell.Z);
            Call(cell, pick.Kind, heralds.Where(h => Sees(h, cell)).OrderBy(h => h.Position.DistanceTo(cell)).FirstOrDefault(), dev: false);
        }
    }

    /// <summary>Calls a strike on <paramref name="cell"/>: marks it, warns the player, and lands it after the delay.</summary>
    public void Call(IntVec3 cell, StrikeKind kind, Pawn? herald, bool dev)
    {
        var tick = Find.TickManager.TicksGame;
        var delay = StrikeRules.DelayTicks(RimChroniclesMod.Settings.StrikeDelaySeconds);
        _pending.Add(new PendingStrike { cell = cell, kind = kind, herald = herald, fireTick = tick + delay, dev = dev });
        _state.Struck(new Cell(cell.x, cell.z), tick);
        FleckMaker.Static(cell, map, FleckDefOf.PsycastAreaEffect, 8f);
        SoundDefOf.OrbitalStrike_Ordered.PlayOneShotOnCamera(map);
        var who = herald is null ? "An Imperial uplink" : $"The Imperial herald {herald.LabelShort}";
        var what = kind switch
        {
            StrikeKind.Salvo => "an orbital salvo",
            StrikeKind.Beam => "a power beam",
            _ => "an orbital strike",
        };
        Find.LetterStack.ReceiveLetter("Orbital strike called",
            $"{who} has called {what} on the marked spot. It lands in about {delay / StrikeRules.TicksPerSecond} seconds.\n\n"
            + (herald is null ? "Get clear." : $"Get clear, or take {herald.LabelShort} down before it lands: without its herald, the strike is called off."),
            LetterDefOf.ThreatBig, new LookTargets(new TargetInfo(cell, map)), EmpireDriver.EmpireFaction);
    }

    public override void ExposeData()
    {
        ScribeArchive.Instance.Deep(ref _state, "strikeState");
        Scribe_Collections.Look(ref _pending, "pendingStrikes", LookMode.Deep);
        _state ??= new StrikeState();
        _pending ??= [];
        _pending.RemoveAll(p => p is null);
    }

    /// <summary>Keeps each pending strike marked, calls it off if its source is gone, and lands it when it's due.</summary>
    private void Telegraph(int tick)
    {
        foreach (var strike in _pending.ToList())
        {
            if (!strike.dev && !SourceHolds(strike))
            {
                _pending.Remove(strike);
                Messages.Message("Rim Chronicles: the orbital strike was called off: its herald is down or lost sight.",
                    new TargetInfo(strike.cell, map), MessageTypeDefOf.PositiveEvent, historical: false);
                continue;
            }

            if (tick >= strike.fireTick)
            {
                _pending.Remove(strike);
                Land(strike);
                continue;
            }

            FleckMaker.Static(strike.cell, map, FleckDefOf.PsycastAreaEffect, 8f);
        }
    }

    private bool SourceHolds(PendingStrike strike) =>
        (strike.herald is { } h && Up(h) && h.Map == map && Sees(h, strike.cell))
        || (EmpireDriver.EmpireFaction is { } empire && UplinkStanding(empire));

    private void Land(PendingStrike strike)
    {
        if (!strike.cell.InBounds(map))
        {
            return;
        }

        if (strike.kind == StrikeKind.Beam)
        {
            var beam = (PowerBeam)GenSpawn.Spawn(ThingDefOf.PowerBeam, strike.cell, map);
            beam.duration = 600; // as Verb_PowerBeam
            beam.instigator = strike.herald;
            beam.StartStrike();
            return;
        }

        // As RoyalTitlePermitWorker_OrbitalStrike.CallBombardment, with the permits' numbers (CallOrbitalStrike, CallOrbitalSalvo).
        var salvo = strike.kind == StrikeKind.Salvo;
        var bombardment = (Bombardment)GenSpawn.Spawn(ThingDefOf.Bombardment, strike.cell, map);
        bombardment.impactAreaRadius = salvo ? 7.9f : 2.9f;
        bombardment.explosionRadiusRange = salvo ? new FloatRange(5.9f, 5.9f) : new FloatRange(7.9f, 7.9f);
        bombardment.bombIntervalTicks = 60;
        bombardment.randomFireRadius = 1;
        bombardment.explosionCount = salvo ? 6 : 1;
        bombardment.warmupTicks = 60;
        bombardment.instigator = strike.herald;
    }

    private bool RaidOnMap(Faction empire) =>
        map.mapPawns.SpawnedPawnsInFaction(empire).Any(p => !p.Downed && UnitKit.Unit(p.kindDef) is not null);

    private List<Pawn> Heralds(Faction empire) =>
        map.mapPawns.SpawnedPawnsInFaction(empire).Where(p => UnitKit.Unit(p.kindDef)?.role == UnitRole.Herald && Up(p)).ToList();

    /// <summary>Alive, spawned, conscious and standing.</summary>
    private static bool Up(Pawn pawn) => pawn is { Dead: false, Spawned: true, Downed: false } && pawn.Awake() && !pawn.InMentalState;

    private bool Sees(Pawn herald, IntVec3 cell) =>
        herald.Position.DistanceTo(cell) <= HeraldSight && GenSight.LineOfSight(herald.Position, cell, map);

    private bool UplinkStanding(Faction empire) =>
        Uplinks.Count > 0 && map.listerBuildings.allBuildingsNonColonist.Any(b => b.Faction == empire && Uplinks.Contains(b.def) && !b.Destroyed);

    private static bool StrikesDown() =>
        RimChroniclesGameComponent.Current is { } c && c.Crisis.IsDown(CrisisCapability.Strikes, GenDate.DaysPassed);

    private static float RaidBudget()
    {
        if (RimChroniclesGameComponent.Current is not { } component || EmpireDriver.EmpireFaction is not { } empire)
        {
            return ThreatBudget.Base(CrisisStage.Probing);
        }

        var stage = CrisisDriver.Stage(component, empire).Stage;
        return CrisisDriver.Budget(component, stage == CrisisStage.None ? CrisisStage.Probing : stage, ColonyStrengthReader.Read());
    }

    /// <summary>Turrets, power buildings and clustered colonists, with who's under each blast and whether a source sees it.</summary>
    public List<StrikeCandidate> Candidates(Faction empire, IReadOnlyList<Pawn> heralds, bool uplink)
    {
        var allies = map.mapPawns.SpawnedPawnsInFaction(empire).Where(p => !p.Downed).Select(p => p.Position).ToList();
        var colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed).Select(p => p.Position).ToList();
        var candidates = new List<StrikeCandidate>();

        StrikeCandidate Make(IntVec3 cell, StrikeTargetKind kind, float value) => new()
        {
            Cell = new Cell(cell.x, cell.z),
            Kind = kind,
            Value = value,
            Colonists = colonists.Count(c => c.DistanceTo(cell) <= StrikeRules.BlastRadius),
            Allies = allies.Count(a => a.DistanceTo(cell) <= StrikeRules.BlastRadius),
            AlliesNearBeam = allies.Count(a => a.DistanceTo(cell) <= StrikeRules.BeamRadius),
            Sighted = uplink || heralds.Any(h => Sees(h, cell)),
            ThickRoof = cell.GetRoof(map)?.isThickRoof == true,
        };

        foreach (var building in map.listerBuildings.allBuildingsColonist)
        {
            if (building is Building_Turret turret && !turret.IsBrokenDown())
            {
                candidates.Add(Make(building.Position, StrikeTargetKind.Turret, building.def.size.x * building.def.size.z > 1 ? 1.5f : 1f));
            }
            else if (building.TryGetComp<CompPowerPlant>() is not null || building.TryGetComp<CompPowerBattery>() is not null)
            {
                candidates.Add(Make(building.Position, StrikeTargetKind.Power, 1f));
            }
        }

        candidates.AddRange(colonists.Select(c => Make(c, StrikeTargetKind.Cluster, 1f)));
        return candidates;
    }
}
