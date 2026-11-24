using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PlotChronicleTests
{
    [Fact]
    public void Titles_NameTheSubject()
    {
        var nemesis = new Plot { Template = NemesisPlot.TemplateId };
        nemesis.SetFact(NemesisPlot.NemesisName, "Kael");
        var family = new Plot { Template = LostFamilyArc.TemplateId };
        family.SetFact(LostFamilyArc.KinName, "Mira");

        Assert.Equal("Nemesis: Kael", PlotChronicle.Title(nemesis));
        Assert.Equal("Lost Family: Mira", PlotChronicle.Title(family));
        Assert.Equal("Nemesis", PlotChronicle.Title(new Plot { Template = NemesisPlot.TemplateId }));
        Assert.Equal("The Silent Press", PlotChronicle.Title(new Plot { Template = "SilentPress" }));
        Assert.Equal("The Awakened Empire", PlotChronicle.Title(new Plot { Template = "AwakenedEmpire" }));
        Assert.Equal("Some new arc", PlotChronicle.Title(new Plot { Template = "SomeNewArc" }));
    }

    [Fact]
    public void Status_SaysWhenItStartedOrHowItEnded()
    {
        Assert.Equal("Under way since day 12", PlotChronicle.Status(new Plot { StartDay = 12 }));
        Assert.Equal("Ended on day 40: killed by colony",
            PlotChronicle.Status(new Plot { Beat = PlotBeat.Ended, BeatDay = 40, Outcome = nameof(NemesisEnd.KilledByColony) }));
        Assert.Equal("Ended on day 9: reparations paid; a cold new reporter",
            PlotChronicle.Status(new Plot { Beat = PlotBeat.Ended, BeatDay = 9, Outcome = "reparations paid; a cold new reporter" }));
    }

    [Theory]
    [InlineData("KilledByColony", "Killed by colony")]
    [InlineData("reunited", "Reunited")]
    [InlineData("Independence", "Independence")]
    [InlineData("a cold new reporter", "A cold new reporter")]
    [InlineData("", "")]
    public void Humanize_SpacesCamelCase(string text, string expected) => Assert.Equal(expected, PlotChronicle.Humanize(text));

    [Fact]
    public void Story_KeepsLettersAndAnswers_AndRoundTrips()
    {
        var plot = new Plot { Id = "nemesis-1" };
        plot.Tell(5, "A raider returns", "Kael is back.");
        plot.Answered(6, "Bounty posted: 300 silver paid.");
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        plot.Expose(saving);

        var loaded = new Plot();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal(2, loaded.Story.Count);
        Assert.Equal((5, "A raider returns", "Kael is back.", false), (loaded.Story[0].Day, loaded.Story[0].Label, loaded.Story[0].Text, loaded.Story[0].Answer));
        Assert.True(loaded.Story[1].Answer);
        Assert.Equal("Bounty posted: 300 silver paid.", loaded.Story[1].Text);
    }

    [Fact]
    public void Story_IsEmpty_ForASaveFromBeforeItWasKept()
    {
        var plot = new Plot { Story = null! };
        plot.Expose(new DictionaryArchive(ArchiveMode.Loading));
        Assert.NotNull(plot.Story);
        Assert.Empty(plot.Story);
    }

    [Fact]
    public void Story_DropsTheOldestPastTheCap()
    {
        var plot = new Plot();
        for (var i = 0; i < PlotChronicle.MaxEntries + 5; i++)
        {
            plot.Tell(i, $"Letter {i}", "");
        }

        Assert.Equal(PlotChronicle.MaxEntries, plot.Story.Count);
        Assert.Equal("Letter 5", plot.Story[0].Label);
    }
}
