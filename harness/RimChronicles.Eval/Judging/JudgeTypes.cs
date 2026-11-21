using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Eval.Judging;

/// <summary>One claim the judge found unsupported by the briefing. Severity is "major" (a new fact about the world,
/// or a contradiction) or "minor" (an embellishment of a briefed fact). Judge v1 had no severity: all major.</summary>
public sealed record InventedFact(string Span, string Reason, string Severity = "major")
{
    public bool IsMinor => Severity == "minor";
}

/// <summary>The judge's structured verdict on one output (prompts/judge schema).</summary>
public sealed record JudgeVerdict(
    IReadOnlyList<InventedFact> InventedFacts, int Voice, int Clarity, int Tone, int Overall, string Comment)
{
    public static JudgeVerdict Parse(string text)
    {
        var node = JsonText.TryParse(text, out var error) ?? throw new FormatException(error ?? "empty verdict");
        var facts = node["inventedFacts"] is JsonArray arr
            ? arr.Select(f => new InventedFact(
                f?["span"]?.GetValue<string>() ?? "",
                f?["reason"]?.GetValue<string>() ?? "",
                f?["severity"]?.GetValue<string>() == "minor" ? "minor" : "major")).ToList()
            : throw new FormatException("inventedFacts is missing");

        return new JudgeVerdict(facts, Score(node, "voice"), Score(node, "clarity"), Score(node, "tone"), Score(node, "overall"),
            node["comment"]?.GetValue<string>() ?? "");
    }

    private static int Score(JsonNode node, string name)
    {
        var value = node[name] is JsonValue v && v.TryGetValue<int>(out var i) ? i : throw new FormatException($"{name} is missing");
        return value is >= 1 and <= 5 ? value : throw new FormatException($"{name} must be 1-5");
    }
}

/// <summary>
/// Enforces the rubric's cap: any major invented fact caps overall at 3, two or more at 2, and minor ones alone at 4.
/// Facts without a severity (judge v1) count as major. A verdict that breaks the cap is sent back once, so the judge
/// stays consistent with its own rubric.
/// </summary>
public sealed class JudgeCapCheck : IOutputCheck
{
    public string Name => "judge-cap";

    public CheckResult Check(string output)
    {
        var node = JsonText.TryParse(output, out _);
        if (node?["inventedFacts"] is not JsonArray facts || node["overall"] is not JsonValue v || !v.TryGetValue<int>(out var overall))
        {
            return CheckResult.Pass; // the schema check reports shape problems
        }

        var minor = facts.Count(f => f?["severity"]?.GetValue<string>() == "minor");
        var major = facts.Count - minor;
        var cap = major switch { 0 => minor > 0 ? 4 : 5, 1 => 3, _ => 2 };
        return overall <= cap
            ? CheckResult.Pass
            : CheckResult.Fail($"With {major} major and {minor} minor invented fact(s) the rubric caps overall at {cap}, but overall is {overall}");
    }
}

/// <summary>One row of judge.csv.</summary>
public sealed record JudgeRow
{
    public required string Cell { get; init; }
    public required string Model { get; init; }
    public required string Tier { get; init; }
    public required string Task { get; init; }
    public required string Snapshot { get; init; }
    public required int Repeat { get; init; }
    public required string RawFile { get; init; }
    public string JudgePromptVersion { get; init; } = "";

    /// <summary>Model, ModelRetry, or Failed when the judge never returned a valid verdict.</summary>
    public string JudgeOutcome { get; init; } = "";

    /// <summary>All invented facts, major and minor.</summary>
    public int? InventedCount { get; init; }

    public int? MajorCount { get; init; }
    public int? MinorCount { get; init; }

    /// <summary>Each fact as "span (reason)", minor ones prefixed "[minor] ", joined with " | ".</summary>
    public string Invented { get; init; } = "";
    public int? Voice { get; init; }
    public int? Clarity { get; init; }
    public int? Tone { get; init; }
    public int? Overall { get; init; }
    public string Comment { get; init; } = "";
    public string? Error { get; init; }

    public bool Judged => Overall is not null;
}

/// <summary>The fields of a raw/*.json file the judge needs.</summary>
public sealed record RawOutput(string Cell, string System, string User, string? Text)
{
    public static RawOutput Load(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path)) ?? throw new JsonException($"{path} is empty");
        var first = node["attempts"]?[0];
        return new RawOutput(
            node["cell"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(path),
            first?["request"]?["system"]?.GetValue<string>() ?? "",
            first?["request"]?["user"]?.GetValue<string>() ?? "",
            first?["ok"]?.GetValue<bool>() == true ? first["text"]?.GetValue<string>() : null);
    }
}
