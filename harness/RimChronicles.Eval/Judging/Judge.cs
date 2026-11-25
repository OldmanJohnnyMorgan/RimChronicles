using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Eval.Running;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.Judging;

public sealed record JudgeOptions
{
    public required string RunFolder { get; init; }
    public required ModelConfig JudgeModel { get; init; }
    public required string PromptsRoot { get; init; }
    public required string CacheDir { get; init; }
    public bool UseCache { get; init; } = true;

    /// <summary>Replaces <see cref="ProviderFactory.Create"/>, for tests.</summary>
    public Func<ModelConfig, IChatProvider>? ProviderFactory { get; init; }
}

/// <summary>
/// Judges every prose output in a run (news bulletins and handoff scenes) with the judge prompt, blind: the judge
/// sees the task, the briefing and the output, never the model name. Writes judge.csv and judgments/*.json.
/// </summary>
public static class Judge
{
    public const string TaskName = "judge";

    /// <summary>Tasks whose outputs are judged.</summary>
    public static readonly IReadOnlySet<string> JudgedTasks = new HashSet<string>
    {
        WorldSummarizer.NewsBulletinTask, WorldSummarizer.HandoffTask, RimChronicles.Core.Plots.PlotLetterTask.TaskName,
        RimChronicles.Core.Empire.EpilogueTask.TaskName,
    };

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, NewLine = "\n" };

    public static async Task<IReadOnlyList<JudgeRow>> RunAsync(JudgeOptions o, TextWriter log, CancellationToken ct)
    {
        var results = ResultFiles.ReadResultsCsv(Path.Combine(o.RunFolder, "results.csv"));
        var prompt = PromptLibrary.Load(o.PromptsRoot).Get(TaskName);
        var todo = results.Where(r => JudgedTasks.Contains(r.Task) && r.FirstOk).ToList();
        var skipped = results.Count(r => JudgedTasks.Contains(r.Task) && !r.FirstOk);

        log.WriteLine($"Judging {todo.Count} outputs with {o.JudgeModel.Id} ({prompt.Meta.Task}/{prompt.Meta.Version}); {skipped} prose cells had no answer to judge.");

        var provider = (o.ProviderFactory ?? ProviderFactory.Create)(o.JudgeModel);
        var identity = o.JudgeModel.Identity();
        var stats = new CacheStats();
        var judgmentsDir = Path.Combine(o.RunFolder, "judgments");
        Directory.CreateDirectory(judgmentsDir);

        var rows = new JudgeRow?[todo.Count];
        var done = 0;
        var logLock = new object();
        using var gate = new SemaphoreSlim(o.JudgeModel.Concurrency);

        await Task.WhenAll(todo.Select(async (cell, index) =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                IChatProvider p = o.UseCache ? new CachingChatProvider(provider, o.CacheDir, identity, 1, stats) : provider;
                rows[index] = await JudgeOneAsync(o.RunFolder, judgmentsDir, cell, prompt, p, ct).ConfigureAwait(false);
                lock (logLock)
                {
                    done++;
                    var r = rows[index]!;
                    var verdict = r.Judged ? $"overall {r.Overall}, {r.InventedCount} invented" : r.Error;
                    log.WriteLine($"[{done}/{todo.Count}] {r.Cell}: {verdict}");
                }
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);

        var all = rows.OfType<JudgeRow>().ToList();
        JudgeFiles.Write(Path.Combine(o.RunFolder, "judge.csv"), all);
        log.WriteLine($"Judged {all.Count(r => r.Judged)}/{all.Count}. Cache: {stats.Hits} hits, {stats.Misses} misses.");
        return all;
    }

    private static async Task<JudgeRow> JudgeOneAsync(string runFolder, string judgmentsDir, CellResult cell,
        PromptTemplate prompt, IChatProvider provider, CancellationToken ct)
    {
        var raw = RawOutput.Load(RunPaths.Raw(runFolder, cell.RawFile));
        var row = new JudgeRow
        {
            Cell = raw.Cell, Model = cell.Model, Tier = cell.Tier, Task = cell.Task, Snapshot = cell.Snapshot,
            Repeat = cell.Repeat, RawFile = cell.RawFile, JudgePromptVersion = $"{prompt.Meta.Task}/{prompt.Meta.Version}",
        };

        if (raw.Text is null)
        {
            return row with { JudgeOutcome = "Failed", Error = "raw file has no first answer" };
        }

        var definition = new TaskDefinition<JudgeVerdict?>
        {
            Prompt = prompt,
            Variables = new Dictionary<string, string>
            {
                ["taskDescription"] = raw.System,
                ["briefing"] = raw.User,
                ["output"] = raw.Text,
            },
            Checks = [new JsonSchemaCheck(prompt.Schema!.Schema), new JudgeCapCheck()],
            Parse = JudgeVerdict.Parse,
            Fallback = new DelegateFallback<JudgeVerdict?>(() => null),
            Tier = ModelTier.High,
            UseGrammar = true,
        };

        var outcome = await new LlmTaskRunner(provider).RunAsync(definition, ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(judgmentsDir, raw.Cell + ".json"), JudgmentJson(raw, outcome).ToJsonString(Indented), ct)
            .ConfigureAwait(false);

        if (outcome.Value is not { } v)
        {
            var errors = outcome.Attempts.SelectMany(a => a.Failures).Distinct();
            return row with { JudgeOutcome = "Failed", Error = string.Join("; ", errors) };
        }

        return row with
        {
            JudgeOutcome = outcome.Source.ToString(),
            InventedCount = v.InventedFacts.Count,
            MajorCount = v.InventedFacts.Count(f => !f.IsMinor),
            MinorCount = v.InventedFacts.Count(f => f.IsMinor),
            Invented = string.Join(" | ", v.InventedFacts.Select(f => $"{(f.IsMinor ? "[minor] " : "")}{f.Span} ({f.Reason})")),
            Voice = v.Voice, Clarity = v.Clarity, Tone = v.Tone, Overall = v.Overall, Comment = v.Comment,
        };
    }

    private static JsonObject JudgmentJson(RawOutput raw, TaskOutcome<JudgeVerdict?> outcome) => new()
    {
        ["cell"] = raw.Cell,
        ["promptVersion"] = outcome.PromptVersion,
        ["outcome"] = outcome.Source.ToString(),
        ["attempts"] = new JsonArray(outcome.Attempts.Select(a => (JsonNode?)new JsonObject
        {
            ["ok"] = a.Result.Ok,
            ["text"] = a.Result.Text,
            ["error"] = a.Result.Error,
            ["failures"] = new JsonArray(a.Failures.Select(f => (JsonNode?)JsonValue.Create(f)).ToArray()),
            ["latencySeconds"] = a.Result.Latency.TotalSeconds,
            ["inputTokens"] = a.Result.InputTokens,
            ["outputTokens"] = a.Result.OutputTokens,
        }).ToArray()),
    };
}
