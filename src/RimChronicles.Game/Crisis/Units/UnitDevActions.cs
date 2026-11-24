using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Units;

/// <summary>Dev Mode tools for crisis units (crisis C3): spawn any unit at any stage, and check that every unit at
/// every stage spawns with exactly its kit and no banned psycast (fairness rule 9).</summary>
public static class UnitDevActions
{
    private static readonly CrisisStage[] Stages = [CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale];

    [DebugAction("Rim Chronicles", "Crisis: spawn unit", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Spawn()
    {
        var nodes = new List<DebugActionNode>();
        foreach (var kind in UnitKit.Kinds)
        {
            var node = new DebugActionNode(kind.defName);
            foreach (var stage in Stages)
            {
                node.AddChild(new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.ToolMap, () => SpawnAt(kind, stage)));
            }

            nodes.Add(node);
        }

        return nodes;
    }

    [DebugAction("Rim Chronicles", "Crisis: check units", allowedGameStates = AllowedGameStates.Playing)]
    public static void Check()
    {
        if (EmpireDriver.EmpireFaction is not { } empire)
        {
            Messages.Message("Rim Chronicles: crisis units need Royalty and the Empire faction.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var lines = new List<string>();
        var failures = 0;
        foreach (var kind in UnitKit.Kinds)
        {
            var unit = UnitKit.Unit(kind)!;
            foreach (var stage in Stages.Where(s => s >= PsycastKits.FirstStage(unit.role, unit.specialty)))
            {
                for (var i = 0; i < 3; i++)
                {
                    var pawn = UnitKit.Generate(kind, empire, stage);
                    var problems = UnitKit.Problems(pawn, stage);
                    if (problems.Count > 0)
                    {
                        failures++;
                        lines.Add($"{kind.defName} at {CrisisStages.Label(stage)}: {string.Join("; ", problems)}");
                    }
                    else if (i == 0)
                    {
                        lines.Add($"{kind.defName} at {CrisisStages.Label(stage)}: psylink {pawn.GetPsylinkLevel()}; "
                            + $"{string.Join(", ", UnitKit.Psycasts(pawn).Select(a => a.def.defName).DefaultIfEmpty("no psycasts"))}");
                    }

                    Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.Discard);
                }
            }
        }

        lines.Insert(0, failures == 0 ? "Every unit at every stage has exactly its kit." : $"{failures} units had problems:");
        Log.Message("[Rim Chronicles] Units:\n" + string.Join("\n", lines));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    private static void SpawnAt(PawnKindDef kind, CrisisStage stage)
    {
        var map = Find.CurrentMap;
        var cell = UI.MouseCell();
        if (map is null || !cell.InBounds(map) || !cell.Standable(map))
        {
            return;
        }

        var faction = EmpireDriver.EmpireFaction ?? Find.FactionManager.RandomEnemyFaction();
        if (faction is null)
        {
            return;
        }

        var pawn = UnitKit.Generate(kind, faction, stage);
        GenSpawn.Spawn(pawn, cell, map);
        var problems = UnitKit.Problems(pawn, stage);
        var kit = string.Join(", ", UnitKit.Psycasts(pawn).Select(a => a.def.label).DefaultIfEmpty("no psycasts"));
        Messages.Message($"Rim Chronicles: {kind.label} at {CrisisStages.Label(stage)}, psylink {pawn.GetPsylinkLevel()}: {kit}."
            + (problems.Count > 0 ? $" Problems: {string.Join("; ", problems)}" : ""),
            pawn, problems.Count > 0 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.NeutralEvent, historical: false);
    }
}
