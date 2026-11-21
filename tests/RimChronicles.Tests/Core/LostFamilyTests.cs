using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;

namespace RimChronicles.Tests.Core;

public class LostFamilyTests
{
    private static readonly PlotTemplate Template = new() { Id = LostFamilyArc.TemplateId };

    private static (PlotBook Book, Plot Plot) NewPlot()
    {
        var book = new PlotBook();
        var plot = book.Start(Template, ProtagonistSlot.ForPawn("p1", "Anouk"), 10, 0, "kin found");
        plot.SetFact(LostFamilyArc.KinName, "Saskia");
        plot.SetFact(LostFamilyArc.Relation, "sister");
        plot.SetFact(LostFamilyArc.KinFaction, "the Ochre Hand");
        return (book, plot);
    }

    [Fact]
    public void Transitions_GoFromLettersToCaptivityToARescue()
    {
        var (book, plot) = NewPlot();
        Assert.Equal("Hook:LetterFromSaskia", Assert.Single(LostFamilyArc.Transitions(plot)).Id);

        book.Advance(plot, PlotBeat.Hook, 12, 5, "letter");
        plot.SetFact(LostFamilyArc.Letters, "1");
        Assert.Equal(["Complication:SaskiaTakenCaptive", "Hook:AnotherLetterFromSaskia", "Wait"], LostFamilyArc.Transitions(plot).Select(o => o.Id));
        plot.SetFact(LostFamilyArc.Letters, "2");
        Assert.DoesNotContain(LostFamilyArc.Transitions(plot), o => o.Id.StartsWith("Hook:", StringComparison.Ordinal));

        book.Advance(plot, PlotBeat.Complication, 20, 5, "taken");
        Assert.Equal(["Choice:RescueSaskia", "Wait"], LostFamilyArc.Transitions(plot).Select(o => o.Id));

        book.Advance(plot, PlotBeat.Choice, 26, 20, "rescue offered");
        Assert.Empty(LostFamilyArc.Transitions(plot)); // no quest yet
        var link = book.LinkQuest(plot, 9, QuestTemplates.Rescue, 26);
        Assert.Empty(LostFamilyArc.Transitions(plot)); // pending
        book.QuestEnded(link, QuestOutcome.Success, 30);
        Assert.Equal(nameof(LostFamilyStep.Reunited), Assert.Single(LostFamilyArc.Transitions(plot)).Step);
        book.QuestEnded(link, QuestOutcome.Ignored, 30);
        Assert.Equal(nameof(LostFamilyStep.Lost), Assert.Single(LostFamilyArc.Transitions(plot)).Step);
    }

    [Theory]
    [InlineData("Sibling", 0.8)]
    [InlineData("Parent", 0.8)]
    [InlineData("Cousin", 0.5)]
    public void Strength_FavoursCloseFamily(string relation, double strength) => Assert.Equal(strength, LostFamilyArc.Strength(relation));

    [Fact]
    public void Letters_TellTheStoryFromTheFacts()
    {
        var (_, plot) = NewPlot();
        plot.SetFact(LostFamilyArc.Captor, "the Stonecutter Raiders");
        var f = LostFamilyLetters.Facts.From(plot, "Wyrmfold");

        Assert.Contains("It is from Saskia, Anouk's sister, who lives among the Ochre Hand", LostFamilyLetters.Letter(f, true).Text);
        Assert.Contains("Saskia, Anouk's sister, is among them", LostFamilyLetters.Taken(f).Text);
        Assert.Equal("Rescue Saskia", LostFamilyLetters.RescueQuest(f).Name);
        Assert.Contains("is free, and home at Wyrmfold", LostFamilyLetters.Reunited(f).Text);
        Assert.Equal("the Stonecutter Raiders took captives from the Ochre Hand", LostFamilyLetters.TakenNews(f));
        Assert.Equal("Saskia, Anouk's sister, is still held by the Stonecutter Raiders", LostFamilyLetters.Unfinished(f));
    }

    [Fact]
    public void Letters_WithoutARelationNeedNoStrayCommas()
    {
        var f = LostFamilyLetters.Facts.From(new Dictionary<string, string> { ["kinName"] = "Saskia" }, "Anouk", "Wyrmfold");

        Assert.Contains("Saskia is among them", LostFamilyLetters.Taken(f).Text);
        Assert.DoesNotContain(", is", LostFamilyLetters.Reunited(f).Text);
    }

    [Theory]
    [InlineData("Letter")]
    [InlineData("AnotherLetter")]
    [InlineData("Taken")]
    [InlineData("Ending:Reunited")]
    [InlineData("Ending:Lost")]
    public void EveryStep_HasATemplateThatPassesThePlotLetterChecks(string step)
    {
        var c = new PlotLetterCase
        {
            Template = LostFamilyArc.TemplateId, Step = step, Protagonist = "Anouk",
            Facts = new Dictionary<string, string> { ["kinName"] = "Saskia", ["relation"] = "sister", ["kinFaction"] = "the Ochre Hand", ["captor"] = "the Stonecutter Raiders" },
        };
        var input = WorldSummarizer.ForPlotLetter(c, "Wyrmfold");
        var (_, text) = PlotLetterTask.Template(c, "Wyrmfold");

        var failures = PlotLetterTask.Checks(c, input).SelectMany(check => check.Check(text).Failures).ToList();

        Assert.True(failures.Count == 0, $"{step}: {text}\n{string.Join("\n", failures)}");
        Assert.Contains("Relative's relation to the protagonist: sister", input.Variables["facts"]);
        Assert.Contains("Lost Family", input.Variables["arc"]);
    }
}
