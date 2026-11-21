using RimChronicles.Core.News;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class ReporterLifeTests
{
    private static NewsDesk ActiveDesk()
    {
        var desk = new NewsDesk();
        desk.NewReporter("Kael Dunmore", "Tola", today: 0, visitInDays: 20);
        desk.Plan(0, 3, false);
        return desk;
    }

    [Fact]
    public void NewReporter_IsActiveWithAVisitPlanned()
    {
        var desk = ActiveDesk();

        Assert.Equal(ReporterStatus.Active, desk.Reporter.Status);
        Assert.False(desk.VisitDue(19));
        Assert.True(desk.VisitDue(20));
        desk.ScheduleVisit(20, 17);
        Assert.False(desk.VisitDue(21));
        Assert.True(desk.VisitDue(37));
    }

    [Fact]
    public void Silence_StopsTheNewsDropsTheWaitingBulletinAndPlansNoVisits()
    {
        var desk = ActiveDesk();
        desk.SetPending(new Bulletin { Text = "Never sent." });

        desk.Silence(today: 2);

        Assert.Equal((ReporterStatus.Silenced, 2), (desk.Reporter.Status, desk.Reporter.SilencedDay));
        Assert.Null(desk.Pending);
        Assert.Equal(DeskAction.None, desk.Plan(50, 3, false));
        Assert.False(desk.VisitDue(100));
        Assert.False(desk.ReplacementDue(100));
    }

    [Fact]
    public void ReporterLost_WaitsForTheReplacementThenResumes()
    {
        var desk = ActiveDesk();

        desk.ReporterLost(today: 4, replaceInDays: 7);

        Assert.Equal(DeskAction.None, desk.Plan(6, 3, false));
        Assert.False(desk.ReplacementDue(10));
        Assert.True(desk.ReplacementDue(11));

        desk.NewReporter("Mira Holt", "Tola", today: 11, visitInDays: 25);
        Assert.Equal(ReporterStatus.Active, desk.Reporter.Status);
        Assert.NotEqual(DeskAction.None, desk.Plan(11, 3, false)); // overdue: writes at once
    }

    [Fact]
    public void ReporterLost_DoesNotOverrideASilencing()
    {
        var desk = ActiveDesk();
        desk.Silence(3);

        desk.ReporterLost(4, 5);

        Assert.Equal(ReporterStatus.Silenced, desk.Reporter.Status);
    }

    [Fact]
    public void Held_PausesTheNewsAndFreedResumesIt()
    {
        var desk = ActiveDesk();
        desk.SetPending(new Bulletin { Text = "Waiting." });

        desk.Held();
        Assert.Equal(DeskAction.None, desk.Plan(5, 3, false));

        desk.Freed();
        Assert.Equal(DeskAction.Deliver, desk.Plan(5, 3, false));
        Assert.Equal("Waiting.", desk.Pending!.Text);
    }

    [Theory]
    [InlineData(true, false, false, -1, 0, true)]      // shot by a colonist, turret or trap
    [InlineData(false, false, false, -1, 0, false)]    // killed by raiders or an animal
    [InlineData(null, false, false, -1, 0, false)]     // disease, no killer
    [InlineData(null, true, false, -1, 0, true)]       // died as the colony's prisoner
    [InlineData(false, false, true, -1, 0, true)]      // recruited, then died
    [InlineData(null, false, false, 1000, 60000, true)]   // bled out a day after the colony's attack
    [InlineData(null, false, false, 1000, 200000, false)] // long after
    [InlineData(false, false, false, 1000, 2000, false)]  // the colony hurt them, but raiders made the kill
    public void KilledByColony_ClassifiesTheCause(bool? killerIsColony, bool held, bool recruited, int harmTick, int nowTick, bool expected)
    {
        var desk = ActiveDesk();
        desk.Reporter.LastColonyHarmTick = harmTick;

        Assert.Equal(expected, desk.KilledByColony(killerIsColony, held, recruited, nowTick));
    }

    [Fact]
    public void ReporterState_RoundTripsAndOldSavesLoadActive()
    {
        var desk = ActiveDesk();
        desk.ReporterLost(4, 6);
        desk.Reporter.LastColonyHarmTick = 1234;
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        desk.Expose(saving);

        var loaded = new NewsDesk();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        var old = new NewsDesk();
        old.Expose(new DictionaryArchive(ArchiveMode.Loading));

        Assert.Equal((ReporterStatus.Replacing, 10, 1234), (loaded.Reporter.Status, loaded.Reporter.ReplaceDay, loaded.Reporter.LastColonyHarmTick));
        Assert.Equal((ReporterStatus.Active, -1, -1), (old.Reporter.Status, old.Reporter.NextVisitDay, old.Reporter.SilencedDay));
    }

    [Fact]
    public void ReporterDied_IsAPlainNewsLine() =>
        Assert.Equal("Kael Dunmore, correspondent for the Rim Dispatch, has died", NewsPhrasing.ReporterDied("Kael Dunmore", "the Rim Dispatch"));
}
