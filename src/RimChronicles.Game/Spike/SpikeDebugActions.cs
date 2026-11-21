using LudeonTK;
using RimChronicles.Core.Snapshots;
using RimChronicles.Game.Engine;
using Verse;

namespace RimChronicles.Game.Spike;

/// <summary>Dev Mode actions for the Stage 1 spike, under "Rim Chronicles" in the debug actions menu.</summary>
public static class SpikeDebugActions
{
    private const string Category = "Rim Chronicles";

    [DebugAction(Category, "Test LLM letter", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestLlmLetter()
    {
        if (RimChroniclesGameComponent.Current is { } component)
        {
            SpikeLlmCall.Start(component);
        }
    }

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

    [DebugAction(Category, "Show spike state", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowSpikeState()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var s = component.Spike;
        var reply = s.LastReplyDay < 0 ? "none yet" : $"day {s.LastReplyDay}: {s.LastReply}";
        LetterSink.Instance.Send("Rim Chronicles: spike state",
            $"Days seen by the daily patch: {s.DaysSeen}\nLast LLM reply: {reply}\n\nLog:\n{string.Join("\n", s.Log)}");
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
        LetterSink.Instance.Send("Rim Chronicles: news desk",
            $"Reporter: {d.Reporter.Name}, {d.Reporter.Outlet}\nNext bulletin due: day {d.NextDueDay} (cadence {RimChroniclesMod.Settings.NewsCadenceDays} days)\n"
            + $"Next bulletin: {pending}\nOld news remembered: {d.Reported.Count}\n\nLast bulletins:\n{last}");
    }
}
