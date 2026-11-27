using RimChronicles.Core.Crisis;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;

namespace RimChronicles.Tests.Core;

public class KeptPawnsTests
{
    private static PlotBook BookWith(params Plot[] plots)
    {
        var book = new PlotBook();
        book.Active.AddRange(plots);
        return book;
    }

    private static Plot Plot(string protagonist, string coLead, params (string Key, string Value)[] facts)
    {
        var plot = new Plot { Protagonist = new ProtagonistSlot { PawnId = protagonist, CoLeadId = coLead } };
        plot.Facts.AddRange(facts.Select(f => new PlotFact { Key = f.Key, Value = f.Value }));
        return plot;
    }

    private static GrudgeLedger Grudges(params (string Killer, string Victim)[] grudges)
    {
        var ledger = new GrudgeLedger();
        ledger.Grudges.AddRange(grudges.Select(g => new Grudge { KillerId = g.Killer, VictimId = g.Victim }));
        return ledger;
    }

    private static CrisisState Leader(string id, string fate) => new() { StellarchPawnId = id, StellarchFate = fate };

    [Fact]
    public void Referenced_covers_grudges_plots_and_both_finale_leaders()
    {
        var ids = KeptPawns.Referenced(
            BookWith(Plot("hero", "friend", ("nemesis", "raider"))),
            Grudges(("killer", "victim")),
            Leader("stellarch", StellarchFate.Alive), Leader("firstVoice", StellarchFate.Dead));

        Assert.Equal(["firstVoice", "friend", "hero", "killer", "raider", "stellarch", "victim"], ids.Order());
    }

    [Fact]
    public void KeepAlive_keeps_killers_plot_pawns_and_leaders_still_in_the_war()
    {
        var ids = KeptPawns.KeepAlive(
            BookWith(Plot("hero", "", ("kin", "lost-sister"))),
            Grudges(("killer", "victim")),
            Leader("stellarch", StellarchFate.Alive), Leader("firstVoice", StellarchFate.Captured), new CrisisState());

        Assert.Equal(["killer", "lost-sister", "stellarch"], ids.Order());
    }

    [Fact]
    public void A_leader_not_yet_met_is_not_kept()
    {
        // The id is set when the leader is made, with fate Alive; before that both are empty.
        Assert.Empty(KeptPawns.KeepAlive(new PlotBook(), new GrudgeLedger(), new CrisisState(), new CrisisState()));
        Assert.Empty(KeptPawns.Referenced(new PlotBook(), new GrudgeLedger(), new CrisisState()));
    }

    [Fact]
    public void Ended_plots_and_empty_ids_are_left_out()
    {
        var book = BookWith(Plot("", ""));
        book.Finished.Add(Plot("old-hero", "", ("nemesis", "old-raider")));

        Assert.Empty(KeptPawns.Referenced(book, new GrudgeLedger()));
    }
}
