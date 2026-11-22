using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Psykers;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Operations;

/// <summary>
/// A crisis operation's lord (crisis C6): its state graph is built from Core's <see cref="OperationProfile"/>, so the
/// phases and the retreat rules tested there are the ones that run. It keeps the squad's focus target, sends in the
/// reserve after a regroup, has a probe report the colony's entrances, and records how the operation ended.
/// </summary>
public sealed class LordJob_RcOperation : LordJob
{
    private const int FocusTicks = 250;

    public string operation = "";
    public CrisisStage stage;
    public float reserve;
    public IntVec3 staging;
    public Pawn? target;

    /// <summary>Fired from Dev Mode: it doesn't pull out when there's no crisis.</summary>
    public bool dev;

    private bool _ended;
    private bool _reserveSent;
    private int _focusTick = -1;
    private Pawn? _focus;

    public LordJob_RcOperation()
    {
    }

    public LordJob_RcOperation(string operation, CrisisStage stage, float reserve, IntVec3 staging, Pawn? target, bool dev)
    {
        this.operation = operation;
        this.stage = stage;
        this.reserve = reserve;
        this.staging = staging;
        this.target = target;
        this.dev = dev;
    }

    public override bool GuiltyOnDowned => true;

    public OperationProfile Profile => OperationProfiles.For(operation);

    public override StateGraph CreateGraph()
    {
        var graph = new StateGraph();
        var profile = Profile;
        var toils = new Dictionary<string, LordToil>();
        foreach (var phase in profile.Phases)
        {
            LordToil toil = phase.Kind switch
            {
                PhaseKind.Regroup => new LordToil_DefendPoint(staging, 8f, 6f),
                PhaseKind.Retreat => new LordToil_RcRetreat(),
                _ => new LordToil_RcAssault(),
            };
            toils[phase.Name] = toil;
            graph.AddToil(toil);
        }

        graph.StartingToil = toils[profile.Start];
        foreach (var phase in profile.Phases)
        {
            foreach (var link in phase.Links)
            {
                if (!toils.TryGetValue(link.To, out var to))
                {
                    continue;
                }

                var transition = new Transition(toils[phase.Name], to);
                transition.AddTrigger(TriggerFor(link));
                transition.AddPreAction(new TransitionAction_Custom(() => OnLink(phase, link)));
                graph.AddTransition(transition);
            }
        }

        return graph;
    }

    private Trigger TriggerFor(PhaseLink link) => link.Trigger switch
    {
        PhaseTrigger.LossesOver => new Trigger_FractionPawnsLost(link.Value),
        PhaseTrigger.TimeUp => new Trigger_TicksPassed((int)link.Value),
        PhaseTrigger.TargetDown => new Trigger_TickCondition(() => target is null || target.Dead || target.Downed || !target.Spawned || target.Map != Map, 60),
        _ => new Trigger_TickCondition(() => !dev && Abandoned(), 250),
    };

    /// <summary>The crisis is off or over, or the colony made peace with the Empire.</summary>
    private static bool Abandoned() =>
        !CrisisDriver.Available || EmpireDriver.EmpireFaction is not { } empire || !empire.HostileTo(Faction.OfPlayer)
        || RimChroniclesGameComponent.Current is not { } c || CrisisDriver.Stage(c, empire).Stage == CrisisStage.None;

    private void OnLink(Phase from, PhaseLink link)
    {
        if (from.Kind == PhaseKind.Probe)
        {
            ReportEntrances();
        }

        if (from.Kind == PhaseKind.Regroup && link.Outcome == OperationOutcome.Running)
        {
            SendReserve(); // the regroup is over: back in, with the reserve
        }
        else if (Profile.Find(link.To)?.Kind == PhaseKind.Retreat)
        {
            End(link.Outcome);
        }
    }

    private void End(string outcome)
    {
        if (_ended || outcome == OperationOutcome.Running)
        {
            return;
        }

        _ended = true;
        if (RimChroniclesGameComponent.Current is { } component && component.Crisis.Running is { } running && running.Operation == operation)
        {
            component.Crisis.Ended(outcome);
        }

        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Crisis {operation}: {outcome}.");
        }
    }

    /// <summary>The lord is gone (everyone dead, downed or left): an operation that never ended otherwise was beaten.</summary>
    public override void Cleanup()
    {
        base.Cleanup();
        End(OperationOutcome.Repelled);
    }

    /// <summary>A probe found the colony's doors: the ones nearest where it came in are what later raids aim for.</summary>
    private void ReportEntrances()
    {
        if (RimChroniclesGameComponent.Current is not { } component || Map is null)
        {
            return;
        }

        var doors = Map.listerBuildings.allBuildingsColonist.OfType<Building_Door>()
            .OrderBy(d => d.Position.DistanceTo(staging))
            .Select(d => (d.Position.x, d.Position.z))
            .ToList();
        if (doors.Count > 0)
        {
            component.Crisis.Learned(doors, GenDate.DaysPassed);
        }
    }

    private void SendReserve()
    {
        if (_reserveSent || reserve <= 0 || Map is null || lord.faction is not { } faction)
        {
            return;
        }

        _reserveSent = true;
        var pawns = OperationSpawner.Force(operation, stage, reserve, faction);
        if (pawns.Count == 0)
        {
            return;
        }

        lord.AddPawns(pawns);
        DropPodUtility.DropThingsNear(staging, Map, pawns, canRoofPunch: false, forbid: false, allowFogged: false, faction: faction);
        Messages.Message($"Imperial reinforcements are dropping in: {pawns.Count} more.", new TargetInfo(staging, Map), MessageTypeDefOf.ThreatBig);
    }

    /// <summary>The squad's shared target: the hunted pawn while it's up and on the map, else the colonist easiest to
    /// bring down (medics first) near the squad. Worked out at most every 250 ticks.</summary>
    public Pawn? Focus()
    {
        var now = Find.TickManager.TicksGame;
        if (now - _focusTick < FocusTicks && (_focus is null || (!_focus.Dead && !_focus.Downed && _focus.Spawned)))
        {
            return _focus;
        }

        _focusTick = now;
        _focus = null;
        if (target is { Dead: false, Downed: false, Spawned: true } hunted && hunted.Map == Map)
        {
            return _focus = hunted;
        }

        if (Map is null || lord.faction is not { } faction || lord.ownedPawns.Count == 0)
        {
            return null;
        }

        var (view, things) = BattleViewBuilder.For(Map, faction);
        var squad = lord.ownedPawns.Where(p => p.Spawned).Select(p => p.Position).ToList();
        if (squad.Count == 0)
        {
            return null;
        }

        var centre = new Core.Crisis.Pieces.Cell((int)squad.Average(c => c.x), (int)squad.Average(c => c.z));
        _focus = Tactics.FocusTarget(view, BattleSide.Empire, centre) is { } unit && things.TryGetValue(unit.Id, out var thing) ? thing as Pawn : null;
        return _focus;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref operation, "operation", "");
        Scribe_Values.Look(ref stage, "stage");
        Scribe_Values.Look(ref reserve, "reserve");
        Scribe_Values.Look(ref staging, "staging");
        Scribe_References.Look(ref target, "target");
        Scribe_Values.Look(ref dev, "dev");
        Scribe_Values.Look(ref _ended, "ended");
        Scribe_Values.Look(ref _reserveSent, "reserveSent");
        operation ??= "";
    }
}

/// <summary>The crisis assault (and probe, and hunt): soldiers fight with focus fire; psykers and Heralds keep close
/// to a Praetorian, which shields them (crisis spec §5.2: "protect key units").</summary>
public sealed class LordToil_RcAssault : LordToil
{
    private const float EscortRadius = 5f;

    public override bool AllowSatisfyLongNeeds => false;

    public override void UpdateAllDuties()
    {
        var praetorians = lord.ownedPawns.Where(p => UnitKit.Unit(p.kindDef)?.role == UnitRole.Praetorian && !p.Downed).ToList();
        var next = 0;
        foreach (var pawn in lord.ownedPawns)
        {
            if (pawn.mindState is null)
            {
                continue;
            }

            var role = UnitKit.Unit(pawn.kindDef)?.role;
            if (role is UnitRole.Adept or UnitRole.Herald && praetorians.Count > 0)
            {
                var guard = praetorians[next++ % praetorians.Count];
                pawn.mindState.duty = new PawnDuty(DutyDefOf.Escort, guard, EscortRadius);
            }
            else
            {
                pawn.mindState.duty = new PawnDuty(CrisisDefs.RimChronicles_Assault);
            }
        }
    }

    /// <summary>A guard went down: its psykers and Heralds pick a new one.</summary>
    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % 250 == 0
            && lord.ownedPawns.Any(p => p.mindState?.duty?.def == DutyDefOf.Escort && p.mindState.duty.focus.Pawn is not { Downed: false, Dead: false, Spawned: true }))
        {
            UpdateAllDuties();
        }
    }
}

/// <summary>The retreat: everyone leaves the map, and downed crisis units are carried off where no colonist is close
/// (crisis spec §5.2: "carry off their downed when retreating if safe, so captures aren't free").</summary>
public sealed class LordToil_RcRetreat() : LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true)
{
    private const int CarryTicks = 120;
    private const float CarryReach = 30f;

    public override void LordToilTick()
    {
        base.LordToilTick();
        if (Find.TickManager.TicksGame % CarryTicks != 0 || lord.Map is not { } map || lord.faction is not { } faction)
        {
            return;
        }

        var (view, _) = BattleViewBuilder.For(map, faction);
        var carriers = lord.ownedPawns.Where(p => p.Spawned && !p.Downed && p.carryTracker?.CarriedThing is null
            && p.CurJobDef != JobDefOf.CarryDownedPawnToExit).ToList();
        foreach (var downed in map.mapPawns.SpawnedPawnsInFaction(faction).Where(p => p.Downed && !p.Dead && UnitKit.Unit(p.kindDef) is not null).ToList())
        {
            if (carriers.Count == 0)
            {
                return;
            }

            if (view.Find(downed.thingIDNumber) is not { } unit || !Tactics.SafeToCarry(view, unit))
            {
                continue;
            }

            var carrier = carriers.Where(c => c.Position.DistanceTo(downed.Position) <= CarryReach
                    && c.CanReserveAndReach(downed, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(c => c.Position.DistanceTo(downed.Position)).FirstOrDefault();
            if (carrier is null || !RCellFinder.TryFindBestExitSpot(carrier, out var exit))
            {
                continue;
            }

            carrier.jobs.StartJob(JobMaker.MakeJob(JobDefOf.CarryDownedPawnToExit, downed, exit), JobCondition.InterruptForced);
            carriers.Remove(carrier);
        }
    }
}

/// <summary>Fights like vanilla's raiders, but the whole squad shoots the same target when it can: the lord's focus
/// (crisis spec §5.2: focus fire).</summary>
public sealed class JobGiver_RcFocusFire : JobGiver_AIFightEnemies
{
    protected override Thing FindAttackTarget(Pawn pawn)
    {
        if (pawn.GetLord()?.LordJob is LordJob_RcOperation operation && operation.Focus() is { } focus
            && focus.Map == pawn.Map && pawn.Position.DistanceTo(focus.Position) <= targetAcquireRadius)
        {
            return focus;
        }

        return base.FindAttackTarget(pawn);
    }
}

[DefOf]
public static class CrisisDefs
{
    public static DutyDef RimChronicles_Assault = null!;

    static CrisisDefs() => DefOfHelper.EnsureInitializedInCtor(typeof(CrisisDefs));
}
