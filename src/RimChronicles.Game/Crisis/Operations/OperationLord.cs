using RimChronicles.Core.Crisis;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Captives;
using RimChronicles.Game.Crisis.Psykers;
using RimChronicles.Game.Crisis.Siege;
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

    /// <summary>A soldier this close to a held captive frees them (Retrieval).</summary>
    public const float FreeReach = 1.9f;

    public string operation = "";
    public CrisisStage stage;
    public float reserve;
    public IntVec3 staging;
    public Pawn? target;

    /// <summary>Fired from Dev Mode: it doesn't pull out when there's no crisis.</summary>
    public bool dev;

    /// <summary>The siege camp, for a siege (C8); null for other operations.</summary>
    public SiegeCamp? siege;

    /// <summary>The Choir's cluster siege camp (War in Heaven W8); null for other operations.</summary>
    public Choir.ClusterCamp? cluster;

    /// <summary>Whose operation (War in Heaven W7): the Empire's (<c>empire</c>, the default, so older saves load as
    /// before) or the Hollow Choir's (<c>choir</c>).</summary>
    public string power = Power.Empire.Id;

    /// <summary>The Choir's chain of command: <c>commanded[i]</c> is a mech that <c>commandedBy[i]</c> commands. When a
    /// commander is killed or downed, its mechs shut down (tech spec R4).</summary>
    public List<Pawn?> commanded = [];

    public List<Pawn?> commandedBy = [];

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

    public bool IsChoir => power == Power.Choir.Id;

    /// <summary>The crisis this operation belongs to.</summary>
    private static CrisisState? StateFor(string power) =>
        RimChroniclesGameComponent.Current is { } c ? (power == Power.Choir.Id ? c.ChoirCrisis : c.Crisis) : null;

    /// <summary>Records that <paramref name="commander"/> commands <paramref name="mech"/>.</summary>
    public void Command(Pawn mech, Pawn commander)
    {
        commanded.Add(mech);
        commandedBy.Add(commander);
    }

    /// <summary>The mechs <paramref name="commander"/> commands.</summary>
    public IEnumerable<Pawn> MechsOf(Pawn commander)
    {
        for (var i = 0; i < commanded.Count; i++)
        {
            if (commandedBy[i] == commander && commanded[i] is { } mech)
            {
                yield return mech;
            }
        }
    }

    /// <summary>A Choir commander was killed or downed: each mech it commanded that's still in the fight shuts down
    /// where it stands (<c>CompMechanoid.Deactivate</c>: it never goes feral, and any colonist can dismantle it, W6).</summary>
    public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
    {
        base.Notify_PawnLost(p, condition);
        if (!IsChoir || condition is not (PawnLostCondition.Killed or PawnLostCondition.Incapped or PawnLostCondition.MadePrisoner))
        {
            return;
        }

        var map = Map;
        var silent = MechsOf(p).Where(m => !m.Dead && m.Spawned && lord.ownedPawns.Contains(m)).ToList();
        for (var i = commanded.Count - 1; i >= 0; i--)
        {
            if (commandedBy[i] == p)
            {
                commanded.RemoveAt(i);
                commandedBy.RemoveAt(i);
            }
        }

        foreach (var mech in silent)
        {
            mech.TryGetComp<CompMechanoid>()?.Deactivate(); // vanilla's ToSleep tells the lord the mech is lost (Incapped)
        }

        if (silent.Count > 0 && map is not null)
        {
            Messages.Message($"With {p.LabelShort} down, {silent.Count} of the Choir's mechs fall silent. Any colonist can dismantle them.",
                new LookTargets(silent), MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>No vanilla panic flee: the profile's loss links are the only retreat, so it isn't cut short at a random
    /// 40–70% of losses and always ends in <see cref="LordToil_RcRetreat"/>, which carries off the downed.</summary>
    public override bool AddFleeToil => false;

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
                PhaseKind.Breach => new LordToil_AssaultColonyBreaching(),
                PhaseKind.Travel => new LordToil_Travel(siege?.spot ?? cluster?.spot ?? staging),
                PhaseKind.Siege => cluster is not null ? new Choir.LordToil_RcClusterSiege() : new LordToil_RcSiege(),
                PhaseKind.Retrieve => new LordToil_RcRetrieve(),
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
        PhaseTrigger.TargetDown => new Trigger_TickCondition(() => target is null || target.Dead || target.Downed || !target.Spawned || target.Map != Map
            || (lord.faction is { } own && target.Faction == own), 60), // a freed captive is the Empire's again
        PhaseTrigger.Arrived => new Trigger_Memo("TravelArrived"),
        PhaseTrigger.BuildersLost => new Trigger_TickCondition(() => (siege is not null && siege.AfterBuilders(lord) == AfterBuilders.Assault)
            || (cluster is not null && cluster.AfterMechanitors(lord) == AfterBuilders.Assault), 250),
        _ => new Trigger_TickCondition(() => !dev && Abandoned(), 250),
    };

    /// <summary>The crisis is off or over, or the colony made peace with the power.</summary>
    private bool Abandoned() => IsChoir ? Choir.ChoirCrisisDriver.Abandoned() : EmpireAbandoned();

    private static bool EmpireAbandoned() =>
        !CrisisDriver.Available || EmpireDriver.EmpireFaction is not { } empire || !empire.HostileTo(Faction.OfPlayer)
        || RimChroniclesGameComponent.Current is not { } c || CrisisDriver.Stage(c, empire).Stage == CrisisStage.None;

    private void OnLink(Phase from, PhaseLink link)
    {
        if (from.Kind == PhaseKind.Probe)
        {
            ReportEntrances();
        }

        var to = Profile.Find(link.To)?.Kind;
        if (from.Kind == PhaseKind.Regroup && link.Outcome == OperationOutcome.Running)
        {
            SendReserve(); // the regroup is over: back in, with the reserve
        }
        else if (to == PhaseKind.Retreat)
        {
            End(link.Outcome);
        }

        if (from.Kind == PhaseKind.Siege && to == PhaseKind.Assault && Map is not null && cluster is not null)
        {
            Messages.Message(link.Trigger == PhaseTrigger.BuildersLost
                    ? "The Choir's cluster has lost most of its mechanitors, but it still has the strength to attack: it's coming in."
                    : "The Choir's cluster has grown all it will: it's coming in.",
                new TargetInfo(cluster.spot, Map), MessageTypeDefOf.ThreatBig);
        }

        if (from.Kind == PhaseKind.Siege && to == PhaseKind.Assault && Map is not null && siege is not null)
        {
            Messages.Message(link.Trigger == PhaseTrigger.BuildersLost
                    ? "The Imperial siege has lost most of its builders, but it still has the strength to attack: it's coming in."
                    : "The Imperial siege is done digging in: it's coming in.",
                new TargetInfo(siege.spot, Map), MessageTypeDefOf.ThreatBig);
        }
    }

    /// <summary>The camp's relay and beacon work while the siege lasts, including its assault, but not in the retreat.</summary>
    public override void LordJobTick()
    {
        base.LordJobTick();
        if (operation == CrisisOperations.Retrieval && Find.TickManager.TicksGame % 60 == 0)
        {
            TryFree();
        }

        if (siege is not null && Find.TickManager.TicksGame % 250 == 0 && lord.CurLordToil is not LordToil_RcRetreat)
        {
            siege.JobTick(lord, this);
        }

        if (cluster is not null && Find.TickManager.TicksGame % 250 == 0 && lord.CurLordToil is not LordToil_RcRetreat)
        {
            cluster.JobTick(lord, this, growing: lord.CurLordToil is Choir.LordToil_RcClusterSiege);
        }
    }

    private void End(string outcome)
    {
        if (_ended || outcome == OperationOutcome.Running)
        {
            return;
        }

        _ended = true;
        if (siege is not null && lord?.Map is not null)
        {
            siege.Teardown(lord);
        }

        if (cluster is not null && lord?.Map is not null)
        {
            cluster.Teardown(lord);
        }

        if (StateFor(power) is { Running: { } running } state && running.Operation == operation)
        {
            state.Ended(outcome);
        }

        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Crisis {operation}: {outcome}.");
        }
    }

    /// <summary>Retrieval: a standing soldier beside the held captive frees them; they join the squad and leave with it.</summary>
    private void TryFree()
    {
        if (target is not { IsPrisonerOfColony: true, Dead: false, Spawned: true } captive || lord.faction is not { } faction
            || !lord.ownedPawns.Any(p => p.Spawned && !p.Downed && p.Map == captive.Map && p.Position.DistanceTo(captive.Position) <= FreeReach))
        {
            return;
        }

        captive.guest.SetGuestStatus(null);
        if (captive.Faction != faction)
        {
            captive.SetFaction(faction);
        }

        lord.AddPawn(captive);
        Messages.Message($"The {(IsChoir ? "Choir's" : "Imperial")} strike force has freed {captive.LabelShort}.", captive, MessageTypeDefOf.NegativeEvent);
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
        if (StateFor(power) is not { } state || Map is null)
        {
            return;
        }

        var doors = Map.listerBuildings.allBuildingsColonist.OfType<Building_Door>()
            .OrderBy(d => d.Position.DistanceTo(staging))
            .Select(d => (d.Position.x, d.Position.z))
            .ToList();
        if (doors.Count > 0)
        {
            state.Learned(doors, GenDate.DaysPassed);
        }
    }

    private void SendReserve()
    {
        if (_reserveSent || reserve <= 0 || Map is null || lord.faction is not { } faction)
        {
            return;
        }

        _reserveSent = true;
        if (IsChoir)
        {
            Choir.ChoirOperations.Reinforce(lord, this, reserve, staging);
            return;
        }

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
        if (target is { Dead: false, Downed: false, Spawned: true, IsPrisonerOfColony: false } hunted && hunted.Map == Map && hunted.Faction != lord.faction)
        {
            return _focus = hunted; // never a captive they came to free
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
        Scribe_Deep.Look(ref siege, "siege");
        Scribe_Deep.Look(ref cluster, "cluster");
        Scribe_Values.Look(ref power, "power", Power.Empire.Id);
        Scribe_Collections.Look(ref commanded, "commanded", LookMode.Reference);
        Scribe_Collections.Look(ref commandedBy, "commandedBy", LookMode.Reference);
        operation ??= "";
        power ??= Power.Empire.Id;
        commanded ??= [];
        commandedBy ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            for (var i = Math.Min(commanded.Count, commandedBy.Count) - 1; i >= 0; i--)
            {
                if (commanded[i] is null || commandedBy[i] is null)
                {
                    commanded.RemoveAt(i);
                    commandedBy.RemoveAt(i);
                }
            }

            if (commanded.Count != commandedBy.Count)
            {
                commanded.Clear();
                commandedBy.Clear();
            }
        }
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
        if (lord.LordJob is LordJob_RcOperation { IsChoir: true } choir)
        {
            Choir.ChoirCommand.UpdateDuties(lord, choir, null);
            return;
        }

        var praetorians = lord.ownedPawns.Where(p => UnitKit.Unit(p.kindDef)?.role == UnitRole.Praetorian && !p.Downed).ToList();
        var next = 0;
        foreach (var pawn in lord.ownedPawns)
        {
            if (pawn.mindState is null)
            {
                continue;
            }

            var role = UnitKit.Unit(pawn.kindDef)?.role;
            if (role is UnitRole.Adept or UnitRole.Herald or UnitRole.Stellarch && praetorians.Count > 0)
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

    /// <summary>A guard went down: its psykers and Heralds pick a new one. The Choir's commanders move every 250 ticks.</summary>
    public override void LordToilTick()
    {
        if (lord.LordJob is LordJob_RcOperation { IsChoir: true })
        {
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                UpdateAllDuties();
            }

            return;
        }

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
            && p.CurJobDef != JobDefOf.CarryDownedPawnToExit && p.CurJobDef != JobDefOf.Kidnap).ToList();
        Kidnap(map, carriers);
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

    /// <summary>Retrieval: a downed defector it came for is carried off the map, kidnapped as vanilla raiders kidnap.</summary>
    private void Kidnap(Map map, List<Pawn> carriers)
    {
        if (lord.LordJob is not LordJob_RcOperation { operation: CrisisOperations.Retrieval, target: { Downed: true, Dead: false, Spawned: true } defector }
            || defector.Map != map || defector.Faction != Faction.OfPlayer || carriers.Count == 0
            || lord.ownedPawns.Any(p => p.CurJobDef == JobDefOf.Kidnap))
        {
            return;
        }

        var carrier = carriers.Where(c => c.CanReserveAndReach(defector, PathEndMode.OnCell, Danger.Deadly)).OrderBy(c => c.Position.DistanceTo(defector.Position)).FirstOrDefault();
        if (carrier is null || !RCellFinder.TryFindBestExitSpot(carrier, out var exit))
        {
            return;
        }

        var job = JobMaker.MakeJob(JobDefOf.Kidnap, defector, exit);
        job.count = 1;
        carrier.jobs.StartJob(job, JobCondition.InterruptForced);
        carriers.Remove(carrier);
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

    /// <summary>A Choir mechanitor's duty (W7): hold and shoot from the spot the commander AI picks.</summary>
    public static DutyDef RimChronicles_Command = null!;

    static CrisisDefs() => DefOfHelper.EnsureInitializedInCtor(typeof(CrisisDefs));
}
