using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Eval.Servers;
using RimChronicles.Eval.Tasks;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;

namespace RimChronicles.Eval.Running;

/// <summary>Runs every cell through the real <see cref="LlmTaskRunner"/>, scores it and writes the run folder.</summary>
public static class Runner
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static async Task<RunResult> RunAsync(RunOptions o, TextWriter log, CancellationToken ct)
    {
        var library = PromptLibrary.Load(o.PromptsRoot);
        var (cells, snapshots) = RunPlan.Build(o);
        var runId = NewRunId(o.OutputRoot);
        var folder = Path.Combine(o.OutputRoot, runId);

        log.WriteLine($"Run {runId}: {cells.Count} cells, {cells.Select(c => c.Model.Id).Distinct().Count()} models, {snapshots.Count} snapshots.");
        if (o.DryRun)
        {
            foreach (var cell in cells)
            {
                log.WriteLine($"  {cell.Id}");
            }

            return new RunResult(runId, folder, [], 0, 0);
        }

        Directory.CreateDirectory(Path.Combine(folder, "raw"));
        var started = DateTimeOffset.UtcNow;
        var stats = new CacheStats();
        var results = new List<(int Index, CellResult Row)>();
        var indexOf = cells.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        var done = 0;
        var logLock = new object();
        var environment = new Dictionary<string, ServerInfo>(StringComparer.Ordinal);
        var probe = o.Probe ?? ((m, token) => ServerProbe.ProbeAsync(m, token));

        foreach (var group in cells.GroupBy(c => c.Model.Id))
        {
            var model = group.First().Model;
            var provider = (o.ProviderFactory ?? ProviderFactory.Create)(model);
            var identity = model.Identity();
            using var gate = new SemaphoreSlim(model.Concurrency);

            if (o.WarmUp && model.Provider == ProviderKind.OpenAi)
            {
                // Untimed, uncached: loads the model into memory so the first timed call doesn't include the load.
                log.WriteLine($"{model.Id}: warming up...");
                await provider.CompleteAsync(new ChatRequest
                {
                    System = "Reply with one word.",
                    User = "Say ready.",
                    MaxOutputTokens = 8,
                    Thinking = model.Thinking,
                }, ct).ConfigureAwait(false);
            }

            var tasks = group.Select(async cell =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    IChatProvider cellProvider = o.UseCache
                        ? new CachingChatProvider(provider, o.CacheDir, identity, cell.Repeat, stats)
                        : provider;
                    var result = await RunCellAsync(library, snapshots[cell.SnapshotId], cell, cellProvider, runId, folder, ct).ConfigureAwait(false);

                    lock (logLock)
                    {
                        results.Add((indexOf[cell.Id], result));
                        done++;
                        var latency = result.LatencySeconds is { } s ? $"{s:0.0} s" : result.FirstError;
                        log.WriteLine($"[{done}/{cells.Count}] {cell.Id}: {result.Outcome} ({latency})");
                    }
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            // While the model is still loaded: what the server says it is, its quantization and its VRAM use.
            var info = await probe(model, ct).ConfigureAwait(false);
            environment[model.Id] = info;
            if (info.Quantization is not null || info.VramBytes is not null)
            {
                log.WriteLine($"{model.Id}: {info.ServerKind} {info.ServerVersion}, {info.Quantization ?? "?"}, VRAM {ServerProbe.FormatGb(info.VramBytes)}{(info.PartlyOnCpu ? " (partly on CPU)" : "")}");
            }
        }

        // Stable order regardless of completion order.
        var ordered = results.OrderBy(r => r.Index).Select(r => r.Row).ToList();

        ResultFiles.WriteResultsCsv(Path.Combine(folder, "results.csv"), ordered);
        var summary = Summary.Build(ordered);
        Summary.WriteCsv(Path.Combine(folder, "summary.csv"), summary);
        WriteRunJson(folder, o, library, runId, started, ordered.Count, stats, environment);

        log.WriteLine();
        Summary.Print(summary, log);
        log.WriteLine();
        log.WriteLine($"Cache: {stats.Hits} hits, {stats.Misses} misses. Results in {folder}");
        return new RunResult(runId, folder, ordered, stats.Hits, stats.Misses);
    }

    private static async Task<CellResult> RunCellAsync(PromptLibrary library, WorldSnapshot snapshot, Cell cell,
        IChatProvider provider, string runId, string folder, CancellationToken ct)
    {
        var baseRow = new CellResult
        {
            RunId = runId, Model = cell.Model.Id, Tier = cell.Model.Tier, Task = cell.Task,
            Snapshot = cell.SnapshotId, Grammar = cell.Grammar, Repeat = cell.Repeat,
        };

        try
        {
            var evalTask = EvalTasks.Build(library, cell.Task, snapshot, cell.Model, cell.UseGrammar);
            var outcome = await new LlmTaskRunner(provider).RunAsync(evalTask.Definition, ct).ConfigureAwait(false);
            var first = outcome.Attempts.FirstOrDefault();
            var metrics = EvalTasks.Score(cell.Task, first?.Result.Ok == true ? first.Result.Text : null, evalTask);
            var rawFile = "raw/" + cell.Id + ".json"; // forward slash on every OS, so a run made on Windows reads on Linux

            await File.WriteAllTextAsync(Path.Combine(folder, rawFile), RawJson(cell, outcome).ToJsonString(Indented), ct).ConfigureAwait(false);

            return baseRow with
            {
                PromptVersion = outcome.PromptVersion,
                Outcome = outcome.Source.ToString(),
                Attempts = outcome.Attempts.Count,
                FirstOk = first?.Result.Ok == true,
                FirstError = first?.Result.Error,
                PassedFirst = first is { Failures.Count: 0 },
                JsonValid = metrics.JsonValid,
                ChoiceAllowed = metrics.ChoiceAllowed,
                WordCount = metrics.WordCount,
                LengthOk = metrics.LengthOk,
                UnknownNameCount = metrics.WordCount is null ? null : metrics.UnknownNames.Count,
                UnknownNames = string.Join(" ", metrics.UnknownNames),
                BannedPhrase = metrics.BannedPhrase,
                LatencySeconds = first?.Result.Ok == true ? first.Result.Latency.TotalSeconds : null,
                TotalLatencySeconds = outcome.Attempts.Sum(a => a.Result.Latency.TotalSeconds),
                InputTokens = first?.Result.InputTokens ?? 0,
                OutputTokens = first?.Result.OutputTokens ?? 0,
                ReasoningTokens = first?.Result.ReasoningTokens,
                RawFile = rawFile,
                FirstText = first?.Result.Text,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return baseRow with { Outcome = "Error", FirstError = $"Harness error: {ex.Message}" };
        }
    }

    private static JsonObject RawJson(Cell cell, TaskOutcome<string> outcome) => new()
    {
        ["cell"] = cell.Id,
        ["model"] = cell.Model.Id,
        ["task"] = cell.Task,
        ["snapshot"] = cell.SnapshotId,
        ["grammar"] = cell.Grammar,
        ["repeat"] = cell.Repeat,
        ["promptVersion"] = outcome.PromptVersion,
        ["outcome"] = outcome.Source.ToString(),
        ["value"] = outcome.Value,
        ["attempts"] = new JsonArray(outcome.Attempts.Select(a => (JsonNode?)new JsonObject
        {
            ["request"] = new JsonObject
            {
                ["system"] = a.Request.System,
                ["user"] = a.Request.User,
                ["maxOutputTokens"] = a.Request.MaxOutputTokens,
                ["useGrammar"] = a.Request.UseGrammar,
                ["thinking"] = a.Request.Thinking.ToString(),
                ["schema"] = a.Request.Schema?.Schema.DeepClone(),
            },
            ["ok"] = a.Result.Ok,
            ["text"] = a.Result.Text,
            ["error"] = a.Result.Error,
            ["failures"] = new JsonArray(a.Failures.Select(f => (JsonNode?)JsonValue.Create(f)).ToArray()),
            ["latencySeconds"] = a.Result.Latency.TotalSeconds,
            ["inputTokens"] = a.Result.InputTokens,
            ["outputTokens"] = a.Result.OutputTokens,
            ["reasoningTokens"] = a.Result.ReasoningTokens,
            ["rawResponse"] = a.Result.RawResponse,
        }).ToArray()),
    };

    private static void WriteRunJson(string folder, RunOptions o, PromptLibrary library, string runId,
        DateTimeOffset started, int cellCount, CacheStats stats, IReadOnlyDictionary<string, ServerInfo> environment)
    {
        var (commit, dirty) = GitInfo();
        var run = new JsonObject
        {
            ["runId"] = runId,
            ["startedUtc"] = started.ToString("O", CultureInfo.InvariantCulture),
            ["finishedUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["gitCommit"] = commit,
            ["gitDirty"] = dirty,
            ["promptVersions"] = new JsonObject(library.Tasks.Order(StringComparer.Ordinal)
                .Select(t => KeyValuePair.Create(t, (JsonNode?)JsonValue.Create(library.Get(t).Meta.Version)))),
            ["config"] = o.ConfigPath,
            ["configSha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(o.ConfigPath))),
            ["models"] = new JsonArray((o.Models ?? o.Config.Models.Select(m => m.Id).ToList()).Select(m => (JsonNode?)JsonValue.Create(m)).ToArray()),
            ["tasks"] = new JsonArray((o.Tasks ?? EvalTasks.All).Select(t => (JsonNode?)JsonValue.Create(t)).ToArray()),
            ["snapshots"] = new JsonArray((o.Snapshots ?? ["*"]).Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
            ["repeats"] = o.Repeats ?? o.Config.Repeats,
            ["cells"] = cellCount,
            ["cacheHits"] = stats.Hits,
            ["cacheMisses"] = stats.Misses,
            ["environment"] = new JsonObject(environment.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.ToJson()))),
        };
        File.WriteAllText(Path.Combine(folder, "run.json"), run.ToJsonString(Indented));
    }

    private static string NewRunId(string outputRoot)
    {
        var baseId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var id = baseId;
        for (var n = 2; Directory.Exists(Path.Combine(outputRoot, id)); n++)
        {
            id = $"{baseId}-{n}";
        }

        return id;
    }

    private static (string? Commit, bool? Dirty) GitInfo()
    {
        try
        {
            return (Git("rev-parse HEAD").Trim(), Git("status --porcelain").Trim().Length > 0);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (null, null);
        }
    }

    private static string Git(string args)
    {
        using var p = Process.Start(new ProcessStartInfo("git", args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })
                      ?? throw new InvalidOperationException("git did not start");
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0 ? output : throw new InvalidOperationException("git failed");
    }
}
