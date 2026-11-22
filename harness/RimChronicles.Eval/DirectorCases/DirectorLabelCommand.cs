using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>Options for <c>director-label</c>.</summary>
public sealed record LabelOptions
{
    public required string CasesDir { get; init; }
    public required string OutDir { get; init; }
    public required string PromptsRoot { get; init; }
    public required string CacheDir { get; init; }

    /// <summary>Label folders whose cases are skipped (labelled already, by a stronger teacher).</summary>
    public IReadOnlyList<string> SkipLabelDirs { get; init; } = [];

    /// <summary>Label exactly these cases (a recheck of another teacher's labels); null picks <see cref="Count"/>.</summary>
    public IReadOnlySet<string>? OnlyCases { get; init; }

    public int Count { get; init; } = 4000;

    /// <summary>Samples per case, each with the options in a fresh order (the training spec's 5, cut for GPU time).</summary>
    public int Samples { get; init; } = 3;

    public int Seed { get; init; } = 1;
    public double Temperature { get; init; } = 0.7;

    /// <summary>Generator seeds of the frozen test split: never labelled by a local teacher.</summary>
    public IReadOnlySet<int> TestSeeds { get; init; } = new HashSet<int> { 9001 };

    /// <summary>Reject a label that contradicts the case's <c>waitExpected</c> (set by the generator by construction,
    /// the same rule the hand labels matched 20/20 and 222/222), so a teacher that never pauses can't teach that.
    /// Open cases (null) are left to the teacher.</summary>
    public bool EnforceWait { get; init; } = true;

    /// <summary>Attempts per item before it's recorded as a failure (each retry is a new sample, so a new cache key).</summary>
    public int MaxAttempts { get; init; } = 3;
}

/// <summary>
/// <c>director-label</c> (training track D4, bulk): a model from models.json labels cases with the teacher rubric
/// (<c>prompts/director-teacher/</c>), writing a pack in the same format a hand-labelled one has (<c>items.jsonl</c>,
/// <c>labels.jsonl</c>), so <c>director-labels-check</c> and <c>director-dataset</c> treat both alike. Each call is
/// grammar-constrained to exactly the shown options, checked like a hand label, cached, and appended as it finishes:
/// stopping and running again carries on where it left off.
/// </summary>
public static partial class DirectorLabelCommand
{
    /// <summary>Words from the rubric's machinery. A reason the student copies should be about the story (D4b recheck v2:
    /// "Since the beat has waited fewer than 25 days, Wait is the highest scoring pacing option").</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"\b(pacing|scor(e|es|ed|ing)|step [12]|rubric)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    public static partial System.Text.RegularExpressions.Regex RubricTalk();

    private static readonly JsonSerializerOptions Line = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions Plain = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<int> RunAsync(LabelOptions o, ModelConfig model, IChatProvider provider, TextWriter log, CancellationToken ct)
    {
        var prompt = PromptLibrary.Load(o.PromptsRoot).Get(DirectorLabels.TeacherTask);
        Directory.CreateDirectory(o.OutDir);
        var itemsPath = Path.Combine(o.OutDir, "items.jsonl");
        var labelsPath = Path.Combine(o.OutDir, "labels.jsonl");
        var failuresPath = Path.Combine(o.OutDir, "failures.jsonl");

        var version = $"{DirectorLabels.TeacherTask}/{prompt.Meta.Version}";
        if (File.Exists(itemsPath) && File.ReadLines(itemsPath).FirstOrDefault(l => l.Trim().Length > 0) is { } first
            && JsonNode.Parse(first)!["promptVersion"]?.GetValue<string>() is { } planned && planned != version)
        {
            throw new ArgumentException($"{o.OutDir} was labelled with {planned}, not {version}: "
                + "use another --out, or delete the folder to start over (finished calls stay in the cache).");
        }

        var items = File.Exists(itemsPath) ? LoadItems(itemsPath) : PlanItems(o, prompt.Meta.Version, itemsPath, log);
        if (items.Count == 0)
        {
            log.WriteLine("Nothing to label.");
            return 1;
        }

        var done = File.Exists(labelsPath)
            ? File.ReadAllLines(labelsPath).Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!["item"]!.GetValue<int>()).ToHashSet()
            : [];
        var todo = items.Where(i => !done.Contains(i.Number)).ToList();
        // Earlier failed runs of an item: a retry takes new attempts, since the cache would replay the old answers.
        var earlierFailures = File.Exists(failuresPath)
            ? File.ReadAllLines(failuresPath).Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!["item"]!.GetValue<int>())
                .GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count())
            : [];
        log.WriteLine($"{model.Id}: {items.Count} items in {o.OutDir}, {done.Count} labelled, {todo.Count} to go "
            + $"({o.Samples} per case, temperature {o.Temperature}).");

        var stats = new CacheStats();
        var identity = model.Identity();
        var clock = Stopwatch.StartNew();
        int ok = 0, failed = 0, waitRejects = 0, forced = 0;
        foreach (var item in todo)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = SnapshotJson.Load(Path.Combine(o.CasesDir, item.Case + ".json"));
            var rendered = prompt.Render(ModelTier.High, DirectorLabels.Variables(snapshot, item.Order));
            var allowedNames = WorldSummarizer.ForDirector(snapshot, ModelTier.High).AllowedNames;
            var errors = new List<string>();
            JsonObject? label = null;

            var waitRule = o.EnforceWait ? snapshot.Director?.WaitExpected : null;
            var generation = earlierFailures.GetValueOrDefault(item.Number);
            for (var attempt = 0; attempt < o.MaxAttempts && label is null; attempt++)
            {
                // The last attempt on a case with a Wait rule puts the rule in the grammar: the teacher still scores
                // and explains, but can't pick against it. Without this, the wait-right cases it won't wait on would
                // drop out of training, and they're the lesson the student most needs (recheck v3: 5 of 20).
                var force = waitRule is not null && attempt == o.MaxAttempts - 1;
                var repeat = item.Sample * 100 + generation * o.MaxAttempts + attempt;
                var cached = new CachingChatProvider(provider, o.CacheDir, identity, repeat, stats);
                var result = await cached.CompleteAsync(new ChatRequest
                {
                    System = rendered.System,
                    User = rendered.User,
                    MaxOutputTokens = rendered.MaxOutputTokens,
                    Schema = SchemaFor(item.Order, force ? waitRule : null),
                    UseGrammar = true,
                    Thinking = model.ThinkingFor(DirectorLabels.TeacherTask),
                    ExtraBody = new Dictionary<string, JsonNode?> { ["temperature"] = o.Temperature },
                }, ct).ConfigureAwait(false);

                if (!result.Ok)
                {
                    errors.Add(result.Error ?? "call failed");
                    continue;
                }

                if (Parse(result.Text) is not { } parsed)
                {
                    errors.Add("not valid label JSON");
                    continue;
                }

                var problems = DirectorLabels.Problems(parsed.Scores, parsed.Best, parsed.Reason, item.Order, allowedNames);
                if (problems.Count > 0)
                {
                    errors.Add(string.Join("; ", problems));
                    continue;
                }

                if (RubricTalk().IsMatch(parsed.Reason))
                {
                    errors.Add("the reason talks about the scoring, not the colony");
                    continue;
                }

                if (WaitMismatch(o.EnforceWait ? snapshot.Director?.WaitExpected : null, parsed.Best) is { } mismatch)
                {
                    waitRejects++;
                    errors.Add(mismatch);
                    continue;
                }

                label = new JsonObject
                {
                    ["item"] = item.Number,
                    ["teacher"] = $"{model.Id} ({model.Model})",
                    ["scores"] = new JsonObject(parsed.Scores.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
                    ["best"] = parsed.Best,
                    ["reason"] = parsed.Reason,
                    ["attempts"] = attempt + 1,
                };
                if (force)
                {
                    label["forced"] = true;
                    forced++;
                }
            }

            if (label is not null)
            {
                ok++;
                await File.AppendAllTextAsync(labelsPath, JsonSerializer.Serialize(label, Plain) + "\n", ct).ConfigureAwait(false);
            }
            else
            {
                failed++;
                await File.AppendAllTextAsync(failuresPath, JsonSerializer.Serialize(new JsonObject
                {
                    ["item"] = item.Number,
                    ["case"] = item.Case,
                    ["errors"] = new JsonArray(errors.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray()),
                }, Plain) + "\n", ct).ConfigureAwait(false);
            }

            var n = ok + failed;
            if (n % 25 == 0 || n == todo.Count)
            {
                var perItem = clock.Elapsed.TotalSeconds / n;
                var left = TimeSpan.FromSeconds(perItem * (todo.Count - n));
                log.WriteLine($"[{done.Count + n}/{items.Count}] {ok} labelled, {failed} failed, {waitRejects} answers against the Wait rule; {perItem:0.0} s per item, "
                    + $"about {left.TotalHours:0.0} h left (cache {stats.Hits} hits)");
            }
        }

        log.WriteLine($"Answers rejected for contradicting the case's Wait rule: {waitRejects} (they were asked again); "
            + $"{forced} labels came from the last attempt, with the rule in the grammar.");
        log.WriteLine($"Done: {ok} labelled, {failed} failed{(failed > 0 ? $" (see {failuresPath}; run again to retry them with fresh attempts)" : "")}. "
            + $"Next: director-labels-check --pack {o.OutDir} --cases {o.CasesDir}");
        return 0;
    }

    /// <summary>Why a pick contradicts the case's expected answer about waiting, or null.</summary>
    public static string? WaitMismatch(bool? waitExpected, string best) => waitExpected switch
    {
        true when best != "Wait" => "the colony is reeling and the beat is young: the case calls for Wait",
        false when best == "Wait" => "the beat has stalled in a colony that isn't reeling: the case calls for moving on",
        _ => null,
    };

    public sealed record Item(int Number, string Case, int Sample, IReadOnlyList<string> Order);

    /// <summary>Picks the cases (round-robin over arc and beat, skipping test seeds and cases labelled elsewhere) and
    /// fixes every item's option order, so the plan is the same however often the run is restarted.</summary>
    public static List<Item> PlanItems(LabelOptions o, string promptVersion, string itemsPath, TextWriter log)
    {
        var skip = o.SkipLabelDirs.Select(d => Path.Combine(d, "labels-final.jsonl")).Where(File.Exists)
            .SelectMany(File.ReadAllLines).Where(l => l.Trim().Length > 0)
            .Select(l => JsonNode.Parse(l)!["case"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var cases = Directory.GetFiles(o.CasesDir, "*.json").Where(f => Path.GetFileName(f) != "manifest.json")
            .Order(StringComparer.Ordinal).Select(SnapshotJson.Load).Where(s => s.Director?.PlotId is not null).ToList();
        if (cases.FirstOrDefault(s => s.Director?.Seed is { } seed && o.TestSeeds.Contains(seed)) is { } test)
        {
            throw new ArgumentException($"{o.CasesDir} holds test-split cases ({test.Id}, seed {test.Director!.Seed}); "
                + "the test set is labelled by Claude only (training spec: never train or tune on it).");
        }

        var candidates = cases.Where(s => !skip.Contains(s.Id) && (o.OnlyCases?.Contains(s.Id) ?? true)).ToList();
        var rng = new Random(o.Seed);
        var picked = DirectorLabels.PickRoundRobin(candidates, o.Count, rng);
        var items = new List<Item>();
        foreach (var s in picked)
        {
            for (var k = 1; k <= o.Samples; k++)
            {
                items.Add(new Item(items.Count + 1, s.Id, k, s.AllowedTransitions.OrderBy(_ => rng.Next()).ToList()));
            }
        }

        var sb = new StringBuilder();
        foreach (var i in items)
        {
            sb.Append(JsonSerializer.Serialize(new JsonObject
            {
                ["item"] = i.Number,
                ["case"] = i.Case,
                ["sample"] = i.Sample,
                ["order"] = new JsonArray(i.Order.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["promptVersion"] = $"{DirectorLabels.TeacherTask}/{promptVersion}",
            }, Line)).Append('\n');
        }

        File.WriteAllText(itemsPath, sb.ToString());
        log.WriteLine($"Planned {picked.Count} cases × {o.Samples} samples = {items.Count} items "
            + $"({cases.Count - candidates.Count} cases skipped as labelled already). "
            + $"Groups: {string.Join(", ", picked.GroupBy(DirectorLabels.Group).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"))}.");
        return items;
    }

    /// <summary>The case ids in a folder's <c>labels-final.jsonl</c>.</summary>
    public static HashSet<string> LabelledCases(string labelDir) =>
        File.ReadAllLines(Path.Combine(labelDir, "labels-final.jsonl")).Where(l => l.Trim().Length > 0)
            .Select(l => JsonNode.Parse(l)!["case"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    /// <summary>How often a recheck's best answers match the reference labels: overall, on Wait, and per arc and beat.</summary>
    public static int Compare(string referenceDir, string recheckDir, TextWriter log)
    {
        static Dictionary<string, JsonNode> Load(string dir) => File.ReadAllLines(Path.Combine(dir, "labels-final.jsonl"))
            .Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!).ToDictionary(n => n["case"]!.GetValue<string>());
        var reference = Load(referenceDir);
        var recheck = Load(recheckDir);
        var both = reference.Keys.Where(recheck.ContainsKey).Order(StringComparer.Ordinal).ToList();
        if (both.Count == 0)
        {
            log.WriteLine("No cases in common.");
            return 1;
        }

        string Best(Dictionary<string, JsonNode> d, string id) => d[id]["best"]!.GetValue<string>();
        var agree = both.Count(id => Best(reference, id) == Best(recheck, id));
        var refWait = both.Count(id => Best(reference, id) == "Wait");
        var newWait = both.Count(id => Best(recheck, id) == "Wait");
        log.WriteLine($"Agreement with {Path.GetFileName(referenceDir.TrimEnd('/', '\\'))}: {agree}/{both.Count} cases "
            + $"({100.0 * agree / both.Count:0}%) picked the same best option.");
        log.WriteLine($"Wait as best: reference {refWait}/{both.Count}, recheck {newWait}/{both.Count}.");
        foreach (var g in both.GroupBy(id => reference[id]["group"]?.GetValue<string>() ?? "?").OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            log.WriteLine($"  {g.Key}: {g.Count(id => Best(reference, id) == Best(recheck, id))}/{g.Count()}");
        }

        foreach (var id in both.Where(id => Best(reference, id) != Best(recheck, id)))
        {
            log.WriteLine($"  differs {id}: reference {Best(reference, id)}, recheck {Best(recheck, id)} ({recheck[id]["reason"]})");
        }

        return 0;
    }

    private static List<Item> LoadItems(string path) =>
        File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!).Select(n => new Item(
            n["item"]!.GetValue<int>(), n["case"]!.GetValue<string>(), n["sample"]!.GetValue<int>(),
            n["order"]!.AsArray().Select(x => x!.GetValue<string>()).ToList())).ToList();

    /// <summary>The teacher schema narrowed to this item: a score for exactly each shown option, and best one of them.
    /// With <paramref name="waitRule"/> set, the case's Wait rule is in the grammar too: true makes Wait the best with
    /// a 5 and every other option at most 4; false keeps Wait out of best and scores it at most 2.</summary>
    public static JsonSchemaSpec SchemaFor(IReadOnlyList<string> order, bool? waitRule = null)
    {
        var props = new JsonObject();
        foreach (var id in order)
        {
            var (min, max) = (waitRule, id == "Wait") switch
            {
                (true, true) => (5, 5),
                (true, false) => (1, 4),
                (false, true) => (1, 2),
                _ => (1, 5),
            };
            props[id] = new JsonObject { ["type"] = "integer", ["minimum"] = min, ["maximum"] = max };
        }

        var bestChoices = waitRule switch
        {
            true => ["Wait"],
            false => order.Where(id => id != "Wait").ToList(),
            _ => order.ToList(),
        };

        return new JsonSchemaSpec("director_label", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["scores"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = new JsonArray(order.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                    ["additionalProperties"] = false,
                },
                ["best"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(bestChoices.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) },
                ["reason"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray("scores", "best", "reason"),
            ["additionalProperties"] = false,
        });
    }

    public sealed record Parsed(Dictionary<string, int> Scores, string Best, string Reason);

    public static Parsed? Parse(string? text)
    {
        try
        {
            if (text is null || JsonText.TryParse(text, out _) is not JsonObject o
                || o["scores"] is not JsonObject scores || o["best"] is not JsonValue best || o["reason"] is not JsonValue reason)
            {
                return null;
            }

            return new Parsed(scores.ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<int>()), best.GetValue<string>(), reason.GetValue<string>().Trim());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
