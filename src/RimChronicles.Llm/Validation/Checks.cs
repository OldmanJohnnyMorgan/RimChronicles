using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RimChronicles.Llm.Validation;

/// <summary>The output must be JSON matching a schema.</summary>
public sealed class JsonSchemaCheck(JsonObject schema) : IOutputCheck
{
    public string Name => "json-schema";

    public CheckResult Check(string output)
    {
        var node = JsonText.TryParse(output, out var error);
        return error is not null ? CheckResult.Fail(error) : CheckResult.From(JsonSchemaValidator.Validate(node, schema));
    }
}

/// <summary>A string field of the JSON output must be one of the allowed options (e.g. the director's "choice").</summary>
public sealed class AllowedChoiceCheck(string field, IReadOnlyCollection<string> allowed) : IOutputCheck
{
    public string Name => "allowed-choice";

    public CheckResult Check(string output)
    {
        var node = JsonText.TryParse(output, out var error);
        if (error is not null)
        {
            return CheckResult.Fail(error);
        }

        var value = node is JsonObject obj && obj[field] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
        return value is not null && allowed.Contains(value)
            ? CheckResult.Pass
            : CheckResult.Fail($"\"{field}\" must be exactly one of: {string.Join(", ", allowed)}");
    }
}

/// <summary>The word count must be inside a range.</summary>
public sealed partial class WordCountCheck(int min, int max) : IOutputCheck
{
    [GeneratedRegex(@"\S+")]
    private static partial Regex Word();

    public string Name => "word-count";

    public static int Count(string text) => Word().Count(text);

    public CheckResult Check(string output)
    {
        // The message is the retry feedback, so it says what to change, not just the numbers.
        var count = Count(output);
        return count < min ? CheckResult.Fail($"Too short: {count} words, but at least {min} are needed. Add a sentence or two to each paragraph.")
            : count > max ? CheckResult.Fail($"Too long: {count} words, but at most {max} are allowed. Cut a sentence or two from each paragraph.")
            : CheckResult.Pass;
    }
}

/// <summary>
/// Cheap grounding heuristic: every capitalised word that isn't the first word of a sentence must come from
/// the allowed names (the digest's people, places and factions) or the stock vocabulary. Words at the start of a
/// sentence are skipped, so this misses some invented names; the harness judge is the real fact check.
/// </summary>
public sealed partial class GroundingCheck : IOutputCheck
{
    [GeneratedRegex(@"(?<start>^|[.!?:;""“”]\s+|\n\s*)?(?<word>\p{Lu}[\p{L}'’\-]*)")]
    private static partial Regex CapitalisedWord();

    [GeneratedRegex(@"^I['’](m|ve|d|ll)$")]
    private static partial Regex IContraction();

    private readonly HashSet<string> _allowedWords;

    public GroundingCheck(IEnumerable<string> allowedNames, IEnumerable<string>? stockVocabulary = null)
    {
        _allowedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var phrase in allowedNames.Concat(stockVocabulary ?? DefaultStockVocabulary))
        {
            foreach (var word in phrase.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries))
            {
                _allowedWords.Add(Normalise(word));
            }
        }
    }

    /// <summary>Words that may appear capitalised in any bulletin: calendar, titles and common in-world terms.</summary>
    public static readonly IReadOnlyList<string> DefaultStockVocabulary =
    [
        "I", "Rim", "Dispatch", "Aprimay", "Jugust", "Septober", "Decembary",
        "Spring", "Summer", "Fall", "Winter", "Empire", "Imperial", "Imperials",
        "Lord", "Lady", "Sir", "Captain", "Commander", "Marshal", "Sergeant", "Knight", "Count", "Baron", "Duke", "Stellarch",
        "News", "Week", "Day", "Quadrum",
    ];

    public string Name => "grounding";

    public IReadOnlyList<string> FindUnknownNames(string output) =>
        CapitalisedWord().Matches(output)
            .Where(m => !m.Groups["start"].Success)
            .Select(m => Normalise(m.Groups["word"].Value))
            .Where(w => w.Length > 1 && !IsAllowed(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // A hyphenated word is allowed when each capitalised part is: "Iron-willed" matches the trait "Iron-willed",
    // which the summarizer records as "Iron".
    private bool IsAllowed(string word) =>
        _allowedWords.Contains(word) ||
        (word.Contains('-') && word.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => char.IsUpper(part[0]))
            .All(part => _allowedWords.Contains(Normalise(part))));

    public CheckResult Check(string output)
    {
        var unknown = FindUnknownNames(output);
        return unknown.Count == 0
            ? CheckResult.Pass
            : CheckResult.Fail($"Do not mention names that are not in the briefing: {string.Join(", ", unknown)}");
    }

    private static string Normalise(string word)
    {
        var trimmed = word.Trim('\'', '’', '‘', '"', '“', '”', '-', ',', '.', '(', ')');
        if (IContraction().IsMatch(trimmed))
        {
            return "I"; // "I'm", "I've", "I'd", "I'll"
        }

        foreach (var possessive in (string[])["'s", "’s"])
        {
            if (trimmed.EndsWith(possessive, StringComparison.Ordinal))
            {
                trimmed = trimmed[..^possessive.Length];
            }
        }

        return trimmed;
    }
}

/// <summary>The output must not contain meta-text that breaks the fiction.</summary>
public sealed class BannedPhraseCheck(IEnumerable<string>? phrases = null) : IOutputCheck
{
    public static readonly IReadOnlyList<string> DefaultPhrases =
    [
        "as an ai", "language model", "here is the", "here's the", "i hope this", "certainly!", "sure!",
        "word count", "bulletin:", "as requested",
        // Briefing headings a model may copy into its bulletin.
        "heard since your last bulletin", "already covered (old news", "news since your last bulletin", "already reported:",
    ];

    private readonly IReadOnlyList<string> _phrases = (phrases ?? DefaultPhrases).ToList();

    public string Name => "banned-phrase";

    public CheckResult Check(string output)
    {
        var lower = output.ToLowerInvariant();
        var found = _phrases.Where(p => lower.Contains(p, StringComparison.Ordinal)).ToList();
        return found.Count == 0
            ? CheckResult.Pass
            : CheckResult.Fail($"Write only the in-world text, without phrases like: {string.Join(", ", found.Select(p => $"\"{p}\""))}");
    }
}

/// <summary>
/// Fails output that repeats an earlier text word for word: any run of <paramref name="minRun"/> or more words found in
/// one of <paramref name="earlier"/>. Words are compared lower-case, with punctuation stripped. Small models reuse
/// whole sentences across quiet weeks even when told not to (Stage 2 M6).
/// </summary>
public sealed partial class RepetitionCheck(IEnumerable<string> earlier, int minRun = 8) : IOutputCheck
{
    [GeneratedRegex(@"[\p{L}\p{N}'’]+")]
    private static partial Regex Word();

    private readonly List<string[]> _earlier = earlier.Select(Words).Where(w => w.Length >= minRun).ToList();

    public string Name => "repetition";

    public CheckResult Check(string output)
    {
        if (_earlier.Count == 0)
        {
            return CheckResult.Pass;
        }

        var runs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var words in _earlier)
        {
            for (var i = 0; i + minRun <= words.Length; i++)
            {
                runs.Add(string.Join(' ', words, i, minRun));
            }
        }

        var mine = Words(output);
        for (var i = 0; i + minRun <= mine.Length; i++)
        {
            var run = string.Join(' ', mine, i, minRun);
            if (runs.Contains(run))
            {
                return CheckResult.Fail($"This repeats your earlier bulletin word for word (\"{run}…\"). Say it in new words, with a new joke.");
            }
        }

        return CheckResult.Pass;
    }

    private static string[] Words(string text) =>
        Word().Matches(text ?? "").Select(m => m.Value.ToLowerInvariant().Replace('’', '\'')).ToArray();
}
