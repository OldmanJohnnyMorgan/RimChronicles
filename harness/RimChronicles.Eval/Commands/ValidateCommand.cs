using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Tasks;
using RimChronicles.Llm;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.Commands;

/// <summary>Checks snapshots, prompts and the model config before anything is spent on a run.</summary>
public static class ValidateCommand
{
    public static int Run(string snapshotsDir, string schemaPath, string promptsRoot, string? configPath, TextWriter log)
    {
        var errors = new List<string>();
        var schema = (JsonObject)JsonNode.Parse(File.ReadAllText(schemaPath))!;
        var snapshots = new List<WorldSnapshot>();

        var files = Directory.GetFiles(snapshotsDir, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            try
            {
                var schemaErrors = JsonSchemaValidator.Validate(JsonNode.Parse(File.ReadAllText(file)), schema);
                errors.AddRange(schemaErrors.Select(e => $"{name}: {e}"));

                var snapshot = SnapshotJson.Load(file);
                if (snapshot.Id != Path.GetFileNameWithoutExtension(file))
                {
                    errors.Add($"{name}: id '{snapshot.Id}' does not match the file name");
                }

                var known = snapshot.PublicEvents.Concat(snapshot.ActivePlots.Select(p => p.PublicHint).OfType<string>()).ToHashSet(StringComparer.Ordinal);
                errors.AddRange(snapshot.ReportedEvents.Where(e => !known.Contains(e))
                    .Select(e => $"{name}: reportedEvents entry is not a public event or plot hint: '{e}'"));

                snapshots.Add(snapshot);
            }
            catch (JsonException ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        log.WriteLine($"Snapshots: {files.Count} files checked.");

        PromptLibrary? library = null;
        try
        {
            library = PromptLibrary.Load(promptsRoot);
            log.WriteLine($"Prompts: {string.Join(", ", library.Tasks.Order(StringComparer.Ordinal).Select(t => $"{t}/{library.Get(t).Meta.Version}"))}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or KeyNotFoundException)
        {
            errors.Add($"prompts: {ex.Message}");
        }

        if (library is not null)
        {
            foreach (var missing in EvalTasks.All.Append("judge").Where(t => !library.Tasks.Contains(t)))
            {
                errors.Add($"prompts: no current version of '{missing}'");
            }

            foreach (var s in snapshots)
            {
                foreach (var tier in new[] { ModelTier.Minimum, ModelTier.Recommended, ModelTier.High })
                {
                    foreach (var task in EvalTasks.All.Where(t => EvalTasks.AppliesTo(t, s) && library.Tasks.Contains(t)))
                    {
                        errors.AddRange(CheckVariables(library.Get(task), task, s, tier));
                    }
                }
            }
        }

        if (configPath is not null)
        {
            if (!File.Exists(configPath))
            {
                errors.Add($"config: {configPath} not found");
            }
            else
            {
                try
                {
                    var config = HarnessConfig.Load(configPath);
                    log.WriteLine($"Config: {config.Models.Count} models, judge {config.Judge?.Id ?? "(none)"}.");
                }
                catch (Exception ex) when (ex is InvalidDataException or JsonException)
                {
                    errors.Add($"config: {ex.Message}");
                }
            }
        }

        foreach (var error in errors.Distinct())
        {
            log.WriteLine($"ERROR {error}");
        }

        log.WriteLine(errors.Count == 0 ? "OK" : $"{errors.Distinct().Count()} problem(s).");
        return errors.Count == 0 ? 0 : 1;
    }

    private static IEnumerable<string> CheckVariables(PromptTemplate prompt, string task, WorldSnapshot s, ModelTier tier)
    {
        TaskPromptInput input;
        try
        {
            input = task switch
            {
                WorldSummarizer.DirectorTask => WorldSummarizer.ForDirector(s, tier),
                WorldSummarizer.NewsBulletinTask => WorldSummarizer.ForNewsBulletin(s, tier),
                _ => WorldSummarizer.ForHandoff(s),
            };
        }
        catch (InvalidOperationException ex)
        {
            return [$"{s.Id} {task}: {ex.Message}"];
        }

        var missing = prompt.Variables.Except(input.Variables.Keys).ToList();
        var unused = input.Variables.Keys.Except(prompt.Variables).ToList();
        var problems = new List<string>();
        if (missing.Count > 0)
        {
            problems.Add($"{task}/{prompt.Meta.Version}: template uses variables the task does not supply: {string.Join(", ", missing)}");
        }

        if (unused.Count > 0)
        {
            problems.Add($"{task}/{prompt.Meta.Version}: task supplies variables no template uses: {string.Join(", ", unused)}");
        }

        return problems;
    }
}
