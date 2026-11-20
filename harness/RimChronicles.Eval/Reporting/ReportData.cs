using RimChronicles.Eval.Judging;
using RimChronicles.Eval.Running;

namespace RimChronicles.Eval.Reporting;

/// <summary>Judge aggregates for one model, task and grammar mode.</summary>
public sealed record JudgeStats(int Judged, int Failed, double? InventedPer10, double? Voice, double? Clarity, double? Tone, double? Overall);

/// <summary>One report row: deterministic metrics plus judge scores, with pass/fail against the bars.</summary>
public sealed record ReportRow(SummaryRow Metrics, JudgeStats? Judge, TaskBars Bars)
{
    public string Key => $"{Metrics.Model}|{Metrics.Task}|{Metrics.Grammar}";

    public BarVerdict JsonValid => PassBars.AtLeast(Metrics.JsonValidRate,
        Metrics.Grammar == "on" ? Bars.JsonValidGrammarOn : Bars.JsonValidGrammarOff);

    public BarVerdict ChoiceAllowed => PassBars.AtLeast(Metrics.ChoiceAllowedRate, Bars.ChoiceAllowed);
    public BarVerdict LengthOk => PassBars.AtLeast(Metrics.LengthOkRate, Bars.LengthOk);
    public BarVerdict Invented => PassBars.AtMost(Judge?.InventedPer10, Bars.InventedPer10);

    public BarVerdict Overall => PassBars.AppliesTo(Bars.OverallTiers, Metrics.Tier)
        ? PassBars.AtLeast(Judge?.Overall, Bars.Overall)
        : BarVerdict.None;

    public BarVerdict Latency => PassBars.AppliesTo(Bars.LatencyTiers, Metrics.Tier)
        ? PassBars.AtMost(Metrics.MedianLatency, Bars.MedianLatencySeconds)
        : BarVerdict.None;

    /// <summary>Fail if any bar fails; pass if at least one bar applies and none fail.</summary>
    public BarVerdict AllBars
    {
        get
        {
            var verdicts = new[] { JsonValid, ChoiceAllowed, LengthOk, Invented, Overall, Latency };
            return verdicts.Contains(BarVerdict.Fail) ? BarVerdict.Fail
                : verdicts.Contains(BarVerdict.Pass) ? BarVerdict.Pass
                : BarVerdict.None;
        }
    }
}

/// <summary>A run loaded from its folder, with everything the report needs.</summary>
public sealed record RunData(string RunId, string Folder, IReadOnlyList<CellResult> Cells, IReadOnlyList<JudgeRow> Judgments, IReadOnlyList<ReportRow> Rows)
{
    public bool HasJudge => Judgments.Count > 0;

    public static RunData Load(string folder, PassBars bars)
    {
        var cells = ResultFiles.ReadResultsCsv(Path.Combine(folder, "results.csv"));
        var judgePath = Path.Combine(folder, "judge.csv");
        var judgments = File.Exists(judgePath) ? JudgeFiles.Read(judgePath) : [];
        return new RunData(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), folder, cells, judgments, Build(cells, judgments, bars));
    }

    public static IReadOnlyList<ReportRow> Build(IReadOnlyList<CellResult> cells, IReadOnlyList<JudgeRow> judgments, PassBars bars)
    {
        var byRaw = judgments.ToDictionary(j => j.RawFile, StringComparer.Ordinal);
        return Summary.Build(cells).Select(m =>
        {
            var group = cells.Where(c => c.Model == m.Model && c.Task == m.Task && c.Grammar == m.Grammar)
                .Select(c => byRaw.GetValueOrDefault(c.RawFile))
                .OfType<JudgeRow>()
                .ToList();
            return new ReportRow(m, group.Count == 0 ? null : Stats(group), bars.For(m.Task));
        }).ToList();
    }

    private static JudgeStats Stats(IReadOnlyList<JudgeRow> rows)
    {
        var judged = rows.Where(r => r.Judged).ToList();
        double? Mean(Func<JudgeRow, int?> f) => judged.Count == 0 ? null : judged.Average(r => f(r)!.Value);
        return new JudgeStats(
            judged.Count,
            rows.Count - judged.Count,
            judged.Count == 0 ? null : judged.Sum(r => r.InventedCount!.Value) * 10.0 / judged.Count,
            Mean(r => r.Voice), Mean(r => r.Clarity), Mean(r => r.Tone), Mean(r => r.Overall));
    }
}
