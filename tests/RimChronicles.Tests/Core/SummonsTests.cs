using RimChronicles.Core.Persistence;
using RimChronicles.Core.Powers;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The summons (War in Heaven W6, spec §6.1).</summary>
public class SummonsTests
{
    private static readonly SummonsCandidate Ada = new("p1", "Ada", Mechanitor: true, Psycaster: false);
    private static readonly SummonsCandidate Bo = new("p2", "Bo", Mechanitor: false, Psycaster: true);
    private static readonly SummonsCandidate Cy = new("p3", "Cy", Mechanitor: true, Psycaster: true);
    private static readonly SummonsCandidate Di = new("p4", "Di", Mechanitor: false, Psycaster: false);

    [Fact]
    public void EachSide_SummonsTheKindItCantKeep()
    {
        var book = new SummonsBook();
        Assert.Same(Ada, book.Next(Power.Empire, [Di, Bo, Ada, Cy]));
        Assert.Same(Bo, book.Next(Power.Choir, [Di, Ada, Bo, Cy]));
        Assert.Null(book.Next(Power.Empire, [Di, Bo]));
    }

    [Fact]
    public void OneAtATime_AndEachColonistOnce()
    {
        var book = new SummonsBook();
        var first = book.Start(Power.Empire, Ada, 41, 100);
        Assert.Null(book.Next(Power.Empire, [Ada, Cy])); // one pending at a time
        Assert.False(book.Finish(first, SummonsStatus.Returned));
        Assert.Same(Cy, book.Next(Power.Empire, [Ada, Cy])); // Ada was summoned already
        Assert.Same(Cy, book.Next(Power.Choir, [Ada, Cy])); // another side may summon a pawn the first returned
    }

    [Fact]
    public void Refusing_IsWhatSetsTheColonyIndependent()
    {
        var book = new SummonsBook();
        Assert.True(book.Finish(book.Start(Power.Choir, Bo, 7, 100), SummonsStatus.Refused));
        Assert.False(book.Finish(book.Start(Power.Empire, Ada, 8, 101), SummonsStatus.Ended));
    }

    [Fact]
    public void ItSavesAndLoads()
    {
        var book = new SummonsBook();
        book.Start(Power.Empire, Ada, 41, 100);
        var save = new DictionaryArchive(ArchiveMode.Saving);
        save.Deep(ref book, "summons");
        SummonsBook loaded = null!;
        new DictionaryArchive(ArchiveMode.Loading, save.Data).Deep(ref loaded, "summons");
        Assert.Equal(("p1", "empire", 41, SummonsStatus.Pending), (loaded.Records[0].PawnId, loaded.Records[0].Power, loaded.Records[0].QuestId, loaded.Records[0].Status));
    }
}

/// <summary>The summons' defs agree with the rules (War in Heaven W6).</summary>
public class SummonsDefTests
{
    [Fact]
    public void TheChorusTuning_GivesTheFormulasBandwidthAtEveryLevel()
    {
        var def = System.Xml.Linq.XDocument.Load(Repo.Path("Defs", "HediffDefs", "RimChronicles_Summons.xml")).Root!.Elements("HediffDef").Single();
        Assert.Equal("Ludeon.RimWorld.Biotech", (string?)def.Attribute("MayRequire"));
        var stages = def.Element("stages")!.Elements("li").ToList();
        Assert.Equal(6, stages.Count);
        for (var level = 1; level <= 6; level++)
        {
            var stage = stages[level - 1];
            Assert.Equal(level.ToString(System.Globalization.CultureInfo.InvariantCulture), stage.Element("minSeverity")!.Value);
            var offset = (int?)stage.Element("statOffsets")?.Element("MechBandwidth") ?? 0;
            Assert.Equal(Conversion.BonusBandwidthFor(level), offset);
        }
    }

    [Fact]
    public void TheQuest_IsStartedOnlyFromCode_AndNeedsBothDlcs()
    {
        var quest = System.Xml.Linq.XDocument.Load(Repo.Path("Defs", "QuestScriptDefs", "RimChronicles_Quests.xml")).Root!
            .Elements("QuestScriptDef").Single(e => (string?)e.Element("defName") == "RimChronicles_Summons");
        Assert.Equal("true", (string?)quest.Element("isRootSpecial"));
        Assert.Equal("false", (string?)quest.Element("randomlySelectable"));
        Assert.Equal("Ludeon.RimWorld.Royalty,Ludeon.RimWorld.Biotech", (string?)quest.Attribute("MayRequire"));
    }
}
