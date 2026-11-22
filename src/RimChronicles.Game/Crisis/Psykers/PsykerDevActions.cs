using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Psykers;

/// <summary>Dev Mode tools for the psyker AI (crisis C4, spec §9): a psyker squad to watch, the debug text over each
/// psyker, a report of every psyker's last decision, and the scorer check.</summary>
public static class PsykerDevActions
{
    private static readonly CrisisStage[] Stages = [CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale];

    [DebugAction("Rim Chronicles", "Crisis: spawn psyker squad", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> SpawnSquad() => Stages
        .Select(stage => new DebugActionNode($"stage {(int)stage} {CrisisStages.Label(stage)}", DebugActionType.ToolMap, () => Squad(stage)))
        .ToList();

    [DebugAction("Rim Chronicles", "Crisis: psyker debug on/off", allowedGameStates = AllowedGameStates.Playing)]
    public static void ToggleDebug()
    {
        PsykerLog.Debug = !PsykerLog.Debug;
        Messages.Message($"Rim Chronicles: psyker debug text {(PsykerLog.Debug ? "on" : "off")}.", MessageTypeDefOf.NeutralEvent, historical: false);
    }

    [DebugAction("Rim Chronicles", "Crisis: psyker report", allowedGameStates = AllowedGameStates.Playing)]
    public static void Report()
    {
        var lines = new List<string>
        {
            $"Casts chosen by scorers: {PsykerLog.ChosenCasts}. Casts the scorers didn't choose: {PsykerLog.UnchosenCasts.Count}.",
        };
        lines.AddRange(PsykerLog.UnchosenCasts.TakeLast(10).Select(l => "  " + l));
        lines.Add("");
        foreach (var entry in PsykerLog.Entries)
        {
            lines.Add($"{entry.Pawn} (tick {entry.Tick}, heat {entry.Heat:P0}, psyfocus {entry.Psyfocus:P0}): {entry.Decision}");
            lines.AddRange(entry.Top.Select(c => $"    {c.Psycast} {c.Utility:0.0} via {c.Scorer}: {c.Why}"));
        }

        Log.Message("[Rim Chronicles] Psykers:\n" + string.Join("\n", lines));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    [DebugAction("Rim Chronicles", "Crisis: check psycasts", allowedGameStates = AllowedGameStates.Playing)]
    public static void CheckPsycasts()
    {
        var lines = ScorerRegistry.Report.ToList();
        var kits = UnitKit.Kinds.Select(UnitKit.Unit).OfType<UnitExtension>()
            .SelectMany(u => PsycastKits.For(u.role, u.specialty)).Select(p => p.Def).Distinct().OrderBy(d => d).ToList();
        var unscored = kits.Where(d => ScorerRegistry.For(d).Count == 0).ToList();
        lines.Add("");
        lines.Add($"Kit psycasts with no scorer (never cast): {string.Join(", ", unscored.DefaultIfEmpty("none"))}");
        Log.Message("[Rim Chronicles] Psycast scorers:\n" + string.Join("\n", lines));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    /// <summary>One adept of each specialty the stage allows, three Praetorians and a Herald, assaulting the colony.</summary>
    private static void Squad(CrisisStage stage)
    {
        var map = Find.CurrentMap;
        var root = UI.MouseCell();
        if (map is null || !root.InBounds(map) || (EmpireDriver.EmpireFaction ?? Find.FactionManager.RandomEnemyFaction()) is not { } faction)
        {
            return;
        }

        var kinds = UnitKit.Kinds.Where(k => UnitKit.Unit(k) is { } u && u.role != UnitRole.Stellarch
            && (u.role == UnitRole.Adept ? stage >= PsycastKits.FirstStage(u.role, u.specialty) : u.role is UnitRole.Herald)).ToList();
        var praetorian = UnitKit.Kinds.FirstOrDefault(k => UnitKit.Unit(k)?.role == UnitRole.Praetorian);
        if (praetorian is not null)
        {
            kinds.AddRange(Enumerable.Repeat(praetorian, 3));
        }

        var pawns = new List<Pawn>();
        foreach (var kind in kinds)
        {
            if (CellFinder.TryFindRandomCellNear(root, map, 4, c => c.Standable(map) && c.GetFirstPawn(map) is null, out var cell))
            {
                var pawn = UnitKit.Generate(kind, faction, stage);
                GenSpawn.Spawn(pawn, cell, map);
                pawns.Add(pawn);
            }
        }

        LordMaker.MakeNewLord(faction, new RimWorld.LordJob_AssaultColony(faction), map, pawns);
        Messages.Message($"Rim Chronicles: a stage {(int)stage} psyker squad of {pawns.Count} is attacking.", MessageTypeDefOf.ThreatBig, historical: false);
    }
}
