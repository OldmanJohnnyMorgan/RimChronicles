using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Crisis.Strikes;

/// <summary>What a strike is: one heavy shell (precise), a salvo over an area, or a power beam (fire over a wide area).
/// The game spawns Royalty's <c>Bombardment</c> or <c>PowerBeam</c> with the matching settings.</summary>
public enum StrikeKind
{
    Shell,
    Salvo,
    Beam,
}

/// <summary>What a strike could hit (crisis spec §5.4: "prefers turrets, clustered colonists and power infrastructure").</summary>
public enum StrikeTargetKind
{
    Turret,
    Cluster,
    Power,
    Wall,
    Food,
    Leader,
}

/// <summary>A siege's goal (crisis spec §5.5): it changes what strikes aim at, not the camp. The director picks it from C11.</summary>
public enum SiegeGoal
{
    None,
    BreakWalls,
    Starve,
    KillLeader,
}

/// <summary>A cell a strike could aim at, as the game reads it.</summary>
public sealed record StrikeCandidate
{
    public required Cell Cell { get; init; }
    public required StrikeTargetKind Kind { get; init; }

    /// <summary>How much hitting it matters (a turret's damage, a generator's output, a colonist's worth).</summary>
    public float Value { get; init; } = 1;

    /// <summary>Colonists within the strike's blast.</summary>
    public int Colonists { get; init; }

    /// <summary>Empire units within the largest blast (<see cref="StrikeRules.BeamRadius"/> for a beam). A strike never
    /// lands where it would hit its own side.</summary>
    public int Allies { get; init; }

    public int AlliesNearBeam { get; init; }

    /// <summary>A Herald can see the cell (or an uplink stands, which can strike anywhere).</summary>
    public bool Sighted { get; init; }

    /// <summary>Under overhead mountain: vanilla's orbital strike permit refuses these cells, and so do we.</summary>
    public bool ThickRoof { get; init; }
}

/// <summary>One raid's strike bookkeeping on one map. Saved with the map.</summary>
public sealed class StrikeState : IArchivable
{
    public int LastStrikeTick = -1;
    public int StrikesThisRaid;

    /// <summary>The raid's cap, set when the raid's first strike source appears (0 until then).</summary>
    public int Cap;

    public bool HasLastCell;
    public int LastX;
    public int LastZ;

    public Cell? LastCell => HasLastCell ? new Cell(LastX, LastZ) : null;

    public void Struck(Cell cell, int tick)
    {
        LastStrikeTick = tick;
        StrikesThisRaid++;
        HasLastCell = true;
        LastX = cell.X;
        LastZ = cell.Z;
    }

    /// <summary>The raid is over: the next one starts fresh.</summary>
    public void Reset()
    {
        StrikesThisRaid = 0;
        Cap = 0;
        HasLastCell = false;
    }

    public void Expose(IArchive archive)
    {
        archive.Value(ref LastStrikeTick, "lastStrikeTick", -1);
        archive.Value(ref StrikesThisRaid, "strikesThisRaid");
        archive.Value(ref Cap, "cap");
        archive.Value(ref HasLastCell, "hasLastCell");
        archive.Value(ref LastX, "lastX");
        archive.Value(ref LastZ, "lastZ");
    }
}

/// <summary>
/// The orbital strike rules (crisis spec §5.4, fairness rule 1): every strike needs a source (a living, conscious Herald
/// who can see the target, or a standing uplink), is telegraphed for 6–10 seconds first, waits a cooldown, counts
/// against a per-raid cap from the threat budget, never hits the same spot twice in a row, never lands on its own
/// troops or under overhead mountain, and prefers turrets, clustered colonists and power.
/// </summary>
public static class StrikeRules
{
    public const int TicksPerSecond = 60;
    public const int MinDelaySeconds = 6;
    public const int MaxDelaySeconds = 10;
    public const int DefaultDelaySeconds = 8;

    /// <summary>Between strikes on one map: about half a minute of game time at normal speed.</summary>
    public const int CooldownTicks = 1800;

    public const int MinCap = 1;
    public const int MaxCap = 6;

    /// <summary>Budget points per strike in a raid.</summary>
    public const float BudgetPerStrike = 1200f;

    /// <summary>A new strike within this distance of the last one counts as the same spot.</summary>
    public const float SameSpot = 4f;

    /// <summary>The blast a shell or salvo throws, for counting who's under it (vanilla's permits: 7.9 for the shell,
    /// a 7.9 area for the salvo).</summary>
    public const float BlastRadius = 8f;

    /// <summary>A power beam sets fires this far out (vanilla's <c>PowerBeam.Radius</c>).</summary>
    public const float BeamRadius = 15f;

    public static int DelayTicks(int seconds) => Math.Clamp(seconds, MinDelaySeconds, MaxDelaySeconds) * TicksPerSecond;

    public static int CapFor(float budget) => Math.Clamp((int)Math.Round(budget / BudgetPerStrike), MinCap, MaxCap);

    /// <summary>Null if a strike may be called now, else why not.</summary>
    public static string? Blocked(StrikeState state, bool heraldUp, bool uplinkStanding, bool strikesDown, int tick)
    {
        if (strikesDown)
        {
            return "the colony knocked out the Empire's orbital uplink";
        }

        if (!heraldUp && !uplinkStanding)
        {
            return "no Herald or uplink to call a strike";
        }

        if (state.Cap > 0 && state.StrikesThisRaid >= state.Cap)
        {
            return $"this raid's {state.Cap} strikes are spent";
        }

        if (state.LastStrikeTick >= 0 && tick - state.LastStrikeTick < CooldownTicks)
        {
            return $"cooling down ({(CooldownTicks - (tick - state.LastStrikeTick)) / TicksPerSecond} s)";
        }

        return null;
    }

    /// <summary>The strike to call, or null when no candidate is fit to hit.</summary>
    public static (StrikeCandidate Target, StrikeKind Kind)? Pick(IEnumerable<StrikeCandidate> candidates, StrikeState state, SiegeGoal goal = SiegeGoal.None)
    {
        var last = state.LastCell;
        var best = candidates
            .Where(c => c.Sighted && !c.ThickRoof && c.Allies == 0)
            .Where(c => last is not { } l || Dist(l, c.Cell) > SameSpot)
            .Select(c => (Candidate: c, Score: Score(c, goal)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Candidate.Cell.X).ThenBy(x => x.Candidate.Cell.Z)
            .Select(x => x.Candidate)
            .FirstOrDefault();
        return best is null ? null : (best, KindFor(best));
    }

    public static float Score(StrikeCandidate c, SiegeGoal goal)
    {
        var score = c.Kind switch
        {
            StrikeTargetKind.Turret => 3 * c.Value,
            StrikeTargetKind.Cluster => c.Colonists >= 2 ? 2f * c.Colonists : 0,
            StrikeTargetKind.Power => 2 * c.Value,
            StrikeTargetKind.Wall => 0.5f * c.Value,
            StrikeTargetKind.Food => c.Value,
            StrikeTargetKind.Leader => 2 * c.Value,
            _ => 0,
        } + (c.Kind != StrikeTargetKind.Cluster ? 0.5f * c.Colonists : 0);
        var factor = (goal, c.Kind) switch
        {
            (SiegeGoal.BreakWalls, StrikeTargetKind.Wall or StrikeTargetKind.Turret) => 2f,
            (SiegeGoal.Starve, StrikeTargetKind.Food or StrikeTargetKind.Power) => 3f,
            (SiegeGoal.KillLeader, StrikeTargetKind.Leader) => 3f,
            _ => 1f,
        };
        return score * factor;
    }

    /// <summary>A shell for one precise target, a salvo for groups and walls, a beam to burn fields and stores, and only
    /// where no Empire unit is within its fires.</summary>
    public static StrikeKind KindFor(StrikeCandidate c) => c.Kind switch
    {
        StrikeTargetKind.Cluster or StrikeTargetKind.Wall => StrikeKind.Salvo,
        StrikeTargetKind.Food when c.AlliesNearBeam == 0 => StrikeKind.Beam,
        StrikeTargetKind.Food => StrikeKind.Salvo,
        _ => StrikeKind.Shell,
    };

    public static float Dist(Cell a, Cell b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
