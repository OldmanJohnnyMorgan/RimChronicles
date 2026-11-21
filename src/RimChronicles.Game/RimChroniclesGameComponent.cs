using RimChronicles.Core.Engine;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
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
    private NewsLog _news = new();
    private NewsDesk _desk = new();
    private PlotBook _plots = new();
    private Pawn? _reporterPawn;

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

    /// <summary>Public world news collected during play (Stage 2 M2).</summary>
    public NewsLog News => _news;

    /// <summary>The bulletin schedule and archive (Stage 2 M3).</summary>
    public NewsDesk Desk => _desk;

    /// <summary>Every minor plot in this save (Stage 3).</summary>
    public PlotBook Plots => _plots;

    /// <summary>The reporter's pawn, a world pawn kept forever, or null until one is recruited (Stage 2 M4).</summary>
    public Pawn? ReporterPawn
    {
        get => _reporterPawn;
        set => _reporterPawn = value;
    }

    /// <summary>True while a bulletin is being written in the background. Not saved: after a reload, writing restarts.</summary>
    public bool WritingBulletin { get; set; }

    // Update runs every frame, paused or not, so replies arrive while the game is paused too.
    public override void GameComponentUpdate() =>
        _queue.Drain(ex => Log.Error($"[Rim Chronicles] Main-thread work failed: {ex}"));

    public override void ExposeData()
    {
        ScribeArchive.Instance.Deep(ref _spike, "spike");
        ScribeArchive.Instance.Deep(ref _news, "news");
        ScribeArchive.Instance.Deep(ref _desk, "newsDesk");
        ScribeArchive.Instance.Deep(ref _plots, "plots");
        Scribe_References.Look(ref _reporterPawn, "reporterPawn");
        _spike ??= new SpikeState();
        _news ??= new NewsLog();
        _desk ??= new NewsDesk();
        _plots ??= new PlotBook();
    }

    /// <summary>The last day the daily work ran for, or -1 until the first tick after a new game or a load. Not
    /// saved: the first tick after loading runs the daily work once, so anything that fell due while the game was
    /// saved (or during a clock jump) happens then.</summary>
    private int _lastDay = -1;

    /// <summary>Called by the daily patch every tick with the current day number.</summary>
    internal void CheckDay(int day)
    {
        if (day == _lastDay)
        {
            return;
        }

        var firstSinceLoad = _lastDay < 0;
        _lastDay = day;
        if (!firstSinceLoad)
        {
            _spike.DaysSeen++;
            _spike.AddLog($"day {day}: daily patch ran ({_spike.DaysSeen} days seen)");
        }

        Game.News.ReporterLife.Daily(this);
        Game.News.ReporterKeeper.EnsureReporter(this);
        Game.Plots.PlotDriver.Daily(this);
        Game.News.NewsDriver.Tick(this);
    }
}
