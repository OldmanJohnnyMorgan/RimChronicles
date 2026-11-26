using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Powers;

/// <summary>A settlement the Empire could attack. <see cref="Distance"/> is in world tiles from the nearest Imperial
/// settlement; <see cref="FactionSettlements"/> is how many its owner holds, a stand-in for the owner's strength.</summary>
public sealed record ExpansionTarget(string Id, string Name, string Faction, float Distance, int FactionSettlements);

public enum ExpansionResult
{
    /// <summary>The settlement changes hands: it is the Empire's now.</summary>
    Conquered,

    /// <summary>The settlement is destroyed.</summary>
    Razed,

    /// <summary>The attack fails; nothing changes on the map.</summary>
    Repelled,
}

/// <summary>One expansion, saved for the log and the acts.</summary>
public sealed class ExpansionRecord : IArchivable
{
    public int Day;
    public string Settlement = "";
    public string Faction = "";
    public ExpansionResult Result;

    public void Expose(IArchive archive)
    {
        archive.Value(ref Day, "day");
        archive.Value(ref Settlement, "settlement", "");
        archive.Value(ref Faction, "faction", "");
        archive.Value(ref Result, "result", ExpansionResult.Repelled);
        Settlement ??= "";
        Faction ??= "";
    }

    public override string ToString() => $"Day {Day}: {Settlement} ({Faction}) {Result.ToString().ToLowerInvariant()}";
}

/// <summary>Territory expansion (design spec, Stage 5): pure rules, no model. The Empire picks a settlement in range,
/// weighted towards near and weak targets, and the attack ends in conquest, razing or failure, with stronger owners
/// more likely to hold. Random numbers come in from the caller.</summary>
public static class PowerExpansion
{
    /// <summary>Targets within this many tiles of an Imperial settlement are in range. With none in range, the
    /// nearest is.</summary>
    public const float RangeTiles = 60f;

    public const int MinGapDays = 15;
    public const int MaxGapDays = 30;

    /// <summary>The first attack comes this many days after the awakening.</summary>
    public const int FirstAttackDays = 7;

    public const double RazeChance = 0.2;

    public static double Weight(ExpansionTarget t) => 1.0 / (1.0 + t.Distance / 10.0) / Math.Max(1, t.FactionSettlements);

    /// <summary>During the War in Heaven (W10), the rival power's settlements count this many times over: each power
    /// turns its armies on the other first.</summary>
    public const double FrontWeight = 3;

    /// <summary>A target's weight, times <see cref="FrontWeight"/> for the rival's (<paramref name="rivalFaction"/>, by
    /// name) during the War in Heaven.</summary>
    public static double Weight(ExpansionTarget t, string? rivalFaction) =>
        Weight(t) * (rivalFaction is not null && t.Faction == rivalFaction ? FrontWeight : 1);

    /// <summary>The target, or null with none. <paramref name="roll"/> is in [0, 1). With <paramref name="rivalFaction"/>
    /// (the War in Heaven), the rival's settlements are weighted up; without it, the pick is as before W10.</summary>
    public static ExpansionTarget? PickTarget(IReadOnlyList<ExpansionTarget> candidates, double roll, string? rivalFaction = null)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var inRange = candidates.Where(c => c.Distance <= RangeTiles).ToList();
        if (inRange.Count == 0)
        {
            return candidates.OrderBy(c => c.Distance).ThenBy(c => c.Id, StringComparer.Ordinal).First();
        }

        var total = inRange.Sum(c => Weight(c, rivalFaction));
        var target = Math.Clamp(roll, 0, 0.999999) * total;
        foreach (var c in inRange)
        {
            target -= Weight(c, rivalFaction);
            if (target < 0)
            {
                return c;
            }
        }

        return inRange[^1];
    }

    /// <summary>The chance an attack on this target fails: 15%, plus 5% for each settlement its owner holds (up to 6).</summary>
    public static double RepelChance(ExpansionTarget t) => 0.15 + 0.05 * Math.Clamp(t.FactionSettlements, 0, 6);

    /// <summary>The numbers behind one attack, for the Dev Mode log: the rolls, the target's odds and how many
    /// settlements were in reach.</summary>
    public static string Odds(ExpansionTarget t, int candidates, double targetRoll, double resultRoll)
    {
        var repel = RepelChance(t);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Result roll {resultRoll:0.000}: repelled below {repel:0.00}, razed below {repel + RazeChance:0.00}, else taken "
            + $"({t.Faction} holds {t.FactionSettlements}; {t.Distance:0} tiles; target roll {targetRoll:0.000} of {candidates} in reach).");
    }

    public static ExpansionResult PickResult(ExpansionTarget t, double roll)
    {
        var repel = RepelChance(t);
        return roll < repel ? ExpansionResult.Repelled : roll < repel + RazeChance ? ExpansionResult.Razed : ExpansionResult.Conquered;
    }
}
