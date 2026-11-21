using RimChronicles.Core.Engine;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class PlotEngineTests
{
    private static readonly PlotTemplate Nemesis = new() { Id = "Nemesis", InheritanceMutation = "the hunt becomes revenge" };
    private static readonly PlotTemplate SilentPress = new() { Id = "SilentPress", CountsTowardCap = false };
    private static readonly Dictionary<string, PlotTemplate> Templates = new() { ["Nemesis"] = Nemesis, ["SilentPress"] = SilentPress };

    private static PlotTemplate? Lookup(string id) => Templates.GetValueOrDefault(id);

    private sealed class ClosedGate(string reason) : IPacingGate
    {
        public string? BlockReason() => reason;
    }

    [Fact]
    public void Start_GivesEachPlotAnIdAndAHookDay()
    {
        var book = new PlotBook();

        var plot = book.Start(Nemesis, ProtagonistSlot.ForPawn("Human1", "Mara"), today: 10, hookDelayDays: 3, why: "Vex escaped");

        Assert.Equal("nemesis-1", plot.Id);
        Assert.Equal((PlotBeat.Hook, -1, 13), (plot.Beat, plot.BeatDay, plot.NextBeatDay));
        Assert.Equal(["Day 10: started: Vex escaped"], plot.Log);
        Assert.Equal(10, book.LastStartDay);
    }

    [Fact]
    public void CannotStart_EnforcesTheCapAndTheGapBetweenNewPlots()
    {
        var book = new PlotBook();
        Assert.Null(book.CannotStart(Nemesis, 0, Lookup));

        book.Start(Nemesis, ProtagonistSlot.ForPawn("a", "A"), 0, 1, "x");
        Assert.Contains("next may start on day 15", book.CannotStart(Nemesis, 14, Lookup));
        Assert.Null(book.CannotStart(Nemesis, 15, Lookup));

        book.Start(Nemesis, ProtagonistSlot.ForPawn("b", "B"), 15, 1, "x");
        book.Start(Nemesis, ProtagonistSlot.ForPawn("c", "C"), 30, 1, "x");
        Assert.Contains("3 plots", book.CannotStart(Nemesis, 100, Lookup));
    }

    [Fact]
    public void SilentPress_IgnoresTheCapButRunsOnlyOnce()
    {
        var book = new PlotBook();
        book.Start(Nemesis, ProtagonistSlot.ForPawn("a", "A"), 0, 1, "x");
        book.Start(Nemesis, ProtagonistSlot.ForPawn("b", "B"), 15, 1, "x");
        book.Start(Nemesis, ProtagonistSlot.ForPawn("c", "C"), 30, 1, "x");

        Assert.Null(book.CannotStart(SilentPress, 31, Lookup));
        book.Start(SilentPress, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = "colony", Name = "New Arrivals" }, 31, 1, "reporter killed");

        Assert.Equal(30, book.LastStartDay);
        Assert.Equal(3, book.CountingTowardCap(Lookup));
        Assert.Contains("already running", book.CannotStart(SilentPress, 40, Lookup));
    }

    [Fact]
    public void DueToday_WaitsForTheBeatDayTheGateAndBeatSpacing()
    {
        var book = new PlotBook();
        var plot = book.Start(Nemesis, ProtagonistSlot.ForPawn("a", "A"), 0, hookDelayDays: 5, why: "x");

        Assert.Empty(book.DueToday(4, OpenGate.Instance, out _));
        Assert.Empty(book.DueToday(5, new ClosedGate("raid on the map"), out var why));
        Assert.Equal("raid on the map", why);
        Assert.Same(plot, Assert.Single(book.DueToday(5, OpenGate.Instance, out _)));

        book.Advance(plot, PlotBeat.Hook, 5, gapDays: 7, why: "sighting letter");
        var other = book.Start(Nemesis, ProtagonistSlot.ForPawn("b", "B"), 20, 0, "x");
        Assert.Empty(book.DueToday(6, OpenGate.Instance, out _)); // nothing due yet
        book.LastBeatDay = 20;
        Assert.Empty(book.DueToday(21, OpenGate.Instance, out var spacing));
        Assert.Contains("day 20", spacing);
        Assert.Equal([plot, other], book.DueToday(22, OpenGate.Instance, out _));
    }

    [Fact]
    public void Advance_AndEnd_MoveThePlotAlongAndLogIt()
    {
        var book = new PlotBook();
        var plot = book.Start(Nemesis, ProtagonistSlot.ForPawn("a", "A"), 0, 0, "x");

        book.Advance(plot, PlotBeat.Complication, 3, 9, "Vex raided");
        Assert.Equal((PlotBeat.Complication, 3, 12, 3), (plot.Beat, plot.BeatDay, plot.NextBeatDay, book.LastBeatDay));

        book.End(plot, 20, "nemesis killed");
        Assert.Empty(book.Active);
        Assert.Same(plot, Assert.Single(book.Finished));
        Assert.Equal("Day 20: ended: nemesis killed", plot.Log[^1]);
        Assert.Same(plot, book.Find("nemesis-1"));
    }

    [Fact]
    public void Facts_AreSetAndReplaced()
    {
        var plot = new Plot();
        plot.SetFact("nemesis", "Human7");
        plot.SetFact("nemesis", "Human8");

        Assert.Equal("Human8", plot.Fact("nemesis"));
        Assert.Null(plot.Fact("missing"));
    }

    [Fact]
    public void PlotBook_RoundTripsAndLoadsEmptyFromAnOldSave()
    {
        var book = new PlotBook();
        var plot = book.Start(Nemesis, ProtagonistSlot.ForPawn("Human1", "Mara"), 4, 2, "Vex escaped");
        plot.SetFact("nemesis", "Human7");
        plot.PublicHint = "Vex was seen at a pirate outpost";
        book.Advance(plot, PlotBeat.Hook, 6, 8, "letter");
        Inheritance.PassTo(plot.Protagonist, new HeirChoice(ProtagonistKind.Pawn, "Human2", "Tomas", "blood kin"), "died");

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        book.Expose(saving);
        var loaded = new PlotBook();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        var old = new PlotBook();
        old.Expose(new DictionaryArchive(ArchiveMode.Loading));

        var p = Assert.Single(loaded.Active);
        Assert.Equal(("nemesis-1", PlotBeat.Hook, 6, 14), (p.Id, p.Beat, p.BeatDay, p.NextBeatDay));
        Assert.Equal(("Human2", "Tomas"), (p.Protagonist.PawnId, p.Protagonist.Name));
        Assert.Equal(["Mara (died)"], p.Protagonist.History);
        Assert.Equal("Human7", p.Fact("nemesis"));
        Assert.Equal(2, loaded.NextId);
        Assert.Equal(6, loaded.LastBeatDay);
        Assert.Empty(old.Active);
        Assert.Equal(1, old.NextId);
    }

    [Fact]
    public void ToInfo_GivesTheSnapshotView()
    {
        var plot = new PlotBook().Start(Nemesis, ProtagonistSlot.ForPawn("Human1", "Mara"), 0, 0, "Vex escaped");
        plot.PublicHint = "Vex was seen near the gorge";

        var info = plot.ToInfo();

        Assert.Equal(("nemesis-1", "Nemesis", "Hook", "Human1", "Vex was seen near the gorge"), (info.Id, info.Template, info.Beat, info.Protagonist, info.PublicHint));
        Assert.Contains("Vex escaped", info.Summary);
    }

    private static HeirCandidate H(string id, bool kin = false, bool partner = false, int opinion = 0) => new(id, id, kin, partner, false, opinion);

    private static HeirCandidate Animal(string id) => new(id, id, false, false, true, 0);

    [Fact]
    public void Inheritance_FollowsTheSpecOrder()
    {
        Assert.Equal("kin", Inheritance.PickHeir([H("friend", opinion: 90), H("lover", partner: true), H("kin", kin: true, opinion: -10)], "C").Id);
        Assert.Equal("lover", Inheritance.PickHeir([H("friend", opinion: 90), H("lover", partner: true), Animal("dog")], "C").Id);

        var animal = Inheritance.PickHeir([H("friend", opinion: 90), H("rival", opinion: -50), Animal("dog")], "C");
        Assert.Equal((ProtagonistKind.Animal, "dog", "friend", "bonded animal"), (animal.Kind, animal.Id, animal.CoLeadId, animal.Rule));

        Assert.Equal("friend", Inheritance.PickHeir([H("rival", opinion: -50), H("friend", opinion: 90)], "C").Id);

        var colony = Inheritance.PickHeir([Animal("dog")], "New Arrivals");
        Assert.Equal((ProtagonistKind.Colony, "colony", "New Arrivals"), (colony.Kind, colony.Id, colony.Name));
        Assert.Equal(ProtagonistKind.Colony, Inheritance.PickHeir([], "C").Kind);
    }

    [Fact]
    public void Inheritance_BreaksTiesByOpinionThenId()
    {
        Assert.Equal("b", Inheritance.PickHeir([H("a", kin: true, opinion: 10), H("b", kin: true, opinion: 40)], "C").Id);
        Assert.Equal("a", Inheritance.PickHeir([H("b", opinion: 5), H("a", opinion: 5)], "C").Id);
    }

    [Fact]
    public void WithProtagonist_FindsLeadsAndCoLeads()
    {
        var book = new PlotBook();
        var plot = book.Start(Nemesis, new ProtagonistSlot { Kind = ProtagonistKind.Animal, PawnId = "dog", CoLeadId = "Human3" }, 0, 0, "x");

        Assert.Same(plot, Assert.Single(book.WithProtagonist("Human3")));
        Assert.Same(plot, Assert.Single(book.WithProtagonist("dog")));
        Assert.Empty(book.WithProtagonist("Human9"));
    }
}
