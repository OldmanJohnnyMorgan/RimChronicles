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
    private PowerSaga _choir = new() { Power = Power.Choir };
    private CrisisState _crisis = new();
    private CrisisState _choirCrisis = new();
    private Allegiance _allegiance = new();
    private Core.Choir.ChoirRanks _choirRanks = new();
    private Core.War.WarInHeaven _war = new();
    private SummonsBook _summons = new();
    private List<Pawn> _tracked = [];
    private Pawn? _reporterPawn;

    public RimChroniclesGameComponent(Verse.Game game)
    {
        Current?.End();
        Current = this;

        // Static Dev Mode and lookup state from the last game (code review fix 9). Maps load after this.
        Game.Crisis.Psykers.PsykerLog.Reset();
        Game.Crisis.Sites.MapComponent_RcCitadel.Shielded.Clear();
    }

    /// <summary>The game is over (quit to the main menu, or another game replaced it): stops its background work and
    /// lets it be collected (code review fix 8). Its queue is never drained again.</summary>
    internal void End()
    {
        if (Current == this)
        {
            Current = null;
        }

        if (_ended)
        {
            return;
        }

        // Cancelled, not disposed: background tasks may still hold its token, and a source with no timer holds nothing
        // that needs freeing.
        _ended = true;
        _cancel.Cancel();
    }

    private bool _ended;

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

    /// <summary>The Hollow Choir, the Empire's old enemy (War in Heaven W4).</summary>
    public PowerSaga Choir => _choir;

    /// <summary>Colonists' standing in the Chorus (War in Heaven W4b, spec D13).</summary>
    public Core.Choir.ChoirRanks ChoirRanks => _choirRanks;

    /// <summary>The summons (War in Heaven W6).</summary>
    public SummonsBook Summons => _summons;

    /// <summary>The War in Heaven (W10).</summary>
    public Core.War.WarInHeaven War => _war;

    /// <summary>The saga of <paramref name="power"/>.</summary>
    public PowerSaga SagaOf(Power power) => power == Power.Choir ? _choir : _empire;

    /// <summary>The Imperial crisis: operations against the colony (crisis C1).</summary>
    public CrisisState Crisis => _crisis;

    /// <summary>The Hollow Choir's crisis (War in Heaven W7), kept apart from the Empire's.</summary>
    public CrisisState ChoirCrisis => _choirCrisis;

    /// <summary>The colony's side toward every power (War in Heaven W2).</summary>
    public Allegiance Allegiance => _allegiance;

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

    /// <summary>True for a living pawn the world-pawn cleanup must keep: a raider with an open grudge, any pawn an
    /// active plot names (a nemesis, a relative), or a crisis's finale leader still in the war (<see cref="KeptPawns"/>).</summary>
    public bool KeepsAlive(Pawn pawn) =>
        !pawn.Dead && KeptPawns.KeepAlive(_plots, _grudges, _crisis, _choirCrisis).Contains(pawn.ThingID);

    /// <summary>Drops pawns nothing in the mod refers to any more (<see cref="KeptPawns.Referenced"/>).</summary>
    public void ForgetUntracked()
    {
        var needed = KeptPawns.Referenced(_plots, _grudges, _crisis, _choirCrisis);
        _tracked.RemoveAll(p => p is null || !needed.Contains(p.ThingID));
    }

    /// <summary>Daily: pins every tracked world pawn the cleanup must keep. The pin is normally set as a pawn leaves a
    /// map, but spawning drops it, and a save from before a keep rule existed has none, so it is checked again.</summary>
    private void PinKeptWorldPawns()
    {
        if (Find.WorldPawns is not { } world)
        {
            return;
        }

        var keep = KeptPawns.KeepAlive(_plots, _grudges, _crisis, _choirCrisis);
        foreach (var pawn in _tracked)
        {
            if (pawn is { Dead: false } && keep.Contains(pawn.ThingID) && world.Contains(pawn))
            {
                world.ForcefullyKeptPawns.Add(pawn);
            }
        }
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
        ScribeArchive.Instance.Deep(ref _choir, Power.Choir.Id);
        ScribeArchive.Instance.Deep(ref _crisis, "crisis");
        ScribeArchive.Instance.Deep(ref _choirCrisis, "choirCrisis");
        ScribeArchive.Instance.Deep(ref _allegiance, "allegiance");
        ScribeArchive.Instance.Deep(ref _choirRanks, "choirRanks");
        ScribeArchive.Instance.Deep(ref _war, Core.War.WarInHeaven.PlotId);
        ScribeArchive.Instance.Deep(ref _summons, "summons");
        Scribe_Collections.Look(ref _tracked, "trackedPawns", LookMode.Reference);
        Scribe_References.Look(ref _reporterPawn, "reporterPawn");
        _news ??= new NewsLog();
        _desk ??= new NewsDesk();
        _plots ??= new PlotBook();
        _grudges ??= new GrudgeLedger();
        _empire ??= new PowerSaga();
        _choir ??= new PowerSaga();
        _choir.Power = Power.Choir; // not saved: a loaded saga starts as the Empire's
        _crisis ??= new CrisisState();
        _choirCrisis ??= new CrisisState();
        _allegiance ??= new Allegiance();
        _choirRanks ??= new Core.Choir.ChoirRanks();
        _war ??= new Core.War.WarInHeaven();
        _summons ??= new SummonsBook();
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
        LastDailyFailures = Steps.RunAll(DailySteps(), (name, ex) => Guard.Report($"Daily step {name}", ex));
    }

    /// <summary>The daily work, in order. Each step runs even if one before it throws (code review fix 2).</summary>
    private IEnumerable<Step> DailySteps()
    {
        yield return new("pin kept pawns", PinKeptWorldPawns);
        yield return new("dev fault", DevFault);
        yield return new("reporter life", () => Game.News.ReporterLife.Daily(this));
        yield return new("reporter keeper", () => Game.News.ReporterKeeper.EnsureReporter(this));
        yield return new("nemesis tracker", () => Game.Plots.NemesisTracker.Daily(this));
        yield return new("quest starter", () => Game.Quests.QuestStarter.Daily(this));
        yield return new("succession", () => Game.Plots.SuccessionDriver.Daily(this));
        yield return new("silent press", () => Game.Plots.SilentPressBeats.Daily(this));
        yield return new("lost family", () => Game.Plots.LostFamilyBeats.Daily(this));
        yield return new("plot director", () => Game.Plots.PlotDriver.Daily(this));
        yield return new("powers", () => Game.Powers.PowerDrivers.Daily(this));
        yield return new("Imperial crisis", () => Game.Crisis.CrisisDriver.Daily(this));
        yield return new("Choir crisis", () => Game.Crisis.Choir.ChoirCrisisDriver.Daily(this));
        yield return new("War in Heaven", () => Game.War.WarDriver.Daily(this));
        yield return new("news", () => Game.News.NewsDriver.Tick(this));
    }

    /// <summary>The daily steps that threw on the last day they ran. Not saved.</summary>
    public IReadOnlyList<string> LastDailyFailures { get; private set; } = [];

    /// <summary>Dev Mode: the next day's "dev fault" step throws once, to check that the later steps still run.</summary>
    public bool FailNextDay { get; set; }

    private void DevFault()
    {
        if (FailNextDay)
        {
            FailNextDay = false;
            throw new InvalidOperationException("Dev Mode test fault (Dev: fail next daily step)");
        }
    }

    [LudeonTK.DebugAction("Rim Chronicles", "Dev: fail next daily step", allowedGameStates = LudeonTK.AllowedGameStates.Playing)]
    public static void DevFailNextDay()
    {
        if (Current is { } component)
        {
            component.FailNextDay = true;
            Messages.Message("Rim Chronicles: tomorrow's daily work starts with a test fault; every later step should still run.",
                RimWorld.MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    [LudeonTK.DebugAction("Rim Chronicles", "Dev: daily step failures", allowedGameStates = LudeonTK.AllowedGameStates.Playing)]
    public static void DevShowFailures() =>
        Find.WindowStack.Add(new Dialog_MessageBox(Current is not { } c ? "No game."
            : c.LastDailyFailures.Count == 0 ? $"No daily step failed on day {c._lastDay}."
            : $"Failed on day {c._lastDay}: {string.Join(", ", c.LastDailyFailures)}. The log has each error once."));
}
