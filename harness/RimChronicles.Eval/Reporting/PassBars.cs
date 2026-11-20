using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RimChronicles.Eval.Reporting;

public enum BarVerdict
{
    /// <summary>No bar for this metric, tier or task, or no data to judge.</summary>
    None,
    Pass,
    Fail,
}

/// <summary>Pass bars for one task. Null means the metric has no bar.</summary>
public sealed record TaskBars
{
    public double? JsonValidGrammarOn { get; init; }
    public double? JsonValidGrammarOff { get; init; }
    public double? ChoiceAllowed { get; init; }
    public double? LengthOk { get; init; }
    public double? InventedPer10 { get; init; }
    public double? Overall { get; init; }

    /// <summary>Tiers the overall bar applies to (the spec sets it for the default tier). Null: every tier.</summary>
    public IReadOnlyList<string>? OverallTiers { get; init; }

    public double? MedianLatencySeconds { get; init; }

    /// <summary>Tiers the latency bar applies to. Null: every tier.</summary>
    public IReadOnlyList<string>? LatencyTiers { get; init; }
}

/// <summary>harness/passbars.json, keyed by task.</summary>
public sealed class PassBars(IReadOnlyDictionary<string, TaskBars> byTask)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static PassBars Load(string path) =>
        new(JsonSerializer.Deserialize<Dictionary<string, TaskBars>>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"{path} is empty."));

    public static readonly PassBars Empty = new(new Dictionary<string, TaskBars>());

    public TaskBars For(string task) => byTask.TryGetValue(task, out var bars) ? bars : new TaskBars();

    /// <summary>At least the bar (rates and scores).</summary>
    public static BarVerdict AtLeast(double? value, double? bar) =>
        value is null || bar is null ? BarVerdict.None : value >= bar - 1e-9 ? BarVerdict.Pass : BarVerdict.Fail;

    /// <summary>At most the bar (invented facts, latency).</summary>
    public static BarVerdict AtMost(double? value, double? bar) =>
        value is null || bar is null ? BarVerdict.None : value <= bar + 1e-9 ? BarVerdict.Pass : BarVerdict.Fail;

    public static bool AppliesTo(IReadOnlyList<string>? tiers, string tier) => tiers is null || tiers.Contains(tier);
}
