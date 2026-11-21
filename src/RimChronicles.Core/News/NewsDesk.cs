using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;

namespace RimChronicles.Core.News;

/// <summary>What the game should do for the news today.</summary>
public enum DeskAction
{
    None,

    /// <summary>Start writing the next bulletin (on a background task).</summary>
    Generate,

    /// <summary>Send the waiting bulletin to the player.</summary>
    Deliver,
}

/// <summary>
/// The news schedule and archive, saved with the game. The next bulletin is written <see cref="LeadDays"/> days before
/// it is due and kept as <see cref="Pending"/>, so a reload never asks the model again for a bulletin it already wrote;
/// it is delivered on its due day and then never changes.
/// </summary>
public sealed class NewsDesk : IArchivable
{
    public const int LeadDays = 2;

    /// <summary>Old news kept so the reporter doesn't repeat it as new.</summary>
    public const int MaxReported = 60;

    /// <summary>News older than this is left out of a bulletin's digest.</summary>
    public const int NewsWindowDays = 15;

    public ReporterState Reporter = new();

    /// <summary>The day the next bulletin is due, or -1 before the first schedule.</summary>
    public int NextDueDay = -1;

    /// <summary>The next bulletin, written and waiting for its due day; null if not written yet.</summary>
    public Bulletin? Pending;

    public List<Bulletin> Delivered = [];

    /// <summary>News texts earlier bulletins covered, oldest first.</summary>
    public List<string> Reported = [];

    /// <summary>Decides today's action. <paramref name="writing"/> is true while a bulletin is being written (not saved:
    /// after a reload the write simply starts again).</summary>
    public DeskAction Plan(int today, int cadenceDays, bool writing)
    {
        if (NextDueDay < 0)
        {
            NextDueDay = today + cadenceDays;
        }

        if (Pending is not null)
        {
            return today >= NextDueDay ? DeskAction.Deliver : DeskAction.None;
        }

        return !writing && today >= WriteDay ? DeskAction.Generate : DeskAction.None;
    }

    /// <summary>The day writing starts: <see cref="LeadDays"/> before the due day, but never before the last delivery.</summary>
    public int WriteDay => Math.Max(NextDueDay - LeadDays, Delivered.Count == 0 ? 0 : Delivered[^1].DeliveredDay);

    /// <summary>Brings the next bulletin forward to today (Dev Mode "Force bulletin").</summary>
    public void DueNow(int today)
    {
        NextDueDay = today;
    }

    /// <summary>Fills in the parts of a snapshot the bulletin task reads: reporter, world news, old news and the
    /// last two bulletins. Returns the news items that are new, which the bulletin will cover.</summary>
    public (WorldSnapshot Snapshot, List<string> Fresh) PrepareSnapshot(WorldSnapshot world, NewsLog news)
    {
        var publicEvents = news.Since(world.Day - NewsWindowDays).Select(e => e.Text).Distinct(StringComparer.Ordinal).ToList();
        var reported = Reported.ToHashSet(StringComparer.Ordinal);
        var snapshot = world with
        {
            Reporter = Reporter.ToInfo(),
            PublicEvents = publicEvents,
            ReportedEvents = publicEvents.Where(reported.Contains).ToList(),
            PreviousBulletins = Delivered.TakeLast(2).Select(b => b.Text).ToList(),
        };
        return (snapshot, publicEvents.Where(e => !reported.Contains(e)).ToList());
    }

    /// <summary>Stores a written bulletin for its due day. Ignored if one is already waiting (a stale result).</summary>
    public bool SetPending(Bulletin bulletin)
    {
        if (Pending is not null)
        {
            return false;
        }

        bulletin.DueDay = NextDueDay;
        Pending = bulletin;
        return true;
    }

    /// <summary>Delivers the waiting bulletin and schedules the next one. Returns it, or null if none was waiting.</summary>
    public Bulletin? Deliver(int today, int cadenceDays)
    {
        if (Pending is not { } bulletin)
        {
            return null;
        }

        bulletin.DeliveredDay = today;
        Delivered.Add(bulletin);
        Pending = null;
        Reported.AddRange(bulletin.NewsCovered.Where(n => !Reported.Contains(n)));
        if (Reported.Count > MaxReported)
        {
            Reported.RemoveRange(0, Reported.Count - MaxReported);
        }

        NextDueDay = today + cadenceDays;
        return bulletin;
    }

    public void Expose(IArchive archive)
    {
        archive.Deep(ref Reporter, "reporter");
        archive.Value(ref NextDueDay, "nextDueDay", -1);
        var pending = Pending is null ? [] : new List<Bulletin> { Pending };
        archive.DeepList(ref pending, "pending");
        archive.DeepList(ref Delivered, "delivered");
        archive.Strings(ref Reported, "reported");
        if (archive.Mode == ArchiveMode.Loading)
        {
            Pending = pending.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Text));
        }

        Reporter ??= new ReporterState();
        Delivered ??= [];
        Reported ??= [];
    }
}
