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
        if (Reporter.Status != ReporterStatus.Active)
        {
            return DeskAction.None; // held, silenced or awaiting a replacement: no news
        }

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
        if (Reporter.ColdBulletinsLeft > 0)
        {
            Reporter.ColdBulletinsLeft--;
        }

        return bulletin;
    }

    /// <summary>How long a bleed-out may follow the colony's attack and still count as the colony's kill.</summary>
    public const int ColonyHarmWindowTicks = 2 * 60000;

    public const int MinVisitGapDays = 15;
    public const int MaxVisitGapDays = 30;
    public const int MinReplaceDays = 5;
    public const int MaxReplaceDays = 10;

    /// <summary>A new reporter takes over: active, with a first visit <paramref name="visitInDays"/> from now.</summary>
    public void NewReporter(string name, string faction, int today, int visitInDays)
    {
        Reporter.Name = name;
        Reporter.Faction = faction;
        Reporter.Status = ReporterStatus.Active;
        Reporter.ReplaceDay = -1;
        Reporter.LastColonyHarmTick = -1;
        Reporter.NextVisitDay = today + visitInDays;
    }

    /// <summary>True when an active reporter's visit is due today.</summary>
    public bool VisitDue(int today) =>
        Reporter.Status == ReporterStatus.Active && Reporter.NextVisitDay >= 0 && today >= Reporter.NextVisitDay;

    public void ScheduleVisit(int today, int inDays) => Reporter.NextVisitDay = today + inDays;

    /// <summary>True when an outlet's replacement reporter is due.</summary>
    public bool ReplacementDue(int today) => Reporter.Status == ReporterStatus.Replacing && today >= Reporter.ReplaceDay;

    /// <summary>Whether a death with this cause was the colony's doing. <paramref name="killerIsColony"/> is null when
    /// the game gave no killer (bleeding out, disease).</summary>
    public bool KilledByColony(bool? killerIsColony, bool heldByColony, bool recruitedByColony, int nowTick) =>
        killerIsColony == true || heldByColony || recruitedByColony ||
        (killerIsColony is null && Reporter.LastColonyHarmTick >= 0 && nowTick - Reporter.LastColonyHarmTick <= ColonyHarmWindowTicks);

    /// <summary>The colony killed or recruited the reporter: the news stops, with no letter and no notice. The waiting
    /// bulletin is dropped.</summary>
    public void Silence(int today)
    {
        if (Reporter.Status == ReporterStatus.Silenced)
        {
            return;
        }

        Reporter.Status = ReporterStatus.Silenced;
        Reporter.SilencedDay = today;
        Reporter.NextVisitDay = -1;
        Pending = null;
    }

    /// <summary>The reporter died some other way: a replacement comes in <paramref name="replaceInDays"/> days.</summary>
    public void ReporterLost(int today, int replaceInDays)
    {
        if (Reporter.Status is ReporterStatus.Silenced or ReporterStatus.Replacing)
        {
            return;
        }

        Reporter.Status = ReporterStatus.Replacing;
        Reporter.ReplaceDay = today + replaceInDays;
        Reporter.NextVisitDay = -1;
        Pending = null;
    }

    /// <summary>Silent Press reparations paid: the outlet sends a new reporter in <paramref name="inDays"/> days, whose
    /// first <paramref name="coldBulletins"/> bulletins are cold toward the colony.</summary>
    public void Reinstate(int today, int inDays, int coldBulletins, string coldPersona)
    {
        if (Reporter.Status != ReporterStatus.Silenced)
        {
            return;
        }

        Reporter.Status = ReporterStatus.Replacing;
        Reporter.ReplaceDay = today + inDays;
        Reporter.ColdBulletinsLeft = coldBulletins;
        Reporter.ColdPersona = coldPersona;
    }

    /// <summary>The colony took the reporter prisoner: the news pauses.</summary>
    public void Held()
    {
        if (Reporter.Status == ReporterStatus.Active)
        {
            Reporter.Status = ReporterStatus.Held;
        }
    }

    /// <summary>The reporter is free again: the news resumes, and anything overdue goes out next.</summary>
    public void Freed()
    {
        if (Reporter.Status == ReporterStatus.Held)
        {
            Reporter.Status = ReporterStatus.Active;
        }
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
