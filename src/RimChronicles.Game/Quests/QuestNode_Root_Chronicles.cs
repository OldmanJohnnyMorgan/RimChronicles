using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>
/// Base for every Rim Chronicles quest (Stage 4). The plot puts the quest's name, description and parameters in the
/// slate; this root sets the name and description directly (RimWorld would otherwise build them from grammar rules our
/// defs don't have) and lets the template build its parts. The model never writes quest logic: it only words the
/// briefing, and rewards and threat come from the storyteller's points, never from the model.
/// </summary>
public abstract class QuestNode_Root_Chronicles : QuestNode
{
    public const string NameVar = "rcName";
    public const string DescriptionVar = "rcDescription";
    public const string PlotIdVar = "rcPlotId";

    /// <summary>Only a plot can start these quests: without its id in the slate (e.g. from the vanilla Dev Mode quest
    /// menus), the quest won't generate.</summary>
    protected override bool TestRunInt(Slate slate) => !string.IsNullOrEmpty(slate.Get<string>(PlotIdVar));

    protected override void RunInt()
    {
        var quest = QuestGen.quest;
        var slate = QuestGen.slate;
        slate.Set("resolvedQuestName", slate.Get(NameVar, "A story of the rim"));
        slate.Set("resolvedQuestDescription", slate.Get(DescriptionVar, ""));
        Build(quest, slate);
    }

    /// <summary>Adds the template's quest parts. Called once, while the quest is generated.</summary>
    protected abstract void Build(Quest quest, Slate slate);
}

/// <summary>A test quest for the framework (Dev Mode): accept it and it succeeds an hour later; ignore it and the offer
/// expires after a day. It has no rewards.</summary>
public sealed class QuestNode_Root_ChroniclesTest : QuestNode_Root_Chronicles
{
    protected override void Build(Quest quest, Slate slate) =>
        quest.Delay(GenDate.TicksPerHour, () => QuestGen_End.End(quest, QuestEndOutcome.Success));
}
