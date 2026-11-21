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
}
