namespace RimChronicles.Core.News;

/// <summary>Template bulletins, used when the model is off, unreachable or fails its checks twice (rule 7). Plain and
/// short: they repeat the news as logged, with a few rotating openings and closings so they don't read identically.</summary>
public static class BulletinTemplates
{
    private static readonly string[] Openings =
    [
        "This is {reporter} for {outlet}.",
        "{reporter} here, with the news from {outlet}.",
        "From the wires of {outlet}, this is {reporter}.",
    ];

    private static readonly string[] QuietLines =
    [
        "Nothing new has reached the wires since the last bulletin. The roads are quiet, and the traders have little to say.",
        "A quiet stretch: no news worth the name has come in. Travellers report nothing out of the ordinary.",
        "The wires have been silent this week. Quiet weeks are rare on the rim, so enjoy it while it lasts.",
    ];

    private static readonly string[] LeadIns =
    [
        "Here is what has reached the wires since the last bulletin.",
        "A few things have come in since we last spoke.",
        "The traders and travellers have brought news since the last bulletin.",
    ];

    private static readonly string[] Closings =
    [
        "More when there is more to tell.",
        "Stay safe out there.",
        "That's the news. Keep your walls high.",
    ];

    /// <summary>Writes a bulletin from the news items. <paramref name="day"/> picks the variant, so the same day
    /// always gives the same text.</summary>
    public static string Compose(ReporterState reporter, IReadOnlyList<string> fresh, int day)
    {
        var pick = Math.Abs(day);
        var opening = Openings[pick % Openings.Length]
            .Replace("{reporter}", reporter.Name, StringComparison.Ordinal)
            .Replace("{outlet}", reporter.Outlet, StringComparison.Ordinal);
        var body = fresh.Count == 0
            ? QuietLines[pick % QuietLines.Length]
            : LeadIns[pick % LeadIns.Length] + " " + string.Join(" ", fresh.Take(3).Select(Sentence));
        return $"{Capitalise(opening)} {body} {Closings[pick % Closings.Length]} Until next time, this has been {reporter.Name}.";
    }

    private static string Sentence(string news)
    {
        var text = news.Trim();
        return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? Capitalise(text) : Capitalise(text) + ".";
    }

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
