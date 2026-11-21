using RimChronicles.Core.Plots;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>Starts quests for plots and feeds their outcomes back (Stage 4 M2). Main thread only.</summary>
public static class QuestStarter
{
    /// <summary>Offers a quest for <paramref name="plot"/> from the quest script <paramref name="scriptDefName"/>. The
    /// quest's outcome comes back as the plot fact "quest.&lt;template&gt;". Returns the quest, or null if it couldn't
    /// be generated (the plot carries on without it).</summary>
    public static Quest? Offer(RimChroniclesGameComponent component, Plot plot, string template, string scriptDefName,
        string name, string description, Action<Slate>? parameters = null)
    {
        if (DefDatabase<QuestScriptDef>.GetNamedSilentFail(scriptDefName) is not { } script)
        {
            Log.Error($"[Rim Chronicles] No quest script {scriptDefName}.");
            return null;
        }

        var slate = new Slate();
        slate.Set(QuestNode_Root_Chronicles.NameVar, name);
        slate.Set(QuestNode_Root_Chronicles.DescriptionVar, description);
        slate.Set(QuestNode_Root_Chronicles.PlotIdVar, plot.Id);
        if (Find.AnyPlayerHomeMap is { } map)
        {
            slate.Set("map", map);
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
        }

        parameters?.Invoke(slate);
        Quest quest;
        try
        {
            quest = QuestUtility.GenerateQuestAndMakeAvailable(script, slate);
        }
        catch (Exception ex)
        {
            Log.Error($"[Rim Chronicles] Generating quest {scriptDefName} for {plot.Id} failed: {ex}");
            return null;
        }

        QuestUtility.SendLetterQuestAvailable(quest);
        component.Plots.LinkQuest(plot, quest.id, template, GenDate.DaysPassed);
        return quest;
    }

    /// <summary>Called daily: a linked quest that has ended reports its outcome to its plot.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var book = component.Plots;
        var today = GenDate.DaysPassed;
        foreach (var link in book.Quests.Where(l => l.Pending).ToList())
        {
            var quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == link.QuestId);
            var outcome = quest is null ? QuestOutcome.Other : Outcome(quest.State);
            if (outcome is null)
            {
                continue; // still offered, or under way
            }

            book.QuestEnded(link, outcome, today);
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] Quest {link.Template} for {link.PlotId}: {outcome}.");
            }
        }

        book.PruneQuests(today);
    }

    /// <summary>The plot's view of a quest state, or null while the quest is still open.</summary>
    public static string? Outcome(QuestState state) => state switch
    {
        QuestState.EndedSuccess => QuestOutcome.Success,
        QuestState.EndedFailed => QuestOutcome.Fail,
        QuestState.EndedOfferExpired => QuestOutcome.Ignored,
        QuestState.EndedUnknownOutcome or QuestState.EndedInvalid => QuestOutcome.Other,
        _ => null,
    };
}
