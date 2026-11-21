using System.Collections.Concurrent;

namespace RimChronicles.Core.Engine;

/// <summary>A thread-safe queue of work for the main thread. Any thread may <see cref="Post"/>; only the
/// main thread calls <see cref="Drain"/> (the Game layer does it once per frame).</summary>
public sealed class MainThreadQueue : IMainThread
{
    private readonly ConcurrentQueue<Action> _work = new();

    /// <summary>Items waiting to run.</summary>
    public int Count => _work.Count;

    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _work.Enqueue(work);
    }

    /// <summary>Runs up to <paramref name="max"/> queued items in the order they were posted. An item that
    /// throws is reported to <paramref name="onError"/> and does not stop the rest.</summary>
    /// <returns>How many items ran.</returns>
    public int Drain(Action<Exception> onError, int max = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(onError);
        var ran = 0;
        while (ran < max && _work.TryDequeue(out var work))
        {
            ran++;
            try
            {
                work();
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }

        return ran;
    }
}
