using LudeonTK;
using RimChronicles.Core.Crisis.Strikes;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Empire;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Strikes;

/// <summary>Dev Mode tools for orbital strikes (crisis C5): call one at the mouse from the nearest Herald that can see
/// it, and show a map's strike state with today's best targets.</summary>
public static class StrikeDevActions
{
    [DebugAction("Rim Chronicles", "Crisis: call orbital strike", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static List<DebugActionNode> Call() => Enum.GetValues<StrikeKind>()
        .Select(kind => new DebugActionNode(kind.ToString(), DebugActionType.ToolMap, () => CallAt(kind)))
        .ToList();

    [DebugAction("Rim Chronicles", "Crisis: strike state", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void Show()
    {
        var map = Find.CurrentMap;
        if (MapComponent_RcStrikes.For(map) is not { } strikes || EmpireDriver.EmpireFaction is not { } empire)
        {
            Messages.Message("Rim Chronicles: strikes need Royalty and the Empire faction.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var heralds = Heralds(map, empire);
        var state = strikes.State;
        var tick = Find.TickManager.TicksGame;
        var candidates = strikes.Candidates(empire, heralds, uplink: false);
        var lines = new List<string>
        {
            $"Heralds up: {heralds.Count}. Strikes this raid: {state.StrikesThisRaid} of {(state.Cap == 0 ? "?" : state.Cap)}; last at tick {state.LastStrikeTick} ({state.LastCell?.ToString() ?? "none"}).",
            $"Blocked: {StrikeRules.Blocked(state, heralds.Count > 0, false, false, tick) ?? "no"}.",
            $"Pending: {string.Join(", ", strikes.Pending.Select(p => $"{p.kind} at {p.cell} in {(p.fireTick - tick) / StrikeRules.TicksPerSecond} s").DefaultIfEmpty("none"))}.",
            $"Would pick: {(StrikeRules.Pick(candidates, state) is { } pick ? $"{pick.Kind} on {pick.Target.Kind} at {pick.Target.Cell}" : "nothing")}.",
            "",
            "Best candidates:",
        };
        lines.AddRange(candidates.OrderByDescending(c => StrikeRules.Score(c, SiegeGoal.None)).Take(10).Select(c =>
            $"  {c.Kind} at {c.Cell}: score {StrikeRules.Score(c, SiegeGoal.None):0.0}, {c.Colonists} colonists, {c.Allies} Imperial in blast, "
            + $"{(c.Sighted ? "sighted" : "not sighted")}{(c.ThickRoof ? ", under mountain" : "")}"));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    private static void CallAt(StrikeKind kind)
    {
        var map = Find.CurrentMap;
        var cell = UI.MouseCell();
        if (map is null || !cell.InBounds(map) || MapComponent_RcStrikes.For(map) is not { } strikes)
        {
            return;
        }

        var herald = EmpireDriver.EmpireFaction is { } empire
            ? Heralds(map, empire).Where(h => GenSight.LineOfSight(h.Position, cell, map)).OrderBy(h => h.Position.DistanceTo(cell)).FirstOrDefault()
            : null;
        strikes.Call(cell, kind, herald, dev: herald is null);
        Messages.Message(herald is null
            ? "Rim Chronicles: no Herald can see that spot, so this strike has no source and can't be called off."
            : $"Rim Chronicles: {herald.LabelShort} called it; down them to call it off.", MessageTypeDefOf.NeutralEvent, historical: false);
    }

    private static List<Pawn> Heralds(Map map, Faction empire) => map.mapPawns.SpawnedPawnsInFaction(empire)
        .Where(p => UnitKit.Unit(p.kindDef)?.role == UnitRole.Herald && !p.Downed).ToList();
}
