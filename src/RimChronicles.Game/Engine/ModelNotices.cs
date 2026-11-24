using RimChronicles.Llm.Tasks;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>Tells the player, once per outage, that a model isn't answering and the mod is using its written text
/// (see <see cref="ModelHealth"/>). Without it the only sign was a log warning they never see.</summary>
public static class ModelNotices
{
    private static readonly ModelHealth Health = new();

    /// <summary>Call from a background task with a finished task's attempts; the check and any letter run on the main thread.</summary>
    public static void Post(Core.Engine.IMainThread mainThread, IReadOnlyList<TaskAttempt> attempts, string role) =>
        mainThread.Post(() =>
        {
            if (Health.Observe(attempts, role) is { } notice)
            {
                Find.LetterStack.ReceiveLetter("Rim Chronicles: model not answering", notice, LetterDefOf.NeutralEvent);
            }
        });
}
