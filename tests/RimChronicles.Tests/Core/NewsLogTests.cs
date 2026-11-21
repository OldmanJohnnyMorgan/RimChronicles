using RimChronicles.Core.News;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class NewsLogTests
{
    [Fact]
    public void Add_SkipsTheSameNewsOnTheSameDay()
    {
        var log = new NewsLog();

        Assert.True(log.Add(new NewsEvent(3, NewsKind.ConditionStarted, "The cold snap has begun across the region")));
        Assert.False(log.Add(new NewsEvent(3, NewsKind.ConditionStarted, "The cold snap has begun across the region")));
        Assert.True(log.Add(new NewsEvent(9, NewsKind.ConditionStarted, "The cold snap has begun across the region")));
        Assert.False(log.Add(new NewsEvent(9, NewsKind.ConditionStarted, " ")));

        Assert.Equal(2, log.Events.Count);
    }

    [Fact]
    public void Prune_DropsOldNews()
    {
        var log = new NewsLog();
        log.Add(new NewsEvent(1, NewsKind.LeaderDied, "old"));
        log.Add(new NewsEvent(80, NewsKind.LeaderDied, "new"));

        Assert.Equal(1, log.Prune(today: 100));
        Assert.Equal("new", Assert.Single(log.Events).Text);
    }

    [Fact]
    public void Since_ReturnsNewsFromADayOnOldestFirst()
    {
        var log = new NewsLog();
        log.Add(new NewsEvent(7, NewsKind.LeaderDied, "b"));
        log.Add(new NewsEvent(2, NewsKind.LeaderDied, "a"));
        log.Add(new NewsEvent(5, NewsKind.LeaderDied, "c"));

        Assert.Equal(["c", "b"], log.Since(5).Select(e => e.Text));
    }

    [Fact]
    public void NewsLog_RoundTripsAndLoadsEmptyFromAnOldSave()
    {
        var log = new NewsLog();
        log.Add(new NewsEvent(4, NewsKind.FactionDefeated, "Wioog has lost its last settlement and is no more", "Wioog"));
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        log.Expose(saving);

        var loaded = new NewsLog();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        var fresh = new NewsLog { Events = null! };
        fresh.Expose(new DictionaryArchive(ArchiveMode.Loading));

        var e = Assert.Single(loaded.Events);
        Assert.Equal((4, NewsKind.FactionDefeated, "Wioog"), (e.Day, e.Kind, e.Faction));
        Assert.Empty(fresh.Events);
    }

    [Theory]
    [InlineData("toxic fallout", null, false, "The toxic fallout has begun across the region")]
    [InlineData("cold snap", "New Arrivals", false, "The cold snap has begun around New Arrivals")]
    [InlineData("volcanic winter", null, true, "The volcanic winter across the region has ended")]
    public void Condition_PhrasesStartAndEnd(string label, string? place, bool ended, string expected) =>
        Assert.Equal(expected, NewsPhrasing.Condition(label, place, ended));

    [Theory]
    [InlineData(RelationKind.Hostile, RelationKind.Neutral, "Wioog turned hostile to New Arrivals")]
    [InlineData(RelationKind.Ally, RelationKind.Neutral, "Wioog became allies of New Arrivals")]
    [InlineData(RelationKind.Neutral, RelationKind.Hostile, "Wioog made peace with New Arrivals")]
    [InlineData(RelationKind.Neutral, RelationKind.Ally, "Wioog is no longer allied with New Arrivals")]
    public void RelationChanged_PhrasesEachChange(RelationKind now, RelationKind before, string expected) =>
        Assert.Equal(expected, NewsPhrasing.RelationChanged("Wioog", "New Arrivals", now, before));

    [Fact]
    public void LeaderDied_NamesTheSuccessorWhenThereIsOne()
    {
        Assert.Equal("Vasha, chief of Wioog, has died", NewsPhrasing.LeaderDied("Wioog", "chief", "Vasha", null));
        Assert.Equal("Vasha, chief of Wioog, has died, and Orn now leads them", NewsPhrasing.LeaderDied("Wioog", "chief", "Vasha", "Orn"));
    }
}
