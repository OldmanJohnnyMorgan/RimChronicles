using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Prompts;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>Options for <c>director-dataset</c>.</summary>
public sealed record DatasetOptions
{
    /// <summary>Folders with a <c>labels-final.jsonl</c> (from <c>director-labels-check</c>).</summary>
    public required IReadOnlyList<string> LabelDirs { get; init; }

    /// <summary>Folders the labelled cases live in (searched by case id).</summary>
    public required IReadOnlyList<string> CaseDirs { get; init; }

    public required string OutDir { get; init; }
    public required string PromptsRoot { get; init; }

    /// <summary>The tier the prompts are rendered for: the world summary is fitted to its token budget.</summary>
    public ModelTier Tier { get; init; } = ModelTier.Recommended;

    /// <summary>Generator seeds whose cases go to the test split; everything else trains.</summary>
    public IReadOnlySet<int> TestSeeds { get; init; } = new HashSet<int>();

    /// <summary>Share of recorded-in-game cases (no seed) that go to test, by a hash of the id.</summary>
    public double ExportedTestShare { get; init; } = 0.3;

    /// <summary>Share of the remaining cases held out for validation, by a hash of the id.</summary>
    public double ValShare { get; init; } = 0.1;

    /// <summary>Labels below this agreement are left out of SFT (training spec §4.3).</summary>
    public double MinAgreement { get; init; } = 0.6;

    /// <summary>Bump when the test split's rule changes (training spec: never edit it silently).</summary>
    public int TestVersion { get; init; } = 1;

    public int Seed { get; init; } = 1;
}

/// <summary>
/// <c>director-dataset</c> (training track D5): renders each labelled case through the director prompt exactly as the
/// game does and writes <c>train.jsonl</c>, <c>val.jsonl</c> and <c>test.jsonl</c>, one row per example with the
/// rendered system and user text and the target JSON. Splits go by case (a case and its augmented copies never
/// straddle two splits) and the test split by generator seed. Training rows are augmented as the training spec §4.6
/// says: three option orderings, ids renamed in 20% of copies, and a decoy mention of an option that isn't allowed in
/// 5%. A manifest records every input hash, so a dataset can be rebuilt exactly.
/// </summary>
public static class DirectorDataset
{
    public const string DirectorTask = "director";

    /// <summary>Plain escaping (apostrophes, quotes and accents as-is where JSON allows): the target is what the model
    /// learns to write, and <c>\u0027</c> instead of <c>'</c> would teach it escape codes.</summary>
    private static readonly JsonSerializerOptions Line = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Build(DatasetOptions o, TextWriter log)
    {
        var prompt = PromptLibrary.Load(o.PromptsRoot).Get(DirectorTask);
        var rng = new Random(o.Seed);
        var splits = new Dictionary<string, StringBuilder> { ["train"] = new(), ["val"] = new(), ["test"] = new() };
        var counts = new Dictionary<string, int> { ["train"] = 0, ["val"] = 0, ["test"] = 0 };
        var cases = new Dictionary<string, int> { ["train"] = 0, ["val"] = 0, ["test"] = 0 };
        int lowAgreement = 0, missing = 0, stale = 0, duplicate = 0;
        var inputs = new JsonArray();
        var seen = new Dictionary<string, string>();

        foreach (var dir in o.LabelDirs)
        {
            var file = Path.Combine(dir, "labels-final.jsonl");
            inputs.Add(new JsonObject { ["labels"] = Path.GetFileName(dir.TrimEnd('/', '\\')), ["sha256"] = Sha(File.ReadAllText(file)) });
            foreach (var line in File.ReadAllLines(file).Where(l => l.Length > 0))
            {
                var label = JsonNode.Parse(line)!;
                var caseId = label["case"]!.GetValue<string>();
                if (seen.TryGetValue(caseId, out var firstDir))
                {
                    duplicate++;
                    log.WriteLine($"{caseId}: labelled again in {Path.GetFileName(dir.TrimEnd('/', '\\'))}; keeping the label from {firstDir}");
                    continue;
                }

                seen[caseId] = Path.GetFileName(dir.TrimEnd('/', '\\'));
                if (label["agreement"]!.GetValue<double>() < o.MinAgreement)
                {
                    lowAgreement++;
                    continue;
                }

                var path = o.CaseDirs.Select(d => Path.Combine(d, caseId + ".json")).FirstOrDefault(File.Exists);
                if (path is null)
                {
                    missing++;
                    log.WriteLine($"{caseId}: case file not found in {string.Join(", ", o.CaseDirs)}");
                    continue;
                }

                var snapshot = SnapshotJson.Load(path);
                var best = label["best"]!.GetValue<string>();
                if (StaleReason(label, best, snapshot) is { } why)
                {
                    stale++;
                    log.WriteLine($"{caseId}: the case no longer matches its label ({why}); was it regenerated by a newer director-gen?");
                    continue;
                }

                var split = Split(snapshot, o);
                cases[split]++;
                var reason = label["reason"]!.GetValue<string>();
                foreach (var (variant, augment) in Variants(snapshot, best, split == "train", rng))
                {
                    var rendered = prompt.Render(o.Tier, WorldSummarizer.ForDirector(variant.Snapshot, o.Tier).Variables);
                    splits[split].Append(JsonSerializer.Serialize(new JsonObject
                    {
                        ["id"] = $"{caseId}#{augment}",
                        ["case"] = caseId,
                        ["seed"] = snapshot.Director?.Seed is { } s ? JsonValue.Create(s) : null,
                        ["group"] = DirectorLabels.Group(snapshot),
                        ["waitExpected"] = snapshot.Director?.WaitExpected is { } w ? JsonValue.Create(w) : null,
                        ["promptVersion"] = $"{DirectorTask}/{prompt.Meta.Version}",
                        ["tier"] = o.Tier.ToString(),
                        ["augment"] = augment,
                        ["system"] = rendered.System,
                        ["user"] = rendered.User,
                        ["allowed"] = new JsonArray(variant.Snapshot.AllowedTransitions.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray()),
                        ["target"] = JsonSerializer.Serialize(new { choice = variant.Choice, reason }, Line),
                    }, Line)).Append('\n');
                    counts[split]++;
                }
            }
        }

        Directory.CreateDirectory(o.OutDir);
        var hashes = new JsonObject();
        foreach (var (name, text) in splits)
        {
            File.WriteAllText(Path.Combine(o.OutDir, name + ".jsonl"), text.ToString());
            hashes[name] = Sha(text.ToString());
        }

        var manifest = new JsonObject
        {
            ["builder"] = "director-dataset",
            ["promptVersion"] = $"{DirectorTask}/{prompt.Meta.Version}",
            ["tier"] = o.Tier.ToString(),
            ["testVersion"] = o.TestVersion,
            ["testSeeds"] = new JsonArray(o.TestSeeds.Order().Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
            ["exportedTestShare"] = o.ExportedTestShare,
            ["valShare"] = o.ValShare,
            ["minAgreement"] = o.MinAgreement,
            ["seed"] = o.Seed,
            ["inputs"] = inputs,
            ["cases"] = new JsonObject(cases.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
            ["rows"] = new JsonObject(counts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
            ["sha256"] = hashes,
            ["leftOut"] = new JsonObject { ["lowAgreement"] = lowAgreement, ["caseMissing"] = missing, ["caseChanged"] = stale, ["duplicate"] = duplicate },
        };
        File.WriteAllText(Path.Combine(o.OutDir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        log.WriteLine($"Cases: train {cases["train"]}, val {cases["val"]}, test {cases["test"]} (rows {counts["train"]} / {counts["val"]} / {counts["test"]}); "
            + $"left out: {lowAgreement} low agreement, {missing} missing, {stale} changed since labelling, {duplicate} duplicate. Prompt {DirectorTask}/{prompt.Meta.Version}, tier {o.Tier}. Written to {o.OutDir}.");
        return missing == 0 && stale == 0 ? 0 : 1;
    }

    /// <summary>Why a label no longer fits its case, or null. Labels are committed but cases regenerate from their seed,
    /// so a newer generator can change a case under its label: the scored options must still be the case's options.</summary>
    public static string? StaleReason(JsonNode label, string best, WorldSnapshot s)
    {
        if (!s.AllowedTransitions.Contains(best))
        {
            return $"best '{best}' is not an allowed option";
        }

        if (label["scores"] is JsonObject scores)
        {
            var scored = scores.Select(kv => kv.Key).Order(StringComparer.Ordinal);
            if (!scored.SequenceEqual(s.AllowedTransitions.Order(StringComparer.Ordinal)))
            {
                return $"scored options [{string.Join(", ", scores.Select(kv => kv.Key))}] differ from [{string.Join(", ", s.AllowedTransitions)}]";
            }
        }

        return null;
    }

    /// <summary>The split for a case: test for the test seeds (and a share of in-game cases), then val by hash, else train.</summary>
    public static string Split(WorldSnapshot s, DatasetOptions o)
    {
        if (s.Director?.Seed is { } seed)
        {
            if (o.TestSeeds.Contains(seed))
            {
                return "test";
            }
        }
        else if (Unit(GroupKey(s.Id), "test") < o.ExportedTestShare)
        {
            return "test";
        }

        return Unit(GroupKey(s.Id), "val") < o.ValShare ? "val" : "train";
    }

    /// <summary>The key a case is split by. In-game cases (<c>dir-game-{colony}-day{n}-{plot}</c>) from one colony are
    /// near-copies of each other, so they split by colony: otherwise day 5 trains and day 6 tests. Others split by id.</summary>
    public static string GroupKey(string id) =>
        System.Text.RegularExpressions.Regex.Match(id, @"^dir-game-(.+)-day\d+-") is { Success: true } m ? "game:" + m.Groups[1].Value : id;

    /// <summary>A stable number in [0, 1) from an id, so a case always lands in the same split.</summary>
    public static double Unit(string id, string salt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + id));
        return BitConverter.ToUInt32(bytes, 0) / (double)uint.MaxValue;
    }

    public sealed record Variant(WorldSnapshot Snapshot, string Choice);

    /// <summary>The case as labelled; for training, two more orderings, with ids renamed in about 20% of copies and a
    /// decoy mention of a not-allowed option in about 5%.</summary>
    public static IEnumerable<(Variant Variant, string Augment)> Variants(WorldSnapshot s, string best, bool train, Random rng)
    {
        yield return (new Variant(s, best), "orig");
        if (!train)
        {
            yield break;
        }

        for (var copy = 1; copy <= 2; copy++)
        {
            var order = s.AllowedTransitions.OrderBy(_ => rng.Next()).ToList();
            var snapshot = s with { AllowedTransitions = order };
            var choice = best;
            var tags = new List<string> { $"order{copy}" };
            if (rng.NextDouble() < 0.2)
            {
                (snapshot, choice) = RenameIds(snapshot, best);
                tags.Add("ids");
            }

            if (rng.NextDouble() < 0.05)
            {
                snapshot = Decoy(snapshot, rng);
                tags.Add("decoy");
            }

            yield return (new Variant(snapshot, choice), string.Join("+", tags));
        }
    }

    /// <summary>Option ids become C1, C2, … (Wait stays "Wait"); descriptions are kept, so the model must read them.</summary>
    public static (WorldSnapshot, string) RenameIds(WorldSnapshot s, string best)
    {
        var map = new Dictionary<string, string>();
        var n = 1;
        foreach (var id in s.AllowedTransitions)
        {
            map[id] = id == "Wait" ? "Wait" : $"C{n++}";
        }

        var descriptions = s.Director?.Descriptions ?? new Dictionary<string, string>();
        var renamed = s with
        {
            AllowedTransitions = s.AllowedTransitions.Select(id => map[id]).ToList(),
            Director = (s.Director ?? new DirectorCase()) with
            {
                Descriptions = descriptions.Where(kv => map.ContainsKey(kv.Key)).ToDictionary(kv => map[kv.Key], kv => kv.Value),
            },
        };
        return (renamed, map[best]);
    }

    private static readonly string[] DecoyOptions = ["Empire:TributeDemand", "Complication:RaidOnTheColony", "Act3:StormTheCapital", "Choice:RescueTheCaptive"];

    /// <summary>A recent event naming an option id that isn't allowed, to teach that only the listed options count.</summary>
    public static WorldSnapshot Decoy(WorldSnapshot s, Random rng)
    {
        var decoy = DecoyOptions.Where(d => !s.AllowedTransitions.Contains(d)).OrderBy(_ => rng.Next()).FirstOrDefault();
        if (decoy is null)
        {
            return s;
        }

        return s with { RecentEvents = [.. s.RecentEvents, $"Day {s.Day}: a drifter kept repeating \"{decoy}\" as if it meant something."] };
    }

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
