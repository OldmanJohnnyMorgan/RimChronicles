using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class NemesisSeedTests
{
    private static readonly PlotTemplate Template = new() { Id = NemesisPlot.TemplateId };

    private static Grudge G(string killer, string victim, string kind = GrudgeKind.Killed, int day = 0, bool escaped = true) => new()
    {
        KillerId = killer, KillerName = killer + "Name", KillerFaction = "Venom Gang",
        VictimId = victim, VictimName = victim + "Name", Kind = kind, Day = day, Escaped = escaped,
        Part = kind == GrudgeKind.Maimed ? "arm" : "",
    };

    [Fact]
    public void Record_KeepsOneGrudgePerRaiderVictimAndKind()
    {
        var ledger = new GrudgeLedger();

        Assert.True(ledger.Record(G("vex", "tomas")));
        Assert.False(ledger.Record(G("vex", "tomas")));
        Assert.True(ledger.Record(G("vex", "tomas", GrudgeKind.Maimed)));
        Assert.Equal(["vex"], ledger.TrackedKillers);
    }

    [Fact]
    public void Strongest_NeedsAnEscapePrefersKillingsAndFresherOnes()
    {
        var ledger = new GrudgeLedger();
        ledger.Record(G("a", "x", GrudgeKind.Killed, day: 0, escaped: false));
        Assert.Null(ledger.Strongest(1));

        ledger.Record(G("b", "y", GrudgeKind.Maimed, day: 5));
        ledger.Record(G("c", "z", GrudgeKind.Killed, day: 2));
        Assert.Equal("c", ledger.Strongest(6)!.KillerId);

        ledger.MarkEscaped("a");
        Assert.Equal("c", ledger.Strongest(6)!.KillerId); // both killings; day 2 is fresher than day 0
    }

    [Fact]
    public void Strength_FadesWithTime() =>
        Assert.True(GrudgeLedger.Strength(G("a", "x"), today: 40) < GrudgeLedger.Strength(G("a", "x"), today: 1));

    [Fact]
    public void Settle_DropsUnusedGrudgesButKeepsOnesInAPlot()
    {
        var ledger = new GrudgeLedger();
        ledger.Record(G("vex", "tomas"));
        ledger.Record(G("vex", "ana", GrudgeKind.Maimed));
        ledger.Grudges[0].PlotId = "nemesis-1";

        Assert.Equal(1, ledger.Settle("vex"));
        Assert.Equal("nemesis-1", Assert.Single(ledger.Grudges).PlotId);
    }

    [Fact]
    public void Expire_DropsOldUnusedGrudges()
    {
        var ledger = new GrudgeLedger();
        ledger.Record(G("a", "x", day: 0));
        ledger.Record(G("b", "y", day: 50));

        Assert.Equal(1, ledger.Expire(today: 70));
        Assert.Equal("b", Assert.Single(ledger.Grudges).KillerId);
    }

    [Fact]
    public void Strongest_SkipsARaiderWhoIsAlreadySomeonesNemesis()
    {
        var ledger = new GrudgeLedger();
        ledger.Record(G("vex", "tomas"));
        ledger.Record(G("vex", "ana", GrudgeKind.Maimed));
        ledger.Grudges[0].PlotId = "nemesis-1";

        Assert.Null(ledger.Strongest(1));
    }

    [Fact]
    public void Protagonist_IsTheMaimedVictimOrTheKilledVictimsHeir()
    {
        var kin = new HeirCandidate("mara", "Mara", IsBloodKin: true, IsPartner: false, IsBondedAnimal: false, Opinion: 20);
        var friend = new HeirCandidate("jun", "Jun", false, false, false, 80);

        var maimed = NemesisPlot.Protagonist(G("vex", "ana", GrudgeKind.Maimed), [kin, friend], "C");
        var killed = NemesisPlot.Protagonist(G("vex", "tomas"), [friend, kin], "C");
        var alone = NemesisPlot.Protagonist(G("vex", "tomas"), [], "New Arrivals");

        Assert.Equal(("ana", "the victim"), (maimed.Id, maimed.Rule));
        Assert.Equal(("mara", "blood kin"), (killed.Id, killed.Rule));
        Assert.Equal(ProtagonistKind.Colony, alone.Kind);
    }

    [Fact]
    public void Seed_StartsThePlotWithItsFactsAndUsesTheGrudge()
    {
        var book = new PlotBook();
        var grudge = G("vex", "tomas");
        var heir = new HeirChoice(ProtagonistKind.Pawn, "mara", "Mara", "spouse or lover");

        var plot = NemesisPlot.Seed(book, Template, grudge, heir, "husband", today: 12, hookDelayDays: 3);

        Assert.Equal(("nemesis-1", "mara", 15), (plot.Id, plot.Protagonist.PawnId, plot.NextBeatDay));
        Assert.Equal("vex", plot.Fact(NemesisPlot.NemesisId));
        Assert.Equal("husband", plot.Fact(NemesisPlot.Relation));
        Assert.Equal(GrudgeKind.Killed, plot.Fact(NemesisPlot.Grievance));
        Assert.Equal("nemesis-1", grudge.PlotId);
        Assert.Equal("Day 12: started: vexName of Venom Gang killed Mara's husband tomasName and escaped", plot.Log[0]);
    }

    [Fact]
    public void Describe_PhrasesAMaiming() =>
        Assert.Equal("Vex of Venom Gang cost Ana their arm and escaped",
            NemesisPlot.Describe(new Grudge { KillerName = "Vex", KillerFaction = "Venom Gang", VictimName = "Ana", Kind = GrudgeKind.Maimed, Part = "arm" }, "Ana", ""));

    [Fact]
    public void Ledger_RoundTripsAndLoadsEmptyFromAnOldSave()
    {
        var ledger = new GrudgeLedger();
        ledger.Record(G("vex", "ana", GrudgeKind.Maimed, day: 7));
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        ledger.Expose(saving);

        var loaded = new GrudgeLedger();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        var old = new GrudgeLedger();
        old.Expose(new DictionaryArchive(ArchiveMode.Loading));

        var g = Assert.Single(loaded.Grudges);
        Assert.Equal(("vex", GrudgeKind.Maimed, "arm", 7, true), (g.KillerId, g.Kind, g.Part, g.Day, g.Escaped));
        Assert.Empty(old.Grudges);
    }
}
