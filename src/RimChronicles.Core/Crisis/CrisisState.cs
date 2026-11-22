using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Crisis;

/// <summary>How a crisis operation ended.</summary>
public static class OperationOutcome
{
    /// <summary>Under way.</summary>
    public const string Running = "running";

    /// <summary>The colony beat it back, or it retreated.</summary>
    public const string Repelled = "repelled";

    /// <summary>It reached its goal.</summary>
    public const string Succeeded = "succeeded";

    /// <summary>It never got going (no map, no spawn spot), or the game cut it short.</summary>
    public const string Aborted = "aborted";
}

/// <summary>One crisis operation, for cooldowns, the stage rules and the log.</summary>
public sealed class OperationRecord : IArchivable
{
    public int Day;

    /// <summary>The operation's step ("ProbingRaid").</summary>
    public string Operation = "";

    public CrisisStage Stage;
    public string Outcome = OperationOutcome.Running;
    public float Budget;

    public void Expose(IArchive archive)
    {
        archive.Value(ref Day, "day");
        archive.Value(ref Operation, "operation", "");
        archive.Value(ref Stage, "stage", CrisisStage.None);
        archive.Value(ref Outcome, "outcome", OperationOutcome.Running);
        archive.Value(ref Budget, "budget");
        Operation ??= "";
        Outcome ??= OperationOutcome.Running;
    }
}

/// <summary>An Imperial capability the colony has knocked out until a day (a destroyed relay or uplink, crisis spec §6).</summary>
public sealed class CapabilityHold : IArchivable
{
    public string Capability = "";
    public int UntilDay;

    public void Expose(IArchive archive)
    {
        archive.Value(ref Capability, "capability", "");
        archive.Value(ref UntilDay, "untilDay");
        Capability ??= "";
    }
}

/// <summary>Capabilities that sites can knock out (crisis spec §4.1).</summary>
public static class CrisisCapability
{
    /// <summary>Psychic relays: Empire psykers are weaker while it's down.</summary>
    public const string Psykers = "psykers";

    /// <summary>Orbital uplinks: no orbital strikes while it's down.</summary>
    public const string Strikes = "strikes";
}

/// <summary>
/// The Imperial crisis in one save (tech spec §4.1): its operations, the stage last announced, and what the colony
/// has knocked out. Saved with the game; a save without it loads as "no crisis yet". The stage itself is worked out
/// each day by <see cref="CrisisStages"/>.
/// </summary>
public sealed class CrisisState : IArchivable
{
    public const int MaxHistory = 50;

    public List<OperationRecord> History = [];

    /// <summary>The stage the news last announced, so a change is announced once.</summary>
    public CrisisStage AnnouncedStage = CrisisStage.None;

    public int AnnouncedDay = -1;

    public List<CapabilityHold> Holds = [];

    public int LastOperationDay => History.Count == 0 ? -1 : History.Max(h => h.Day);

    public int OperationsSurvived => History.Count(h => h.Outcome == OperationOutcome.Repelled);

    public OperationRecord? Running => History.LastOrDefault(h => h.Outcome == OperationOutcome.Running);

    /// <summary>The last day <paramref name="operation"/> ran, or -1.</summary>
    public int LastDayOf(string operation) => History.Where(h => h.Operation == operation).Select(h => h.Day).DefaultIfEmpty(-1).Max();

    public OperationRecord Started(string operation, CrisisStage stage, float budget, int today)
    {
        var record = new OperationRecord { Day = today, Operation = operation, Stage = stage, Budget = budget };
        History.Add(record);
        if (History.Count > MaxHistory)
        {
            History.RemoveRange(0, History.Count - MaxHistory);
        }

        return record;
    }

    /// <summary>Records how the running operation ended. Does nothing when none is running.</summary>
    public void Ended(string outcome)
    {
        if (Running is { } record)
        {
            record.Outcome = outcome;
        }
    }

    /// <summary>True while a site has knocked <paramref name="capability"/> out.</summary>
    public bool IsDown(string capability, int today) => Holds.Any(h => h.Capability == capability && today < h.UntilDay);

    /// <summary>Knocks a capability out until <paramref name="untilDay"/> (a later hold wins).</summary>
    public void Hold(string capability, int untilDay)
    {
        Holds.RemoveAll(h => h.Capability == capability && h.UntilDay <= untilDay);
        if (!Holds.Any(h => h.Capability == capability))
        {
            Holds.Add(new CapabilityHold { Capability = capability, UntilDay = untilDay });
        }
    }

    /// <summary>The stage to announce today, or null if it hasn't changed since the last announcement.</summary>
    public CrisisStage? StageChange(CrisisStage stage, int today)
    {
        if (stage == AnnouncedStage)
        {
            return null;
        }

        AnnouncedStage = stage;
        AnnouncedDay = today;
        return stage;
    }

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref History, "history");
        archive.Value(ref AnnouncedStage, "announcedStage", CrisisStage.None);
        archive.Value(ref AnnouncedDay, "announcedDay", -1);
        archive.DeepList(ref Holds, "holds");
        History ??= [];
        Holds ??= [];
    }
}
