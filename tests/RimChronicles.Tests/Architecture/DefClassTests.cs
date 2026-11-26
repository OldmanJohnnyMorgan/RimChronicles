using System.Text.RegularExpressions;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Architecture;

/// <summary>
/// Every mod type the XML names (<c>Class="RimChronicles…"</c>, <c>&lt;thingClass&gt;</c>, <c>&lt;workerClass&gt;</c> and the
/// like) must exist under that full name in <c>RimChronicles.Game</c>. RimWorld only finds out at load time ("Could not
/// find type named …"), and the Game project can't be loaded in tests, so this reads the source instead: a namespace
/// moved or a class renamed without its def fails here, not in the game.
/// </summary>
public partial class DefClassTests
{
    [GeneratedRegex(@"^namespace\s+([\w.]+)", RegexOptions.Multiline)]
    private static partial Regex Namespace();

    [GeneratedRegex(@"\b(?:class|struct|enum)\s+(\w+)")]
    private static partial Regex TypeName();

    [GeneratedRegex(@"(?:Class=""|<\w*[cC]lass>|<li>)\s*(RimChronicles\.[\w.]+)")]
    private static partial Regex XmlType();

    [Fact]
    public void EveryModTypeNamedInXml_ExistsInTheGameSource()
    {
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Repo.Path("src", "RimChronicles.Game"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var ns = Namespace().Match(text) is { Success: true } m ? m.Groups[1].Value : "";
            foreach (Match t in TypeName().Matches(text))
            {
                types.Add($"{ns}.{t.Groups[1].Value}");
            }
        }

        var named = new List<string>();
        var missing = new List<string>();
        foreach (var folder in new[] { "Defs", "Patches" }.Select(f => Repo.Path(f)).Where(Directory.Exists))
        {
            foreach (var file in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
            {
                foreach (Match x in XmlType().Matches(File.ReadAllText(file)))
                {
                    var type = x.Groups[1].Value;
                    named.Add(type);
                    if (!types.Contains(type))
                    {
                        missing.Add($"{Path.GetFileName(file)}: {type}");
                    }
                }
            }
        }

        Assert.NotEmpty(named);
        Assert.Empty(missing);
    }
}
