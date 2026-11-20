namespace RimChronicles.Llm.Validation;

/// <summary>The outcome of one check. Failures are short, plain sentences that can be shown back to the model.</summary>
public sealed record CheckResult(bool Passed, IReadOnlyList<string> Failures)
{
    public static readonly CheckResult Pass = new(true, []);

    public static CheckResult Fail(params string[] failures) => new(false, failures);

    public static CheckResult From(IReadOnlyList<string> failures) => failures.Count == 0 ? Pass : new(false, failures);
}

/// <summary>A rule a model's output must satisfy. Checks never throw on bad output; they fail.</summary>
public interface IOutputCheck
{
    /// <summary>Short id for logs and reports, e.g. "word-count".</summary>
    string Name { get; }

    CheckResult Check(string output);
}
