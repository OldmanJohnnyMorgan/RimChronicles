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
}
