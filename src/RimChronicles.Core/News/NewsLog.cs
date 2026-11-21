using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.News;

/// <summary>World news collected during play, saved with the game. The news pipeline (Stage 2 M3) reads it to build
/// each bulletin's digest; the snapshot exporter puts it in <c>publicEvents</c>.</summary>
public sealed class NewsLog : IArchivable
{
    /// <summary>News older than this many days is dropped, so the save stays small.</summary>
    public const int KeepDays = 60;

    public List<NewsEvent> Events = [];

    /// <summary>Adds news unless the same text was already logged that day. Returns whether it was added.</summary>
    public bool Add(NewsEvent news)
    {
        if (string.IsNullOrWhiteSpace(news.Text))
        {
            return false;
        }

        if (Events.Any(e => e.Day == news.Day && e.Text == news.Text))
        {
            return false;
        }

        Events.Add(news);
        return true;
    }

    /// <summary>Drops news older than <see cref="KeepDays"/> before <paramref name="today"/>.</summary>
    public int Prune(int today) => Events.RemoveAll(e => e.Day < today - KeepDays);

    /// <summary>News from <paramref name="fromDay"/> on, oldest first.</summary>
    public IReadOnlyList<NewsEvent> Since(int fromDay) => Events.Where(e => e.Day >= fromDay).OrderBy(e => e.Day).ToList();

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref Events, "events");
        Events.RemoveAll(e => string.IsNullOrWhiteSpace(e.Text));
    }
}
