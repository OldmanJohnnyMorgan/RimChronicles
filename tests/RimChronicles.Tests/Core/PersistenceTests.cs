using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Spike;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PersistenceTests
{
    private sealed class Holder : IArchivable
    {
        public SpikeState State = new();

        public void Expose(IArchive archive) => archive.Deep(ref State, "spike");
    }

    [Fact]
    public void SpikeState_RoundTrips()
    {
        var state = new SpikeState { DaysSeen = 4, LastReply = "Quiet week.", LastReplyDay = 3 };
        state.AddLog("day 3: reply");

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        state.Expose(saving);
        var loaded = new SpikeState();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal(4, loaded.DaysSeen);
        Assert.Equal("Quiet week.", loaded.LastReply);
        Assert.Equal(3, loaded.LastReplyDay);
        Assert.Equal(["day 3: reply"], loaded.Log);
    }

    [Fact]
    public void SpikeState_LoadsDefaultsFromASaveWithoutTheMod()
    {
        var loaded = new SpikeState { DaysSeen = 9, LastReply = "stale", Log = null! };

        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading));

        Assert.Equal(0, loaded.DaysSeen);
        Assert.Equal("", loaded.LastReply);
        Assert.Equal(-1, loaded.LastReplyDay);
        Assert.Empty(loaded.Log);
    }

    [Fact]
    public void Deep_MissingOnLoadGivesANewInstance()
    {
        var holder = new Holder { State = null! };

        holder.Expose(new DictionaryArchive(ArchiveMode.Loading));

        Assert.NotNull(holder.State);
        Assert.Equal(-1, holder.State.LastReplyDay);
    }

    [Fact]
    public void SpikeState_LogKeepsTheNewestLines()
    {
        var state = new SpikeState();
        for (var i = 0; i < SpikeState.MaxLog + 5; i++)
        {
            state.AddLog($"line {i}");
        }

        Assert.Equal(SpikeState.MaxLog, state.Log.Count);
        Assert.Equal("line 5", state.Log[0]);
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
