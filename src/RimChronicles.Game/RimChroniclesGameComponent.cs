using RimChronicles.Core.Crisis;
using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Engine;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
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

    private NewsLog _news = new();
    private NewsDesk _desk = new();
    private PlotBook _plots = new();
    private GrudgeLedger _grudges = new();
    private PowerSaga _empire = new();
    private CrisisState _crisis = new();
    private List<Pawn> _tracked = [];
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


    /// <summary>Public world news collected during play (Stage 2 M2).</summary>
    public NewsLog News => _news;

    /// <summary>The bulletin schedule and archive (Stage 2 M3).</summary>
    public NewsDesk Desk => _desk;

    /// <summary>Plots whose director decision is being made in the background. Not saved: after a reload it starts again.</summary>
    public HashSet<string> Deciding { get; } = [];

    /// <summary>Every minor plot in this save (Stage 3).</summary>
    public PlotBook Plots => _plots;

    /// <summary>The Awakened Empire, the major plot (Stage 5).</summary>
    public PowerSaga Empire => _empire;

    /// <summary>The Imperial crisis: operations against the colony (crisis C1).</summary>
    public CrisisState Crisis => _crisis;

    /// <summary>Raiders who killed or maimed colonists (Stage 3 M2).</summary>
    public GrudgeLedger Grudges => _grudges;

    /// <summary>Keeps a reference to a pawn a grudge or plot needs (raider or victim), saved with the game.</summary>
    public void Track(Pawn pawn)
    {
        if (!_tracked.Contains(pawn))
        {
            _tracked.Add(pawn);
        }
    }

    public Pawn? Tracked(string thingId) => _tracked.FirstOrDefault(p => p?.ThingID == thingId);

    /// <summary>A tracked pawn, or a colonist (living or dead) with this id.</summary>
    public Pawn? TrackedOrFind(string thingId) =>
        Tracked(thingId) ?? RimWorld.PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.FirstOrDefault(p => p.ThingID == thingId);

    /// <summary>True for a living raider with an open grudge, or any living pawn an active plot names (a nemesis, a
    /// relative): the world-pawn cleanup must keep them.</summary>
    public bool KeepsAlive(Pawn pawn) =>
        !pawn.Dead && (_grudges.TrackedKillers.Contains(pawn.ThingID) || _plots.Active.Any(p => p.Facts.Any(f => f.Value == pawn.ThingID)));

    /// <summary>Drops pawns no grudge or active plot refers to any more.</summary>
    public void ForgetUntracked()
    {
        var needed = _grudges.Grudges.SelectMany(g => new[] { g.KillerId, g.VictimId })
            .Concat(_plots.Active.SelectMany(p => p.Facts.Select(f => f.Value)))
            .Concat(_plots.Active.SelectMany(p => new[] { p.Protagonist.PawnId, p.Protagonist.CoLeadId }))
            .ToHashSet();
        _tracked.RemoveAll(p => p is null || !needed.Contains(p.ThingID));
    }

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
        ScribeArchive.Instance.Deep(ref _news, "news");
        ScribeArchive.Instance.Deep(ref _desk, "newsDesk");
        ScribeArchive.Instance.Deep(ref _plots, "plots");
        ScribeArchive.Instance.Deep(ref _grudges, "grudges");
        ScribeArchive.Instance.Deep(ref _empire, "empire");
        ScribeArchive.Instance.Deep(ref _crisis, "crisis");
        Scribe_Collections.Look(ref _tracked, "trackedPawns", LookMode.Reference);
        Scribe_References.Look(ref _reporterPawn, "reporterPawn");
        _news ??= new NewsLog();
        _desk ??= new NewsDesk();
        _plots ??= new PlotBook();
        _grudges ??= new GrudgeLedger();
        _empire ??= new PowerSaga();
        _crisis ??= new CrisisState();
        _tracked ??= [];
        _tracked.RemoveAll(p => p is null);
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

        _lastDay = day;
        Game.News.ReporterLife.Daily(this);
        Game.News.ReporterKeeper.EnsureReporter(this);
        Game.Plots.NemesisTracker.Daily(this);
        Game.Quests.QuestStarter.Daily(this);
        Game.Plots.SuccessionDriver.Daily(this);
        Game.Plots.SilentPressBeats.Daily(this);
        Game.Plots.LostFamilyBeats.Daily(this);
        Game.Plots.PlotDriver.Daily(this);
        Game.Empire.EmpireDriver.Daily(this);
        Game.Crisis.CrisisDriver.Daily(this);
        Game.News.NewsDriver.Tick(this);
    }
}
