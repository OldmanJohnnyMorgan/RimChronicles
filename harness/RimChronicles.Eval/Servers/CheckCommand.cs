using System.Globalization;
using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Providers;
using RimChronicles.Eval.Tasks;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;

namespace RimChronicles.Eval.Servers;

/// <summary>The result of one test call during a check.</summary>
public sealed record ProbeCall(string Task, bool Answered, string? Error, double LatencySeconds, int OutputTokens,
    bool ThinkingSeen, bool? JsonValid, bool? ChoiceAllowed, int? Words);

/// <summary>Everything the check learned about one model.</summary>
public sealed record ModelCheck(ModelConfig Model, ServerInfo Server, IReadOnlyList<ProbeCall> Calls, IReadOnlyList<string> Problems, IReadOnlyList<string> Warnings);

/// <summary>
/// Preflight before a real run: is each server up, is each model there under the configured name, does a
/// director call return valid JSON, does "thinking off" actually switch thinking off, and does the model fit in
/// VRAM. Makes two small calls per model.
/// </summary>
public static class CheckCommand
{
    private const string Snapshot = "snap-07-mid-strained";

    public static async Task<IReadOnlyList<ModelCheck>> RunAsync(HarnessConfig config, IReadOnlyList<string>? only, string promptsRoot,
        string snapshotsDir, double vramBudgetGb, TextWriter log, CancellationToken ct,
        Func<ModelConfig, IChatProvider>? providerFactory = null, Func<ModelConfig, CancellationToken, Task<ServerInfo>>? probe = null)
    {
        var library = PromptLibrary.Load(promptsRoot);
        var snapshot = SnapshotJson.Load(Path.Combine(snapshotsDir, Snapshot + ".json"));
        probe ??= (m, token) => ServerProbe.ProbeAsync(m, token);
        providerFactory ??= ProviderFactory.Create;

        var models = config.Models.Where(m => only is null || only.Contains(m.Id)).ToList();
        var results = new List<ModelCheck>();
        foreach (var m in models)
        {
            log.WriteLine($"Checking {m.Id} ({m.Provider}{(m.Endpoint is null ? "" : " at " + m.Endpoint)}, {m.Model})...");
            results.Add(await CheckModelAsync(m, library, snapshot, vramBudgetGb, providerFactory, probe, ct).ConfigureAwait(false));
        }

        if (only is null && config.Judge is { } judge && judge.ApiKeyEnv is { } env && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(env)))
        {
            results.Add(new ModelCheck(judge, new ServerInfo { ModelId = judge.Id }, [], [$"judge: environment variable {env} is not set"], []));
        }

        Print(results, vramBudgetGb, log);
        return results;
    }

    private static async Task<ModelCheck> CheckModelAsync(ModelConfig m, PromptLibrary library, WorldSnapshot snapshot, double vramBudgetGb,
        Func<ModelConfig, IChatProvider> providerFactory, Func<ModelConfig, CancellationToken, Task<ServerInfo>> probe, CancellationToken ct)
    {
        var problems = new List<string>();
        var warnings = new List<string>();

        var server = await probe(m, ct).ConfigureAwait(false);
        if (!server.Reachable)
        {
            problems.Add(server.Error ?? "server unreachable");
            return new ModelCheck(m, server, [], problems, warnings);
        }

        if (server.ModelListed == false)
        {
            var similar = server.AvailableModels.Where(a => SharesFamily(a, m.Model!)).ToList();
            problems.Add($"model '{m.Model}' is not on the server" + (similar.Count > 0 ? $"; similar: {string.Join(", ", similar)}" : $"; it has: {string.Join(", ", server.AvailableModels.Take(10))}"));
            return new ModelCheck(m, server, [], problems, warnings);
        }

        IChatProvider provider;
        try
        {
            provider = providerFactory(m);
        }
        catch (InvalidOperationException ex)
        {
            problems.Add(ex.Message);
            return new ModelCheck(m, server, [], problems, warnings);
        }

        var calls = new List<ProbeCall>();
        foreach (var task in new[] { WorldSummarizer.DirectorTask, WorldSummarizer.NewsBulletinTask })
        {
            var evalTask = EvalTasks.Build(library, task, snapshot, m, grammar: m.SupportsGrammar);
            var outcome = await new LlmTaskRunner(provider, maxAttempts: 1).RunAsync(evalTask.Definition, ct).ConfigureAwait(false);
            var attempt = outcome.Attempts.FirstOrDefault();
            if (attempt is null)
            {
                problems.Add($"{task}: no call was made");
                continue;
            }

            var r = attempt.Result;
            var metrics = EvalTasks.Score(task, r.Ok ? r.Text : null, evalTask);
            var call = new ProbeCall(task, r.Ok, r.Error, r.Latency.TotalSeconds, r.OutputTokens, ThinkingSeen(r),
                metrics.JsonValid, metrics.ChoiceAllowed, metrics.WordCount);
            calls.Add(call);

            if (!r.Ok)
            {
                problems.Add($"{task}: {r.Error}{CutOffHint(m, task, r, call.ThinkingSeen)}");
                continue;
            }

            if (m.ThinkingFor(task) == ThinkingMode.Off && call.ThinkingSeen && m.Provider == ProviderKind.OpenAi)
            {
                warnings.Add($"{task}: thinking is set Off but the model still thought ({r.ReasoningTokens?.ToString(CultureInfo.InvariantCulture) ?? "?"} reasoning tokens). The server ignores thinkingOffBody; try another field for this server.");
            }

            if (task == WorldSummarizer.DirectorTask && m.SupportsGrammar && call.JsonValid == false)
            {
                warnings.Add("director: invalid JSON with grammar on. The server may ignore response_format; consider supportsGrammar: false.");
            }
        }

        // Probe again now the model is loaded, for VRAM.
        var loaded = await probe(m, ct).ConfigureAwait(false);
        server = loaded.Reachable ? loaded : server;
        if (server.PartlyOnCpu)
        {
            warnings.Add($"only {ServerProbe.FormatGb(server.VramBytes)} of {ServerProbe.FormatGb(server.SizeBytes)} is on the GPU; the rest runs on the CPU and latency will suffer. Use a smaller quantization or close other GPU programs.");
        }

        if (server.VramGb > vramBudgetGb)
        {
            warnings.Add($"uses {ServerProbe.FormatGb(server.VramBytes)} of VRAM, over the {vramBudgetGb.ToString("0", CultureInfo.InvariantCulture)} GB budget that leaves room for RimWorld.");
        }

        return new ModelCheck(m, server, calls, problems, warnings);
    }

    /// <summary>Why an answer may be empty: the token limit went on thinking. Internal for tests.</summary>
    internal static string CutOffHint(ModelConfig m, string task, ChatResult r, bool thinkingSeen)
    {
        if (m.Provider != ProviderKind.OpenAi || !thinkingSeen)
        {
            return "";
        }

        return m.ThinkingFor(task) == ThinkingMode.Off
            ? ". The model spent the limit thinking although thinking is set Off: the server ignores thinkingOffBody; try another field for this server."
            : ". The model spent the limit thinking: set \"thinking\": \"Off\" (with a thinkingOffBody) for this model.";
    }

    /// <summary>Whether the raw response shows reasoning: counted reasoning tokens, a reasoning field, or think tags.</summary>
    internal static bool ThinkingSeen(ChatResult r)
    {
        if (r.ReasoningTokens > 0)
        {
            return true;
        }

        try
        {
            var message = JsonNode.Parse(r.RawResponse)?["choices"]?[0]?["message"];
            foreach (var field in new[] { "reasoning_content", "reasoning", "thinking" })
            {
                if (message?[field] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
                {
                    return true;
                }
            }

            return message?["content"] is JsonValue c && c.TryGetValue<string>(out var content)
                   && content.Contains("<think", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool SharesFamily(string available, string wanted) =>
        available.Split(':')[0].StartsWith(wanted.Split(':')[0][..Math.Min(4, wanted.Split(':')[0].Length)], StringComparison.OrdinalIgnoreCase);

    private static void Print(IReadOnlyList<ModelCheck> results, double budget, TextWriter log)
    {
        static string Sec(double v) => v.ToString("0.0", CultureInfo.InvariantCulture) + " s";

        log.WriteLine();
        log.WriteLine($"{"model",-14} {"server",-10} {"quant",-8} {"VRAM",-8} {"director",-28} {"bulletin",-22}");
        foreach (var c in results)
        {
            var director = c.Calls.FirstOrDefault(x => x.Task == WorldSummarizer.DirectorTask);
            var bulletin = c.Calls.FirstOrDefault(x => x.Task == WorldSummarizer.NewsBulletinTask);
            var d = director is null ? "-" : !director.Answered ? "failed"
                : $"{(director.JsonValid == true ? "json ok" : "bad json")}, {Sec(director.LatencySeconds)}{(director.ThinkingSeen ? ", thought" : "")}";
            var b = bulletin is null ? "-" : !bulletin.Answered ? "failed"
                : $"{bulletin.Words} words, {Sec(bulletin.LatencySeconds)}{(bulletin.ThinkingSeen ? ", thought" : "")}";
            log.WriteLine($"{c.Model.Id,-14} {c.Server.ServerKind ?? "-",-10} {c.Server.Quantization ?? "-",-8} {ServerProbe.FormatGb(c.Server.VramBytes),-8} {d,-28} {b,-22}");
        }

        log.WriteLine();
        foreach (var c in results)
        {
            foreach (var p in c.Problems)
            {
                log.WriteLine($"PROBLEM {c.Model.Id}: {p}");
            }

            foreach (var w in c.Warnings)
            {
                log.WriteLine($"WARNING {c.Model.Id}: {w}");
            }
        }

        var blocking = results.Sum(r => r.Problems.Count);
        log.WriteLine(blocking == 0
            ? $"Ready: every model answered. Review any warnings before the full run. (VRAM budget {budget.ToString("0", CultureInfo.InvariantCulture)} GB)"
            : $"{blocking} problem(s) to fix before a full run.");
    }
}
