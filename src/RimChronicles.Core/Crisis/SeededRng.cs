namespace RimChronicles.Core.Crisis;

/// <summary>
/// A small seeded random number generator (SplitMix64) for anything that must come out the same every time from one
/// seed, such as a site's layout (tech spec R18). <see cref="System.Random"/>'s seeded sequence isn't promised to stay
/// the same across .NET versions; this one is fixed by its tests.
/// </summary>
public sealed class SeededRng
{
    private ulong _state;

    public SeededRng(ulong seed) => _state = seed;

    /// <summary>A seed from a number and a text (for example a world object id and an archetype's defName).</summary>
    public static ulong Seed(long id, string salt = "")
    {
        var hash = 14695981039346656037UL; // FNV-1a, 64-bit
        foreach (var c in salt)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return Mix(unchecked((ulong)id) ^ hash);
    }

    public ulong NextULong()
    {
        _state = unchecked(_state + 0x9E3779B97F4A7C15UL);
        return Mix(_state);
    }

    /// <summary>A number in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>A whole number in [0, <paramref name="max"/>).</summary>
    public int Next(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "max must be positive");
        }

        return (int)(NextDouble() * max);
    }

    /// <summary>A whole number in [<paramref name="min"/>, <paramref name="max"/>], both included.</summary>
    public int Range(int min, int max) => max <= min ? min : min + Next(max - min + 1);

    private static ulong Mix(ulong z)
    {
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }
}
