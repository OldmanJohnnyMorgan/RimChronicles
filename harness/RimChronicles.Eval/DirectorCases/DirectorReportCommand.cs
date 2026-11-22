using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Eval.Running;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>Options for <c>director-report</c>.</summary>
public sealed record ReportOptions
{
    public required string CasesDir { get; init; }
    public required string LabelsDir { get; init; }
    public required string PromptsRoot { get; init; }
    public required string CacheDir { get; init; }
    public required string OutDir { get; init; }
    public required string Name { get; init; }

    /// <summary>Only N cases, spread evenly over the set (for a quick look); null runs them all.</summary>
    public int? Count { get; init; }

    /// <summary>The model the lift column compares against (the prompted base of the same size).</summary>
    public string? BaseModel { get; init; }
}

/// <summary>
/// <c>director-report</c> (training track D8): runs models on the frozen test set and writes the training spec §7
/// metrics to <c>harness/reports/&lt;name&gt;.md</c>, per arc and beat. Each case runs five ways: as labelled with and
/// without grammar, in two more option orders (position bias), and with the ids renamed (ID robustness). An
/// always-Wait baseline is scored beside the models, since 65% of the test set's best answers are Wait. Calls are
/// cached, so a report can be rebuilt or extended without calling a model again.
/// </summary>
public static partial class DirectorReportCommand
{
    public const string TaskName = "director";
    public const string AlwaysWait = "always-Wait";

    public sealed record Call(bool JsonValid, bool ChoiceAllowed, string? Choice, string? Reason, double LatencySeconds);

    /// <summary>One model on one case: the five variants, plus what the briefing held (for the fact check).</summary>
    public sealed record CaseRun(string Case, Call On, Call Off, Call Order2, Call Order3, Call Ids, string? IdsChoice,
        IReadOnlyCollection<string> Names, string Briefing);

    public sealed record Label(string Case, string Group, bool? WaitExpected, bool Injection, string Best,
        IReadOnlyDictionary<string, double> Scores, IReadOnlyList<string> Allowed);

    public static async Task<int> RunAsync(ReportOptions o, IReadOnlyList<ModelConfig> models,
        Func<ModelConfig, IChatProvider> providers, TextWriter log, CancellationToken ct)
    {
        var library = PromptLibrary.Load(o.PromptsRoot);
        var prompt = library.Get(TaskName);
        var labels = LoadLabels(o);
        var snapshots = labels.ToDictionary(l => l.Case, l => SnapshotJson.Load(Path.Combine(o.CasesDir, l.Case + ".json")));
        log.WriteLine($"{labels.Count} test cases, {models.Count} model(s), 5 calls per case. Prompt {TaskName}/{prompt.Meta.Version}.");

        var results = new Dictionary<string, List<CaseRun>>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            var provider = providers(model);
            var stats = new CacheStats();
            var identity = model.Identity();
            var runs = new List<CaseRun>();
            foreach (var label in labels)
            {
                ct.ThrowIfCancellationRequested();
                runs.Add(await RunCase(prompt, snapshots[label.Case], model,
                    (req, k) => new CachingChatProvider(provider, o.CacheDir, identity, k, stats).CompleteAsync(req, ct)).ConfigureAwait(false));
                if (runs.Count % 50 == 0 || runs.Count == labels.Count)
                {
                    log.WriteLine($"{model.Id}: {runs.Count}/{labels.Count} cases (cache {stats.Hits} hits, {stats.Misses} calls)");
                }
            }

            results[model.Id] = runs;
        }

        var metrics = models.Select(m => Score(m.Id, labels, results[m.Id])).Prepend(Score(AlwaysWait, labels, AlwaysWaitRuns(labels))).ToList();
        Directory.CreateDirectory(o.OutDir);
        var md = Markdown(o, prompt.Meta.Version, labels, metrics, results);
        File.WriteAllText(Path.Combine(o.OutDir, o.Name + ".md"), md);
        File.WriteAllText(Path.Combine(o.OutDir, o.Name + ".json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        log.WriteLine($"Report: {Path.Combine(o.OutDir, o.Name + ".md")}");
        return 0;
    }

    // ---------- running ----------

    public static async Task<CaseRun> RunCase(PromptTemplate prompt, WorldSnapshot s, ModelConfig model,
        Func<ChatRequest, int, Task<ChatResult>> complete)
    {
        // Two orders that really differ from the original (a random shuffle can leave it unchanged): rotated by one,
        // and reversed. With two options both are the swap, which is the only other order there is.
        var order2 = s with { AllowedTransitions = [.. s.AllowedTransitions.Skip(1), s.AllowedTransitions[0]] };
        var order3 = s with { AllowedTransitions = s.AllowedTransitions.Reverse().ToList() };
        var (renamed, map) = Rename(s);

        var input = WorldSummarizer.ForDirector(s, model.PromptTier);
        var on = await Ask(prompt, s, model, grammar: true, complete, 0).ConfigureAwait(false);
        var off = await Ask(prompt, s, model, grammar: false, complete, 1).ConfigureAwait(false);
        var o2 = await Ask(prompt, order2, model, grammar: true, complete, 2).ConfigureAwait(false);
        var o3 = await Ask(prompt, order3, model, grammar: true, complete, 3).ConfigureAwait(false);
        var ids = await Ask(prompt, renamed, model, grammar: true, complete, 4).ConfigureAwait(false);
        var back = map.ToDictionary(kv => kv.Value, kv => kv.Key);
        var idsChoice = ids.Choice is { } c && back.TryGetValue(c, out var original) ? original : null;
        var briefing = prompt.Render(model.PromptTier, input.Variables).User;
        return new CaseRun(s.Id, on, off, o2, o3, ids, idsChoice, input.AllowedNames, briefing);
    }

    public static async Task<Call> Ask(PromptTemplate prompt, WorldSnapshot s, ModelConfig model, bool grammar,
        Func<ChatRequest, int, Task<ChatResult>> complete, int variant)
    {
        var input = WorldSummarizer.ForDirector(s, model.PromptTier);
        var rendered = prompt.Render(model.PromptTier, input.Variables);
        var schema = Director.WithChoiceEnum(prompt.Schema!, s.AllowedTransitions);
        var result = await complete(new ChatRequest
        {
            System = rendered.System,
            User = rendered.User,
            MaxOutputTokens = rendered.MaxOutputTokens,
            Schema = schema,
            UseGrammar = grammar,
            Thinking = model.ThinkingFor(TaskName),
        }, variant).ConfigureAwait(false);

        if (!result.Ok || result.Text is null)
        {
            return new Call(false, false, null, null, result.Latency.TotalSeconds);
        }

        var json = new JsonSchemaCheck(prompt.Schema!.Schema).Check(result.Text).Passed;
        var allowed = new AllowedChoiceCheck("choice", s.AllowedTransitions).Check(result.Text).Passed;
        var node = JsonText.TryParse(result.Text, out _) as JsonObject;
        var choice = node?["choice"] is JsonValue cv && cv.TryGetValue<string>(out var cs) ? cs : null;
        var reason = node?["reason"] is JsonValue rv && rv.TryGetValue<string>(out var rs) ? rs : null;
        return new Call(json, allowed, allowed ? choice : null, reason, result.Latency.TotalSeconds);
    }

    /// <summary>Option ids become C1, C2, … (Wait stays); returns the renamed case and the map from old to new ids.</summary>
    public static (WorldSnapshot, Dictionary<string, string>) Rename(WorldSnapshot s)
    {
        var map = DirectorDataset.RenameMap(s.AllowedTransitions);
        var (renamed, _) = DirectorDataset.RenameIds(s, s.AllowedTransitions[0]);
        return (renamed, map);
    }

    private static List<CaseRun> AlwaysWaitRuns(IReadOnlyList<Label> labels) => labels.Select(l =>
    {
        var pick = l.Allowed.Contains("Wait") ? "Wait" : l.Allowed[0];
        var call = new Call(true, true, pick, null, 0);
        return new CaseRun(l.Case, call, call, call, call, call, pick, [], "");
    }).ToList();

    // ---------- labels ----------

    public static List<Label> LoadLabels(ReportOptions o)
    {
        var lines = File.ReadAllLines(Path.Combine(o.LabelsDir, "labels-final.jsonl")).Where(l => l.Trim().Length > 0)
            .Select(l => JsonNode.Parse(l)!).OrderBy(n => n["case"]!.GetValue<string>(), StringComparer.Ordinal).ToList();
        var labels = new List<Label>();
        // A quick look samples evenly: the generator lays situations out in blocks of ids, so the first N would all be alike.
        var step = o.Count is { } c && c > 0 && c < lines.Count ? (double)lines.Count / c : 1.0;
        var picked = step == 1.0 ? lines : Enumerable.Range(0, o.Count!.Value).Select(i => lines[(int)(i * step)]).ToList();
        foreach (var n in picked)
        {
            var id = n["case"]!.GetValue<string>();
            var s = SnapshotJson.Load(Path.Combine(o.CasesDir, id + ".json"));
            var text = SnapshotJson.Serialize(s);
            labels.Add(new Label(id, DirectorLabels.Group(s), s.Director?.WaitExpected,
                CasePools.InjectionNames.Any(text.Contains), n["best"]!.GetValue<string>(),
                n["scores"]!.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<double>()), s.AllowedTransitions));
        }

        return labels;
    }

    // ---------- scoring ----------

    public sealed record Metrics
    {
        public required string Model { get; init; }
        public int Cases { get; init; }
        public double JsonOn { get; init; }
        public double JsonOff { get; init; }
        public double ChoiceOn { get; init; }
        public double ChoiceOff { get; init; }
        public double Top1 { get; init; }
        public double Top2 { get; init; }
        public double Regret { get; init; }
        public double? WaitRight { get; init; }
        public double? WaitWrong { get; init; }
        public double? Position { get; init; }
        public double? Ids { get; init; }
        public double? InventedPer50 { get; init; }
        public double? Injection { get; init; }
        public double? P50 { get; init; }
        public double? P95 { get; init; }
        public double ChoseWait { get; init; }
        public Dictionary<string, double> Top1ByGroup { get; init; } = [];
        public List<string> InventedExamples { get; init; } = [];
    }

    public static Metrics Score(string model, IReadOnlyList<Label> labels, IReadOnlyList<CaseRun> runs)
    {
        var byCase = runs.ToDictionary(r => r.Case);
        var pairs = labels.Select(l => (L: l, R: byCase[l.Case])).ToList();
        var baseline = model == AlwaysWait;
        double Share<T>(IEnumerable<T> xs, Func<T, bool> f) { var list = xs.ToList(); return list.Count == 0 ? double.NaN : (double)list.Count(f) / list.Count; }

        var invented = baseline ? [] : pairs.Select(p => (p.L.Case, Found: InventedFacts(p.R.On.Reason, p.R.Names, p.R.Briefing))).Where(x => x.Found.Count > 0).ToList();
        var latencies = runs.Select(r => r.On.LatencySeconds).Where(x => x > 0).Order().ToList();
        var waitRight = pairs.Where(p => p.L.WaitExpected == true).ToList();
        var waitWrong = pairs.Where(p => p.L.WaitExpected == false).ToList();
        var injection = pairs.Where(p => p.L.Injection).ToList();

        return new Metrics
        {
            Model = model,
            Cases = pairs.Count,
            JsonOn = Share(pairs, p => p.R.On.JsonValid),
            JsonOff = Share(pairs, p => p.R.Off.JsonValid),
            ChoiceOn = Share(pairs, p => p.R.On.ChoiceAllowed),
            ChoiceOff = Share(pairs, p => p.R.Off.ChoiceAllowed),
            Top1 = Share(pairs, p => p.R.On.Choice == p.L.Best),
            Top2 = Share(pairs, p => p.R.On.Choice is { } c && TopTwo(p.L.Scores).Contains(c)),
            Regret = pairs.Average(p => Regret(p.L.Scores, p.L.Best, p.R.On.Choice)),
            WaitRight = waitRight.Count == 0 ? null : Share(waitRight, p => p.R.On.Choice == "Wait"),
            WaitWrong = waitWrong.Count == 0 ? null : Share(waitWrong, p => p.R.On.Choice is { } c && c != "Wait"),
            Position = baseline ? null : Share(pairs, p => p.R.On.Choice is { } c && p.R.Order2.Choice == c && p.R.Order3.Choice == c),
            Ids = baseline ? null : Share(pairs, p => p.R.On.Choice is { } c && p.R.IdsChoice == c),
            InventedPer50 = baseline ? null : 50.0 * invented.Count / Math.Max(1, pairs.Count),
            Injection = baseline || injection.Count == 0 ? null : Share(injection, p => p.R.On.ChoiceAllowed && p.R.Off.ChoiceAllowed),
            P50 = baseline || latencies.Count == 0 ? null : Percentile(latencies, 0.5),
            P95 = baseline || latencies.Count == 0 ? null : Percentile(latencies, 0.95),
            ChoseWait = Share(pairs, p => p.R.On.Choice == "Wait"),
            Top1ByGroup = pairs.GroupBy(p => p.L.Group).OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => Share(g, p => p.R.On.Choice == p.L.Best)),
            InventedExamples = invented.Take(10).Select(x => $"{x.Case}: {string.Join("; ", x.Found)}").ToList(),
        };
    }

    /// <summary>The options with the top two mean scores (ties included).</summary>
    public static HashSet<string> TopTwo(IReadOnlyDictionary<string, double> scores)
    {
        var distinct = scores.Values.Distinct().OrderDescending().ToList();
        var cut = distinct.Count > 1 ? distinct[1] : distinct.FirstOrDefault();
        return scores.Where(kv => kv.Value >= cut).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The teacher's score for its best minus its score for the pick; an invalid pick costs as much as the
    /// worst option.</summary>
    public static double Regret(IReadOnlyDictionary<string, double> scores, string best, string? choice) =>
        scores[best] - (choice is not null && scores.TryGetValue(choice, out var s) ? s : scores.Values.Min());

    private static double Percentile(IReadOnlyList<double> sorted, double p) => sorted[(int)Math.Clamp(Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

    // ---------- invented facts ----------

    /// <summary>Claims a reason makes about the colony's condition, and words in the briefing that would back each.</summary>
    private static readonly (string Claim, Regex Says, string[] Backed)[] Claims =
    [
        ("food or starving", ClaimFood(), ["food", "starv", "hunger", "famine"]),
        ("a raid", ClaimRaid(), ["raid", "attack"]),
        ("a death", ClaimDeath(), ["died", "dead", "killed", "death"]),
        ("a cold snap", ClaimCold(), ["cold"]),
        ("disease", ClaimDisease(), ["plague", "infection", "sick", "disease"]),
        ("mental breaks", ClaimBreaks(), ["mental break", "berserk"]),
    ];

    [GeneratedRegex(@"\b(starv\w*|famine|hunger|hungry|food)\b", RegexOptions.IgnoreCase)] private static partial Regex ClaimFood();
    [GeneratedRegex(@"\b(raid(ed|s|ers)?|attack(ed)?)\b", RegexOptions.IgnoreCase)] private static partial Regex ClaimRaid();
    [GeneratedRegex(@"\b(died|death|dead|killed|grieving|mourning)\b", RegexOptions.IgnoreCase)] private static partial Regex ClaimDeath();
    [GeneratedRegex(@"\bcold\b", RegexOptions.IgnoreCase)] private static partial Regex ClaimCold();
    [GeneratedRegex(@"\b(plague|sick\w*|disease|infect\w*)\b", RegexOptions.IgnoreCase)] private static partial Regex ClaimDisease();
    [GeneratedRegex(@"\bmental breaks?\b|\bberserk\b", RegexOptions.IgnoreCase)] private static partial Regex ClaimBreaks();

    /// <summary>What a reason states that the briefing doesn't: names it doesn't contain, and claims about the colony
    /// (food, raids, deaths, weather, disease, breaks) with nothing behind them. D0's student wrote "starving" for
    /// colonies short of steel or medicine.</summary>
    public static List<string> InventedFacts(string? reason, IReadOnlyCollection<string> names, string briefing)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return [];
        }

        var found = new GroundingCheck(names).FindUnknownNames(reason).Select(n => $"names {n}").ToList();
        foreach (var (claim, says, backed) in Claims)
        {
            if (says.IsMatch(reason) && !backed.Any(b => briefing.Contains(b, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add($"claims {claim}");
            }
        }

        return found;
    }

    // ---------- report ----------

    private static string Pct(double? x) => x is { } v && !double.IsNaN(v) ? (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%" : "–";
    private static string Num(double? x, string f = "0.00") => x is { } v && !double.IsNaN(v) ? v.ToString(f, CultureInfo.InvariantCulture) : "–";

    public static string Markdown(ReportOptions o, string promptVersion, IReadOnlyList<Label> labels, IReadOnlyList<Metrics> metrics,
        IReadOnlyDictionary<string, List<CaseRun>> results)
    {
        var (commit, dirty) = Runner.GitInfo();
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"# Director report: {o.Name}\n\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"{labels.Count} test cases from `{Path.GetFileName(o.CasesDir.TrimEnd('/', '\\'))}`, labels `{Path.GetFileName(o.LabelsDir.TrimEnd('/', '\\'))}`; "
            + $"prompt `{TaskName}/{promptVersion}`; commit `{commit?[..Math.Min(7, commit.Length)] ?? "?"}`{(dirty == true ? " (uncommitted changes)" : "")}; "
            + $"{DateTime.UtcNow:yyyy-MM-dd}.\n\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"Best answer is Wait in {labels.Count(l => l.Best == "Wait")} of {labels.Count} cases; {labels.Count(l => l.WaitExpected == true)} wait-right, "
            + $"{labels.Count(l => l.WaitExpected == false)} wait-wrong, {labels.Count(l => l.Injection)} with an instruction-like name. "
            + $"**always-Wait** picks Wait whenever it's offered: a model has to beat it to be worth anything.\n\n");

        sb.Append("| Model | JSON on / off | Choice on / off | Top-1 | Top-2 | Regret | Wait-right | Wait-wrong | Same over orders | Same with ids renamed | Invented / 50 | Injection | p50 / p95 s | Picks Wait |\n");
        sb.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var m in metrics)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"| {m.Model} | {Pct(m.JsonOn)} / {Pct(m.JsonOff)} | {Pct(m.ChoiceOn)} / {Pct(m.ChoiceOff)} | {Pct(m.Top1)} | {Pct(m.Top2)} | {Num(m.Regret)} | "
                + $"{Pct(m.WaitRight)} | {Pct(m.WaitWrong)} | {Pct(m.Position)} | {Pct(m.Ids)} | {Num(m.InventedPer50, "0.0")} | {Pct(m.Injection)} | "
                + $"{Num(m.P50, "0.0")} / {Num(m.P95, "0.0")} | {Pct(m.ChoseWait)} |\n");
        }

        var baseline = metrics.First(m => m.Model == AlwaysWait);
        var basePrompted = o.BaseModel is { } b ? metrics.FirstOrDefault(m => m.Model == b) : null;
        sb.Append("\n## Ship bars (training spec §7)\n\n");
        sb.Append("| Model | JSON + choice with grammar 100% | without ≥98% | Top-1 ≥85% | Top-2 ≥95% | Regret ≤0.4 | Wait accuracy ≥85% each | Orders ≥90% | Ids ≥90% | Invented ≤1/50 | Injection 100% | p95 <10 s | Beats always-Wait | Lift over base ≥10 pts |\n");
        sb.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        static string Ok(bool? pass) => pass switch { true => "✅", false => "❌", null => "–" };
        foreach (var m in metrics.Where(m => m.Model != AlwaysWait))
        {
            var lift = basePrompted is null || basePrompted.Model == m.Model ? (bool?)null : m.Top1 - basePrompted.Top1 >= 0.10;
            sb.Append(CultureInfo.InvariantCulture,
                $"| {m.Model} | {Ok(m.JsonOn >= 1 && m.ChoiceOn >= 1)} | {Ok(m.JsonOff >= 0.98 && m.ChoiceOff >= 0.98)} | {Ok(m.Top1 >= 0.85)} | {Ok(m.Top2 >= 0.95)} | "
                + $"{Ok(m.Regret <= 0.4)} | {Ok(m.WaitRight is null || m.WaitWrong is null ? null : m.WaitRight >= 0.85 && m.WaitWrong >= 0.85)} | "
                + $"{Ok(m.Position >= 0.9)} | {Ok(m.Ids >= 0.9)} | {Ok(m.InventedPer50 <= 1)} | {Ok(m.Injection is null ? null : m.Injection >= 1)} | "
                + $"{Ok(m.P95 is null ? null : m.P95 < 10)} | {Ok(m.Top1 > baseline.Top1)} | {Ok(lift)} |\n");
        }

        sb.Append("\nTop-1 is against the teacher's labels, whose self-agreement on repeated cases was 100% (inflated: one session), so the bar is 85% of that.\n");

        sb.Append("\n## Top-1 by arc and beat\n\n| Group | Cases | ");
        sb.Append(string.Join(" | ", metrics.Select(m => m.Model))).Append(" |\n| --- | --- | ").Append(string.Join(" | ", metrics.Select(_ => "---"))).Append(" |\n");
        foreach (var g in labels.GroupBy(l => l.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append(CultureInfo.InvariantCulture, $"| {g.Key} | {g.Count()} | ").Append(string.Join(" | ", metrics.Select(m => Pct(m.Top1ByGroup.GetValueOrDefault(g.Key, double.NaN))))).Append(" |\n");
        }

        foreach (var m in metrics.Where(m => m.InventedExamples.Count > 0))
        {
            sb.Append(CultureInfo.InvariantCulture, $"\n## Invented facts: {m.Model} (first {m.InventedExamples.Count})\n\n");
            foreach (var e in m.InventedExamples)
            {
                sb.Append("- ").Append(e).Append('\n');
            }
        }

        sb.Append("\nInvented facts are found automatically: names not in the briefing, and claims about food, raids, deaths, cold, disease or mental breaks that the briefing doesn't mention. A Claude judge pass (training spec §7) isn't run, to spare API credits; check the examples by eye.\n");
        return sb.ToString();
    }
}
