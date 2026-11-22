using LudeonTK;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>Dev Mode actions for the quest framework.</summary>
public static class QuestDevActions
{
    /// <summary>Offers the framework's test quest for the oldest active plot: accept it and it succeeds an hour later;
    /// ignore it and it expires after a day. Either way, the outcome shows up in the plot's facts the next day.</summary>
    [DebugAction("Rim Chronicles", "Quest: test (oldest plot)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestQuest()
    {
        if (RimChroniclesGameComponent.Current is not { } component || component.Plots.Active.OrderBy(p => p.StartDay).FirstOrDefault() is not { } plot)
        {
            Messages.Message("Rim Chronicles: start a plot first (Nemesis: force seed).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        QuestStarter.Offer(component, plot, "Test", "RimChronicles_Test", $"Test for {plot.Id}",
            "A test quest from Rim Chronicles. Accept it, and it succeeds an hour later. Ignore it, and the offer expires after a day.");
    }

    [DebugAction("Rim Chronicles", "Quest: show links", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowLinks()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var links = component.Plots.Quests;
        var text = links.Count == 0
            ? "No quests linked to plots."
            : string.Join("\n", links.Select(l =>
            {
                var quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == l.QuestId);
                return $"Day {l.StartDay}: {l.Template} for {l.PlotId}, quest {l.QuestId} ({quest?.State.ToString() ?? "gone"}): "
                    + (l.Pending ? "pending" : l.Outcome);
            }));
        LetterSink.Instance.Send($"Rim Chronicles: quests ({links.Count})", text);
    }

    private static Core.Plots.Plot? OldestPlot(out RimChroniclesGameComponent? component)
    {
        component = RimChroniclesGameComponent.Current;
        var plot = component?.Plots.Active.OrderBy(p => p.StartDay).FirstOrDefault();
        if (plot is null)
        {
            Messages.Message("Rim Chronicles: start a plot first (Nemesis: force seed).", MessageTypeDefOf.RejectInput, historical: false);
        }

        return plot;
    }

    private static Pawn? MakePawn(Func<Faction, bool> factionFilter)
    {
        var faction = Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.RandomPawnKind() is not null && factionFilter(f))
            .RandomElementWithFallback();
        if (faction?.RandomPawnKind() is not { } kind)
        {
            return null;
        }

        var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
        Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
        return pawn;
    }

    /// <summary>A Hunt quest for the oldest plot, against a new raider from a hostile faction.</summary>
    [DebugAction("Rim Chronicles", "Quest: Hunt (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestHunt()
    {
        if (OldestPlot(out var component) is not { } plot || MakePawn(f => f.HostileTo(Faction.OfPlayer)) is not { } target)
        {
            return;
        }

        QuestOffers.Hunt(component!, plot, target, $"Hunt {target.LabelShort}",
            $"A test Hunt quest: {target.LabelShort} of {target.Faction?.Name} is at a camp nearby. Kill or capture them.");
    }

    /// <summary>A Rescue quest for the oldest plot: a member of a friendly faction held by a hostile one.</summary>
    [DebugAction("Rim Chronicles", "Quest: Rescue (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestRescue()
    {
        if (OldestPlot(out var component) is not { } plot || MakePawn(f => !f.HostileTo(Faction.OfPlayer)) is not { } captive)
        {
            return;
        }

        var captor = Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)).RandomElementWithFallback();
        if (captor is null)
        {
            return;
        }

        QuestOffers.Rescue(component!, plot, captive, captor, $"Rescue {captive.LabelShort}",
            $"A test Rescue quest: {captor.Name} hold {captive.LabelShort} at a camp nearby. Free them, and they will join the colony.");
    }

    /// <summary>A Deliver quest for the oldest plot: 50 steel to the nearest friendly settlement.</summary>
    [DebugAction("Rim Chronicles", "Quest: Deliver (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestDeliver()
    {
        if (OldestPlot(out var component) is not { } plot)
        {
            return;
        }

        if (QuestOffers.NearestFriendlySettlement() is not { } settlement)
        {
            Messages.Message("Rim Chronicles: no friendly settlement can take a delivery.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        QuestOffers.Deliver(component!, plot, settlement, ThingDefOf.Steel, 50, $"Deliver steel to {settlement.Label}",
            $"A test Deliver quest: {settlement.Label} needs 50 steel, brought by caravan within {Core.Plots.QuestTemplates.DeliverDays} days.");
    }

    /// <summary>A Choice letter for the oldest plot, writing its answer to the fact choice.Test.</summary>
    [DebugAction("Rim Chronicles", "Quest: Choice letter (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestChoice()
    {
        if (OldestPlot(out _) is not { } plot)
        {
            return;
        }

        ChoiceLetter_Plot.Send(plot, "choice.Test", "A test choice",
            "A test of the Choice letter template. Each option writes its answer to the plot fact choice.Test.",
            [new PlotChoice("Pay 10 silver", "paid", Silver: 10), new PlotChoice("Decline", "declined")], timeoutDays: 3);
    }

    /// <summary>An Escort quest for the oldest plot: one member of a friendly faction to its nearest other settlement.</summary>
    [DebugAction("Rim Chronicles", "Quest: Escort (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestEscort()
    {
        if (OldestPlot(out var component) is not { } plot)
        {
            return;
        }

        var destination = QuestOffers.NearestSettlement(s => !s.Faction.HostileTo(Faction.OfPlayer) && s.Faction.def.humanlikeFaction);
        if (destination is null)
        {
            Messages.Message("Rim Chronicles: no friendly settlement to escort anyone to.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        QuestOffers.Escort(component!, plot, destination.Faction, destination, 1, $"Escort to {destination.Label}",
            $"A test Escort quest: a traveller of {destination.Faction.Name} needs to reach {destination.Label}. Take them there by caravan within "
            + $"{Core.Plots.QuestTemplates.EscortDays} days.");
    }

    /// <summary>A Defend ally quest for the oldest plot: a hostile faction's camp beside the nearest friendly settlement.</summary>
    [DebugAction("Rim Chronicles", "Quest: Defend ally (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestDefendAlly()
    {
        if (OldestPlot(out var component) is not { } plot)
        {
            return;
        }

        var ally = QuestOffers.NearestSettlement(s => !s.Faction.HostileTo(Faction.OfPlayer) && s.Faction.def.humanlikeFaction);
        var attacker = Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && f != ally?.Faction)
            .RandomElementWithFallback();
        if (ally is null || attacker is null)
        {
            Messages.Message("Rim Chronicles: needs a friendly settlement and a hostile faction.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        QuestOffers.DefendAlly(component!, plot, ally, attacker, $"Defend {ally.Label}",
            $"A test Defend ally quest: {attacker.Name} have camped outside {ally.Label}. Clear their camp.");
    }

    /// <summary>A Sabotage quest for the oldest plot: a comms console in the nearest hostile settlement.</summary>
    [DebugAction("Rim Chronicles", "Quest: Sabotage (test)", allowedGameStates = AllowedGameStates.Playing)]
    public static void TestSabotage()
    {
        if (OldestPlot(out var component) is not { } plot)
        {
            return;
        }

        if (QuestOffers.NearestSettlement(s => s.Faction.HostileTo(Faction.OfPlayer) && s.Faction.def.humanlikeFaction) is not { } target)
        {
            Messages.Message("Rim Chronicles: no hostile settlement to sabotage.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        QuestOffers.Sabotage(component!, plot, target, null, $"Sabotage {target.Label}",
            $"A test Sabotage quest: destroy the comms console inside {target.Label}, held by {target.Faction.Name}, within "
            + $"{Core.Plots.QuestTemplates.SabotageDays} days.");
    }
}
