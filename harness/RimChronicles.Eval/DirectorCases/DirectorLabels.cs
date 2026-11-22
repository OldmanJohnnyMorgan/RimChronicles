using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>
/// Labelling packs for a teacher working by hand (training track D4: Claude Code on the subscription). <c>director-pack</c>
/// picks cases spread across arcs and beats, renders each through the teacher prompt with the options shuffled, and
/// writes <c>pack.md</c> (to read) and <c>items.jsonl</c> (what each item showed). The teacher writes
/// <c>labels.jsonl</c>, one line per item: <c>{"item":1,"scores":{...},"best":"...","reason":"..."}</c>.
/// <c>director-labels-check</c> validates the labels and aggregates them per case into <c>labels-final.jsonl</c>.
/// </summary>
public static class DirectorLabels
{
    public const string TeacherTask = "director-teacher";
    public const int MaxReasonWords = 30;

    private static readonly JsonSerializerOptions Line = new() { WriteIndented = false };

    public static int Pack(string casesDir, int count, int repeat, int seed, string outDir, string promptsRoot, TextWriter log)
    {
        var prompt = PromptLibrary.Load(promptsRoot).Get(TeacherTask);
        var cases = Directory.GetFiles(casesDir, "*.json").Where(f => Path.GetFileName(f) != "manifest.json")
            .Order(StringComparer.Ordinal).Select(SnapshotJson.Load).Where(s => s.Director?.PlotId is not null).ToList();
        var rng = new Random(seed);
        var picked = PickRoundRobin(cases, count, rng);

        // The first `repeat` cases are shown twice, in another option order, to measure the teacher's self-agreement.
        var items = picked.Select(s => (Case: s, Sample: 1)).Concat(picked.Take(repeat).Select(s => (Case: s, Sample: 2)))
            .OrderBy(_ => rng.Next()).ToList();

        Directory.CreateDirectory(outDir);
        var md = new StringBuilder();
        var rendered0 = prompt.Render(ModelTier.High, Variables(items[0].Case, items[0].Case.AllowedTransitions));
        md.Append("# Director labelling pack\n\n").Append(CultureInfo(picked.Count, repeat, prompt.Meta.Version))
          .Append("\n\n## Teacher instructions (the system prompt)\n\n").Append(rendered0.System.Trim()).Append("\n\n");
        var jsonl = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
        {
            var (snapshot, sample) = items[i];
            var order = snapshot.AllowedTransitions.OrderBy(_ => rng.Next()).ToList();
            var rendered = prompt.Render(ModelTier.High, Variables(snapshot, order));
            md.Append($"---\n\n## Item {i + 1}\n\n").Append(rendered.User.Trim()).Append("\n\n");
            jsonl.Append(JsonSerializer.Serialize(new JsonObject
            {
                ["item"] = i + 1,
                ["case"] = snapshot.Id,
                ["sample"] = sample,
                ["order"] = new JsonArray(order.Select(o => (JsonNode?)JsonValue.Create(o)).ToArray()),
                ["promptVersion"] = $"{TeacherTask}/{prompt.Meta.Version}",
            }, Line)).Append('\n');
        }

        File.WriteAllText(Path.Combine(outDir, "pack.md"), md.ToString());
        File.WriteAllText(Path.Combine(outDir, "items.jsonl"), jsonl.ToString());
        log.WriteLine($"Wrote {items.Count} items ({picked.Count} cases, {repeat} shown twice) to {outDir}. "
            + $"Groups: {string.Join(", ", picked.GroupBy(Group).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"))}.");
        return 0;
    }

    /// <summary>Round-robin over (arc, beat) groups, cases shuffled within each, so a small or partial pick still covers
    /// every group.</summary>
    public static List<WorldSnapshot> PickRoundRobin(IReadOnlyList<WorldSnapshot> cases, int count, Random rng)
    {
        var groups = cases.GroupBy(Group).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new Queue<WorldSnapshot>(g.OrderBy(_ => rng.Next()))).ToList();
        var picked = new List<WorldSnapshot>();
        while (picked.Count < count && groups.Any(g => g.Count > 0))
        {
            foreach (var g in groups.Where(g => g.Count > 0))
            {
                if (picked.Count < count)
                {
                    picked.Add(g.Dequeue());
                }
            }
        }

        return picked;
    }

    /// <summary>What's wrong with one label for an item that showed <paramref name="order"/>; empty when it's fine.</summary>
    public static List<string> Problems(IReadOnlyDictionary<string, int> scores, string best, string reason,
        IReadOnlyList<string> order, IReadOnlyCollection<string> allowedNames)
    {
        var problems = new List<string>();
        if (!scores.Keys.Order(StringComparer.Ordinal).SequenceEqual(order.Order(StringComparer.Ordinal)))
        {
            problems.Add("scores must cover exactly the shown options");
        }

        if (scores.Values.Any(v => v is < 1 or > 5))
        {
            problems.Add("scores must be 1 to 5");
        }

        if (!order.Contains(best) || scores.GetValueOrDefault(best) < scores.Values.DefaultIfEmpty(0).Max())
        {
            problems.Add("best must be a shown option with the top score");
        }

        if (WordCountCheck.Count(reason) > MaxReasonWords)
        {
            problems.Add($"reason over {MaxReasonWords} words");
        }

        if (new GroundingCheck(allowedNames).FindUnknownNames(reason) is { Count: > 0 } unknown)
        {
            problems.Add($"reason names {string.Join(", ", unknown)}, not in the briefing");
        }

        return problems;
    }

    private static string CultureInfo(int cases, int repeat, string version) =>
        $"{cases} cases, {repeat} of them shown twice with the options in another order. Teacher prompt {TeacherTask}/{version}. "
        + "Write one JSON line per item to labels.jsonl: {\"item\": N, \"scores\": {...every option...}, \"best\": \"...\", \"reason\": \"...\"}.";

    /// <summary>"Nemesis/Hook", "AwakenedEmpire/Ascendant".</summary>
    public static string Group(WorldSnapshot s) =>
        s.ActivePlots.FirstOrDefault(p => p.Id == s.Director!.PlotId) is { } p ? $"{p.Template}/{p.Beat}" : "unknown";

    public static Dictionary<string, string> Variables(WorldSnapshot s, IReadOnlyList<string> order)
    {
        var input = WorldSummarizer.ForDirector(s with { AllowedTransitions = order }, ModelTier.High);
        return new Dictionary<string, string>(input.Variables);
    }

    /// <summary>Checks each label against what its item showed, and aggregates per case. Returns the problem count.</summary>
    public static int Check(string packDir, string casesDir, TextWriter log)
    {
        var items = File.ReadAllLines(Path.Combine(packDir, "items.jsonl")).Where(l => l.Length > 0)
            .Select(l => JsonNode.Parse(l)!).ToDictionary(n => n["item"]!.GetValue<int>());
        var labels = File.ReadAllLines(Path.Combine(packDir, "labels.jsonl")).Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!).ToList();
        var problems = new List<string>();
        var byCase = new Dictionary<string, List<(Dictionary<string, int> Scores, string Best, string Reason)>>();

        foreach (var label in labels)
        {
            var n = label["item"]!.GetValue<int>();
            if (!items.TryGetValue(n, out var item))
            {
                problems.Add($"item {n}: not in the pack");
                continue;
            }

            var caseId = item["case"]!.GetValue<string>();
            var order = item["order"]!.AsArray().Select(o => o!.GetValue<string>()).ToList();
            var scores = label["scores"]!.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<int>());
            var best = label["best"]!.GetValue<string>();
            var reason = label["reason"]!.GetValue<string>();
            var snapshot = SnapshotJson.Load(Path.Combine(casesDir, caseId + ".json"));
            var allowed = WorldSummarizer.ForDirector(snapshot, ModelTier.High).AllowedNames;
            var found = Problems(scores, best, reason, order, allowed);
            if (found.Count > 0)
            {
                problems.AddRange(found.Select(p => $"item {n}: {p}"));
                continue; // reported, and kept out of labels-final, so nothing trains on it
            }

            byCase.TryAdd(caseId, []);
            byCase[caseId].Add((scores, best, reason));
        }

        // Agreement counts against the samples planned for a case, not the ones that came back: a case where two of
        // three samples failed has one opinion, not a unanimous one.
        var planned = items.Values.GroupBy(i => i["case"]!.GetValue<string>()).ToDictionary(g => g.Key, g => g.Count());
        var final = new StringBuilder();
        int repeated = 0, agreed = 0, waitRight = 0, waitWrong = 0, waitRightPicked = 0, waitWrongAvoided = 0;
        foreach (var (caseId, samples) in byCase.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var snapshot = SnapshotJson.Load(Path.Combine(casesDir, caseId + ".json"));
            var counts = samples.GroupBy(s => s.Best).ToDictionary(g => g.Key, g => g.Count());
            var top = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
            var mean = samples.SelectMany(s => s.Scores).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => Math.Round(g.Average(kv => kv.Value), 2));
            if (samples.Count > 1)
            {
                repeated++;
                agreed += samples.All(s => s.Best == samples[0].Best) ? 1 : 0;
            }

            switch (snapshot.Director?.WaitExpected)
            {
                case true:
                    waitRight++;
                    waitRightPicked += top == "Wait" ? 1 : 0;
                    break;
                case false:
                    waitWrong++;
                    waitWrongAvoided += top != "Wait" ? 1 : 0;
                    break;
            }

            final.Append(JsonSerializer.Serialize(new JsonObject
            {
                ["case"] = caseId,
                ["group"] = Group(snapshot),
                ["samples"] = samples.Count,
                ["planned"] = planned[caseId],
                ["choiceCounts"] = new JsonObject(counts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
                ["scores"] = new JsonObject(mean.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
                ["best"] = top,
                ["agreement"] = Math.Round(counts[top] / (double)Math.Max(samples.Count, planned[caseId]), 2),
                ["reason"] = samples.First(s => s.Best == top).Reason,
                ["waitExpected"] = snapshot.Director?.WaitExpected is { } w ? JsonValue.Create(w) : null,
            }, Line)).Append('\n');
        }

        File.WriteAllText(Path.Combine(packDir, "labels-final.jsonl"), final.ToString());
        foreach (var p in problems)
        {
            log.WriteLine(p);
        }

        log.WriteLine($"{labels.Count} labels for {byCase.Count} cases; {problems.Count} problem(s).");
        log.WriteLine($"Self-agreement: {agreed}/{repeated} repeated cases picked the same option.");
        log.WriteLine($"Wait: picked in {waitRightPicked}/{waitRight} wait-right cases; avoided in {waitWrongAvoided}/{waitWrong} wait-wrong cases.");
        return problems.Count;
    }
}
