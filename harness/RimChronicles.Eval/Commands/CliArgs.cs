namespace RimChronicles.Eval.Commands;

/// <summary>Minimal "--name value" / "--flag" parser. Unknown options are errors.</summary>
public sealed class CliArgs
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.Ordinal);

    public CliArgs(IEnumerable<string> args, IReadOnlySet<string> valueOptions, IReadOnlySet<string> flags)
    {
        using var e = args.GetEnumerator();
        while (e.MoveNext())
        {
            var arg = e.Current;
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                Positional.Add(arg);
                continue;
            }

            var name = arg[2..];
            if (flags.Contains(name))
            {
                _options[name] = null;
            }
            else if (valueOptions.Contains(name))
            {
                _options[name] = e.MoveNext() ? e.Current : throw new ArgumentException($"--{name} needs a value.");
            }
            else
            {
                throw new ArgumentException($"Unknown option --{name}.");
            }
        }
    }

    public List<string> Positional { get; } = [];

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.GetValueOrDefault(name);

    /// <summary>A comma-separated option as a list; null when absent.</summary>
    public IReadOnlyList<string>? List(string name) =>
        Get(name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public int Int(string name, int fallback) =>
        Get(name) is { } v ? int.TryParse(v, out var i) && i > 0 ? i : throw new ArgumentException($"--{name} must be a positive number.") : fallback;
}
