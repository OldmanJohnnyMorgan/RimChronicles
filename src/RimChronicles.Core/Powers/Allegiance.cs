using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Powers;

/// <summary>The colony's side (War in Heaven spec §6): one power's ally, independent (against every power), or not yet
/// declared.</summary>
public static class Side
{
    public const string Undeclared = "";
    public const string Independent = "independent";
}

/// <summary>Why a stance changed, for the log.</summary>
public static class AllegianceCause
{
    /// <summary>The power's own plot set it (the envoy's answer, tribute, a final demand, a treaty).</summary>
    public const string Plot = "plot";

    /// <summary>The colony sided with a rival power, which makes it this power's enemy.</summary>
    public const string Rival = "rival";

    /// <summary>The colony refused a summons (W6).</summary>
    public const string Summons = "summons";

    public const string Dev = "dev";
}

/// <summary>The colony's stance toward one power, with when and why it was last set.</summary>
public sealed class PowerStanceEntry : IArchivable
{
    public string Power = "";
    public string Stance = PowerStance.Neutral;
    public int Day;
    public string Cause = "";

    public void Expose(IArchive archive)
    {
        archive.Value(ref Power, "power", "");
        archive.Value(ref Stance, "stance", PowerStance.Neutral);
        archive.Value(ref Day, "day");
        archive.Value(ref Cause, "cause", "");
        Power ??= "";
        Stance ??= PowerStance.Neutral;
        Cause ??= "";
    }
}

/// <summary>
/// Where the colony stands toward every power (War in Heaven tech spec R8, §5), in one place. Siding with one power makes
/// the colony every other power's enemy; independence opposes them all. Each power's plot keeps its own <c>stance</c>
/// fact, which the game syncs with this record daily: a stance the plot sets comes in here, and a stance this record
/// changes (siding with a rival) goes out to that power's plot. A save from before the War in Heaven has no allegiance;
/// its first daily sync reads the Empire plot's stance. Saved with the game.
/// </summary>
public sealed class Allegiance : IArchivable
{
    public List<PowerStanceEntry> Stances = [];

    /// <summary>"Day N: what changed", oldest first.</summary>
    public List<string> Log = [];

    /// <summary>The colony's stance toward <paramref name="power"/>: neutral until something sets it.</summary>
    public string StanceToward(Power power) => Entry(power.Id)?.Stance ?? PowerStance.Neutral;

    /// <summary>The power it has sided with, by id; <see cref="Side.Independent"/> when it opposes every one of
    /// <paramref name="powers"/>; otherwise <see cref="Side.Undeclared"/>.</summary>
    public string SideAmong(IReadOnlyList<Power> powers)
    {
        if (powers.FirstOrDefault(p => StanceToward(p) == PowerStance.Ally) is { } ally)
        {
            return ally.Id;
        }

        return powers.Count > 0 && powers.All(p => StanceToward(p) == PowerStance.Oppose) ? Side.Independent : Side.Undeclared;
    }

    /// <summary>Sets the stance toward <paramref name="power"/>. Siding with it (<see cref="PowerStance.Ally"/>) makes
    /// the colony the enemy of every other power in <paramref name="powers"/>. Returns every stance that changed, this
    /// one first, so the game can update each power's plot and faction.</summary>
    public IReadOnlyList<(Power Power, string Stance)> Set(Power power, string stance, IReadOnlyList<Power> powers, int day, string cause)
    {
        var changed = new List<(Power, string)>();
        if (Change(power, stance, day, cause))
        {
            changed.Add((power, stance));
        }

        if (stance == PowerStance.Ally)
        {
            foreach (var rival in powers.Where(p => p != power))
            {
                if (Change(rival, PowerStance.Oppose, day, AllegianceCause.Rival))
                {
                    changed.Add((rival, PowerStance.Oppose));
                }
            }
        }

        return changed;
    }

    /// <summary>Independence: the colony opposes every power in <paramref name="powers"/>. Returns the stances that
    /// changed.</summary>
    public IReadOnlyList<(Power Power, string Stance)> DeclareIndependence(IReadOnlyList<Power> powers, int day, string cause) =>
        powers.Where(p => Change(p, PowerStance.Oppose, day, cause)).Select(p => (p, PowerStance.Oppose)).ToList();

    /// <summary>The day the colony's current war began: the earliest day it turned against a power it still opposes, or
    /// -1 while it opposes none.</summary>
    public int WarSince() =>
        Stances.Where(e => e.Stance == PowerStance.Oppose).Select(e => e.Day).DefaultIfEmpty(-1).Min();

        private bool Change(Power power, string stance, int day, string cause)
    {
        var entry = Entry(power.Id);
        if ((entry?.Stance ?? PowerStance.Neutral) == stance && entry is not null)
        {
            return false;
        }

        if (entry is null)
        {
            entry = new PowerStanceEntry { Power = power.Id };
            Stances.Add(entry);
        }

        var was = entry.Stance;
        entry.Stance = stance;
        entry.Day = day;
        entry.Cause = cause;
        Log.Add($"Day {day}: {power.Id} {was} -> {stance} ({cause})");
        return true;
    }

    private PowerStanceEntry? Entry(string powerId) => Stances.FirstOrDefault(e => e.Power == powerId);

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref Stances, "stances");
        archive.Strings(ref Log, "log");
        Stances ??= [];
        Log ??= [];
    }
}
