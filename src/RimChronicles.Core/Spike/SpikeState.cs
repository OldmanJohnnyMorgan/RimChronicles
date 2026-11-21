using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Spike;

/// <summary>Stage 1 spike data, saved with the game to prove save and load work (spike test 4).
/// Replaced by the real story state in Stage 2.</summary>
public sealed class SpikeState : IArchivable
{
    /// <summary>In-game days the daily patch has seen in this save (spike test 2).</summary>
    public int DaysSeen;

    /// <summary>The last reply from the test LLM call (spike test 3), or empty.</summary>
    public string LastReply = "";

    /// <summary>The day <see cref="LastReply"/> arrived, or -1.</summary>
    public int LastReplyDay = -1;

    /// <summary>Short log lines, newest last, capped at <see cref="MaxLog"/>.</summary>
    public List<string> Log = [];

    public const int MaxLog = 20;

    public void AddLog(string line)
    {
        Log.Add(line);
        if (Log.Count > MaxLog)
        {
            Log.RemoveRange(0, Log.Count - MaxLog);
        }
    }

    public void Expose(IArchive archive)
    {
        archive.Value(ref DaysSeen, "daysSeen");
        archive.Value(ref LastReply, "lastReply", "");
        archive.Value(ref LastReplyDay, "lastReplyDay", -1);
        archive.Strings(ref Log, "log");
        LastReply ??= "";
    }
}
