using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Plots;

/// <summary>How a plot's quest ended, as the plot sees it (design spec, Stage 4 "Outcomes").</summary>
public static class QuestOutcome
{
    public const string Pending = "";
    public const string Success = "Success";
    public const string Fail = "Fail";

    /// <summary>Offered but never accepted. Ignoring a quest is a valid choice: the plot carries on without the player.</summary>
    public const string Ignored = "Ignored";

    /// <summary>Ended some other way (cancelled, invalid).</summary>
    public const string Other = "Other";
}

/// <summary>A quest started for a plot, so its outcome can come back to the plot as a transition trigger.</summary>
public sealed class QuestLink : IArchivable
{
    /// <summary>The game's quest id.</summary>
    public int QuestId;

    public string PlotId = "";

    /// <summary>The quest template, e.g. "Hunt".</summary>
    public string Template = "";

    public int StartDay;

    public string Outcome = QuestOutcome.Pending;

    public bool Pending => Outcome == QuestOutcome.Pending;

    /// <summary>The plot fact that carries this quest's outcome: "quest.Hunt".</summary>
    public string FactKey => FactFor(Template);

    public static string FactFor(string template) => $"quest.{template}";

    public void Expose(IArchive archive)
    {
        archive.Value(ref QuestId, "questId");
        archive.Value(ref PlotId, "plotId", "");
        archive.Value(ref Template, "template", "");
        archive.Value(ref StartDay, "startDay");
        archive.Value(ref Outcome, "outcome", QuestOutcome.Pending);
        PlotId ??= "";
        Template ??= "";
        Outcome ??= QuestOutcome.Pending;
    }
}
