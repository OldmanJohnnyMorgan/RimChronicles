using RimChronicles.Core.Persistence;
using RimChronicles.Core.Powers;

namespace RimChronicles.Core.War;

/// <summary>Where the War in Heaven stands.</summary>
public enum WarState
{
    /// <summary>Not started: the powers are at war in name only.</summary>
    NotYet,

    /// <summary>The powers fight it in full.</summary>
    Started,

    /// <summary>It reached an ending.</summary>
    Ended,
}

/// <summary>
/// The War in Heaven (War in Heaven spec §7, tech spec R9): when the Empire and the Hollow Choir turn their armies on
/// each other. It can start once both are awake and the colony is at war with at least one; each day there's a chance it
/// starts, rising the longer the colony's war goes on; on game day <see cref="CertainDay"/> it starts for certain (D3).
/// Each power's fate is tracked here too: destroyed when its capital falls (D10: no longer an ending of its own). Plain
/// decisions; the roll comes in from the caller. Saved with the game.
/// </summary>
public sealed class WarInHeaven : IArchivable
{
    public const string PlotId = "war";
    public const string NewsKind = "WarInHeaven";
    public const string TemplateId = "WarInHeaven";

    /// <summary>Game day 600: the War in Heaven starts for certain (spec D3).</summary>
    public const int CertainDay = 600;

    /// <summary>Days of war before the odds start rising.</summary>
    public const int QuietWarDays = 30;

    /// <summary>The daily chance after <see cref="QuietWarDays"/> + <see cref="RiseDays"/> days of war.</summary>
    public const double FullOdds = 0.03;

    public const int RiseDays = 150;

    /// <summary>The daily chance never goes above this.</summary>
    public const double MaxOdds = 0.1;

    public WarState State = WarState.NotYet;
    public int StartDay = -1;

    /// <summary>Why it started, for the log and letters ("day 600", "the war went on").</summary>
    public string Cause = "";

    /// <summary>The colony's side's standing in the war: + for each job won for it (or against a power the colony
    /// opposes), - for each lost. A power's final push opens at <see cref="WarArc.FrontForPush"/>.</summary>
    public int Front;

    /// <summary>Power ids destroyed (their capital fell), in order.</summary>
    public List<string> Destroyed = [];

    /// <summary>"Day N: what happened", oldest first.</summary>
    public List<string> Log = [];

    public bool IsDestroyed(Power power) => Destroyed.Contains(power.Id);

    /// <summary>The daily chance it starts after <paramref name="warDays"/> days of the colony's war (times
    /// <paramref name="steepness"/>, a setting).</summary>
    public static double Odds(int warDays, double steepness = 1)
    {
        if (warDays < QuietWarDays)
        {
            return 0;
        }

        return Math.Min(MaxOdds, FullOdds * (warDays - QuietWarDays) / RiseDays * Math.Max(0, steepness));
    }

    /// <summary>Whether it may start: both powers awake, neither's story over, and the colony at war with at least one.</summary>
    public bool CanStart(PowerSaga empire, PowerSaga choir, bool colonyAtWar) =>
        State == WarState.NotYet && colonyAtWar && Awake(empire) && Awake(choir);

    private static bool Awake(PowerSaga saga) =>
        saga.State is PowerState.Awakened or PowerState.Ascendant or PowerState.Broken && saga.Ending.Length == 0;

    /// <summary>Whether it starts today: for certain from <see cref="CertainDay"/>, else when <paramref name="roll"/>
    /// (in [0, 1)) falls under the odds.</summary>
    public static bool StartsToday(int today, int warDays, double roll, double steepness = 1, int certainDay = CertainDay) =>
        today >= certainDay || roll < Odds(warDays, steepness);

    public void Start(int today, string cause)
    {
        State = WarState.Started;
        StartDay = today;
        Cause = cause;
        Note(today, $"the War in Heaven began: {cause}");
    }

    /// <summary>A power's capital fell: it is destroyed (spec D10).</summary>
    public void Destroy(Power power, int today, string how)
    {
        if (!Destroyed.Contains(power.Id))
        {
            Destroyed.Add(power.Id);
            Note(today, $"{power.Id} destroyed: {how}");
        }
    }

    public void End(int today, string ending)
    {
        State = WarState.Ended;
        Note(today, $"ending: {ending}");
    }

    public void Note(int day, string line) => Log.Add($"Day {day}: {line}");

    public void Expose(IArchive archive)
    {
        archive.Value(ref State, "state", WarState.NotYet);
        archive.Value(ref StartDay, "startDay", -1);
        archive.Value(ref Cause, "cause", "");
        archive.Value(ref Front, "front");
        archive.Strings(ref Destroyed, "destroyed");
        archive.Strings(ref Log, "log");
        Cause ??= "";
        Destroyed ??= [];
        Log ??= [];
    }
}
