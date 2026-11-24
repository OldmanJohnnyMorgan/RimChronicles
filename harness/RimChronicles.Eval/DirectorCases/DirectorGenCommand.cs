using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.DirectorCases;

/// <summary><c>director-gen</c>: writes <paramref name="count"/> generated director cases for a seed, each checked against
/// the snapshot schema, plus a coverage table and a manifest whose hash changes if any case does.</summary>
public static class DirectorGenCommand
{
    /// <summary>Bump when the generator's output changes for the same seed.</summary>
    public const int GeneratorVersion = 4; // v4: waitExpected is open when a treaty or governorship is offered

    /// <summary>Bump when the crisis cases' output changes for the same seed (crisis C11).</summary>
    public const int CrisisGeneratorVersion = 2; // v2: Reclaimed Holding in about half the Site cases

    /// <summary>Writes the cases; with <paramref name="sites"/> (the <c>crisis</c> arc), the crisis cases instead.</summary>
    public static int Run(int seed, int count, string outDir, string schemaPath, TextWriter log, IReadOnlyList<Core.Crisis.Sites.SiteArchetype>? sites = null)
    {
        var crisis = sites is not null;
        var scenarios = crisis ? CaseGenerator.CrisisScenarios : CaseGenerator.Scenarios;
        var schema = (JsonObject)JsonNode.Parse(File.ReadAllText(schemaPath))!;
        Directory.CreateDirectory(outDir);
        var coverage = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var waits = new Dictionary<string, int> { ["true"] = 0, ["false"] = 0, ["open"] = 0, ["no Wait offered"] = 0 };
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var errors = 0;

        for (var i = 0; i < count; i++)
        {
            var result = crisis ? CaseGenerator.GenerateCrisis(seed, i, sites!) : CaseGenerator.Generate(seed, i);
            var json = SnapshotJson.Serialize(result.Snapshot);
            var problems = JsonSchemaValidator.Validate(JsonNode.Parse(json), schema);
            if (problems.Count > 0)
            {
                errors++;
                log.WriteLine($"{result.Snapshot.Id}: {string.Join("; ", problems)}");
                continue;
            }

            File.WriteAllText(Path.Combine(outDir, result.Snapshot.Id + ".json"), json + "\n");
            hash.AppendData(Encoding.UTF8.GetBytes(json));
            var key = $"{result.Scenario} | {result.Mood}";
            coverage[key] = coverage.GetValueOrDefault(key) + 1;
            var wait = result.Snapshot.Director!.WaitExpected;
            waits[!result.Snapshot.AllowedTransitions.Contains("Wait") ? "no Wait offered" : wait is null ? "open" : wait.Value ? "true" : "false"]++;
        }

        var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        var manifest = new JsonObject
        {
            ["generator"] = "director-gen",
            ["generatorVersion"] = crisis ? CrisisGeneratorVersion : GeneratorVersion,
            ["seed"] = seed,
            ["count"] = count - errors,
            ["sha256"] = digest,
            ["scenarios"] = new JsonArray(scenarios.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
            ["waitExpected"] = new JsonObject(waits.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
        };
        if (crisis)
        {
            manifest["arc"] = "crisis";
        }

        File.WriteAllText(Path.Combine(outDir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

        var table = new StringBuilder("scenario,mood,cases\n");
        foreach (var (key, n) in coverage)
        {
            var parts = key.Split(" | ");
            table.Append(parts[0]).Append(',').Append(parts[1]).Append(',').Append(n).Append('\n');
        }

        File.WriteAllText(Path.Combine(outDir, "coverage.csv"), table.ToString());
        log.WriteLine($"Wrote {count - errors} cases to {outDir} (seed {seed}, sha256 {digest[..12]}).");
        log.WriteLine($"Coverage: {scenarios.Length} scenarios x {CaseGenerator.Moods.Length} moods, "
            + $"{coverage.Values.DefaultIfEmpty(0).Min()}-{coverage.Values.DefaultIfEmpty(0).Max()} cases each (coverage.csv).");
        log.WriteLine($"Wait expected: {string.Join(", ", waits.Select(kv => $"{kv.Key} {kv.Value}"))}.");
        return errors == 0 ? 0 : 1;
    }
}
