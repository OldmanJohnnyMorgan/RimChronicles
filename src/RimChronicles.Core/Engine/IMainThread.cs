namespace RimChronicles.Core.Engine;

/// <summary>Hands work to the game's main thread. Background tasks (LLM calls) post their results here;
/// they never touch game state themselves.</summary>
public interface IMainThread
{
    /// <summary>Queues <paramref name="work"/> to run on the main thread at the next drain. Safe from any thread.</summary>
    void Post(Action work);
}
