using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.News;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PersistenceTests
{
    private sealed class Holder : IArchivable
    {
        public ReporterState Reporter = new();

        public void Expose(IArchive archive) => archive.Deep(ref Reporter, "reporter");
    }

    [Fact]
    public void Deep_RoundTripsANestedObject()
    {
        var holder = new Holder();
        holder.Reporter.Name = "Kael Dunmore";
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        holder.Expose(saving);

        var loaded = new Holder { Reporter = null! };
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal("Kael Dunmore", loaded.Reporter.Name);
    }

    [Fact]
    public void Deep_MissingOnLoadGivesANewInstance()
    {
        var holder = new Holder { Reporter = null! };

        holder.Expose(new DictionaryArchive(ArchiveMode.Loading));

        Assert.NotNull(holder.Reporter);
        Assert.Equal(ReporterStatus.Active, holder.Reporter.Status);
    }

    [Theory]
    [InlineData(null, "content")]
    [InlineData(0.8f, "content")]
    [InlineData(0.35f, "content")]
    [InlineData(0.3f, "strained")]
    [InlineData(0.1f, "desperate")]
    public void ColonyMood_MapsAverageToWord(float? average, string expected) =>
        Assert.Equal(expected, ColonyMood.FromAverage(average));

    [Theory]
    [InlineData("New Hope", 12, "export-new-hope-day12")]
    [InlineData("Fort: \"Ash\"/Vale?", 3, "export-fort-ash-vale-day3")]
    [InlineData("???", 0, "export-colony-day0")]
    [InlineData("", 5, "export-colony-day5")]
    public void SnapshotIds_AreFileNameSafe(string colony, int day, string expected) =>
        Assert.Equal(expected, SnapshotIds.ForExport(colony, day));

    [Theory]
    [InlineData("Raid repelled", "Raid repelled")]
    [InlineData("The three of you awake in your cryptosleep sarcophagi to the sound of sirens. You barely get to the pods.", "The three of you awake in your cryptosleep sarcophagi to the sound of sirens.")]
    [InlineData("Trader arrived.", "Trader arrived.")]
    public void EventText_KeepsTheFirstSentence(string text, string expected) =>
        Assert.Equal(expected, EventText.Shorten(text));

    [Fact]
    public void EventText_CutsALongSentenceAtAWord()
    {
        var shortened = EventText.Shorten(string.Join(' ', Enumerable.Repeat("word", 40)), max: 30);

        Assert.True(shortened.Length <= 30);
        Assert.EndsWith("word…", shortened);
    }
}
