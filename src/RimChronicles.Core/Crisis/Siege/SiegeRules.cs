using RimChronicles.Core.Crisis.Strikes;

namespace RimChronicles.Core.Crisis.Siege;

/// <summary>A siege goal as the crisis offers it (crisis spec §5.5): its id, weight for the pick and description.</summary>
public sealed record GoalSpec(SiegeGoal Goal, double Weight, string Desc)
{
    public string Id => SiegeGoals.Prefix + Goal;
}

/// <summary>What the colony has that a goal could aim at.</summary>
public sealed record SiegeTargets(bool HasWalls, bool HasFood, bool HasLeader);

/// <summary>
/// The siege goals (crisis spec §5.5): they change what orbital strikes aim at, not the camp. Until the director model
/// ships (C11) the goal is a weighted pick among those the colony gives a target for.
/// </summary>
public static class SiegeGoals
{
    public const string Prefix = "Goal:";

    public static readonly IReadOnlyList<GoalSpec> All =
    [
        new(SiegeGoal.BreakWalls, 2, "strikes go for the colony's walls and turrets, to open it for the assault"),
        new(SiegeGoal.Starve, 1, "strikes go for the colony's fields, food stores and power"),
        new(SiegeGoal.KillLeader, 1, "strikes go for the colony's leader whenever they're in the open"),
    ];

    public static GoalSpec Get(SiegeGoal goal) => All.First(g => g.Goal == goal);

    public static IReadOnlyList<GoalSpec> Allowed(SiegeTargets targets) => All.Where(g => g.Goal switch
    {
        SiegeGoal.BreakWalls => targets.HasWalls,
        SiegeGoal.Starve => targets.HasFood,
        SiegeGoal.KillLeader => targets.HasLeader,
        _ => false,
    }).ToList();

    /// <summary>A weighted pick with <paramref name="roll"/> in [0, 1); <see cref="SiegeGoal.None"/> when nothing is allowed.</summary>
    public static SiegeGoal Pick(IReadOnlyList<GoalSpec> allowed, double roll)
    {
        var total = allowed.Sum(g => g.Weight);
        if (total <= 0)
        {
            return SiegeGoal.None;
        }

        var at = Math.Clamp(roll, 0, 0.999999) * total;
        foreach (var goal in allowed)
        {
            if ((at -= goal.Weight) < 0)
            {
                return goal.Goal;
            }
        }

        return allowed[^1].Goal;
    }

    public static SiegeGoal Parse(string? text) =>
        Enum.TryParse<SiegeGoal>(text?.StartsWith(Prefix, StringComparison.Ordinal) == true ? text[Prefix.Length..] : text, out var g) ? g : SiegeGoal.None;
}

/// <summary>What the siege does once its builders fall (crisis spec §5.5).</summary>
public enum AfterBuilders
{
    /// <summary>Builders still hold.</summary>
    Hold,

    /// <summary>Most builders are down but the force is strong: it attacks.</summary>
    Assault,

    /// <summary>Most builders are down and the force is spent: it leaves.</summary>
    Retreat,
}

/// <summary>
/// The siege's rules besides the camp's shape: how many build, when the camp has lost its builders and what follows,
/// how long it digs in before it attacks anyway, and the beacon's reinforcement waves. Numbers are first guesses for
/// the balance pass (C14).
/// </summary>
public static class SiegeRules
{
    /// <summary>Share of the force set to build (vanilla's sieges: 25–40%).</summary>
    public const float BuilderShare = 0.35f;

    public const int MinBuilders = 2;

    /// <summary>The camp has lost most of its builders when fewer than this share of those it started with can still
    /// work (crisis spec §5.5: "loses most of its builders").</summary>
    public const float BuildersLeftShare = 0.5f;

    /// <summary>Past this share of the force lost, the siege leaves instead of attacking.</summary>
    public const float RetreatLosses = 0.5f;

    /// <summary>The assault after the siege leaves after this share is lost.</summary>
    public const float AssaultRetreatLosses = 0.65f;

    /// <summary>The walk to the camp spot gives up after this long and digs in where it is.</summary>
    public const int TravelTicks = 5000;

    /// <summary>The siege attacks after this long whatever its camp looks like (vanilla's: 1.5–3 days).</summary>
    public const int SiegeTicks = 120000;

    /// <summary>Between the beacon's reinforcement waves: a quarter day.</summary>
    public const int WaveTicks = 15000;

    public const int MaxWaves = 3;

    /// <summary>How many of <paramref name="force"/> pawns that can build are set to it: a share, at least two, and at
    /// least one per mortar so every mortar is manned.</summary>
    public static int Builders(int force, int canBuild, int mortars) =>
        Math.Min(canBuild, Math.Max(Math.Max(MinBuilders, (int)Math.Ceiling(force * BuilderShare)), mortars));

    public static bool BuildersFallen(int start, int up) => start > 0 && up < start * BuildersLeftShare;

    public static AfterBuilders AfterBuildersFall(int startBuilders, int buildersUp, float lostShare) =>
        !BuildersFallen(startBuilders, buildersUp) ? AfterBuilders.Hold
        : lostShare < RetreatLosses ? AfterBuilders.Assault
        : AfterBuilders.Retreat;

    /// <summary>A wave is due when the beacon stands, a reserve is left, the cap isn't reached, and the last wave (or the
    /// beacon's lighting) was <see cref="WaveTicks"/> ago.</summary>
    public static bool WaveDue(bool beaconStanding, int wavesSent, float reserveLeft, int sinceLast) =>
        beaconStanding && wavesSent < MaxWaves && reserveLeft > 0 && sinceLast >= WaveTicks;

    /// <summary>Points for the next wave: the reserve spread evenly over the waves left.</summary>
    public static float WavePoints(float reserveLeft, int wavesSent) =>
        wavesSent >= MaxWaves ? 0 : reserveLeft / (MaxWaves - wavesSent);
}
