using System.Globalization;
using System.Text;

namespace RimChronicles.Eval.Running;

/// <summary>Aggregates per model, task and grammar mode.</summary>
public sealed record SummaryRow(
    string Model, string Tier, string Task, string Grammar, int N,
    double? FirstOkRate, double? JsonValidRate, double? ChoiceAllowedRate, double? LengthOkRate,
    double? GroundedRate, double? PassedFirstRate, double? FallbackRate,
    double? MedianLatency, double? P90Latency, double? MeanOutputTokens, double? MeanReasoningTokens);

public static class Summary
{
    public static IReadOnlyList<SummaryRow> Build(IReadOnlyList<CellResult> rows) =>
        rows.GroupBy(r => (r.Model, r.Tier, r.Task, r.Grammar))
            .Select(g =>
            {
                var list = g.ToList();
                var latencies = list.Where(r => r.LatencySeconds is not null).Select(r => r.LatencySeconds!.Value).Order().ToList();
                var answered = list.Where(r => r.FirstOk).ToList();
                return new SummaryRow(
                    g.Key.Model, g.Key.Tier, g.Key.Task, g.Key.Grammar, list.Count,
                    Rate(list, r => r.FirstOk),
                    Rate(answered, r => r.JsonValid),
                    Rate(answered, r => r.ChoiceAllowed),
                    Rate(answered, r => r.LengthOk),
                    Rate(answered, r => r.UnknownNameCount is null ? null : r.UnknownNameCount == 0),
                    Rate(list, r => r.PassedFirst),
                    Rate(list, r => r.Outcome is "Template" or "Error"),
                    Percentile(latencies, 0.5),
                    Percentile(latencies, 0.9),
                    answered.Count == 0 ? null : answered.Average(r => r.OutputTokens),
                    answered.Any(r => r.ReasoningTokens is not null) ? answered.Where(r => r.ReasoningTokens is not null).Average(r => r.ReasoningTokens!.Value) : null);
            })
            .ToList();

    /// <summary>Share of rows where the metric is true, among rows where it applies; null if it never applies.</summary>
    private static double? Rate(IReadOnlyList<CellResult> rows, Func<CellResult, bool?> metric)
    {
        var applicable = rows.Select(metric).Where(v => v is not null).ToList();
        return applicable.Count == 0 ? null : applicable.Count(v => v == true) / (double)applicable.Count;
    }

    /// <summary>Nearest-rank percentile of sorted values.</summary>
    internal static double? Percentile(IReadOnlyList<double> sorted, double p) =>
        sorted.Count == 0 ? null : sorted[(int)Math.Clamp(Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    public static void WriteCsv(string path, IReadOnlyList<SummaryRow> rows)
    {
        var sb = new StringBuilder();
        Csv.AppendRow(sb,
        [
            "model", "tier", "task", "grammar", "n", "first_ok_rate", "json_valid_rate", "choice_allowed_rate",
            "length_ok_rate", "grounded_rate", "passed_first_rate", "fallback_rate", "median_latency_s", "p90_latency_s",
            "mean_output_tokens", "mean_reasoning_tokens",
        ]);
        foreach (var r in rows)
        {
            Csv.AppendRow(sb,
            [
                r.Model, r.Tier, r.Task, r.Grammar, Csv.Num(r.N), Csv.Num(r.FirstOkRate), Csv.Num(r.JsonValidRate),
                Csv.Num(r.ChoiceAllowedRate), Csv.Num(r.LengthOkRate), Csv.Num(r.GroundedRate), Csv.Num(r.PassedFirstRate),
                Csv.Num(r.FallbackRate), Csv.Num(r.MedianLatency), Csv.Num(r.P90Latency), Csv.Num(r.MeanOutputTokens),
                Csv.Num(r.MeanReasoningTokens),
            ]);
        }

        File.WriteAllText(path, sb.ToString());
    }

    public static void Print(IReadOnlyList<SummaryRow> rows, TextWriter log)
    {
        static string Pct(double? v) => v is null ? "-" : (v.Value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string Sec(double? v) => v is null ? "-" : v.Value.ToString("0.0", CultureInfo.InvariantCulture);

        log.WriteLine($"{"model",-18} {"task",-20} {"gram",-4} {"n",4} {"json",5} {"choice",6} {"length",6} {"ground",6} {"pass1",5} {"fallbk",6} {"p50 s",6} {"p90 s",6}");
        foreach (var r in rows)
        {
            log.WriteLine(
                $"{r.Model,-18} {r.Task,-20} {r.Grammar,-4} {r.N,4} {Pct(r.JsonValidRate),5} {Pct(r.ChoiceAllowedRate),6} " +
                $"{Pct(r.LengthOkRate),6} {Pct(r.GroundedRate),6} {Pct(r.PassedFirstRate),5} {Pct(r.FallbackRate),6} {Sec(r.MedianLatency),6} {Sec(r.P90Latency),6}");
        }
    }
}
