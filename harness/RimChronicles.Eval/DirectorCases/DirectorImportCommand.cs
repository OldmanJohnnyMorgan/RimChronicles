using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.DirectorCases;

/// <summary><c>director-import</c> (training track D3): copies director cases recorded in game into the exported case
/// set. Each must pass the snapshot schema and offer a real choice; cases already imported are skipped by id. Rewrites
/// the set's manifest (count and a sha256 over all its cases, in id order).</summary>
public static class DirectorImportCommand
{
    public static int Run(string fromDir, string outDir, string schemaPath, TextWriter log)
    {
        if (!Directory.Exists(fromDir))
        {
            throw new IOException($"No folder {fromDir}. In game, turn on \"Dev: record director decisions for training\" in the mod settings.");
        }

        var schema = (JsonObject)JsonNode.Parse(File.ReadAllText(schemaPath))!;
        Directory.CreateDirectory(outDir);
        int added = 0, skipped = 0, rejected = 0;
        foreach (var file in Directory.GetFiles(fromDir, "*.json").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            string json;
            WorldSnapshot snapshot;
            try
            {
                json = File.ReadAllText(file);
                var problems = JsonSchemaValidator.Validate(JsonNode.Parse(json), schema);
                snapshot = SnapshotJson.Parse(json);
                if (snapshot.Director?.PlotId is null || snapshot.AllowedTransitions.Count < 2)
                {
                    problems = [.. problems, "not a director case (no director block, or fewer than two options)"];
                }

                if (problems.Count > 0)
                {
                    rejected++;
                    log.WriteLine($"{name}: {string.Join("; ", problems)}");
                    continue;
                }
            }
            catch (JsonException ex)
            {
                rejected++;
                log.WriteLine($"{name}: {ex.Message}");
                continue;
            }

            var target = Path.Combine(outDir, snapshot.Id + ".json");
            if (File.Exists(target))
            {
                skipped++;
                continue;
            }

            File.WriteAllText(target, json.TrimEnd() + "\n");
            added++;
        }

        var all = Directory.GetFiles(outDir, "*.json").Where(f => Path.GetFileName(f) != "manifest.json").Order(StringComparer.Ordinal).ToList();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var f in all)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(File.ReadAllText(f).TrimEnd()));
        }

        var manifest = new JsonObject
        {
            ["source"] = "exported",
            ["count"] = all.Count,
            ["sha256"] = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
        };
        File.WriteAllText(Path.Combine(outDir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        log.WriteLine($"Imported {added} new case(s), skipped {skipped} already imported, rejected {rejected}. {all.Count} exported cases in {outDir}.");
        return rejected == 0 ? 0 : 1;
    }
}
