using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Core.Summaries;
using RimChronicles.Llm.Validation;

namespace RimChronicles.Tests.Core;

public class SuccessionTests
{
    private static readonly PlotTemplate Nemesis = new() { Id = NemesisPlot.TemplateId, InheritanceMutation = "the hunt becomes revenge, carried by the heir" };

    private static Plot NemesisPlotFor(string protagonist)
    {
        var plot = new PlotBook().Start(Nemesis, ProtagonistSlot.ForPawn("p1", protagonist), 0, 0, "seed");
        plot.SetFact(NemesisPlot.NemesisName, "Vex");
        plot.SetFact(NemesisPlot.VictimName, "Dorin");
        plot.SetFact(NemesisPlot.VictimPronoun, "he");
        plot.SetFact(NemesisPlot.Relation, "husband");
        plot.SetFact(NemesisPlot.Grievance, GrudgeKind.Killed);
        return plot;
    }

    [Fact]
    public void KilledByTheNemesis_MakesTheFallenProtagonistTheNewVictim()
    {
        var plot = NemesisPlotFor("Mara");
        var heir = new HeirChoice(ProtagonistKind.Pawn, "p6", "Sela", "blood kin");

        var line = Succession.Pass(plot, Nemesis, heir, ProtagonistLoss.KilledByNemesis,
            new Succession.Context("Mara", "she", "mother", "father", "she"), today: 40);

        Assert.Equal(("p6", "Sela", ProtagonistKind.Pawn), (plot.Protagonist.PawnId, plot.Protagonist.Name, plot.Protagonist.Kind));
        Assert.Equal(["Mara (killed by the nemesis)"], plot.Protagonist.History);
        Assert.Equal(("Mara", "she", "mother", GrudgeKind.Killed),
            (plot.Fact(NemesisPlot.VictimName), plot.Fact(NemesisPlot.VictimPronoun), plot.Fact(NemesisPlot.Relation), plot.Fact(NemesisPlot.Grievance)));
        Assert.Equal("she", plot.Fact(NemesisPlot.ProtagonistPronoun));
        Assert.Contains("the hunt becomes revenge", line);
        Assert.StartsWith("Day 40: inherited: Mara killed by the nemesis; the story passes to Sela (blood kin)", plot.Log[^1]);

        var letter = NemesisLetters.Raid(NemesisLetters.Facts.From(plot, "Hearthstead")).Text;
        Assert.Contains("killed Sela's mother Mara", letter);
    }

    [Fact]
    public void OtherLosses_KeepTheVictimAndRetellTheRelationFromTheHeirsSide()
    {
        var plot = NemesisPlotFor("Mara");
        var heir = new HeirChoice(ProtagonistKind.Pawn, "p6", "Sela", "blood kin");

        Succession.Pass(plot, Nemesis, heir, ProtagonistLoss.Taken, new Succession.Context("Mara", "she", "mother", "father", "she"), 40);

        Assert.Equal(("Dorin", "father"), (plot.Fact(NemesisPlot.VictimName), plot.Fact(NemesisPlot.Relation)));
        Assert.Contains("killed Sela's father Dorin", NemesisLetters.Raid(NemesisLetters.Facts.From(plot, "C")).Text);
    }

    [Fact]
    public void TheColonyAsHeir_SpeaksAsThey()
    {
        var plot = NemesisPlotFor("Mara");

        Succession.Pass(plot, Nemesis, new HeirChoice(ProtagonistKind.Colony, "colony", "Hearthstead", "the colony"), ProtagonistLoss.Died,
            new Succession.Context("Mara", "she", "", "", "she"), 40);

        Assert.Equal(("they", ProtagonistKind.Colony), (plot.Fact(NemesisPlot.ProtagonistPronoun), plot.Protagonist.Kind));
    }

    [Theory]
    [InlineData(ProtagonistLoss.Died, ProtagonistKind.Pawn, "Mara is gone.", "who was Mara's daughter")]
    [InlineData(ProtagonistLoss.Taken, ProtagonistKind.Pawn, "Mara was taken", "who was Mara's daughter")]
    [InlineData(ProtagonistLoss.Left, ProtagonistKind.Colony, "Mara has left the colony.", "the whole colony takes it up")]
    [InlineData(ProtagonistLoss.Died, ProtagonistKind.Animal, "Mara is gone.", "the animal bonded to Mara")]
    public void Templates_TellEachLossAndHeir(ProtagonistLoss loss, ProtagonistKind kind, string lost, string who)
    {
        var (label, text) = HandoffTask.Template("Mara", kind == ProtagonistKind.Colony ? "Hearthstead" : "Sela", "daughter", loss,
            "Vex, who killed Dorin, is still out there", kind);

        Assert.Contains("takes up the cause", label);
        Assert.Contains(lost, text);
        Assert.Contains(who, text);
    }

    [Fact]
    public void Template_PassesTheHandoffLength()
    {
        var (_, text) = HandoffTask.Template("Mara", "Sela", "daughter", ProtagonistLoss.Died, "Vex, who killed Dorin, is still out there", ProtagonistKind.Pawn);
        var (min, max) = WorldSummarizer.HandoffWordRange;

        Assert.InRange(WordCountCheck.Count(text), min, max);
    }
}
