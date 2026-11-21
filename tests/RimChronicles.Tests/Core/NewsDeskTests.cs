using RimChronicles.Core.News;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Llm.Tasks;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class NewsDeskTests
{
    private static Bulletin Written(string text, params string[] covered) =>
        new() { Text = text, Source = "Model", PromptVersion = "news-bulletin/v8", NewsCovered = [.. covered] };

    [Fact]
    public void Plan_WritesTwoDaysEarlyAndDeliversOnTheDueDay()
    {
        var desk = new NewsDesk();

        Assert.Equal(DeskAction.None, desk.Plan(today: 0, cadenceDays: 3, writing: false));
        Assert.Equal(3, desk.NextDueDay);
        Assert.Equal(DeskAction.Generate, desk.Plan(1, 3, writing: false));
        Assert.Equal(DeskAction.None, desk.Plan(1, 3, writing: true));

        desk.SetPending(Written("Bulletin one."));
        Assert.Equal(DeskAction.None, desk.Plan(2, 3, false));
        Assert.Equal(DeskAction.Deliver, desk.Plan(3, 3, false));

        var delivered = desk.Deliver(3, 3);
        Assert.Equal((3, 3), (delivered!.DueDay, delivered.DeliveredDay));
        Assert.Equal(6, desk.NextDueDay);
        Assert.Null(desk.Pending);
    }

    [Fact]
    public void Plan_WithACadenceOfOneWritesRightAfterTheLastDelivery()
    {
        var desk = new NewsDesk();
        desk.Plan(0, 1, false);
        desk.SetPending(Written("One."));
        desk.Deliver(1, 1);

        Assert.Equal(1, desk.WriteDay);
        Assert.Equal(DeskAction.Generate, desk.Plan(1, 1, false));
    }

    [Fact]
    public void Plan_WritesAtOnceWhenTheDueDayHasAlreadyCome()
    {
        var desk = new NewsDesk { NextDueDay = 10 };

        Assert.Equal(DeskAction.Generate, desk.Plan(12, 3, false));
    }

    [Fact]
    public void SetPending_IgnoresASecondResult()
    {
        var desk = new NewsDesk { NextDueDay = 5 };

        Assert.True(desk.SetPending(Written("First.")));
        Assert.False(desk.SetPending(Written("Stale.")));
        Assert.Equal("First.", desk.Pending!.Text);
    }

    [Fact]
    public void Deliver_MarksCoveredNewsAsOld()
    {
        var desk = new NewsDesk { NextDueDay = 3 };
        desk.SetPending(Written("B.", "The cold snap has begun across the region"));
        desk.Deliver(3, 3);

        var news = new NewsLog();
        news.Add(new NewsEvent(2, NewsKind.ConditionStarted, "The cold snap has begun across the region"));
        news.Add(new NewsEvent(4, NewsKind.RelationChanged, "Wioog turned hostile to New Arrivals"));
        var (snapshot, fresh) = desk.PrepareSnapshot(World(day: 5), news);

        Assert.Equal(["Wioog turned hostile to New Arrivals"], fresh);
        Assert.Equal(["The cold snap has begun across the region"], snapshot.ReportedEvents);
        Assert.Equal(2, snapshot.PublicEvents.Count);
        Assert.Equal(["B."], snapshot.PreviousBulletins);
        Assert.Equal(WorldSummarizer.DefaultReporter.Name, snapshot.Reporter!.Name);
        Assert.Equal(1, WorldSummarizer.NewNewsCount(snapshot));
    }

    [Fact]
    public void PrepareSnapshot_LeavesOutNewsOlderThanTheWindow()
    {
        var news = new NewsLog();
        news.Add(new NewsEvent(1, NewsKind.LeaderDied, "Old news"));
        news.Add(new NewsEvent(20, NewsKind.LeaderDied, "New news"));

        var (snapshot, _) = new NewsDesk().PrepareSnapshot(World(day: 30), news);

        Assert.Equal(["New news"], snapshot.PublicEvents);
    }

    [Fact]
    public void Reload_KeepsTheWaitingAndDeliveredBulletinsWordForWord()
    {
        var desk = new NewsDesk { NextDueDay = 3 };
        desk.SetPending(Written("Delivered text.", "a"));
        desk.Deliver(3, 3);
        desk.SetPending(Written("Waiting text.", "b"));

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        desk.Expose(saving);
        var loaded = new NewsDesk();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal("Delivered text.", Assert.Single(loaded.Delivered).Text);
        Assert.Equal(3, loaded.Delivered[0].DeliveredDay);
        Assert.Equal("Waiting text.", loaded.Pending!.Text);
        Assert.Equal(6, loaded.Pending.DueDay);
        Assert.Equal(["a"], loaded.Reported);
        Assert.Equal(DeskAction.Deliver, loaded.Plan(6, 3, false));
    }

    [Fact]
    public void Reload_KeepsTheRecruitedReporter()
    {
        var desk = new NewsDesk();
        desk.Reporter.Name = "Kael Dunmore";
        desk.Reporter.Faction = "Ania Confederacy";
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        desk.Expose(saving);

        var loaded = new NewsDesk();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal(("Kael Dunmore", "Ania Confederacy"), (loaded.Reporter.Name, loaded.Reporter.Faction));
        Assert.Equal(WorldSummarizer.DefaultReporter.Outlet, loaded.Reporter.Outlet);
    }

    [Fact]
    public void Reload_FromASaveWithoutTheDeskStartsFresh()
    {
        var loaded = new NewsDesk { NextDueDay = 9, Pending = Written("x") };

        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading));

        Assert.Equal(-1, loaded.NextDueDay);
        Assert.Null(loaded.Pending);
        Assert.Empty(loaded.Delivered);
        Assert.Equal(WorldSummarizer.DefaultReporter.Name, loaded.Reporter.Name);
        Assert.Equal("", loaded.Reporter.Faction);
    }

    [Fact]
    public void Templates_ReportTheNewsOrAQuietWeekAndStayStablePerDay()
    {
        var reporter = new ReporterState();

        var news = BulletinTemplates.Compose(reporter, ["Wioog turned hostile to New Arrivals"], day: 4);
        var quiet = BulletinTemplates.Compose(reporter, [], day: 4);

        Assert.Contains("Wioog turned hostile to New Arrivals.", news);
        Assert.Contains(reporter.Name, news);
        Assert.DoesNotContain("Wioog", quiet);
        Assert.Equal(news, BulletinTemplates.Compose(reporter, ["Wioog turned hostile to New Arrivals"], day: 4));
        Assert.NotEqual(quiet, BulletinTemplates.Compose(reporter, [], day: 5));
    }

    [Fact]
    public async Task Task_WithNoModelFallsBackToTheTemplate()
    {
        var library = PromptLibrary.Load(Repo.Prompts);
        var news = new NewsLog();
        news.Add(new NewsEvent(4, NewsKind.ConditionStarted, "The toxic fallout has begun across the region"));
        var desk = new NewsDesk();
        var (snapshot, fresh) = desk.PrepareSnapshot(World(day: 5), news);
        var fallback = new DelegateFallback<string>(() => BulletinTemplates.Compose(desk.Reporter, fresh, 5));

        var (definition, _) = BulletinTask.Build(library, snapshot, ModelTier.Recommended, ThinkingMode.Off, fallback);
        var outcome = await new LlmTaskRunner(provider: null).RunAsync(definition, CancellationToken.None);

        Assert.Equal(OutcomeSource.Template, outcome.Source);
        Assert.Contains("The toxic fallout has begun across the region.", outcome.Value);
    }

    [Fact]
    public void Templates_PassTheBulletinChecks()
    {
        var news = new NewsLog();
        news.Add(new NewsEvent(4, NewsKind.ConditionStarted, "The toxic fallout has begun across the region"));
        news.Add(new NewsEvent(4, NewsKind.RelationChanged, "Wioog turned hostile to New Arrivals", "Wioog"));
        var desk = new NewsDesk();
        foreach (var (snapshot, fresh) in new[] { desk.PrepareSnapshot(World(5), news), desk.PrepareSnapshot(World(5), new NewsLog()) })
        {
            var input = WorldSummarizer.ForNewsBulletin(snapshot, ModelTier.Recommended);
            var text = BulletinTemplates.Compose(desk.Reporter, fresh, 5);

            var failures = BulletinTask.Checks(snapshot, input).SelectMany(c => c.Check(text).Failures).ToList();

            Assert.True(failures.Count == 0, $"{text}\n{string.Join("\n", failures)}");
        }
    }

    private static WorldSnapshot World(int day) => new()
    {
        Id = "test",
        Day = day,
        Season = "Aprimay",
        Year = 5500,
        Colony = new ColonyInfo { Name = "New Arrivals", Wealth = 16000, Population = 3, Mood = "content" },
        Factions = [new FactionInfo { Name = "Wioog", Goodwill = -80, Settlements = 29 }],
    };
}
