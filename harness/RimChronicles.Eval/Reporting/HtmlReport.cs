using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Judging;
using RimChronicles.Eval.Running;
using RimChronicles.Eval.Servers;

namespace RimChronicles.Eval.Reporting;

/// <summary>
/// Writes summary.html: a single static, offline file (no scripts, no external assets) with an at-a-glance verdict,
/// one table and latency chart per task, an optional comparison with an earlier run, and every output.
/// </summary>
public static class HtmlReport
{
    private static readonly string[] Tasks = [WorldSummarizer.DirectorTask, WorldSummarizer.NewsBulletinTask, WorldSummarizer.HandoffTask];

    private static readonly Dictionary<string, string> TaskTitles = new()
    {
        [WorldSummarizer.DirectorTask] = "Director decision",
        [WorldSummarizer.NewsBulletinTask] = "News bulletin",
        [WorldSummarizer.HandoffTask] = "Inheritance handoff",
    };

    public static string Write(RunData run, RunData? compare)
    {
        var path = Path.Combine(run.Folder, "summary.html");
        File.WriteAllText(path, Render(run, compare));
        return path;
    }

    public static string Render(RunData run, RunData? compare)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
          .Append($"<title>Eval run {E(run.RunId)}</title>\n<style>{Css}</style>\n</head>\n<body>\n<main>\n");

        Header(sb, run);
        AtAGlance(sb, run);
        foreach (var task in Tasks.Where(t => run.Rows.Any(r => r.Metrics.Task == t)))
        {
            TaskSection(sb, run, task);
        }

        if (compare is not null)
        {
            Comparison(sb, run, compare);
        }

        Outputs(sb, run);
        sb.Append("</main>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static void Header(StringBuilder sb, RunData run)
    {
        var meta = File.Exists(Path.Combine(run.Folder, "run.json"))
            ? JsonNode.Parse(File.ReadAllText(Path.Combine(run.Folder, "run.json")))
            : null;

        sb.Append("<header>\n<h1>Rim Chronicles model evaluation</h1>\n<p class=\"meta\">");
        sb.Append($"Run <strong>{E(run.RunId)}</strong> · {run.Cells.Count} outputs · {run.Cells.Select(c => c.Model).Distinct().Count()} models");
        if (meta?["gitCommit"]?.GetValue<string>() is { } commit)
        {
            var dirty = meta["gitDirty"]?.GetValue<bool>() == true ? " (uncommitted changes)" : "";
            sb.Append($" · commit <code>{E(commit[..Math.Min(8, commit.Length)])}</code>{dirty}");
        }

        if (meta?["promptVersions"] is JsonObject versions)
        {
            sb.Append(" · prompts ").Append(string.Join(", ", versions.Select(kv => $"<code>{E(kv.Key)}/{E(kv.Value?.GetValue<string>() ?? "")}</code>")));
        }

        sb.Append("</p>\n");
        sb.Append(run.HasJudge
            ? $"<p class=\"meta\">Judged {run.Judgments.Count(j => j.Judged)} prose outputs blind ({E(run.Judgments[0].JudgePromptVersion)}).</p>\n"
            : "<p class=\"note\">Not judged yet: invented facts and prose quality are empty. Run <code>rimchron-eval judge " + E(run.RunId) + "</code>.</p>\n");
        sb.Append("</header>\n");
    }

    private static void AtAGlance(StringBuilder sb, RunData run)
    {
        var tasks = Tasks.Where(t => run.Rows.Any(r => r.Metrics.Task == t)).ToList();
        sb.Append("<section>\n<h2>At a glance</h2>\n<p>Whether each model meets every pass bar that applies to it, per task. Failing bars are named; details follow below.</p>\n");
        var showEnv = run.Environment.Values.Any(e => e.Quantization is not null || e.VramBytes is not null);
        sb.Append("<div class=\"scroll\"><table>\n<thead><tr><th>Model</th><th>Tier</th>");
        if (showEnv)
        {
            sb.Append("<th>Server</th><th>Quantization</th><th class=\"num\">VRAM</th>");
        }

        foreach (var t in tasks)
        {
            sb.Append($"<th>{E(TaskTitles[t])}</th>");
        }

        sb.Append("</tr></thead>\n<tbody>\n");
        foreach (var model in run.Rows.GroupBy(r => (r.Metrics.Model, r.Metrics.Tier)))
        {
            sb.Append($"<tr><th scope=\"row\">{E(model.Key.Model)}</th><td>{E(model.Key.Tier)}</td>");
            if (showEnv)
            {
                var env = run.Environment.GetValueOrDefault(model.Key.Model);
                var vram = ServerProbe.FormatGb(env?.VramBytes) + (env?.PartlyOnCpu == true ? " (partly on CPU)" : "");
                sb.Append($"<td>{E(env?.ServerKind ?? "–")}</td><td>{E(env?.Quantization ?? "–")}</td><td class=\"num\">{E(vram)}</td>");
            }

            foreach (var t in tasks)
            {
                var rows = model.Where(r => r.Metrics.Task == t).ToList();
                if (rows.Count == 0)
                {
                    sb.Append("<td class=\"muted\">not run</td>");
                    continue;
                }

                var failing = rows.SelectMany(FailingBars).Distinct().ToList();
                var verdict = failing.Count > 0 ? BarVerdict.Fail
                    : rows.Any(r => r.AllBars == BarVerdict.Pass) ? BarVerdict.Pass
                    : BarVerdict.None;
                var text = verdict switch
                {
                    BarVerdict.Fail => "Fails: " + string.Join(", ", failing),
                    BarVerdict.Pass => "Meets all bars",
                    _ => "No bars apply",
                };
                sb.Append($"<td>{Icon(verdict)}{E(text)}</td>");
            }

            sb.Append("</tr>\n");
        }

        sb.Append("</tbody></table></div>\n</section>\n");
    }

    private static IEnumerable<string> FailingBars(ReportRow r)
    {
        var grammar = r.Metrics.Grammar is "on" or "off" ? $" (grammar {r.Metrics.Grammar})" : "";
        if (r.JsonValid == BarVerdict.Fail) yield return "JSON validity" + grammar;
        if (r.ChoiceAllowed == BarVerdict.Fail) yield return "allowed choice" + grammar;
        if (r.LengthOk == BarVerdict.Fail) yield return "length";
        if (r.MajorInvented == BarVerdict.Fail) yield return "invented facts";
        if (r.MinorInvented == BarVerdict.Fail) yield return "embellishments";
        if (r.Overall == BarVerdict.Fail) yield return "prose quality";
        if (r.Latency == BarVerdict.Fail) yield return "latency" + grammar;
    }

    private static void TaskSection(StringBuilder sb, RunData run, string task)
    {
        var rows = run.Rows.Where(r => r.Metrics.Task == task).ToList();
        var bars = rows[0].Bars;
        sb.Append($"<section>\n<h2>{E(TaskTitles[task])}</h2>\n<p>{E(BarsSentence(task, bars))}</p>\n");
        sb.Append("<div class=\"scroll\"><table>\n<thead><tr><th>Model</th><th>Tier</th>");

        var prose = task != WorldSummarizer.DirectorTask;
        if (prose)
        {
            sb.Append("<th class=\"num\">Outputs</th><th class=\"num\">Judged</th><th class=\"num\">Length in range</th><th class=\"num\">Invented facts / 10</th><th class=\"num\">Embellishments / 10</th>")
              .Append("<th class=\"num\">Voice</th><th class=\"num\">Clarity</th><th class=\"num\">Tone</th><th class=\"num\">Overall</th>")
              .Append("<th class=\"num\">Names grounded</th>");
        }
        else
        {
            sb.Append("<th>Grammar</th><th class=\"num\">Outputs</th><th class=\"num\">Valid JSON</th><th class=\"num\">Allowed choice</th>");
        }

        sb.Append("<th class=\"num\">Passed first try</th><th class=\"num\">Fell back</th><th class=\"num\">Median s</th><th class=\"num\">p90 s</th><th class=\"num\">Output tokens</th><th class=\"num\">Reasoning tokens</th></tr></thead>\n<tbody>\n");

        foreach (var r in rows)
        {
            var m = r.Metrics;
            sb.Append($"<tr><th scope=\"row\">{E(m.Model)}</th><td>{E(m.Tier)}</td>");
            if (prose)
            {
                sb.Append(Num(m.N)).Append(Num(r.Judge?.Judged))
                  .Append(Cell(Pct(m.LengthOkRate), r.LengthOk))
                  .Append(Cell(Fixed(r.Judge?.MajorPer10), r.MajorInvented))
                  .Append(Cell(Fixed(r.Judge?.MinorPer10), r.MinorInvented))
                  .Append(Num(Fixed(r.Judge?.Voice))).Append(Num(Fixed(r.Judge?.Clarity))).Append(Num(Fixed(r.Judge?.Tone)))
                  .Append(Cell(Fixed(r.Judge?.Overall), r.Overall))
                  .Append(Num(Pct(m.GroundedRate)));
            }
            else
            {
                sb.Append($"<td>{E(m.Grammar)}</td>").Append(Num(m.N))
                  .Append(Cell(Pct(m.JsonValidRate), r.JsonValid))
                  .Append(Cell(Pct(m.ChoiceAllowedRate), r.ChoiceAllowed));
            }

            sb.Append(Num(Pct(m.PassedFirstRate))).Append(Num(Pct(m.FallbackRate)))
              .Append(Cell(Fixed(m.MedianLatency), r.Latency)).Append(Num(Fixed(m.P90Latency)))
              .Append(Num(Fixed(m.MeanOutputTokens, "0"))).Append(Num(Fixed(m.MeanReasoningTokens, "0")))
              .Append("</tr>\n");
        }

        sb.Append("</tbody></table></div>\n");
        LatencyChart(sb, task, rows, bars.MedianLatencySeconds);
        sb.Append("</section>\n");
    }

    private static string BarsSentence(string task, TaskBars b)
    {
        var parts = new List<string>();
        if (b.JsonValidGrammarOn is { } on) parts.Add($"valid JSON {P(on)} with grammar and {P(b.JsonValidGrammarOff ?? 0)} without");
        if (b.ChoiceAllowed is { } choice) parts.Add($"allowed choice {P(choice)}");
        if (b.LengthOk is { } len) parts.Add($"length in range at least {P(len)}");
        if (b.MajorInventedPer10 is { } inv) parts.Add($"at most {inv.ToString("0.#", CultureInfo.InvariantCulture)} invented fact per 10 outputs (a new person, place, number, quote or event, or a contradiction)");
        if (b.MinorInventedPer10 is { } emb) parts.Add($"at most {emb.ToString("0.#", CultureInfo.InvariantCulture)} embellishments per 10 (an added detail or inference about a briefed fact)");
        if (b.Overall is { } overall) parts.Add($"overall quality at least {overall.ToString("0.0", CultureInfo.InvariantCulture)}" + Tiers(b.OverallTiers));
        if (b.MedianLatencySeconds is { } lat) parts.Add($"median latency at most {lat.ToString("0", CultureInfo.InvariantCulture)} s" + Tiers(b.LatencyTiers));
        return parts.Count == 0 ? "No pass bars for this task." : "Pass bars: " + string.Join("; ", parts) + ". Rates use first answers only.";

        static string P(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string Tiers(IReadOnlyList<string>? t) => t is null ? "" : $" ({string.Join(", ", t)} tier{(t.Count == 1 ? "" : "s")})";
    }

    /// <summary>One row per model (and grammar mode): a dot at the median, a whisker to p90, the bar as a dashed line.</summary>
    private static void LatencyChart(StringBuilder sb, string task, IReadOnlyList<ReportRow> rows, double? threshold)
    {
        var data = rows.Where(r => r.Metrics.MedianLatency is not null).ToList();
        if (data.Count == 0)
        {
            return;
        }

        const int labelWidth = 230, plotWidth = 440, rowHeight = 28, top = 40, right = 40;
        var max = Math.Max(data.Max(r => r.Metrics.P90Latency ?? 0), threshold ?? 0);
        var step = NiceStep(max / 4);
        var axisMax = Math.Max(step, Math.Ceiling(max / step) * step);
        double X(double v) => labelWidth + v / axisMax * plotWidth;
        var height = top + data.Count * rowHeight + 36;
        var width = labelWidth + plotWidth + right;
        var title = $"{TaskTitles[task]} latency: median (dot) and 90th percentile (line end), seconds";

        sb.Append($"<figure>\n<svg class=\"chart\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"{E(title)}\">\n");
        sb.Append($"<text class=\"chart-title\" x=\"0\" y=\"16\">{E(title)}</text>\n");

        for (var t = 0.0; t <= axisMax + 1e-9; t += step)
        {
            sb.Append($"<line class=\"grid\" x1=\"{F(X(t))}\" x2=\"{F(X(t))}\" y1=\"{top - 6}\" y2=\"{height - 30}\"/>")
              .Append($"<text class=\"tick\" x=\"{F(X(t))}\" y=\"{height - 12}\" text-anchor=\"middle\">{F(t, "0.#")}</text>\n");
        }

        if (threshold is { } th)
        {
            sb.Append($"<line class=\"threshold\" x1=\"{F(X(th))}\" x2=\"{F(X(th))}\" y1=\"{top - 10}\" y2=\"{height - 30}\"/>")
              // Label on whichever side of the line has room, so it never runs past the chart's edge.
              .Append(X(th) > labelWidth + plotWidth / 2.0
                  ? $"<text class=\"tick\" x=\"{F(X(th) - 4)}\" y=\"{top - 12}\" text-anchor=\"end\">bar {F(th, "0.#")} s (local tiers)</text>\n"
                  : $"<text class=\"tick\" x=\"{F(X(th) + 4)}\" y=\"{top - 12}\">bar {F(th, "0.#")} s (local tiers)</text>\n");
        }

        for (var i = 0; i < data.Count; i++)
        {
            var m = data[i].Metrics;
            var y = top + i * rowHeight + rowHeight / 2.0;
            var label = m.Grammar is "on" or "off" ? $"{m.Model} · grammar {m.Grammar}" : m.Model;
            var median = m.MedianLatency!.Value;
            var p90 = m.P90Latency ?? median;
            sb.Append("<g class=\"row\">")
              .Append($"<title>{E(label)}: median {F(median, "0.0")} s, p90 {F(p90, "0.0")} s ({m.N} outputs)</title>")
              .Append($"<rect class=\"hit\" x=\"0\" y=\"{F(y - rowHeight / 2.0)}\" width=\"{width}\" height=\"{rowHeight}\"/>")
              .Append($"<text class=\"label\" x=\"{labelWidth - 12}\" y=\"{F(y + 4)}\" text-anchor=\"end\">{E(label)}</text>")
              .Append($"<line class=\"whisker\" x1=\"{F(X(median))}\" x2=\"{F(X(p90))}\" y1=\"{F(y)}\" y2=\"{F(y)}\"/>")
              .Append($"<circle class=\"dot\" cx=\"{F(X(median))}\" cy=\"{F(y)}\" r=\"5\"/>")
              .Append("</g>\n");
        }

        sb.Append("</svg>\n</figure>\n");
    }

    private static double NiceStep(double raw)
    {
        if (raw <= 0)
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
    }

    private static void Comparison(StringBuilder sb, RunData run, RunData old)
    {
        var oldRows = old.Rows.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var matched = run.Rows.Where(r => oldRows.ContainsKey(r.Key)).ToList();
        sb.Append($"<section>\n<h2>Compared with run {E(old.RunId)}</h2>\n");
        if (matched.Count == 0)
        {
            sb.Append("<p>No model, task and grammar combination appears in both runs.</p>\n</section>\n");
            return;
        }

        sb.Append("<p>Earlier value → this run. ▲ is better, ▼ is worse; unchanged values show no arrow.</p>\n");
        sb.Append("<div class=\"scroll\"><table>\n<thead><tr><th>Model</th><th>Task</th><th>Grammar</th><th class=\"num\">Valid JSON</th><th class=\"num\">Allowed choice</th>")
          .Append("<th class=\"num\">Length in range</th><th class=\"num\">Invented / 10</th><th class=\"num\">Embellished / 10</th><th class=\"num\">Overall</th><th class=\"num\">Fell back</th><th class=\"num\">Median s</th></tr></thead>\n<tbody>\n");

        foreach (var r in matched)
        {
            var o = oldRows[r.Key];
            sb.Append($"<tr><th scope=\"row\">{E(r.Metrics.Model)}</th><td>{E(TaskTitles.GetValueOrDefault(r.Metrics.Task, r.Metrics.Task))}</td><td>{E(r.Metrics.Grammar)}</td>")
              .Append(Delta(o.Metrics.JsonValidRate, r.Metrics.JsonValidRate, higherIsBetter: true, pct: true))
              .Append(Delta(o.Metrics.ChoiceAllowedRate, r.Metrics.ChoiceAllowedRate, true, true))
              .Append(Delta(o.Metrics.LengthOkRate, r.Metrics.LengthOkRate, true, true))
              .Append(Delta(o.Judge?.MajorPer10, r.Judge?.MajorPer10, false, false))
              .Append(Delta(o.Judge?.MinorPer10, r.Judge?.MinorPer10, false, false))
              .Append(Delta(o.Judge?.Overall, r.Judge?.Overall, true, false))
              .Append(Delta(o.Metrics.FallbackRate, r.Metrics.FallbackRate, false, true))
              .Append(Delta(o.Metrics.MedianLatency, r.Metrics.MedianLatency, false, false))
              .Append("</tr>\n");
        }

        sb.Append("</tbody></table></div>\n</section>\n");
    }

    private static string Delta(double? before, double? after, bool higherIsBetter, bool pct)
    {
        string Fmt(double? v) => pct ? Pct(v) : Fixed(v);
        if (before is null && after is null)
        {
            return "<td class=\"num muted\">–</td>";
        }

        var arrow = "";
        if (before is { } b && after is { } a && Math.Abs(a - b) > (pct ? 0.0005 : 0.005))
        {
            var better = higherIsBetter ? a > b : a < b;
            arrow = better ? " <span class=\"icon pass\" aria-label=\"better\">▲</span>" : " <span class=\"icon fail\" aria-label=\"worse\">▼</span>";
        }

        return $"<td class=\"num\">{E(Fmt(before))} → {E(Fmt(after))}{arrow}</td>";
    }

    private static void Outputs(StringBuilder sb, RunData run)
    {
        var judgments = run.Judgments.ToDictionary(j => j.RawFile, StringComparer.Ordinal);
        sb.Append("<section>\n<h2>Outputs</h2>\n<p>Every first answer, grouped by task and model. Open the raw file for the full request, retries and timings.</p>\n");
        foreach (var task in Tasks)
        {
            var cells = run.Cells.Where(c => c.Task == task).ToList();
            if (cells.Count == 0)
            {
                continue;
            }

            sb.Append($"<h3>{E(TaskTitles[task])}</h3>\n");
            foreach (var group in cells.GroupBy(c => c.Model))
            {
                sb.Append($"<details>\n<summary>{E(group.Key)} · {group.Count()} outputs</summary>\n");
                foreach (var c in group)
                {
                    var text = ReadText(run.Folder, c.RawFile);
                    var grammar = c.Grammar is "on" or "off" ? $" · grammar {c.Grammar}" : "";
                    sb.Append($"<article><p class=\"meta\"><a href=\"{E(c.RawFile.Replace('\\', '/'))}\">{E(c.Snapshot)}</a> · repeat {c.Repeat}{grammar} · {E(c.Outcome)}");
                    if (c.WordCount is { } words)
                    {
                        sb.Append($" · {words} words");
                    }

                    if (c.LatencySeconds is { } s)
                    {
                        sb.Append($" · {F(s, "0.0")} s");
                    }

                    sb.Append("</p>\n");
                    sb.Append(text is null
                        ? $"<p class=\"note\">No answer: {E(c.FirstError ?? "unknown error")}</p>\n"
                        : $"<pre>{E(text)}</pre>\n");

                    if (judgments.TryGetValue(c.RawFile, out var j) && j.Judged)
                    {
                        sb.Append($"<p class=\"judge\">Judge: overall {j.Overall}, voice {j.Voice}, clarity {j.Clarity}, tone {j.Tone}. {E(j.Comment)}");
                        if (j.InventedCount > 0)
                        {
                            sb.Append($"<br>Invented: {E(j.Invented)}");
                        }

                        sb.Append("</p>\n");
                    }

                    sb.Append("</article>\n");
                }

                sb.Append("</details>\n");
            }
        }

        sb.Append("</section>\n");
    }

    private static string? ReadText(string folder, string rawFile)
    {
        var path = RunPaths.Raw(folder, rawFile);
        return rawFile.Length > 0 && File.Exists(path) ? RawOutput.Load(path).Text : null;
    }

    private static string Cell(string value, BarVerdict verdict) => $"<td class=\"num\">{Icon(verdict)}{E(value)}</td>";

    private static string Icon(BarVerdict verdict) => verdict switch
    {
        BarVerdict.Pass => "<span class=\"icon pass\" title=\"meets the bar\" aria-label=\"pass\">✓</span> ",
        BarVerdict.Fail => "<span class=\"icon fail\" title=\"misses the bar\" aria-label=\"fail\">✗</span> ",
        _ => "",
    };

    private static string Num(string value) => $"<td class=\"num\">{E(value)}</td>";

    private static string Num(int? value) => Num(value?.ToString(CultureInfo.InvariantCulture) ?? "–");

    private static string Pct(double? v) => v is null ? "–" : (v.Value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Fixed(double? v, string format = "0.0") => v is null ? "–" : v.Value.ToString(format, CultureInfo.InvariantCulture);

    private static string F(double v, string format = "0.#") => v.ToString(format, CultureInfo.InvariantCulture);

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private const string Css = """

        :root {
          color-scheme: light;
          --page: #f9f9f7; --surface: #fcfcfb; --ink: #0b0b0b; --ink-2: #52514e; --muted: #898781;
          --grid: #e1e0d9; --axis: #c3c2b7; --border: rgba(11,11,11,0.10);
          --series: #2a78d6; --pass: #006300; --fail: #d03b3b;
        }
        @media (prefers-color-scheme: dark) {
          :root {
            color-scheme: dark;
            --page: #0d0d0d; --surface: #1a1a19; --ink: #f0efec; --ink-2: #c3c2b7; --muted: #898781;
            --grid: #2c2c2a; --axis: #383835; --border: rgba(255,255,255,0.10);
            --series: #3987e5; --pass: #0ca30c; --fail: #e05555;
          }
        }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--page); color: var(--ink); font: 14px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
        main { max-width: 1100px; margin: 0 auto; padding: 24px 16px 64px; }
        h1 { font-size: 22px; margin: 0 0 4px; }
        h2 { font-size: 18px; margin: 32px 0 8px; }
        h3 { font-size: 15px; margin: 20px 0 8px; }
        p { max-width: 75ch; }
        .meta { color: var(--ink-2); margin: 4px 0; }
        .note { color: var(--ink-2); border-left: 3px solid var(--axis); padding-left: 10px; }
        .muted { color: var(--muted); }
        code { font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .scroll { overflow-x: auto; background: var(--surface); border: 1px solid var(--border); border-radius: 8px; }
        table { border-collapse: collapse; width: 100%; font-variant-numeric: tabular-nums; }
        th, td { padding: 6px 10px; text-align: left; border-bottom: 1px solid var(--grid); white-space: nowrap; }
        thead th { color: var(--ink-2); font-weight: 600; font-size: 12px; }
        tbody th { font-weight: 600; }
        tbody tr:last-child > * { border-bottom: none; }
        .num { text-align: right; }
        .icon { font-weight: 700; }
        .icon.pass { color: var(--pass); }
        .icon.fail { color: var(--fail); }
        figure { margin: 12px 0 0; background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 12px; overflow-x: auto; }
        .chart { width: 100%; min-width: 520px; height: auto; display: block; }
        .chart text { fill: var(--ink-2); font-size: 12px; }
        .chart .chart-title { fill: var(--ink); font-weight: 600; font-size: 13px; }
        .chart .tick { fill: var(--muted); font-size: 11px; }
        .chart .grid { stroke: var(--grid); stroke-width: 1; }
        .chart .threshold { stroke: var(--ink-2); stroke-width: 1.5; stroke-dasharray: 4 4; }
        .chart .whisker { stroke: var(--series); stroke-width: 2; stroke-linecap: round; }
        .chart .dot { fill: var(--series); stroke: var(--surface); stroke-width: 2; }
        .chart .hit { fill: transparent; }
        .chart .row:hover .hit { fill: var(--grid); fill-opacity: 0.5; }
        details { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; margin: 8px 0; padding: 8px 12px; }
        summary { cursor: pointer; font-weight: 600; }
        article { border-top: 1px solid var(--grid); padding: 8px 0; }
        pre { white-space: pre-wrap; font: 13px/1.5 system-ui, sans-serif; margin: 4px 0; }
        .judge { color: var(--ink-2); font-size: 13px; margin: 4px 0; }
        a { color: var(--series); }

        """;
}
