using RimChronicles.Core.Engine;
using RimChronicles.Core.Spike;
using RimChronicles.Game.Engine;
using Verse;

namespace RimChronicles.Game;

/// <summary>Per-game state and the main-thread pump. RimWorld creates one per game (new or loaded) through the
/// <c>(Game)</c> constructor.</summary>
public sealed class RimChroniclesGameComponent : GameComponent
{
    /// <summary>The component of the game now running, or null between games.</summary>
    public static RimChroniclesGameComponent? Current { get; private set; }

    private readonly MainThreadQueue _queue = new();
    private readonly CancellationTokenSource _cancel = new();

    private SpikeState _spike = new();

    public RimChroniclesGameComponent(Verse.Game game)
    {
        Current?._cancel.Cancel();
        Current = this;
    }

    /// <summary>Hand results here from background tasks; they run on the main thread next frame.</summary>
    public IMainThread MainThread => _queue;

    /// <summary>Cancelled when this game ends, so background work for an old game stops.</summary>
    public CancellationToken GameToken => _cancel.Token;

    public SpikeState Spike => _spike;

    // Update runs every frame, paused or not, so replies arrive while the game is paused too.
    public override void GameComponentUpdate() =>
        _queue.Drain(ex => Log.Error($"[Rim Chronicles] Main-thread work failed: {ex}"));

    public override void ExposeData()
    {
        ScribeArchive.Instance.Deep(ref _spike, "spike");
        _spike ??= new SpikeState();
    }

    /// <summary>Called by the daily patch once per in-game day (spike test 2).</summary>
    internal void OnNewDay(int day)
    {
        _spike.DaysSeen++;
        _spike.AddLog($"day {day}: daily patch ran ({_spike.DaysSeen} days seen)");
        Log.Message($"[Rim Chronicles] Day {day}: daily patch ran; {_spike.DaysSeen} days seen in this save.");
    }
}
