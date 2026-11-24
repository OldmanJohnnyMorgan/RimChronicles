using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>Options for <c>director-review</c>.</summary>
public sealed record ReviewOptions
{
    public required IReadOnlyList<string> CaseDirs { get; init; }
    public required IReadOnlyList<string> LabelDirs { get; init; }
    public required string PromptsRoot { get; init; }
    public required string CacheDir { get; init; }
    public required string OutDir { get; init; }
    public int Count { get; init; } = 50;
    public int Seed { get; init; } = 1;
}

/// <summary>
/// <c>director-review</c> (training track D9): a blind review of a director model against the teacher on the test set.
/// It writes a page with <see cref="ReviewOptions.Count"/> cases, each with the briefing and two unlabelled answers in
/// a random order, and a key the page doesn't show. The reviewer picks the better answer or "same", downloads the
/// answers, and <see cref="Score"/> reveals the key. Ship bar (training spec §7): the model is picked or tied in at
/// least 40% of cases. The model's answers go through the same cached call as <c>director-report</c>.
/// </summary>
public static class DirectorReviewCommand
{
    public const double ShipBar = 0.40;

    public sealed record Answer(string Choice, string Description, string Reason);

    public sealed record Item(int Number, string Case, string Group, string Briefing, Answer A, Answer B, string ModelSide);

    public static async Task<int> PrepareAsync(ReviewOptions o, ModelConfig model, IChatProvider provider, TextWriter log, CancellationToken ct)
    {
        var prompt = PromptLibrary.Load(o.PromptsRoot).Get(DirectorReportCommand.TaskName);
        var report = new ReportOptions
        {
            CaseDirs = o.CaseDirs, LabelDirs = o.LabelDirs, PromptsRoot = o.PromptsRoot, CacheDir = o.CacheDir, OutDir = o.OutDir,
            Name = "review", Count = o.Count,
        };
        var labels = DirectorReportCommand.LoadLabels(report);
        var reasons = report.LabelLines()
            .Select(l => JsonNode.Parse(l)!).ToDictionary(n => n["case"]!.GetValue<string>(), n => n["reason"]?.GetValue<string>() ?? "");
        var stats = new CacheStats();
        var cached = new CachingChatProvider(provider, o.CacheDir, model.Identity(), 0, stats);
        var rng = new Random(o.Seed);
        var items = new List<Item>();
        foreach (var label in labels)
        {
            ct.ThrowIfCancellationRequested();
            var s = SnapshotJson.Load(report.CasePath(label.Case));
            // Variant 0 is director-report's first call (grammar on, the case as labelled), so a report fills the cache.
            var call = await DirectorReportCommand.Ask(prompt, s, model, grammar: true, (req, _) => cached.CompleteAsync(req, ct), 0).ConfigureAwait(false);
            var mine = new Answer(call.Choice ?? "(no valid answer)", Describe(s, call.Choice), call.Reason ?? "");
            var teacher = new Answer(label.Best, Describe(s, label.Best), reasons.GetValueOrDefault(label.Case, ""));
            var modelFirst = rng.Next(2) == 0;
            var briefing = prompt.Render(model.PromptTier, WorldSummarizer.ForDirector(s, model.PromptTier).Variables).User;
            items.Add(new Item(items.Count + 1, label.Case, label.Group, briefing, modelFirst ? mine : teacher, modelFirst ? teacher : mine,
                modelFirst ? "A" : "B"));
        }

        Directory.CreateDirectory(o.OutDir);
        var key = new JsonObject
        {
            ["model"] = model.Id,
            ["labels"] = string.Join("+", o.LabelDirs.Select(d => Path.GetFileName(d.TrimEnd('/', '\\')))),
            ["seed"] = o.Seed,
            ["items"] = new JsonArray(items.Select(i => (JsonNode?)new JsonObject
            {
                ["item"] = i.Number, ["case"] = i.Case, ["group"] = i.Group, ["model"] = i.ModelSide,
                ["same"] = i.A.Choice == i.B.Choice,
            }).ToArray()),
        };
        File.WriteAllText(Path.Combine(o.OutDir, "key.json"), key.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllText(Path.Combine(o.OutDir, "review.html"), Html(items, Path.GetFileName(o.OutDir.TrimEnd('/', '\\'))));
        log.WriteLine($"{items.Count} cases ({stats.Hits} answers from the cache, {stats.Misses} new calls); "
            + $"{items.Count(i => i.A.Choice == i.B.Choice)} where both answers pick the same option. Open {Path.Combine(o.OutDir, "review.html")}.");
        return 0;
    }

    private static string Describe(WorldSnapshot s, string? choice) =>
        choice is null ? "" : choice == "Wait" ? "hold this plot for a few days; nothing happens in it yet"
            : s.Director?.Descriptions.GetValueOrDefault(choice) ?? "";

    /// <summary>Reveals the key: how often the model's answer was picked, tied or beaten, overall and per group.</summary>
    public static int Score(string outDir, string answersPath, TextWriter log)
    {
        var key = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "key.json")))!;
        var picks = JsonNode.Parse(File.ReadAllText(answersPath))!["picks"]!.AsObject()
            .ToDictionary(kv => int.Parse(kv.Key, CultureInfo.InvariantCulture), kv => kv.Value!.GetValue<string>());
        var model = key["model"]!.GetValue<string>();
        var rows = key["items"]!.AsArray().Select(n => n!).Select(n => (
            Item: n["item"]!.GetValue<int>(), Group: n["group"]!.GetValue<string>(), ModelSide: n["model"]!.GetValue<string>(),
            Pick: picks.GetValueOrDefault(n["item"]!.GetValue<int>()))).ToList();
        var answered = rows.Where(r => r.Pick is "A" or "B" or "same").ToList();
        if (answered.Count == 0)
        {
            log.WriteLine("No answers in that file.");
            return 1;
        }

        string Outcome(string pick, string side) => pick == "same" ? "same" : pick == side ? "model" : "teacher";
        var outcomes = answered.Select(r => (r.Group, Result: Outcome(r.Pick!, r.ModelSide))).ToList();
        int won = outcomes.Count(x => x.Result == "model"), tied = outcomes.Count(x => x.Result == "same"), lost = outcomes.Count(x => x.Result == "teacher");
        var share = (double)(won + tied) / answered.Count;

        var md = new StringBuilder();
        md.Append(CultureInfo.InvariantCulture, $"# Blind review: {model} vs the teacher\n\n");
        md.Append(CultureInfo.InvariantCulture, $"{answered.Count} of {rows.Count} cases answered (labels `{key["labels"]}`, seed {key["seed"]}).\n\n");
        md.Append("| | Cases | Share |\n| --- | --- | --- |\n");
        md.Append(CultureInfo.InvariantCulture, $"| {model} better | {won} | {100.0 * won / answered.Count:0}% |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Same | {tied} | {100.0 * tied / answered.Count:0}% |\n");
        md.Append(CultureInfo.InvariantCulture, $"| Teacher better | {lost} | {100.0 * lost / answered.Count:0}% |\n\n");
        md.Append(CultureInfo.InvariantCulture,
            $"**Picked or tied: {100 * share:0}%** (ship bar {100 * ShipBar:0}%): {(share >= ShipBar ? "✅ passes" : "❌ misses")}.\n\n");
        md.Append("| Group | Better | Same | Teacher better |\n| --- | --- | --- | --- |\n");
        foreach (var g in outcomes.GroupBy(x => x.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            md.Append(CultureInfo.InvariantCulture,
                $"| {g.Key} | {g.Count(x => x.Result == "model")} | {g.Count(x => x.Result == "same")} | {g.Count(x => x.Result == "teacher")} |\n");
        }

        File.WriteAllText(Path.Combine(outDir, "review.md"), md.ToString());
        File.Copy(answersPath, Path.Combine(outDir, "answers.json"), overwrite: true);
        log.WriteLine($"{model}: better {won}, same {tied}, teacher better {lost} of {answered.Count}; picked or tied {100 * share:0}% "
            + $"(bar {100 * ShipBar:0}%). Written to {Path.Combine(outDir, "review.md")}.");
        return 0;
    }

    // ---------- the page ----------

    private static string E(string text) => WebUtility.HtmlEncode(text);

    public static string Html(IReadOnlyList<Item> items, string name)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append(CultureInfo.InvariantCulture, $"<title>Director review</title>\n<style>{Css}</style></head><body>\n");
        sb.Append($"<header><h1>Director review</h1><p>{items.Count} decisions. For each, read the briefing, "
            + "then pick the better answer, or <b>Same</b> if neither is better. You don't know which answer is which model. "
            + "Your picks are kept in this browser until you download them.</p>"
            + "<p class=\"bar\"><span id=\"done\">0</span> of " + items.Count.ToString(CultureInfo.InvariantCulture)
            + " answered <button id=\"download\">Download answers</button></p></header>\n<main>\n");
        foreach (var i in items)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<section class=\"item\" id=\"item-{i.Number}\" data-item=\"{i.Number}\">\n");
            sb.Append(CultureInfo.InvariantCulture, $"<h2>{i.Number}. {E(i.Group)}</h2>\n");
            sb.Append(CultureInfo.InvariantCulture, $"<details><summary>Briefing</summary><pre>{E(i.Briefing.Trim())}</pre></details>\n");
            sb.Append("<div class=\"answers\">\n");
            foreach (var (side, a) in new[] { ("A", i.A), ("B", i.B) })
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"<div class=\"answer\"><h3>{side}</h3><p class=\"choice\">{E(a.Choice)}</p><p class=\"desc\">{E(a.Description)}</p>"
                    + $"<p class=\"reason\">{E(a.Reason)}</p></div>\n");
            }

            sb.Append("</div>\n<div class=\"pick\">");
            foreach (var (value, label) in new[] { ("A", "A is better"), ("same", "Same"), ("B", "B is better") })
            {
                sb.Append(CultureInfo.InvariantCulture, $"<button data-pick=\"{value}\">{label}</button>");
            }

            sb.Append("</div>\n</section>\n");
        }

        sb.Append("</main>\n<script>\n");
        sb.Append(CultureInfo.InvariantCulture, $"const NAME = {JsonSerializer.Serialize(name)};\n");
        sb.Append(Script);
        sb.Append("</script>\n</body></html>\n");
        return sb.ToString();
    }

    private const string Css = """
        :root { --bg: #fbfaf7; --fg: #1d1c1a; --muted: #6b6760; --card: #ffffff; --line: #e3dfd6; --accent: #8a5a2b; --picked: #f1e4d3; }
        @media (prefers-color-scheme: dark) { :root { --bg: #171614; --fg: #ece9e3; --muted: #a39e94; --card: #211f1c; --line: #34312c; --accent: #e0a96d; --picked: #3a2e22; } }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--bg); color: var(--fg); font: 16px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
        header { position: sticky; top: 0; background: var(--bg); border-bottom: 1px solid var(--line); padding: 12px 16px; z-index: 1; }
        h1 { margin: 0 0 4px; font-size: 1.3rem; } header p { margin: 4px 0; color: var(--muted); max-width: 60rem; }
        .bar { display: flex; gap: 12px; align-items: center; }
        main { max-width: 64rem; margin: 0 auto; padding: 16px; }
        .item { background: var(--card); border: 1px solid var(--line); border-radius: 8px; padding: 12px 16px; margin: 0 0 16px; }
        .item.done { border-color: var(--accent); }
        h2 { font-size: 1rem; margin: 0 0 8px; color: var(--muted); }
        pre { white-space: pre-wrap; font: 13px/1.45 ui-monospace, Consolas, monospace; background: var(--bg); padding: 8px; border-radius: 6px; overflow-x: auto; }
        .answers { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin: 12px 0; }
        @media (max-width: 640px) { .answers { grid-template-columns: 1fr; } }
        .answer { border: 1px solid var(--line); border-radius: 6px; padding: 8px 12px; }
        .answer h3 { margin: 0; font-size: 0.9rem; color: var(--muted); }
        .choice { font-weight: 600; margin: 4px 0; word-break: break-word; } .desc { color: var(--muted); margin: 0 0 6px; font-size: 0.9rem; }
        .reason { margin: 0; }
        .pick { display: flex; gap: 8px; flex-wrap: wrap; }
        button { font: inherit; padding: 6px 14px; border-radius: 6px; border: 1px solid var(--line); background: var(--card); color: var(--fg); cursor: pointer; }
        button.on { background: var(--picked); border-color: var(--accent); font-weight: 600; }
        #download { border-color: var(--accent); }
        """;

    private const string Script = """
        const KEY = "director-review:" + NAME;
        let picks = {};
        try { picks = JSON.parse(localStorage.getItem(KEY) || "{}"); } catch (e) { picks = {}; }
        function save() { try { localStorage.setItem(KEY, JSON.stringify(picks)); } catch (e) {} }
        function show() {
          document.querySelectorAll(".item").forEach(sec => {
            const v = picks[sec.dataset.item];
            sec.classList.toggle("done", !!v);
            sec.querySelectorAll("button[data-pick]").forEach(b => b.classList.toggle("on", b.dataset.pick === v));
          });
          document.getElementById("done").textContent = Object.keys(picks).length;
        }
        document.querySelectorAll("button[data-pick]").forEach(b => b.addEventListener("click", () => {
          const sec = b.closest(".item");
          picks[sec.dataset.item] = b.dataset.pick;
          save(); show();
          const next = sec.nextElementSibling;
          if (next) next.scrollIntoView({ behavior: "smooth", block: "start" });
        }));
        document.getElementById("download").addEventListener("click", () => {
          const blob = new Blob([JSON.stringify({ review: NAME, picks }, null, 2)], { type: "application/json" });
          const a = document.createElement("a");
          a.href = URL.createObjectURL(blob);
          a.download = "review-answers.json";
          a.click();
        });
        show();
        """;
}
