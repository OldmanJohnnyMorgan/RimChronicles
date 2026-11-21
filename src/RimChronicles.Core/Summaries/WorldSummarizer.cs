using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RimChronicles.Core.Snapshots;
using RimChronicles.Llm;

namespace RimChronicles.Core.Summaries;

/// <summary>The variables for one task's prompt, plus the names the output may use (for the grounding check).</summary>
public sealed record TaskPromptInput(IReadOnlyDictionary<string, string> Variables, IReadOnlySet<string> AllowedNames);

/// <summary>
/// Turns a snapshot into compact, stable prompt text for each task. Output order never depends on dictionary or
/// hash order, so the same snapshot always gives byte-identical prompts (diffable and cache-friendly).
/// </summary>
public static partial class WorldSummarizer
{
    /// <summary>The fixed task names, matching the prompt folders.</summary>
    public const string DirectorTask = "director";
    public const string NewsBulletinTask = "news-bulletin";
    public const string HandoffTask = "inheritance-handoff";

    /// <summary>Default reporter when a snapshot has none.</summary>
    public static readonly ReporterInfo DefaultReporter = new()
    {
        Name = "Ilse Varrow",
        Outlet = "the Rim Dispatch",
        Persona = "dry, wry and slightly cynical",
    };

    [GeneratedRegex(@"\bp\d+\b")]
    private static partial Regex PawnId();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"\p{Lu}[\p{L}'’\-]+")]
    private static partial Regex CapitalisedWord();

    /// <summary>Rough prompt budget per tier, in tokens (about 4 characters each).</summary>
    public static int TokenBudget(ModelTier tier) => tier switch
    {
        ModelTier.Minimum => 600,
        ModelTier.High => 2400,
        _ => 1200,
    };

    /// <summary>Variables: worldSummary, plotState, allowedTransitions.</summary>
    public static TaskPromptInput ForDirector(WorldSnapshot s, ModelTier tier)
    {
        var names = new Names(s);
        var worldSummary = FitToBudget(TokenBudget(tier), new Limits(s.Pawns.Count, s.Tales.Count, s.RecentEvents.Count, s.Factions.Count), limits => WorldSummary(s, names, limits));

        var plots = new StringBuilder();
        if (s.ActivePlots.Count == 0)
        {
            plots.Append("No active plots.");
        }

        foreach (var plot in s.ActivePlots)
        {
            plots.Append(CultureInfo.InvariantCulture,
                $"- {plot.Id} ({Words(plot.Template)}): beat {plot.Beat}, protagonist {names.Pawn(plot.Protagonist)}.");
            if (plot.Summary is { } summary)
            {
                plots.Append(' ').Append(names.Replace(summary));
            }

            plots.Append('\n');
        }

        var transitions = string.Join("\n", s.AllowedTransitions.Select(t => $"- {t}"));
        return Build(new()
        {
            ["worldSummary"] = worldSummary,
            ["plotState"] = plots.ToString().TrimEnd(),
            ["allowedTransitions"] = transitions,
        });
    }

    /// <summary>A quiet week has at most this many new items; its bulletin is short instead of padded.</summary>
    public const int QuietWeekMaxNewItems = 1;

    /// <summary>
    /// News the reporter hasn't covered yet: public events and plot hints not in <see cref="WorldSnapshot.ReportedEvents"/>.
    /// Counted before the digest is fitted to a token budget; the fit drops the oldest events first, which are the
    /// reported ones, so the digest's "heard since" list almost always has the same count.
    /// </summary>
    public static int NewNewsCount(WorldSnapshot s)
    {
        var reported = s.ReportedEvents.ToHashSet(StringComparer.Ordinal);
        return NewsItems(s, s.PublicEvents.Count).Count(e => !reported.Contains(e));
    }

    /// <summary>
    /// Allowed bulletin length in words, scaled to the news so a thin week is short instead of padded. The prompt
    /// asks for 40 to 80 words on a quiet week (one new item or none), 50 to 100 with two new items and 70 to 130
    /// with three or more; the range leaves some slack either side so a near miss isn't sent back.
    /// </summary>
    public static (int Min, int Max) BulletinWordRange(WorldSnapshot s) => NewNewsCount(s) switch
    {
        <= QuietWeekMaxNewItems => (30, 100),
        2 => (40, 120),
        _ => (60, 150),
    };

    /// <summary>
    /// Allowed handoff scene length in words. The prompt asks for 60 to 120; the range leaves some slack either side
    /// so a near miss isn't sent back.
    /// </summary>
    public static readonly (int Min, int Max) HandoffWordRange = (50, 130);

    private static List<string> NewsItems(WorldSnapshot s, int events) =>
        s.PublicEvents.TakeLast(events)
            .Concat(s.ActivePlots.Select(p => p.PublicHint).OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Variables: reporterName, outlet, persona, digest, previousBulletins.</summary>
    public static TaskPromptInput ForNewsBulletin(WorldSnapshot s, ModelTier tier)
    {
        var names = new Names(s);
        var reporter = s.Reporter ?? DefaultReporter;
        var digest = FitToBudget(TokenBudget(tier), new Limits(0, 0, s.PublicEvents.Count, s.Factions.Count), limits => NewsDigest(s, names, limits));

        var last = s.PreviousBulletins.TakeLast(2).ToList();
        var previous = last.Count == 0
            ? "None yet. This is your first bulletin."
            : string.Join("\n\n", last.Select((b, i) => $"{(i == last.Count - 1 ? "Latest" : "Earlier")} bulletin:\n{b}"));

        return Build(new()
        {
            ["reporterName"] = reporter.Name,
            ["outlet"] = reporter.Outlet,
            ["persona"] = reporter.Persona,
            ["digest"] = digest,
            ["previousBulletins"] = previous,
        });
    }

    /// <summary>Variables: deadDossier, heirDossier, relationship, plotSummary. Throws if the snapshot has no handoff.</summary>
    public static TaskPromptInput ForHandoff(WorldSnapshot s)
    {
        var handoff = s.Handoff ?? throw new InvalidOperationException($"Snapshot {s.Id} has no handoff case.");
        var names = new Names(s);
        var dead = names.PawnInfo(handoff.DeadPawn);
        var heir = names.PawnInfo(handoff.Heir);

        return Build(new()
        {
            ["deadDossier"] = Dossier(s, dead, names),
            ["heirDossier"] = Dossier(s, heir, names),
            ["relationship"] = $"{heir.Name} was {dead.Name}'s {handoff.Relation}.",
            ["plotSummary"] = names.Replace(handoff.PlotSummary),
        });
    }

    /// <summary>The director's legal options, for filling the choice enum in the schema.</summary>
    public static IReadOnlyList<string> DirectorChoices(WorldSnapshot s) => s.AllowedTransitions;

    private static TaskPromptInput Build(Dictionary<string, string> variables)
    {
        var allowed = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var text in variables.Values)
        {
            foreach (Match m in CapitalisedWord().Matches(text))
            {
                allowed.Add(m.Value);
            }
        }

        return new TaskPromptInput(variables, allowed);
    }

    /// <summary>List lengths used while shrinking a summary to its budget.</summary>
    private sealed record Limits(int Pawns, int Tales, int Events, int Factions);

    /// <summary>
    /// Renders with every list in full, then shortens the longest list by one item (never below two) until the text
    /// fits the budget. Every step shrinks a count, so the loop always ends.
    /// </summary>
    private static string FitToBudget(int tokenBudget, Limits full, Func<Limits, string> render)
    {
        const int minItems = 2;
        var maxChars = tokenBudget * 4;
        var limits = full;
        var text = render(limits);

        while (text.Length > maxChars)
        {
            var (p, t, e, f) = (limits.Pawns, limits.Tales, limits.Events, limits.Factions);
            Limits? next =
                t > minItems && t >= Math.Max(e, Math.Max(p, f)) ? limits with { Tales = t - 1 } :
                e > minItems && e >= Math.Max(p, f) ? limits with { Events = e - 1 } :
                p > minItems && p >= f ? limits with { Pawns = p - 1 } :
                f > minItems ? limits with { Factions = f - 1 } :
                t > minItems ? limits with { Tales = t - 1 } :
                e > minItems ? limits with { Events = e - 1 } :
                p > minItems ? limits with { Pawns = p - 1 } :
                null;
            if (next is null)
            {
                break;
            }

            limits = next;
            text = render(limits);
        }

        return text;
    }

    private static string WorldSummary(WorldSnapshot s, Names names, Limits limits)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Date: day {s.Day} ({s.Season} {s.Year}).\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"Colony: {s.Colony.Name}, {s.Colony.Population} people, wealth {s.Colony.Wealth:N0} silver, mood {s.Colony.Mood}.\n");

        // Plot protagonists first, then everyone else in snapshot order.
        var protagonists = s.ActivePlots.Select(p => p.Protagonist).ToHashSet(StringComparer.Ordinal);
        var pawns = s.Pawns.OrderBy(p => protagonists.Contains(p.Id) ? 0 : 1).Take(limits.Pawns).ToList();
        if (pawns.Count > 0)
        {
            sb.Append("\nPeople:\n");
            foreach (var p in pawns)
            {
                sb.Append("- ").Append(PawnLine(p, names)).Append('\n');
            }
        }

        AppendList(sb, "History:", s.Tales.TakeLast(limits.Tales).Select(t => TaleLine(t, names)));
        AppendList(sb, "Factions:", s.Factions.Take(limits.Factions).Select(FactionLine));
        AppendList(sb, "Recent events:", s.RecentEvents.TakeLast(limits.Events).Select(names.Replace));
        return sb.ToString().TrimEnd();
    }

    private static string NewsDigest(WorldSnapshot s, Names names, Limits limits)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Date: day {s.Day} ({s.Season} {s.Year}).\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"The colony you report near: {s.Colony.Name}. You do not know what happens inside it unless listed below.\n");

        // No settlement counts: they rarely change in a real game, and small models recited them on every quiet week
        // (Stage 2 M6). Real changes in a faction's size reach the reporter as news.
        AppendList(sb, "Factions:", s.Factions.Take(limits.Factions).Select(f =>
        {
            var details = new List<string>();
            if (f.AtWarWith.Count > 0)
            {
                details.Add($"at war with {string.Join(" and ", f.AtWarWith)}");
            }

            if (f.PlotState is { } state)
            {
                details.Add(EmpireRumour(state));
            }

            return $"{f.Name}" + (f.Kind is { } kind ? $" ({kind})" : "") + (details.Count > 0 ? $": {string.Join(", ", details)}" : "");
        }));

        // Split the news into what the reporter may lead with and what earlier bulletins already covered,
        // so a model is never told "don't repeat old news" while being handed only old news.
        var reported = s.ReportedEvents.ToHashSet(StringComparer.Ordinal);
        var items = NewsItems(s, limits.Events);
        var fresh = items.Where(e => !reported.Contains(e)).Select(names.Replace).ToList();
        var old = items.Where(reported.Contains).Select(names.Replace).ToList();

        if (fresh.Count == 0)
        {
            sb.Append("\nYou have heard nothing new since your last bulletin. It has been a quiet stretch.\n");
        }
        else
        {
            AppendList(sb, "Heard since your last bulletin (lead with these):", fresh);
        }

        AppendList(sb, "Your earlier bulletins already covered (old news, background only):", old);
        return sb.ToString().TrimEnd();
    }

    private static string Dossier(WorldSnapshot s, PawnInfo p, Names names)
    {
        var sb = new StringBuilder(PawnLine(p, names));
        var tales = s.Tales.Where(t => t.Actor == p.Id || t.Victim == p.Id).Select(t => TaleLine(t, names)).ToList();
        if (tales.Count > 0)
        {
            sb.Append("\nHistory:\n").Append(string.Join("\n", tales.Select(t => $"- {t}")));
        }

        return sb.ToString();
    }

    private static string PawnLine(PawnInfo p, Names names)
    {
        var parts = new List<string> { $"{p.Name} ({p.Age})" };
        if (p.Status != "alive")
        {
            parts[0] += $", {p.Status}";
        }

        if (p.Traits.Count > 0)
        {
            parts.Add($"traits: {string.Join(", ", p.Traits)}");
        }

        if (p.Backstory is { } backstory)
        {
            parts.Add($"backstory: {backstory}");
        }

        if (p.Relations.Count > 0)
        {
            parts.Add(string.Join(", ", p.Relations.Select(names.Replace)));
        }

        if (p.IdeoRole is { } role)
        {
            parts.Add($"role: {role}");
        }

        if (p.Note is { } note)
        {
            parts.Add(names.Replace(note));
        }

        return string.Join("; ", parts) + ".";
    }

    private static string TaleLine(TaleInfo t, Names names)
    {
        var actor = names.Pawn(t.Actor);
        var victim = t.Victim is null ? null : names.Pawn(t.Victim);
        var what = t.Type switch
        {
            "KilledColonist" or "KilledVisitor" or "KilledBy" => $"{actor} killed {victim}",
            "MaimedColonist" => $"{actor} maimed {victim}",
            "KidnappedColonist" => $"{actor} kidnapped {victim}",
            "Rescued" => $"{actor} rescued {victim}",
            "SocialFight" => $"{actor} fought {victim}",
            "Marriage" => $"{actor} married {victim}",
            "Died" => $"{actor} died",
            "Recruited" => $"{actor} recruited {victim}",
            "TamedAnimal" => $"{actor} tamed {victim}",
            "RaidRepelled" => $"{actor} repelled a raid by {victim}",
            "TradeCaravan" => $"{actor} led a trade caravan to {victim}",
            "CaravanLost" => $"{actor} lost a caravan led by {victim}",
            "CrashedInEscapePod" => $"{actor} was founded by crash survivors",
            "WentOnBerserk" => $"{actor} went berserk",
            "ColdSnap" => $"{actor} was hit by a cold snap",
            "CraftedMasterwork" => $"{actor} made a masterwork: {victim}",
            "LetterFromKin" => $"{victim} received a letter from {actor}",
            "RefusedTitle" => $"{actor} refused a title from {victim}",
            "TitleGranted" => $"{actor} granted a title to {victim}",
            "EnvoyVisited" => $"an envoy of {actor} visited {victim}",
            "PrisonerReleased" => $"{actor} released the prisoner {victim}",
            "DestroyedImperialSite" => $"{actor} destroyed {victim}",
            "SettlementConquered" => $"{actor} conquered {victim}",
            "SettlementRazed" => $"{actor} razed {victim}",
            _ => victim is null ? $"{actor}: {Words(t.Type).ToLowerInvariant()}" : $"{actor} - {Words(t.Type).ToLowerInvariant()} - {victim}",
        };

        var escaped = t.Escaped == true ? " and escaped" : "";
        return $"Day {t.Day}: {what}{escaped}.";
    }

    private static string FactionLine(FactionInfo f)
    {
        var line = $"{f.Name}" + (f.Kind is { } kind ? $" ({kind})" : "") +
                   $": goodwill {f.Goodwill.ToString("+0;-0;0", CultureInfo.InvariantCulture)}, {f.Settlements} settlement{(f.Settlements == 1 ? "" : "s")}";
        if (f.AtWarWith.Count > 0)
        {
            line += $", at war with {string.Join(" and ", f.AtWarWith)}";
        }

        if (f.PlotState is { } state)
        {
            line += $", Empire state: {state}";
        }

        return line + ".";
    }

    /// <summary>What a reporter can say about the Empire's state from the outside.</summary>
    private static string EmpireRumour(string state) => state switch
    {
        "Dormant" => "quiet behind its walls",
        "Stirring" => "sending envoys and making demands of its neighbours",
        "Awakened" => "expanding by force",
        "Ascendant" => "controlling most of the region and issuing final demands",
        "Broken" => "collapsing after heavy losses",
        _ => $"state: {state}",
    };

    private static void AppendList(StringBuilder sb, string heading, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
        {
            return;
        }

        sb.Append('\n').Append(heading).Append('\n');
        foreach (var line in list)
        {
            sb.Append("- ").Append(line).Append('\n');
        }
    }

    private static string Words(string camel) => CamelBoundary().Replace(camel, " ");

    /// <summary>Resolves pawn ids (p3) to names.</summary>
    private sealed class Names(WorldSnapshot s)
    {
        private readonly Dictionary<string, PawnInfo> _pawns = s.Pawns.ToDictionary(p => p.Id, StringComparer.Ordinal);

        public string Pawn(string idOrName) =>
            idOrName == "colony" ? s.Colony.Name : _pawns.TryGetValue(idOrName, out var p) ? p.Name : idOrName;

        public PawnInfo PawnInfo(string id) =>
            _pawns.TryGetValue(id, out var p) ? p : throw new InvalidOperationException($"Snapshot {s.Id} has no pawn '{id}'.");

        public string Replace(string text) => PawnId().Replace(text, m => Pawn(m.Value));
    }
}
