using LudeonTK;
using RimChronicles.Core.Snapshots;
using RimChronicles.Game.Engine;
using Verse;

namespace RimChronicles.Game;

/// <summary>Dev Mode actions, under "Rim Chronicles" in the debug actions menu (rule 7: every LLM feature has one).</summary>
public static class DevActions
{
    private const string Category = "Rim Chronicles";

    [DebugAction(Category, "Dump world snapshot", allowedGameStates = AllowedGameStates.Playing)]
    public static void DumpWorldSnapshot()
    {
        var snapshot = WorldReader.Instance.Read();
        var folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimChronicles", "snapshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{snapshot.Id}.json");
        File.WriteAllText(path, SnapshotJson.Serialize(snapshot));
        Messages.Message($"Rim Chronicles: snapshot written to {path}", RimWorld.MessageTypeDefOf.NeutralEvent, historical: false);
        Log.Message($"[Rim Chronicles] Snapshot written to {path} ({snapshot.Pawns.Count} pawns, {snapshot.Tales.Count} tales, {snapshot.Factions.Count} factions).");
    }

    [DebugAction(Category, "Show news log", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowNewsLog()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var events = component.News.Events;
        var text = events.Count == 0
            ? "No world news yet. Try the debug incidents for a cold snap, toxic fallout or a faction leader's death."
            : string.Join("\n", events.OrderBy(e => e.Day).Select(e => $"Day {e.Day} [{e.Kind}]: {e.Text}"));
        LetterSink.Instance.Send($"Rim Chronicles: news log ({events.Count})", text);
    }

    [DebugAction(Category, "Force bulletin", allowedGameStates = AllowedGameStates.Playing)]
    public static void ForceBulletin()
    {
        if (RimChroniclesGameComponent.Current is { } component)
        {
            News.NewsDriver.Force(component);
        }
    }

    [DebugAction(Category, "Show news desk", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowNewsDesk()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var d = component.Desk;
        var pending = d.Pending is { } p
            ? $"written ({p.Source}), due day {p.DueDay}:\n{p.Text}"
            : component.WritingBulletin ? "being written" : "not written yet";
        var last = d.Delivered.Count == 0 ? "none yet" : string.Join("\n", d.Delivered.TakeLast(3).Select(b => $"Day {b.DeliveredDay} ({b.Source}): {b.Text}"));
        var r = d.Reporter;
        var where = component.ReporterPawn is not { } pawn ? "no pawn" : pawn.Dead ? "dead" : pawn.Spawned ? "on the map" : "in the world";
        LetterSink.Instance.Send("Rim Chronicles: news desk",
            $"Reporter: {r.Name} of {(r.Faction.Length > 0 ? r.Faction : "no faction")}, {r.Outlet} ({where})\n"
            + $"Status: {r.Status}; next visit day {r.NextVisitDay}; replacement day {r.ReplaceDay}; silenced day {r.SilencedDay}\nNext bulletin due: day {d.NextDueDay} (cadence {RimChroniclesMod.Settings.NewsCadenceDays} days)\n"
            + $"Next bulletin: {pending}\nOld news remembered: {d.Reported.Count}\n\nLast bulletins:\n{last}");
    }

    [DebugAction(Category, "Reporter: visit now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ReporterVisitNow()
    {
        if (RimChroniclesGameComponent.Current is { } component && !News.ReporterLife.TryVisit(component))
        {
            var r = component.Desk.Reporter;
            var why = r.Status switch
            {
                Core.News.ReporterStatus.Replacing => $"the reporter is dead; the replacement arrives on day {r.ReplaceDay} (or use Reporter: replacement now)",
                Core.News.ReporterStatus.Silenced => "the colony silenced the reporter, so no one will come",
                _ when component.ReporterPawn is null => "there's no reporter yet",
                _ when component.ReporterPawn.Spawned => "the reporter is already on a map",
                _ => "their faction is hostile, or the colony is under attack",
            };
            Messages.Message($"Rim Chronicles: can't visit now: {why}.", RimWorld.MessageTypeDefOf.RejectInput, historical: false);
        }
    }

    [DebugAction(Category, "Reporter: kill (raiders)", allowedGameStates = AllowedGameStates.Playing)]
    public static void KillReporterOther() => KillReporter(byColony: false);

    [DebugAction(Category, "Reporter: kill (by colony)", allowedGameStates = AllowedGameStates.Playing)]
    public static void KillReporterByColony() => KillReporter(byColony: true);

    /// <summary>Kills the reporter through the real Kill path, with a colonist or no one as the killer, so the death
    /// patch is what gets tested.</summary>
    private static void KillReporter(bool byColony)
    {
        if (RimChroniclesGameComponent.Current?.ReporterPawn is not { Dead: false } pawn)
        {
            Messages.Message("Rim Chronicles: there is no living reporter.", RimWorld.MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var killer = byColony ? RimWorld.PawnsFinder.AllMaps_FreeColonists.FirstOrDefault() : null;
        pawn.Kill(new DamageInfo(RimWorld.DamageDefOf.Cut, 9999f, instigator: killer));
    }

    [DebugAction(Category, "Reporter: replacement now", allowedGameStates = AllowedGameStates.Playing)]
    public static void ReplacementNow()
    {
        if (RimChroniclesGameComponent.Current is not { } component || component.Desk.Reporter.Status != Core.News.ReporterStatus.Replacing)
        {
            Messages.Message("Rim Chronicles: no replacement is pending (the reporter isn't dead, or the colony killed them).",
                RimWorld.MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        component.Desk.Reporter.ReplaceDay = RimWorld.GenDate.DaysPassed;
        News.ReporterKeeper.EnsureReporter(component);
    }
}
