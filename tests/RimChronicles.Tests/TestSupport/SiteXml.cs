using System.Globalization;
using System.Xml.Linq;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Sites;

namespace RimChronicles.Tests.TestSupport;

/// <summary>Reads the repo's site archetypes (<c>Defs/SiteArchetypeDefs/</c>) as <c>SiteArchetypeDef.ToCore</c> does in game.</summary>
public static class SiteXml
{
    public static string File => Repo.Path("Defs", "SiteArchetypeDefs", "RimChronicles_Sites.xml");

    public static IReadOnlyList<SiteArchetype> Load() =>
        XDocument.Load(File).Root!.Elements().Where(e => e.Name.LocalName.EndsWith("SiteArchetypeDef")).Select(Parse).ToList();

    public static SiteArchetype Get(string name) => Load().Single(a => a.Name == name);

    private static SiteArchetype Parse(XElement e)
    {
        string? Text(string name) => e.Element(name)?.Value;
        float F(string? text, float fallback) => float.TryParse(text, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        int I(string? text, int fallback) => int.TryParse(text, out var i) ? i : fallback;
        var size = (Text("size") ?? "(40,40)").Trim('(', ')').Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
        var budget = (Text("budget") ?? "500~3000").Split('~').Select(s => F(s, 0)).ToArray();
        IReadOnlyList<PieceRequest> Requests(string name) => (e.Element(name)?.Elements("li") ?? []).Select(li => new PieceRequest(
            li.Element("tag")!.Value, I(li.Element("count")?.Value, 1), F(li.Element("weight")?.Value, 1), I(li.Element("max")?.Value, 1))).ToList();
        return new SiteArchetype
        {
            Name = Text("archetype")!,
            Desc = Text("description") ?? "",
            Width = size[0],
            Height = size[1],
            ObjectiveTag = Text("objectiveTag")!,
            Required = Requests("required"),
            Optional = Requests("optional"),
            DefenseTag = Text("defenseTag") ?? "Line",
            Gates = I(Text("gates"), 1),
            MinBudget = budget[0],
            MaxBudget = budget[1],
            PieceShare = F(Text("pieceShare"), 0.5f),
            OptionalChance = F(Text("optionalChance"), 0.7f),
            MinStage = Enum.Parse<CrisisStage>(Text("minStage") ?? "Probing"),
            Weight = F(Text("weight"), 1),
            Capability = Text("capability"),
            CapabilityDays = I(Text("capabilityDays"), 0),
            NeedsHolding = Text("needsHolding") == "true",
        };
    }
}
