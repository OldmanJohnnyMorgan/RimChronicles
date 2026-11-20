using RimChronicles.Eval.Config;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Eval.Running;

/// <summary>Everything one run needs. Paths are absolute.</summary>
public sealed record RunOptions
{
    public required HarnessConfig Config { get; init; }
    public required string ConfigPath { get; init; }
    public required string PromptsRoot { get; init; }
    public required string SnapshotsDir { get; init; }

    /// <summary>Run folders go here as &lt;runId&gt;/.</summary>
    public required string OutputRoot { get; init; }

    /// <summary>Shared response cache.</summary>
    public required string CacheDir { get; init; }

    public IReadOnlyList<string>? Models { get; init; }
    public IReadOnlyList<string>? Tasks { get; init; }

    /// <summary>Snapshot id patterns; '*' matches anything.</summary>
    public IReadOnlyList<string>? Snapshots { get; init; }

    public int? Repeats { get; init; }
    public bool UseCache { get; init; } = true;

    /// <summary>List the cells without calling any model.</summary>
    public bool DryRun { get; init; }

    /// <summary>Replaces <see cref="Providers.ProviderFactory.Create"/>, for tests.</summary>
    public Func<ModelConfig, IChatProvider>? ProviderFactory { get; init; }

    /// <summary>Replaces <see cref="Servers.ServerProbe.ProbeAsync"/>, for tests.</summary>
    public Func<ModelConfig, CancellationToken, Task<Servers.ServerInfo>>? Probe { get; init; }
}

/// <summary>One unit of work: a model, a task, a snapshot, a grammar mode and a repeat.</summary>
public sealed record Cell(ModelConfig Model, string Task, string SnapshotId, string Grammar, int Repeat)
{
    /// <summary>"on", "off", or "n/a" for prose tasks.</summary>
    public bool UseGrammar => Grammar == "on";

    public string Id => $"{Model.Id}__{Task}__{SnapshotId}__{Grammar.Replace('/', '-')}__r{Repeat}";
}

/// <summary>One row of results.csv.</summary>
public sealed record CellResult
{
    public required string RunId { get; init; }
    public required string Model { get; init; }
    public required string Tier { get; init; }
    public required string Task { get; init; }
    public required string Snapshot { get; init; }
    public required string Grammar { get; init; }
    public required int Repeat { get; init; }
    public string PromptVersion { get; init; } = "";

    /// <summary>Model, ModelRetry, Template, or Error when the harness itself failed.</summary>
    public string Outcome { get; init; } = "";

    public int Attempts { get; init; }
    public bool FirstOk { get; init; }
    public string? FirstError { get; init; }
    public bool PassedFirst { get; init; }
    public bool? JsonValid { get; init; }
    public bool? ChoiceAllowed { get; init; }
    public int? WordCount { get; init; }
    public bool? LengthOk { get; init; }
    public int? UnknownNameCount { get; init; }
    public string UnknownNames { get; init; } = "";
    public bool? BannedPhrase { get; init; }
    public double? LatencySeconds { get; init; }
    public double TotalLatencySeconds { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int? ReasoningTokens { get; init; }
    public string RawFile { get; init; } = "";

    /// <summary>The first attempt's text, for the judge (M4). Not written to the CSV.</summary>
    public string? FirstText { get; init; }
}

public sealed record RunResult(string RunId, string Folder, IReadOnlyList<CellResult> Cells, int CacheHits, int CacheMisses);
