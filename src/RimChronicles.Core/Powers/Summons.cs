using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Powers;

/// <summary>What kind of pawn a summons takes: the kind the colony's side can't keep (War in Heaven spec §6.1).</summary>
public static class SummonsKind
{
    /// <summary>A pawn with a mechlink: the Empire takes them and returns a psycaster.</summary>
    public const string Mechanitor = "mechanitor";

    /// <summary>A pawn with a psylink: the Hollow Choir takes them and returns a mechanitor.</summary>
    public const string Psycaster = "psycaster";

    /// <summary>The kind <paramref name="side"/> summons: the Empire can't keep mechanitors, the Choir psycasters.</summary>
    public static string? TakenBy(Power side) => side == Power.Empire ? Mechanitor : side == Power.Choir ? Psycaster : null;
}

/// <summary>Where a summons stands.</summary>
public static class SummonsStatus
{
    /// <summary>The shuttle is coming, or the pawn is away.</summary>
    public const string Pending = "pending";

    /// <summary>The pawn came back converted.</summary>
    public const string Returned = "returned";

    /// <summary>The colony refused: the shuttle left without the pawn, or the offer was declined (independence).</summary>
    public const string Refused = "refused";

    /// <summary>It ended another way (the pawn died, the shuttle was destroyed, the side changed).</summary>
    public const string Ended = "ended";
}

/// <summary>One summons: who, by which power, its quest, and how it went.</summary>
public sealed class SummonsRecord : IArchivable
{
    public string PawnId = "";
    public string Name = "";
    public string Power = "";
    public int Day;
    public int QuestId = -1;
    public string Status = SummonsStatus.Pending;

    public void Expose(IArchive archive)
    {
        archive.Value(ref PawnId, "pawnId", "");
        archive.Value(ref Name, "name", "");
        archive.Value(ref Power, "power", "");
        archive.Value(ref Day, "day");
        archive.Value(ref QuestId, "questId", -1);
        archive.Value(ref Status, "status", SummonsStatus.Pending);
        PawnId ??= "";
        Name ??= "";
        Power ??= "";
        Status ??= SummonsStatus.Pending;
    }
}

/// <summary>A colonist the game reads for the summons: their id, name, and whether they have a mechlink or psylink.</summary>
public sealed record SummonsCandidate(string Id, string Name, bool Mechanitor, bool Psycaster);

/// <summary>
/// The summons (War in Heaven spec §6.1, tech spec §5): while the colony stands with one power, that power sends a
/// shuttle for each colonist of the kind it can't keep, one at a time, and each colonist only once per side. Refusing
/// sets the colony independent. Plain decisions; the game offers the quest and reports how it went. Saved with the game.
/// </summary>
public sealed class SummonsBook : IArchivable
{
    /// <summary>Days the summoned pawn is away (spec §6.1).</summary>
    public const int DaysAway = 5;

    public List<SummonsRecord> Records = [];

    public SummonsRecord? Pending(Power power) => Records.FirstOrDefault(r => r.Power == power.Id && r.Status == SummonsStatus.Pending);

    /// <summary>The colonist <paramref name="side"/> should summon next, or null: none while one of its summons is
    /// pending, and never one it has summoned before.</summary>
    public SummonsCandidate? Next(Power side, IEnumerable<SummonsCandidate> colonists)
    {
        if (SummonsKind.TakenBy(side) is not { } kind || Pending(side) is not null)
        {
            return null;
        }

        return colonists
            .Where(c => kind == SummonsKind.Mechanitor ? c.Mechanitor : c.Psycaster)
            .Where(c => !Records.Any(r => r.Power == side.Id && r.PawnId == c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public SummonsRecord Start(Power side, SummonsCandidate pawn, int questId, int day)
    {
        var record = new SummonsRecord { PawnId = pawn.Id, Name = pawn.Name, Power = side.Id, Day = day, QuestId = questId };
        Records.Add(record);
        return record;
    }

    /// <summary>Records how a pending summons ended. Returns true when it was refused, which sets the colony
    /// independent.</summary>
    public bool Finish(SummonsRecord record, string status)
    {
        record.Status = status;
        return status == SummonsStatus.Refused;
    }

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref Records, "records");
        Records ??= [];
    }
}
