using System.Globalization;
using System.Text;

namespace RimChronicles.Eval.Running;

/// <summary>Writes and reads results.csv: one row per cell.</summary>
public static class ResultFiles
{
    private static readonly string[] Header =
    [
        "run_id", "model", "tier", "task", "snapshot", "grammar", "repeat", "prompt_version", "outcome", "attempts",
        "first_ok", "first_error", "passed_first", "json_valid", "choice_allowed", "word_count", "length_ok",
        "unknown_name_count", "unknown_names", "banned_phrase", "latency_s", "total_latency_s",
        "input_tokens", "output_tokens", "reasoning_tokens", "raw_file",
    ];

    public static void WriteResultsCsv(string path, IEnumerable<CellResult> rows)
    {
        var sb = new StringBuilder();
        Csv.AppendRow(sb, Header);
        foreach (var r in rows)
        {
            Csv.AppendRow(sb,
            [
                r.RunId, r.Model, r.Tier, r.Task, r.Snapshot, r.Grammar, Csv.Num(r.Repeat), r.PromptVersion, r.Outcome,
                Csv.Num(r.Attempts), Csv.Bool(r.FirstOk), r.FirstError, Csv.Bool(r.PassedFirst), Csv.Bool(r.JsonValid),
                Csv.Bool(r.ChoiceAllowed), Csv.Num(r.WordCount), Csv.Bool(r.LengthOk), Csv.Num(r.UnknownNameCount),
                r.UnknownNames, Csv.Bool(r.BannedPhrase), Csv.Num(r.LatencySeconds), Csv.Num(r.TotalLatencySeconds),
                Csv.Num(r.InputTokens), Csv.Num(r.OutputTokens), Csv.Num(r.ReasoningTokens), r.RawFile,
            ]);
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Reads results.csv back. The output text is not in the CSV; read it from the raw file when needed.</summary>
    public static IReadOnlyList<CellResult> ReadResultsCsv(string path)
    {
        var table = Csv.ReadTable(File.ReadAllText(path));
        return table.Select(r => new CellResult
        {
            RunId = r["run_id"],
            Model = r["model"],
            Tier = r["tier"],
            Task = r["task"],
            Snapshot = r["snapshot"],
            Grammar = r["grammar"],
            Repeat = Csv.ParseInt(r["repeat"]) ?? 0,
            PromptVersion = r["prompt_version"],
            Outcome = r["outcome"],
            Attempts = Csv.ParseInt(r["attempts"]) ?? 0,
            FirstOk = Csv.ParseBool(r["first_ok"]) ?? false,
            FirstError = Csv.NullIfEmpty(r["first_error"]),
            PassedFirst = Csv.ParseBool(r["passed_first"]) ?? false,
            JsonValid = Csv.ParseBool(r["json_valid"]),
            ChoiceAllowed = Csv.ParseBool(r["choice_allowed"]),
            WordCount = Csv.ParseInt(r["word_count"]),
            LengthOk = Csv.ParseBool(r["length_ok"]),
            UnknownNameCount = Csv.ParseInt(r["unknown_name_count"]),
            UnknownNames = r["unknown_names"],
            BannedPhrase = Csv.ParseBool(r["banned_phrase"]),
            LatencySeconds = Csv.ParseDouble(r["latency_s"]),
            TotalLatencySeconds = Csv.ParseDouble(r["total_latency_s"]) ?? 0,
            InputTokens = Csv.ParseInt(r["input_tokens"]) ?? 0,
            OutputTokens = Csv.ParseInt(r["output_tokens"]) ?? 0,
            ReasoningTokens = Csv.ParseInt(r["reasoning_tokens"]),
            RawFile = r["raw_file"],
        }).ToList();
    }
}

/// <summary>RFC 4180 CSV helpers with invariant number formatting.</summary>
/// <summary>Paths inside a run folder.</summary>
public static class RunPaths
{
    /// <summary>A results.csv raw_file under the run folder. Older Windows runs wrote '\' separators; both work everywhere.</summary>
    public static string Raw(string runFolder, string rawFile) =>
        Path.Combine(runFolder, rawFile.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
}

public static class Csv
{
    public static void AppendRow(StringBuilder sb, IEnumerable<string?> fields)
    {
        sb.AppendJoin(',', fields.Select(Escape)).Append('\n');
    }

    public static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        return field.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : field;
    }

    public static string Bool(bool? b) => b switch { true => "1", false => "0", null => "" };

    public static string Num(int? n) => n?.ToString(CultureInfo.InvariantCulture) ?? "";

    public static string Num(double? d) => d?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";

    public static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    public static bool? ParseBool(string s) => s switch { "1" => true, "0" => false, _ => null };

    public static int? ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    public static double? ParseDouble(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Parses RFC 4180 text (quoted fields may hold commas, quotes and newlines) into rows of fields.</summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Parses CSV with a header row into dictionaries keyed by column name. Missing cells read as "".</summary>
    public static List<Dictionary<string, string>> ReadTable(string text)
    {
        var rows = Parse(text);
        if (rows.Count == 0)
        {
            return [];
        }

        var header = rows[0];
        return rows.Skip(1)
            .Where(r => r.Count > 1 || r is [{ Length: > 0 }])
            .Select(r => header.Select((h, i) => (h, v: i < r.Count ? r[i] : "")).ToDictionary(x => x.h, x => x.v, StringComparer.Ordinal))
            .ToList();
    }
}
