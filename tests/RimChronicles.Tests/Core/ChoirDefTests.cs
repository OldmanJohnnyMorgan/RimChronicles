using System.Xml.Linq;
using RimChronicles.Core.Choir;
using RimChronicles.Core.Powers;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The Hollow Choir's faction and units (War in Heaven W3), read from the repo's Defs XML without the game.</summary>
public class ChoirDefTests
{
    private const string Biotech = "Ludeon.RimWorld.Biotech";

    /// <summary>Vanilla mech pawn kinds (W0 survey, ENGINE_NOTES), so a typo in a group maker fails here.</summary>
    private static readonly HashSet<string> VanillaMechKinds =
    [
        "Mech_Militor", "Mech_Scyther", "Mech_Lancer", "Mech_Pikeman", "Mech_CentipedeBlaster", "Mech_CentipedeGunner",
        "Mech_CentipedeBurner", "Mech_Legionary", "Mech_Tesseron", "Mech_Scorcher", "Mech_Centurion", "Mech_Tunneler",
        "Mech_Warqueen", "Mech_Diabolus", "Mech_Apocriton",
    ];

    private static readonly XElement Faction = XDocument.Load(Repo.Path("Defs", "FactionDefs", "RimChronicles_HollowChoir.xml"))
        .Root!.Elements("FactionDef").Single();

    private static readonly List<XElement> Units = XDocument.Load(Repo.Path("Defs", "PawnKindDefs", "RimChronicles_ChoirUnits.xml"))
        .Root!.Elements("PawnKindDef").ToList();

    private static string? Value(XElement e, string name) => e.Element(name)?.Value;

    [Fact]
    public void TheFaction_IsTheChoirsAndNeedsBiotech()
    {
        Assert.Equal(ChoirIds.FactionDefName, Value(Faction, "defName"));
        Assert.Equal(Biotech, (string?)Faction.Attribute("MayRequire"));
        Assert.Equal("Ultra", Value(Faction, "techLevel"));
        Assert.Equal("First Voice", Value(Faction, "leaderTitle"));
    }

    [Fact]
    public void TheFaction_IsNeverMadeAtWorldCreation()
    {
        // FactionGenerator makes factions that are required or configurable (maxConfigurableAtWorldCreation > 0); the
        // Choir is neither, so it only exists once it wakes (tech spec R2).
        Assert.Null(Faction.Element("requiredCountAtGameStart"));
        Assert.Null(Faction.Element("maxConfigurableAtWorldCreation"));
        Assert.Null(Faction.Element("hidden"));
        Assert.Null(Faction.Element("permanentEnemy"));
    }

    [Fact]
    public void EveryUnit_IsAChoirMechanitorThatNeedsBiotech()
    {
        Assert.Equal(["RimChronicles_Chorister", "RimChronicles_Choirmaster", "RimChronicles_FirstVoice"], Units.Select(u => Value(u, "defName")));
        Assert.All(Units, u =>
        {
            Assert.Equal("MechanitorBase", (string?)u.Attribute("ParentName"));
            Assert.Equal(Biotech, (string?)u.Attribute("MayRequire"));
            Assert.Equal(ChoirIds.FactionDefName, Value(u, "defaultFactionDef"));
        });
        Assert.Equal("true", Value(Units.Single(u => Value(u, "defName") == "RimChronicles_FirstVoice"), "factionLeader"));
    }

    [Fact]
    public void TheGroupMakers_UseOnlyChoirUnitsAndRealMechs_AndEveryFightHasMechanitors()
    {
        var ours = Units.Select(u => Value(u, "defName")!).ToHashSet();
        Assert.Contains(Value(Faction, "basicMemberKind")!, ours);
        Assert.All(Faction.Element("fixedLeaderKinds")!.Elements("li"), li => Assert.Contains(li.Value, ours));

        foreach (var maker in Faction.Element("pawnGroupMakers")!.Elements("li"))
        {
            var kinds = maker.Element("options")!.Elements().Select(o => o.Name.LocalName).ToList();
            Assert.All(kinds, k => Assert.True(ours.Contains(k) || VanillaMechKinds.Contains(k), $"{k} is neither a Choir unit nor a known mech kind"));
            Assert.Contains(kinds, ours.Contains);
        }
    }

    [Fact]
    public void ThePower_IsReadyButNotRunningUntilItsPlotExists()
    {
        Assert.Equal("choir", Power.Choir.Id);
        Assert.Equal("Choir", Power.Choir.OptionPrefix);
        Assert.Equal("Act1:ChoirEnvoy", Power.Choir.EnvoyOption);
        Assert.DoesNotContain(Power.Choir, Power.All); // joins with its saga and plot (W4)
        Assert.Contains("Chorus", Power.Choir.Text.Describe(PowerStep.Envoy));
    }
}
