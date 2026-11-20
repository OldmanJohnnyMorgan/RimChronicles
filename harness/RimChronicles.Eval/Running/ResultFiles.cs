using System.Globalization;
using System.Text;

namespace RimChronicles.Eval.Running;

/// <summary>Writes results.csv: one row per cell.</summary>
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
}

/// <summary>RFC 4180 CSV helpers with invariant number formatting.</summary>
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
}
