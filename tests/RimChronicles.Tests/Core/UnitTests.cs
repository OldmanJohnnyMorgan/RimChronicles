using System.Xml.Linq;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class UnitTests
{
    private static readonly CrisisStage[] Stages = [CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale];

    private static readonly (UnitRole Role, Specialty Specialty)[] Psykers =
    [
        (UnitRole.Adept, Specialty.Skip), (UnitRole.Adept, Specialty.Control), (UnitRole.Adept, Specialty.Mind),
        (UnitRole.Adept, Specialty.Ward), (UnitRole.Stellarch, Specialty.None),
    ];

    /// <summary>Every psycast defName and level in Royalty 1.7 (docs/ENGINE_NOTES.md, "Crisis survey").</summary>
    private static readonly Dictionary<string, int> Royalty = new()
    {
        ["Painblock"] = 1, ["Stun"] = 1, ["Burden"] = 1, ["Chunkskip"] = 1, ["SolarPinhole"] = 1, ["WordOfTrust"] = 1,
        ["BlindingPulse"] = 2, ["EntropyDump"] = 2, ["Waterskip"] = 2, ["WordOfJoy"] = 2,
        ["Beckon"] = 3, ["VertigoPulse"] = 3, ["ChaosSkip"] = 3, ["WordOfLove"] = 3,
        ["Skip"] = 4, ["Wallraise"] = 4, ["Smokepop"] = 4, ["Focus"] = 4, ["WordOfSerenity"] = 4,
        ["Berserk"] = 5, ["Invisibility"] = 5, ["Flashstorm"] = 5, ["Farskip"] = 5, ["WordOfInspiration"] = 5,
        ["BerserkPulse"] = 6, ["ManhunterPulse"] = 6, ["MassChaosSkip"] = 6, ["BulletShield"] = 6, ["Neuroquake"] = 6,
    };

    [Fact]
    public void Kits_UseRealDefNamesAtTheirRealLevels()
    {
        foreach (var (role, specialty) in Psykers)
        {
            foreach (var psycast in PsycastKits.For(role, specialty))
            {
                Assert.True(Royalty.TryGetValue(psycast.Def, out var level), $"{psycast.Def} isn't a Royalty psycast");
                Assert.Equal(level, psycast.Level);
            }
        }
    }

    [Fact]
    public void PsycastLevels_MatchTheGame()
    {
        var file = "/workspaces/reference/data/Royalty/Defs/AbilityDefs/Abilities.xml";
        if (!File.Exists(file))
        {
            return; // the game's XML is only in the codespace, never in the repo or CI
        }

        var defs = XDocument.Load(file).Root!.Elements("AbilityDef").ToList();
        var byName = defs.Where(d => d.Attribute("Name") is not null).ToDictionary(d => d.Attribute("Name")!.Value);
        string? Level(XElement? d)
        {
            for (var i = 0; d is not null && i < 8; i++)
            {
                if (d.Element("level") is { } level)
                {
                    return level.Value;
                }

                d = d.Attribute("ParentName") is { } p && byName.TryGetValue(p.Value, out var parent) ? parent : null;
            }

            return null;
        }

        foreach (var (name, level) in Royalty)
        {
            var def = defs.Single(d => d.Element("defName")?.Value == name);
            Assert.Equal(level.ToString(System.Globalization.CultureInfo.InvariantCulture), Level(def));
        }
    }

    [Fact]
    public void Kits_NoBannedPsycastIsInAnyKit()
    {
        var kits = Psykers.SelectMany(p => PsycastKits.For(p.Role, p.Specialty)).Select(k => k.Def);
        Assert.Empty(kits.Intersect(PsycastKits.RequiredBans));
    }

    [Fact]
    public void Kits_TheStellarchHasSkipAndWardAndFarskip()
    {
        var stellarch = PsycastKits.For(UnitRole.Stellarch, Specialty.None).Select(k => k.Def).ToHashSet();
        Assert.Superset(PsycastKits.Skip.Concat(PsycastKits.Ward).Select(k => k.Def).ToHashSet(), stellarch);
        Assert.Contains("Farskip", stellarch);
    }

    [Theory]
    [InlineData(UnitRole.Praetorian)]
    [InlineData(UnitRole.Herald)]
    [InlineData(UnitRole.Breacher)]
    public void NonPsykers_GetNoPsylinkAndNoPsycasts(UnitRole role)
    {
        foreach (var stage in Stages)
        {
            Assert.Equal(0, PsycastKits.Psylink(role, Specialty.None, stage, 0.99));
            Assert.Empty(PsycastKits.Grant(role, Specialty.None, 6, []));
        }
    }

    [Fact]
    public void Psykers_EveryOneHasSomethingToCast_FromItsFirstStage()
    {
        foreach (var (role, specialty) in Psykers)
        {
            foreach (var stage in Stages.Where(s => s >= PsycastKits.FirstStage(role, specialty)))
            {
                foreach (var roll in new[] { 0.0, 0.5, 0.999 })
                {
                    var psylink = PsycastKits.Psylink(role, specialty, stage, roll);
                    Assert.NotEmpty(PsycastKits.Grant(role, specialty, psylink, []));
                }
            }
        }
    }

    [Fact]
    public void Grant_OnlyPsycastsTheLevelAllows_NeverBanned_NeverMissing()
    {
        Assert.Equal(["ChaosSkip", "Beckon"], PsycastKits.Grant(UnitRole.Adept, Specialty.Skip, 3, []));
        Assert.Equal(["ChaosSkip", "Beckon", "Skip"], PsycastKits.Grant(UnitRole.Adept, Specialty.Skip, 4, []));
        Assert.Equal(["ChaosSkip", "Skip"], PsycastKits.Grant(UnitRole.Adept, Specialty.Skip, 6, ["Beckon"], d => d != "MassChaosSkip"));
        Assert.Empty(PsycastKits.Grant(UnitRole.Adept, Specialty.Mind, 4, []));
    }

    [Fact]
    public void Grant_TheRequiredBansApplyEvenWithAnEmptyList()
    {
        // A kit that somehow lists a banned psycast (a modded kit, a typo in a later change) still never gets it.
        foreach (var banned in PsycastKits.RequiredBans)
        {
            foreach (var (role, specialty) in Psykers)
            {
                Assert.DoesNotContain(banned, PsycastKits.Grant(role, specialty, PsycastKits.MaxPsylink, []));
            }
        }
    }

    [Fact]
    public void Tiers_FollowTheStageTable()
    {
        Assert.Equal((3, 3), Range(PsycastKits.Tier(UnitRole.Adept, CrisisStage.Probing)));
        Assert.Equal((3, 4), Range(PsycastKits.Tier(UnitRole.Adept, CrisisStage.Reclamation)));
        Assert.Equal((4, 5), Range(PsycastKits.Tier(UnitRole.Adept, CrisisStage.War)));
        Assert.Equal((5, 6), Range(PsycastKits.Tier(UnitRole.Adept, CrisisStage.Finale)));
        Assert.Equal((6, 6), Range(PsycastKits.Tier(UnitRole.Stellarch, CrisisStage.Finale)));
        Assert.Equal(GearQuality.Legendary, PsycastKits.Tier(UnitRole.Stellarch, CrisisStage.Finale).Gear);
        Assert.Equal(PsycastKits.Tier(UnitRole.Adept, CrisisStage.Probing), PsycastKits.Tier(UnitRole.Adept, CrisisStage.None));

        static (int, int) Range(UnitTier t) => (t.PsylinkMin, t.PsylinkMax);
    }

    [Fact]
    public void Tiers_GearBionicsAndPsyfocusNeverDropAsTheStageRises()
    {
        foreach (var role in Enum.GetValues<UnitRole>())
        {
            var tiers = Stages.Select(s => PsycastKits.Tier(role, s)).ToList();
            for (var i = 1; i < tiers.Count; i++)
            {
                Assert.True(tiers[i].Gear >= tiers[i - 1].Gear, $"{role} gear");
                Assert.True(tiers[i].Bionics >= tiers[i - 1].Bionics, $"{role} bionics");
                Assert.True(tiers[i].Psyfocus >= tiers[i - 1].Psyfocus, $"{role} psyfocus");
                Assert.True(tiers[i].PsylinkMax >= tiers[i - 1].PsylinkMax, $"{role} psylink");
            }
        }
    }

    [Fact]
    public void Psylink_AMindAdeptIsNeverBelowItsFirstPsycast()
    {
        Assert.Equal(5, PsycastKits.Psylink(UnitRole.Adept, Specialty.Mind, CrisisStage.War, 0));
        Assert.Equal(3, PsycastKits.Psylink(UnitRole.Adept, Specialty.Skip, CrisisStage.Probing, 0));
        Assert.Equal(1 + 3, PsycastKits.Psylink(UnitRole.Adept, Specialty.Control, CrisisStage.Reclamation, 0.99));
        Assert.Equal(6, PsycastKits.Psylink(UnitRole.Stellarch, Specialty.None, CrisisStage.Probing, 0));
    }

    [Fact]
    public void FirstStages_MindAtWar_StellarchAtTheFinale_OthersAtOnce()
    {
        Assert.Equal(CrisisStage.War, PsycastKits.FirstStage(UnitRole.Adept, Specialty.Mind));
        Assert.Equal(CrisisStage.Finale, PsycastKits.FirstStage(UnitRole.Stellarch, Specialty.None));
        Assert.Equal(CrisisStage.Probing, PsycastKits.FirstStage(UnitRole.Adept, Specialty.Ward));
        Assert.Equal(CrisisStage.Probing, PsycastKits.FirstStage(UnitRole.Praetorian, Specialty.None));
    }

    // ---- the defs

    [Fact]
    public void BanListDef_ListsEveryRequiredBan()
    {
        var doc = XDocument.Load(Repo.Path("Defs", "AbilityBanListDefs", "RimChronicles_Bans.xml"));
        var listed = doc.Descendants("abilities").Elements("li").Select(e => e.Value.Trim()).ToHashSet();
        Assert.Superset(PsycastKits.RequiredBans.ToHashSet(), listed);
    }

    [Fact]
    public void UnitDefs_EveryRoleAndSpecialtyHasOneKind_AndEveryKindNeedsRoyalty()
    {
        var doc = XDocument.Load(Repo.Path("Defs", "PawnKindDefs", "RimChronicles_Units.xml"));
        var kinds = doc.Root!.Elements("PawnKindDef").ToList();
        Assert.All(kinds, k => Assert.Equal("Ludeon.RimWorld.Royalty", k.Attribute("MayRequire")?.Value));

        var units = kinds.Where(k => k.Attribute("Abstract") is null)
            .Select(k => (Name: k.Element("defName")!.Value, Ext: k.Descendants("li").Single(li => li.Attribute("Class")?.Value.EndsWith(".UnitExtension") == true)))
            .Select(u => (u.Name, Role: Enum.Parse<UnitRole>(u.Ext.Element("role")!.Value),
                Specialty: u.Ext.Element("specialty") is { } s ? Enum.Parse<Specialty>(s.Value) : Specialty.None))
            .ToList();
        Assert.All(units, u => Assert.StartsWith("RimChronicles_", u.Name));

        var expected = new[]
        {
            (UnitRole.Praetorian, Specialty.None), (UnitRole.Herald, Specialty.None), (UnitRole.Breacher, Specialty.None),
            (UnitRole.Adept, Specialty.Skip), (UnitRole.Adept, Specialty.Control), (UnitRole.Adept, Specialty.Mind),
            (UnitRole.Adept, Specialty.Ward), (UnitRole.Stellarch, Specialty.None),
        };
        Assert.Equal(expected.OrderBy(e => e.ToString()), units.Select(u => (u.Role, u.Specialty)).OrderBy(e => e.ToString()));
    }
}
