using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Game.Crisis.Siege;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Operations;

/// <summary>
/// Spawns crisis operations (crisis C6): the force from the operation's mix and budget, how it arrives, its lord and the
/// letter. Registers each built operation with <see cref="CrisisDriver"/>, so the crisis arc offers it. Main thread only.
/// </summary>
public static class OperationSpawner
{
    public static void Register()
    {
        CrisisDriver.RegisterOperation(CrisisOperations.ProbingRaid, (map, op, stage, budget) => ProbingRaid(map, stage, budget, dev: false));
        CrisisDriver.RegisterOperation(CrisisOperations.Decapitation, (map, op, stage, budget) => Decapitation(map, stage, budget, dev: false));
        CrisisDriver.RegisterOperation(CrisisOperations.Breach, (map, op, stage, budget) => Assault(map, op.Step, stage, budget, dev: false));
        CrisisDriver.RegisterOperation(CrisisOperations.PsychicAssault, (map, op, stage, budget) => Assault(map, op.Step, stage, budget, dev: false));
        CrisisDriver.RegisterOperation(CrisisOperations.SiegeWithBombardment, (map, op, stage, budget) => Siege(map, stage, budget, null, dev: false));
    }

    /// <summary>Units for <paramref name="points"/> of an operation's mix at a stage, kitted out, not spawned.</summary>
    public static List<Pawn> Force(string operation, CrisisStage stage, float points, Faction faction)
    {
        var kinds = UnitKit.Kinds.ToList();
        PawnKindDef? KindFor(MixEntry e) => kinds.FirstOrDefault(k => UnitKit.Unit(k) is { } u && u.role == e.Role && u.specialty == e.Specialty);
        var mix = ForceMix.For(operation).Where(e => KindFor(e) is not null).ToList();
        return ForceMix.Build(mix, stage, points, e => KindFor(e)!.combatPower)
            .Select(e => UnitKit.Generate(KindFor(e)!, faction, stage))
            .ToList();
    }

    /// <summary>A small force walks in from the edge, tests the defenses for about two hours, and pulls back to report.</summary>
    public static bool ProbingRaid(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (EmpireDriver.EmpireFaction is not { } empire
            || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var profile = OperationProfiles.ProbingRaid;
        var pawns = Force(profile.Operation, stage, budget * profile.ForceShare, empire);
        if (pawns.Count == 0)
        {
            return false;
        }

        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
        }

        LordMaker.MakeNewLord(empire, new LordJob_RcOperation(profile.Operation, stage, budget * profile.ReserveShare, entry, null, dev), map, pawns);
        Find.LetterStack.ReceiveLetter("Imperial probe",
            $"A small Imperial force of {pawns.Count} is testing {Faction.OfPlayer.Name}'s defenses. It won't stay long: it means to "
            + "learn the colony's layout and report back, and the raids that follow will know where the doors are.",
            LetterDefOf.ThreatBig, new LookTargets(pawns), empire);
        return true;
    }

    /// <summary>A standard operation (breach, psychic assault): its share of the budget walks in from the edge and
    /// attacks; after heavy losses it regroups at its entry point and comes back once with the reserve.</summary>
    public static bool Assault(Map map, string operation, CrisisStage stage, float budget, bool dev)
    {
        if (EmpireDriver.EmpireFaction is not { } empire
            || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        var profile = OperationProfiles.For(operation);
        var pawns = Force(operation, stage, budget * profile.ForceShare, empire);
        if (pawns.Count == 0)
        {
            return false;
        }

        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 8), map);
        }

        LordMaker.MakeNewLord(empire, new LordJob_RcOperation(operation, stage, budget * profile.ReserveShare, entry, null, dev), map, pawns);
        var (label, text) = operation switch
        {
            CrisisOperations.Breach => ("Imperial breach",
                $"{pawns.Count} Imperial troops are coming for {Faction.OfPlayer.Name}, led by breachers: frag grenades for the walls, EMP for the turrets. "
                + "The rest will follow them through whatever they open."),
            CrisisOperations.PsychicAssault => ("Imperial psychic assault",
                $"{pawns.Count} Imperial troops are coming for {Faction.OfPlayer.Name}, and many of them are psykers, kept behind a Praetorian escort. "
                + "Spread out against their pulses, and go for the psykers when they run hot."),
            _ => ("Imperial assault", $"{pawns.Count} Imperial troops are attacking {Faction.OfPlayer.Name}."),
        };
        Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, new LookTargets(pawns), empire);
        return true;
    }

    /// <summary>
    /// A siege (crisis C8): the force walks in from the map edge nearest the best camp spot (<see cref="SiegeSpotSampler"/>),
    /// digs in there and builds its camp in stages, then attacks. Its goal (<paramref name="goal"/>, else a weighted
    /// pick until C11) sets what its strikes aim at. The reserve comes in through the camp's beacon.
    /// </summary>
    public static bool Siege(Map map, CrisisStage stage, float budget, SiegeGoal? goal, bool dev)
    {
        if (EmpireDriver.EmpireFaction is not { } empire || SiegeSpotSampler.Best(map) is not { } best)
        {
            return false;
        }

        var spot = best.Spot;
        var edge = CellRect.WholeMap(map).EdgeCells.Where(c => c.Standable(map)).Select(c => c.DistanceTo(spot)).DefaultIfEmpty(0).Min();
        if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && c.DistanceTo(spot) <= edge + 25
                && map.reachability.CanReach(c, spot, PathEndMode.OnCell, TraverseMode.PassDoors), map, CellFinder.EdgeRoadChance_Ignore, out var entry))
        {
            return false;
        }

        var profile = OperationProfiles.Siege;
        var pawns = Force(profile.Operation, stage, budget * profile.ForceShare, empire);
        if (pawns.Count == 0)
        {
            return false;
        }

        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 8), map);
        }

        var picked = goal ?? PickGoal(map);
        var facing = SpotScorer.Facing(best.Score.Spot.Cell, new Cell(best.Centre.x, best.Centre.z));
        var job = new LordJob_RcOperation(profile.Operation, stage, budget * profile.ReserveShare, entry, null, dev)
        {
            siege = new SiegeCamp(spot, facing, picked),
        };
        LordMaker.MakeNewLord(empire, job, map, pawns);
        var aim = picked switch
        {
            SiegeGoal.BreakWalls => "Its strikes will go for the walls and turrets, to open the way in.",
            SiegeGoal.Starve => "Its strikes will go for the fields, the food stores and the power.",
            SiegeGoal.KillLeader => "Its strikes will go for the colony's leader whenever they're in the open.",
            _ => "",
        };
        Find.LetterStack.ReceiveLetter("Imperial siege",
            $"{pawns.Count} Imperial troops are marching on {Faction.OfPlayer.Name} to lay siege. They'll dig in at a camp "
            + "out of the turrets' reach and build it up in stages: sandbags and tents, then mortar pits, an orbital uplink, a shield and "
            + $"psychic relay, and last a drop-pod beacon for reinforcements. {aim}\n\nStrike the camp early, while it's weak, "
            + "or face more of it. Kill its builders and the siege must attack or give up.",
            LetterDefOf.ThreatBig, new LookTargets(new TargetInfo(spot, map)), empire);
        return true;
    }

    /// <summary>A weighted pick among the goals the colony gives a target for (the director picks it from C11).</summary>
    public static SiegeGoal PickGoal(Map map)
    {
        var targets = new SiegeTargets(
            HasWalls: map.listerBuildings.allBuildingsColonist.Any(b => b.def.IsWall),
            HasFood: map.zoneManager.AllZones.OfType<Zone_Growing>().Any() || map.listerBuildings.allBuildingsColonist.Any(b => b is Building_Storage),
            HasLeader: Target(map) is not null);
        return SiegeGoals.Pick(SiegeGoals.Allowed(targets), Rand.Value);
    }

    /// <summary>Drop pods land near the colony's leader (or a plot's protagonist), on open sky only, and the squad goes
    /// for them. It pulls out once they're down, or after heavy losses.</summary>
    public static bool Decapitation(Map map, CrisisStage stage, float budget, bool dev)
    {
        if (EmpireDriver.EmpireFaction is not { } empire || Target(map) is not { } target || DropSpot(map, target.Position) is not { } spot)
        {
            return false;
        }

        var profile = OperationProfiles.Decapitation;
        var pawns = Force(profile.Operation, stage, budget * profile.ForceShare, empire);
        if (pawns.Count == 0)
        {
            return false;
        }

        LordMaker.MakeNewLord(empire, new LordJob_RcOperation(profile.Operation, stage, 0, spot, target, dev), map, pawns);
        DropPodUtility.DropThingsNear(spot, map, pawns, canRoofPunch: false, forbid: false, allowFogged: false, faction: empire);
        Find.LetterStack.ReceiveLetter("Imperial drop pods",
            $"Imperial drop pods are falling near {target.LabelShort}. {pawns.Count} soldiers and psykers are coming for them, "
            + $"and they'll pull out once {target.LabelShort} is down.\n\nGet {target.LabelShort} under a roof or behind the guns.",
            LetterDefOf.ThreatBig, new LookTargets(target), empire);
        return true;
    }

    /// <summary>The colony's leader as the Empire sees it: the ideoligion's leader, then a plot's protagonist, then the
    /// colonist with the highest Imperial title, then the most valuable colonist.</summary>
    public static Pawn? Target(Map map)
    {
        var protagonists = RimChroniclesGameComponent.Current?.Plots.Active.Select(p => p.Protagonist.PawnId).ToHashSet() ?? [];
        return map.mapPawns.FreeColonistsSpawned
            .Where(p => !p.Dead)
            .OrderByDescending(p => ModsConfig.IdeologyActive && p.Ideo?.GetRole(p)?.def == PreceptDefOf.IdeoRole_Leader)
            .ThenByDescending(p => protagonists.Contains(p.ThingID))
            .ThenByDescending(p => p.royalty?.MostSeniorTitle?.def.seniority ?? -1)
            .ThenByDescending(p => p.MarketValue)
            .FirstOrDefault();
    }

    /// <summary>An open-sky cell 3–12 cells from the target (fairness rule 3: never under a roof, so never in a room
    /// under overhead mountain).</summary>
    public static IntVec3? DropSpot(Map map, IntVec3 near)
    {
        var cells = GenRadial.RadialCellsAround(near, 12f, useCenter: false)
            .Where(c => c.InBounds(map))
            .Select(c => new DropCell(new Cell(c.x, c.z),
                c.GetRoof(map) is not { } roof ? RoofKind.None : roof.isThickRoof ? RoofKind.Thick : RoofKind.Constructed,
                c.Standable(map) && DropCellFinder.CanPhysicallyDropInto(c, map, canRoofPunch: false), c.Fogged(map)));
        return Tactics.DropSpot(new Cell(near.x, near.z), cells) is { } spot ? new IntVec3(spot.X, 0, spot.Z) : null;
    }

    /// <summary>Fires a siege with a chosen goal (the plain fire action picks one by weight).</summary>
    [DebugAction("Rim Chronicles", "Crisis: fire siege with goal", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> FireSiege() => SiegeGoals.All.Select(goal =>
    {
        var node = new DebugActionNode(goal.Id);
        foreach (var stage in Stages)
        {
            node.AddChild(new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.Action, () =>
            {
                if (RimChroniclesGameComponent.Current is not { } component || Find.CurrentMap is not { } map)
                {
                    return;
                }

                var budget = CrisisDriver.Budget(component, stage, ColonyStrengthReader.Read());
                if (!Siege(map, stage, budget, goal.Goal, dev: true))
                {
                    Messages.Message("Rim Chronicles: no siege camp spot on this map (Crisis: show siege scoring says why).",
                        MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                component.Crisis.Started(CrisisOperations.SiegeWithBombardment, stage, budget, GenDate.DaysPassed);
            }));
        }

        return node;
    }).ToList();

    private static readonly CrisisStage[] Stages = [CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale];

    /// <summary>Fires a built operation on the current map at a stage's budget (crisis spec §9, "RC: Fire operation").
    /// It's recorded like a real one, but doesn't pull out when there's no crisis.</summary>
    [DebugAction("Rim Chronicles", "Crisis: fire operation", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Fire()
    {
        var operations = new (string Step, Func<Map, CrisisStage, float, bool, bool> Run)[]
        {
            (CrisisOperations.ProbingRaid, ProbingRaid),
            (CrisisOperations.Decapitation, Decapitation),
            (CrisisOperations.Breach, (m, s, b, d) => Assault(m, CrisisOperations.Breach, s, b, d)),
            (CrisisOperations.PsychicAssault, (m, s, b, d) => Assault(m, CrisisOperations.PsychicAssault, s, b, d)),
            (CrisisOperations.SiegeWithBombardment, (m, s, b, d) => Siege(m, s, b, null, d)),
        };
        var nodes = new List<DebugActionNode>();
        foreach (var (step, run) in operations)
        {
            var node = new DebugActionNode(step);
            foreach (var stage in Stages)
            {
                node.AddChild(new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.Action, () =>
                {
                    if (RimChroniclesGameComponent.Current is not { } component || Find.CurrentMap is not { } map)
                    {
                        return;
                    }

                    var budget = CrisisDriver.Budget(component, stage, ColonyStrengthReader.Read());
                    if (!run(map, stage, budget, true))
                    {
                        Messages.Message($"Rim Chronicles: {step} couldn't start here (no Empire, entry cell, target, open-sky drop spot or siege camp spot).",
                            MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    component.Crisis.Started(step, stage, budget, GenDate.DaysPassed);
                }));
            }

            nodes.Add(node);
        }

        return nodes;
    }
}
