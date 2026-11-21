using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.News;

/// <summary>Kinds of world news, used to pick templates and priorities.</summary>
public static class NewsKind
{
    public const string ConditionStarted = "ConditionStarted";
    public const string ConditionEnded = "ConditionEnded";
    public const string RelationChanged = "RelationChanged";
    public const string SettlementDestroyed = "SettlementDestroyed";
    public const string FactionDefeated = "FactionDefeated";
    public const string LeaderDied = "LeaderDied";
    public const string ReporterDied = "ReporterDied";
}

/// <summary>One piece of public world news: something the reporter could plausibly hear about. The text is a plain
/// sentence of fact in the style of the hand-written snapshots' public events, without the day.</summary>
public sealed class NewsEvent : IArchivable
{
    public int Day;
    public string Kind = "";
    public string Text = "";

    /// <summary>The faction the news is about, if any (for templates and priorities).</summary>
    public string Faction = "";

    public NewsEvent()
    {
    }

    public NewsEvent(int day, string kind, string text, string faction = "")
    {
        Day = day;
        Kind = kind;
        Text = text;
        Faction = faction;
    }

    public void Expose(IArchive archive)
    {
        archive.Value(ref Day, "day");
        archive.Value(ref Kind, "kind", "");
        archive.Value(ref Text, "text", "");
        archive.Value(ref Faction, "faction", "");
        Kind ??= "";
        Text ??= "";
        Faction ??= "";
    }

    public override string ToString() => $"day {Day}: {Text}";
}
