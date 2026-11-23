using RimChronicles.Core.Crisis.Captives;
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

/// <summary>A cell on the colony's map (a door or gap a probe found).</summary>
public sealed class EntranceCell : IArchivable
{
    public int X;
    public int Z;

    public void Expose(IArchive archive)
    {
        archive.Value(ref X, "x");
        archive.Value(ref Z, "z");
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

    /// <summary>The colony's entrances a probing raid found (crisis spec §5.2), for later raids and sieges.</summary>
    public List<EntranceCell> Entrances = [];

    public int EntrancesDay = -1;

    public const int MaxEntrances = 6;

    // ---- captives, intel and defectors (C10)

    public const int MaxIntel = 40;
    public const int MaxCaptives = 40;

    public List<CaptiveRecord> Captives = [];
    public List<DefectorRecord> Defectors = [];

    /// <summary>The colony's intel log, oldest first.</summary>
    public List<IntelRecord> Intel = [];

    /// <summary>The operation intel warned of; the crisis holds to it while it's valid.</summary>
    public OperationForecast Forecast = new();

    /// <summary>Uplink codes in hand: each makes one orbital strike fail.</summary>
    public int StrikeJams;

    /// <summary>The next psychic assault comes short of adepts (relay intel).</summary>
    public bool AdeptShortfall;

    /// <summary>The Citadel keep's side entrance is known (for C12).</summary>
    public bool KeepEntranceKnown;

    /// <summary>The next operation's budget is multiplied by this, once (a released captive).</summary>
    public float NextBudgetFactor = 1;

    // ---- sites and the Citadel (C9, C12)

    /// <summary>Relay and uplink sites the colony destroyed: each takes one relay or uplink from the Citadel's courtyard.</summary>
    public int RelaySitesDestroyed;

    public int UplinkSitesDestroyed;

    /// <summary>The world-object id of the settlement whose map is the Citadel (-1 until it's generated).</summary>
    public int CitadelSettlementId = -1;

    public int Released;
    public int Executed;
    public int Harvested;
    public int Enslaved;

    public CaptiveRecord? Captive(string pawnId) => Captives.FirstOrDefault(c => c.PawnId == pawnId);

    public bool IsDefector(string pawnId) => Defectors.Any(d => d.PawnId == pawnId);

    /// <summary>Records an Imperial taken prisoner (once).</summary>
    public CaptiveRecord Captured(string pawnId, string name, CaptiveRank rank, int today)
    {
        if (Captive(pawnId) is { } known)
        {
            return known;
        }

        var record = new CaptiveRecord { PawnId = pawnId, Name = name, Rank = rank, Day = today };
        Captives.Add(record);
        if (Captives.Count > MaxCaptives)
        {
            Captives.RemoveRange(0, Captives.Count - MaxCaptives);
        }

        return record;
    }

    /// <summary>A captive joined the colony. Only one interrogated first counts as a defector (crisis spec §5.6);
    /// returns the defector record, or null.</summary>
    public DefectorRecord? Recruited(string pawnId, int today)
    {
        if (Captive(pawnId) is not { Interrogations: > 0 } captive || IsDefector(pawnId))
        {
            return null;
        }

        var defector = new DefectorRecord { PawnId = pawnId, Name = captive.Name, Rank = captive.Rank, Day = today };
        Defectors.Add(defector);
        return defector;
    }

    /// <summary>A captive let go: the next operation is cheaper, once.</summary>
    public void Release()
    {
        Released++;
        NextBudgetFactor = Math.Min(NextBudgetFactor, CaptiveRules.ReleaseBudgetFactor);
    }

    /// <summary>Uses one set of uplink codes, if any: the strike being called fails.</summary>
    public bool TryUseJam()
    {
        if (StrikeJams <= 0)
        {
            return false;
        }

        StrikeJams--;
        return true;
    }

    /// <summary>The live intel log: items that haven't gone stale.</summary>
    public IEnumerable<IntelRecord> LiveIntel(int today) => Intel.Where(i => !i.Expired(today));

    /// <summary>A probe reports what it found: the newest report replaces the last.</summary>
    public void Learned(IEnumerable<(int X, int Z)> cells, int today)
    {
        Entrances = cells.Take(MaxEntrances).Select(c => new EntranceCell { X = c.X, Z = c.Z }).ToList();
        EntrancesDay = today;
    }

    public int LastOperationDay => History.Count == 0 ? -1 : History.Max(h => h.Day);

    public int OperationsSurvived => History.Count(h => h.Outcome == OperationOutcome.Repelled);

    public OperationRecord? Running => History.LastOrDefault(h => h.Outcome == OperationOutcome.Running);

    /// <summary>The last day <paramref name="operation"/> ran, or -1.</summary>
    public int LastDayOf(string operation) => History.Where(h => h.Operation == operation).Select(h => h.Day).DefaultIfEmpty(-1).Max();

    public OperationRecord Started(string operation, CrisisStage stage, float budget, int today)
    {
        var record = new OperationRecord { Day = today, Operation = operation, Stage = stage, Budget = budget };
        History.Add(record);
        Forecast = new OperationForecast(); // the warning came true, or another operation came first: either way it's spent
        NextBudgetFactor = 1;
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
        archive.DeepList(ref Entrances, "entrances");
        archive.Value(ref EntrancesDay, "entrancesDay", -1);
        archive.DeepList(ref Captives, "captives");
        archive.DeepList(ref Defectors, "defectors");
        archive.DeepList(ref Intel, "intel");
        archive.Deep(ref Forecast, "forecast");
        archive.Value(ref StrikeJams, "strikeJams");
        archive.Value(ref AdeptShortfall, "adeptShortfall");
        archive.Value(ref KeepEntranceKnown, "keepEntranceKnown");
        archive.Value(ref NextBudgetFactor, "nextBudgetFactor", 1f);
        archive.Value(ref RelaySitesDestroyed, "relaySitesDestroyed");
        archive.Value(ref UplinkSitesDestroyed, "uplinkSitesDestroyed");
        archive.Value(ref CitadelSettlementId, "citadelSettlementId", -1);
        archive.Value(ref Released, "released");
        archive.Value(ref Executed, "executed");
        archive.Value(ref Harvested, "harvested");
        archive.Value(ref Enslaved, "enslaved");
        Captives ??= [];
        Defectors ??= [];
        Intel ??= [];
        Forecast ??= new OperationForecast();
        History ??= [];
        Holds ??= [];
        Entrances ??= [];
    }
}
