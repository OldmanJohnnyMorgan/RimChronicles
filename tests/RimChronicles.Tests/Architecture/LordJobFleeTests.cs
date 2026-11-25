using System.Text.RegularExpressions;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Architecture;

/// <summary>
/// Vanilla's <c>Lord</c> adds a high-priority panic-flee transition (at a random 40–70% of pawns lost) to every toil of
/// a lord whose faction auto-flees, unless the LordJob turns it off. Our lords decide their own retreats (crisis
/// operation profiles, Citadel garrisons), so each must override <c>AddFleeToil</c> to false; otherwise vanilla's
/// flee pre-empts the profile's regroup and retreat, and units meant to hold fast run.
/// </summary>
public partial class LordJobFleeTests
{
    [GeneratedRegex(@"\bclass\s+(\w+)\s*(?:\([^)]*\))?\s*:\s*LordJob\b(?!_)")]
    private static partial Regex LordJobClass();

    [GeneratedRegex(@"\bclass\s+\w+")]
    private static partial Regex AnyClass();

    [Fact]
    public void EveryModLordJob_TurnsOffVanillaPanicFlee()
    {
        var found = new List<string>();
        var missing = new List<string>();
        foreach (var file in Directory.GetFiles(Repo.Path("src", "RimChronicles.Game"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in LordJobClass().Matches(text))
            {
                var next = AnyClass().Match(text, m.Index + m.Length);
                var body = text[m.Index..(next.Success ? next.Index : text.Length)];
                found.Add(m.Groups[1].Value);
                if (!body.Contains("AddFleeToil => false"))
                {
                    missing.Add(m.Groups[1].Value);
                }
            }
        }

        Assert.Contains("LordJob_RcOperation", found);
        Assert.Contains("LordJob_RcGarrison", found);
        Assert.Empty(missing);
    }
}
