using LudeonTK;
using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>Dev Mode tools for the Hollow Choir's crisis (War in Heaven W7, rule 7).</summary>
public static class ChoirDevActions
{
    private static readonly CrisisStage[] Stages = [CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale];

    /// <summary>Fires a built Choir operation on the current map at a stage's budget. It's recorded like a real one, but
    /// doesn't pull out when there's no crisis.</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: fire operation", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Fire()
    {
        var operations = new (string Step, Func<Map, CrisisStage, float, bool, bool> Run)[]
        {
            (CrisisOperations.ProbingSwarm, ChoirOperations.ProbingSwarm),
            (CrisisOperations.TunnelBreach, ChoirOperations.TunnelBreach),
            (CrisisOperations.TheHum, ChoirOperations.TheHum),
            (CrisisOperations.Retrieval, ChoirOperations.Retrieval),
            (CrisisOperations.Burrowing, ChoirOperations.Burrowing),
            (CrisisOperations.FinalChorus, ChoirOperations.FinalChorus),
            (CrisisOperations.ClusterSiege, (m, s, b, d) => ChoirOperations.ClusterSiege(m, s, b, null, d)),
        };
        var nodes = new List<DebugActionNode>();
        foreach (var (step, run) in operations)
        {
            var node = new DebugActionNode(step);
            var from = CrisisOperations.Find(step, Core.Powers.Power.Choir)?.MinStage ?? CrisisStage.Probing;
            foreach (var stage in Stages.Where(s => s >= from))
            {
                node.AddChild(new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.Action, () =>
                {
                    if (RimChroniclesGameComponent.Current is not { } component || Find.CurrentMap is not { } map)
                    {
                        return;
                    }

                    if (ChoirCrisisDriver.Faction is null)
                    {
                        Messages.Message("Rim Chronicles: no Hollow Choir yet (Hollow Choir: wake now).", MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    var budget = ChoirCrisisDriver.Budget(component, stage, ColonyStrengthReader.Read());
                    if (!run(map, stage, budget, true))
                    {
                        Messages.Message($"Rim Chronicles: {step} couldn't start here (no entry cell, leader, held Choir captive, or open cell to land or burrow in).",
                            MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    component.ChoirCrisis.Started(step, stage, budget, GenDate.DaysPassed);
                }));
            }

            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>Fires a cluster siege with a chosen goal (the plain fire action picks one by weight).</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: fire cluster siege with goal", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> FireSiege() => Core.Crisis.Siege.SiegeGoals.All.Select(goal =>
    {
        var node = new DebugActionNode(goal.Id);
        foreach (var stage in Stages.Where(s => s >= CrisisStage.War))
        {
            node.AddChild(new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.Action, () =>
            {
                if (RimChroniclesGameComponent.Current is not { } component || Find.CurrentMap is not { } map || ChoirCrisisDriver.Faction is null)
                {
                    return;
                }

                var budget = ChoirCrisisDriver.Budget(component, stage, ColonyStrengthReader.Read());
                if (!ChoirOperations.ClusterSiege(map, stage, budget, goal.Goal, dev: true))
                {
                    Messages.Message("Rim Chronicles: no cluster siege spot on this map (Crisis: show siege scoring says why).", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                component.ChoirCrisis.Started(CrisisOperations.ClusterSiege, stage, budget, GenDate.DaysPassed);
            }));
        }

        return node;
    }).ToList();

    /// <summary>Spawns a Choir mechanitor, or a mech of a class, for the Choir at a stage on a clicked cell.</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: spawn unit", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Spawn()
    {
        var nodes = new List<DebugActionNode>();
        foreach (var kind in UnitKit.ChoirKinds)
        {
            var node = new DebugActionNode(kind.defName);
            foreach (var stage in Stages)
            {
                node.AddChild(new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.ToolMap, () =>
                {
                    if (ChoirCrisisDriver.Faction is { } choir && Find.CurrentMap is { } map && UI.MouseCell().InBounds(map))
                    {
                        GenSpawn.Spawn(UnitKit.Generate(kind, choir, stage), UI.MouseCell(), map);
                    }
                }));
            }

            nodes.Add(node);
        }

        foreach (var mechClass in new[] { Core.Crisis.Units.MechClass.Light, Core.Crisis.Units.MechClass.Line, Core.Crisis.Units.MechClass.Heavy,
                     Core.Crisis.Units.MechClass.Tunneler, Core.Crisis.Units.MechClass.Boss })
        {
            nodes.Add(new DebugActionNode($"mech: {mechClass}", DebugActionType.ToolMap, () =>
            {
                if (ChoirCrisisDriver.Faction is { } choir && Find.CurrentMap is { } map && UI.MouseCell().InBounds(map)
                    && ChoirRoster.Pick(mechClass, CrisisStage.Finale, Rand.Value, d => DefDatabase<PawnKindDef>.GetNamedSilentFail(d) is not null) is { } def)
                {
                    GenSpawn.Spawn(ChoirForce.Mech(DefDatabase<PawnKindDef>.GetNamed(def), choir), UI.MouseCell(), map);
                }
            }));
        }

        return nodes;
    }

    /// <summary>Drops a resonator on a clicked cell: the hum starts at once.</summary>
    [DebugAction("Rim Chronicles", "Choir crisis: drop a resonator", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DropResonator()
    {
        if (ChoirCrisisDriver.Faction is { } choir && ChoirHum.ResonatorDef is { } def && Find.CurrentMap is { } map && UI.MouseCell().InBounds(map))
        {
            ChoirHum.Drop(def, UI.MouseCell(), map, choir);
        }
    }
}
