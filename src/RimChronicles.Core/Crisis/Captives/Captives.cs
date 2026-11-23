using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Crisis.Captives;

/// <summary>What a captured Imperial knows, by what they were (crisis spec §5.6: "rank sets how high and what they know").</summary>
public enum CaptiveRank
{
    None,
    Soldier,
    Breacher,
    Herald,
    Psyker,
    Officer,
}

/// <summary>What an intel item does (crisis spec §5.6, tech spec §4.7). Applied by <see cref="IntelEffects"/>.</summary>
public enum IntelEffect
{
    /// <summary>Reveals the garrison of one known Imperial site.</summary>
    Garrison,

    /// <summary>Warns of the next operation's type and earliest day, and holds the crisis to it.</summary>
    NextOperation,

    /// <summary>The next orbital strike called fails.</summary>
    UplinkCodes,

    /// <summary>Reveals a psychic relay site, and the next psychic assault comes with fewer adepts.</summary>
    RelayLocation,

    /// <summary>Reveals the Citadel keep's side entrance (used by the Citadel, C12).</summary>
    CitadelPlans,
}

/// <summary>An intel item as plain data (the game reads it from an <c>IntelDef</c>).</summary>
public sealed record IntelSpec(string Id, IReadOnlyList<CaptiveRank> Ranks, IntelEffect Effect, int ExpiryDays = 0, double Weight = 1)
{
    public string Label { get; init; } = Id;
}

/// <summary>An Imperial held by the colony, for interrogation, defection and the summary. Saved in <see cref="CrisisState"/>.</summary>
public sealed class CaptiveRecord : IArchivable
{
    public string PawnId = "";
    public string Name = "";
    public CaptiveRank Rank;
    public int Day;
    public int Interrogations;

    /// <summary>The intel ids this captive has given, in order.</summary>
    public List<string> Told = [];

    public void Expose(IArchive archive)
    {
        archive.Value(ref PawnId, "pawnId", "");
        archive.Value(ref Name, "name", "");
        archive.Value(ref Rank, "rank", CaptiveRank.None);
        archive.Value(ref Day, "day");
        archive.Value(ref Interrogations, "interrogations");
        archive.Strings(ref Told, "told");
        PawnId ??= "";
        Name ??= "";
        Told ??= [];
    }
}

/// <summary>A captive who joined the colony after interrogation (crisis spec §5.6).</summary>
public sealed class DefectorRecord : IArchivable
{
    public string PawnId = "";
    public string Name = "";
    public CaptiveRank Rank;
    public int Day;

    public void Expose(IArchive archive)
    {
        archive.Value(ref PawnId, "pawnId", "");
        archive.Value(ref Name, "name", "");
        archive.Value(ref Rank, "rank", CaptiveRank.None);
        archive.Value(ref Day, "day");
        PawnId ??= "";
        Name ??= "";
    }
}

/// <summary>One item in the colony's intel log.</summary>
public sealed class IntelRecord : IArchivable
{
    public int Day;
    public string Intel = "";
    public string Text = "";

    /// <summary>-1: never goes stale.</summary>
    public int ExpiresDay = -1;

    public bool Expired(int today) => ExpiresDay >= 0 && today > ExpiresDay;

    public void Expose(IArchive archive)
    {
        archive.Value(ref Day, "day");
        archive.Value(ref Intel, "intel", "");
        archive.Value(ref Text, "text", "");
        archive.Value(ref ExpiresDay, "expiresDay", -1);
        Intel ??= "";
        Text ??= "";
    }
}

/// <summary>A warned-of operation: the crisis holds to it until it fires or the warning goes stale.</summary>
public sealed class OperationForecast : IArchivable
{
    public string Operation = "";
    public int EarliestDay;
    public int ExpiresDay;

    public bool Valid(int today) => Operation.Length > 0 && today <= ExpiresDay;

    public void Expose(IArchive archive)
    {
        archive.Value(ref Operation, "operation", "");
        archive.Value(ref EarliestDay, "earliestDay");
        archive.Value(ref ExpiresDay, "expiresDay");
        Operation ??= "";
    }
}

/// <summary>How a session of interrogation went.</summary>
public sealed record SessionResult(bool Success, IntelSpec? Intel, float ResistanceLoss, float WillLoss, string? NothingToTell);

/// <summary>
/// The captive rules (crisis spec §5.6, tech spec §4.7): resistance and will by rank, how much each rank can tell,
/// the interrogation chance, and what a session costs the captive. Numbers are first guesses for the balance pass (C14).
/// </summary>
public static class CaptiveRules
{
    /// <summary>Resistance and will ranges by rank: high, rising with rank (vanilla raiders: about 15–25 and 3–6).
    /// The unit pawn kinds carry these ranges; a test checks they match.</summary>
    public static (float Min, float Max) Resistance(CaptiveRank rank) => rank switch
    {
        CaptiveRank.Soldier => (35, 50),
        CaptiveRank.Breacher => (30, 45),
        CaptiveRank.Herald => (45, 60),
        CaptiveRank.Psyker => (60, 80),
        CaptiveRank.Officer => (90, 120),
        _ => (0, 0),
    };

    public static (float Min, float Max) Will(CaptiveRank rank) => rank switch
    {
        CaptiveRank.Soldier => (7, 9),
        CaptiveRank.Breacher => (6, 8),
        CaptiveRank.Herald => (8, 10),
        CaptiveRank.Psyker => (11, 14),
        CaptiveRank.Officer => (18, 22),
        _ => (0, 0),
    };

    /// <summary>How many intel items a captive of this rank can give.</summary>
    public static int MaxIntel(CaptiveRank rank) => rank switch
    {
        CaptiveRank.Soldier or CaptiveRank.Breacher => 1,
        CaptiveRank.Herald or CaptiveRank.Psyker => 2,
        CaptiveRank.Officer => 3,
        _ => 0,
    };

    public static CaptiveRank RankOf(UnitRole role) => role switch
    {
        UnitRole.Praetorian => CaptiveRank.Soldier,
        UnitRole.Breacher => CaptiveRank.Breacher,
        UnitRole.Herald => CaptiveRank.Herald,
        UnitRole.Adept => CaptiveRank.Psyker,
        UnitRole.Stellarch => CaptiveRank.Officer,
        _ => CaptiveRank.None,
    };

    /// <summary>The chance one session gets intel, from the warden's social impact (vanilla's identity interrogation:
    /// 15% per point of social impact), harder for higher ranks.</summary>
    public static float Chance(float socialImpact, CaptiveRank rank)
    {
        var penalty = rank switch
        {
            CaptiveRank.Herald => 0.8f,
            CaptiveRank.Psyker => 0.7f,
            CaptiveRank.Officer => 0.5f,
            _ => 1f,
        };
        return Math.Clamp(socialImpact * 0.35f * penalty, 0.05f, 0.8f);
    }

    public const float ResistanceOnSuccess = 4;
    public const float ResistanceOnFailure = 1;
    public const float WillPerSession = 0.5f;

    /// <summary>Why a captive can't be interrogated, or null if they can.</summary>
    public static string? NothingToTell(CaptiveRank rank, CaptiveRecord? record) =>
        rank == CaptiveRank.None ? "knows nothing about the Empire"
        : record is not null && record.Told.Count >= MaxIntel(rank) ? "has told all they know"
        : null;

    /// <summary>One interrogation session: with <paramref name="roll"/> under the chance, the captive gives an item they
    /// haven't given yet (weighted by <paramref name="pick"/>); either way it wears their resistance and will down,
    /// more on a success ("interrogating first lowers loyalty faster").</summary>
    public static SessionResult Session(CaptiveRank rank, CaptiveRecord record, IReadOnlyList<IntelSpec> intel, float socialImpact, double roll, double pick)
    {
        if (NothingToTell(rank, record) is { } why)
        {
            return new SessionResult(false, null, 0, 0, why);
        }

        record.Interrogations++;
        if (roll >= Chance(socialImpact, rank))
        {
            return new SessionResult(false, null, ResistanceOnFailure, WillPerSession, null);
        }

        var known = intel.Where(i => i.Ranks.Contains(rank) && i.Weight > 0).ToList();
        var fresh = known.Where(i => !record.Told.Contains(i.Id)).ToList();
        var item = WeightedPick(fresh.Count > 0 ? fresh : known, pick);
        if (item is not null)
        {
            record.Told.Add(item.Id);
        }

        return new SessionResult(item is not null, item, ResistanceOnSuccess, WillPerSession, null);
    }

    public static IntelSpec? WeightedPick(IReadOnlyList<IntelSpec> items, double roll)
    {
        var total = items.Sum(i => i.Weight);
        if (total <= 0)
        {
            return null;
        }

        var at = Math.Clamp(roll, 0, 0.999999) * total;
        foreach (var item in items)
        {
            if ((at -= item.Weight) < 0)
            {
                return item;
            }
        }

        return items[^1];
    }

    /// <summary>A captive let go: a cheaper next operation (crisis spec §5.6: "a small goodwill or a one-time reduced
    /// operation"; the game adds the goodwill).</summary>
    public const float ReleaseBudgetFactor = 0.85f;

    public const int ReleaseGoodwill = 5;
}

/// <summary>What applying an intel item did, and what the game still has to do for it.</summary>
public sealed record IntelOutcome(string Text)
{
    /// <summary>An archetype name ("PsychicRelay") whose site the game should put on the world map.</summary>
    public string? RevealSite { get; init; }

    /// <summary>The game should report the garrison of a known Imperial site.</summary>
    public bool RevealGarrison { get; init; }
}

/// <summary>
/// The intel effects on <see cref="CrisisState"/> (tech spec §4.7): Core decides what changes and what to tell; the game
/// adds the parts that need the world (revealing a site, counting a garrison). Every item is logged.
/// </summary>
public static class IntelEffects
{
    /// <summary>A warning holds for this many days after the earliest day.</summary>
    public const int ForecastSlackDays = 5;

    public const int MaxStrikeJams = 2;

    public static IntelOutcome Apply(IntelSpec intel, CrisisState state, CrisisContext context, int today, double roll)
    {
        var outcome = intel.Effect switch
        {
            IntelEffect.NextOperation => Forecast(state, context, today, roll),
            IntelEffect.UplinkCodes => Jam(state),
            IntelEffect.RelayLocation => Relay(state),
            IntelEffect.CitadelPlans => Plans(state),
            _ => new IntelOutcome("They gave up the size of an Imperial garrison.") { RevealGarrison = true },
        };
        state.Intel.Add(new IntelRecord
        {
            Day = today, Intel = intel.Id, Text = outcome.Text, ExpiresDay = intel.ExpiryDays > 0 ? today + intel.ExpiryDays : -1,
        });
        if (state.Intel.Count > CrisisState.MaxIntel)
        {
            state.Intel.RemoveRange(0, state.Intel.Count - CrisisState.MaxIntel);
        }

        return outcome;
    }

    /// <summary>Picks the next operation now, by weight among those allowed on the earliest day it could come, and
    /// holds the crisis to it (<see cref="CrisisArc.Allowed"/> honours the forecast).</summary>
    private static IntelOutcome Forecast(CrisisState state, CrisisContext context, int today, double roll)
    {
        var gap = Math.Clamp(context.GapDays, CrisisArc.MinGapDays, CrisisArc.MaxGapDays);
        var earliest = Math.Max(today + 1, state.LastOperationDay < 0 ? today + 1 : state.LastOperationDay + gap);
        state.Forecast = new OperationForecast();
        var options = CrisisArc.Allowed(context with { State = state }, earliest, ignoreBuilt: false, out _)
            .Where(o => !o.FinaleOnly).ToList();
        if (Pick(options, roll) is not { } op)
        {
            return new IntelOutcome("They knew of no operation being readied.");
        }

        state.Forecast = new OperationForecast { Operation = op.Step, EarliestDay = earliest, ExpiresDay = earliest + ForecastSlackDays };
        return new IntelOutcome($"The next Imperial operation is {Describe(op.Step)}, no sooner than day {earliest}.");
    }

    private static OperationSpec? Pick(IReadOnlyList<OperationSpec> options, double roll)
    {
        var total = options.Sum(o => o.Weight);
        if (total <= 0)
        {
            return null;
        }

        var at = Math.Clamp(roll, 0, 0.999999) * total;
        foreach (var o in options)
        {
            if ((at -= o.Weight) < 0)
            {
                return o;
            }
        }

        return options[^1];
    }

    private static IntelOutcome Jam(CrisisState state)
    {
        state.StrikeJams = Math.Min(MaxStrikeJams, state.StrikeJams + 1);
        return new IntelOutcome("They gave up the uplink codes: the next orbital strike the Empire calls will fail.");
    }

    private static IntelOutcome Relay(CrisisState state)
    {
        state.AdeptShortfall = true;
        return new IntelOutcome("They gave up where a psychic relay stands, and that the next psychic assault is short of adepts.")
        {
            RevealSite = "PsychicRelay",
        };
    }

    private static IntelOutcome Plans(CrisisState state)
    {
        state.KeepEntranceKnown = true;
        return new IntelOutcome("They gave up the Citadel's plans: there's a side entrance into the keep.");
    }

    public static string Describe(string step) => step switch
    {
        CrisisOperations.ProbingRaid => "a probing raid",
        CrisisOperations.Breach => "a breach",
        CrisisOperations.PsychicAssault => "a psychic assault",
        CrisisOperations.Retrieval => "a retrieval strike for its captives",
        CrisisOperations.SiegeWithBombardment => "a siege",
        CrisisOperations.Decapitation => "a drop-pod strike on the colony's leader",
        CrisisOperations.FinalAssault => "the final assault",
        _ => step,
    };
}
