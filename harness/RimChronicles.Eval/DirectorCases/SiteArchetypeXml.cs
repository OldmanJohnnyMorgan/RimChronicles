using System.Globalization;
using System.Xml.Linq;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Sites;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>Reads the site archetypes the game loads (<c>Defs/SiteArchetypeDefs</c>) for the crisis cases (C11): the
/// fields the director sees and the offer rules need (name, description, stage, weight). One source for both.</summary>
public static class SiteArchetypeXml
{
    /// <summary>The archetypes of <paramref name="power"/> (a power id; the Empire's by default, so the Imperial crisis
    /// cases never see the Choir's sites).</summary>
    public static IReadOnlyList<SiteArchetype> Load(string defsDir, string? power = null) =>
        Directory.GetFiles(defsDir, "*.xml").Order(StringComparer.Ordinal)
            .SelectMany(f => XDocument.Load(f).Root!.Elements().Where(e => e.Name.LocalName.EndsWith("SiteArchetypeDef", StringComparison.Ordinal)))
            .Where(e => ((string?)e.Element("power") ?? Core.Powers.Power.Empire.Id) == (power ?? Core.Powers.Power.Empire.Id))
            .Select(e => new SiteArchetype
            {
                Name = (string?)e.Element("archetype") ?? throw new InvalidDataException("A site archetype has no <archetype>."),
                Power = (string?)e.Element("power") ?? Core.Powers.Power.Empire.Id,
                Desc = ((string?)e.Element("description") ?? "").Trim(),
                ObjectiveTag = (string?)e.Element("objectiveTag") ?? "",
                MinStage = Enum.TryParse<CrisisStage>((string?)e.Element("minStage"), out var stage) ? stage : CrisisStage.Probing,
                NeedsHolding = (string?)e.Element("needsHolding") == "true",
                Weight = double.TryParse((string?)e.Element("weight"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 1,
            })
            .ToList();
}
