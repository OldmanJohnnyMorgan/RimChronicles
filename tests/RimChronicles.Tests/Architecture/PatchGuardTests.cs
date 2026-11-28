using System.Text.RegularExpressions;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Architecture;

/// <summary>
/// Every Harmony prefix and postfix with a block body must run it inside a try/catch that reports through
/// <c>Guard.Report</c> (code review fix 3), so the mod's failures never reach vanilla's tick, damage, death or load
/// code. One-line expression bodies (a null-safe field read into <c>__state</c>) are left alone. The Game project
/// can't load in tests, so this reads the source.
/// </summary>
public partial class PatchGuardTests
{
    [GeneratedRegex(@"public static (?:void|bool) (?:Prefix|Postfix|Transpiler|Finalizer)\([^)]*\)\s*\{(?<body>[^{]*\{)")]
    private static partial Regex BlockPatch();

    [Fact]
    public void Every_block_bodied_patch_is_guarded()
    {
        var files = Directory.GetFiles(Repo.Path("src", "RimChronicles.Game", "Patches"), "*.cs");
        Assert.NotEmpty(files);
        var unguarded = new List<string>();
        var checkedCount = 0;
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in BlockPatch().Matches(text))
            {
                checkedCount++;
                var head = m.Groups["body"].Value; // from the method's '{' to the next '{': "__state = default; try"
                if (!Regex.IsMatch(head, @"^\s*(?:__state = [^;]+;\s*)?try\s*\{$"))
                {
                    var line = text[..m.Index].Count(c => c == '\n') + 1;
                    unguarded.Add($"{Path.GetFileName(file)}:{line}");
                }
            }
        }

        Assert.True(checkedCount >= 25, $"Only {checkedCount} patches found; did the pattern stop matching?");
        Assert.True(unguarded.Count == 0, "Unguarded patch bodies: " + string.Join(", ", unguarded));
    }
}
