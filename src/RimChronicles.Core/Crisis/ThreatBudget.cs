namespace RimChronicles.Core.Crisis;

/// <summary>
/// The colony's fighting strength, as plain data the game reads each day (crisis spec §6). Wealth counts for less than
/// in vanilla, so the crisis can't be dodged by staying poor.
/// </summary>
public sealed record ColonyStrength
{
    /// <summary>Colonists who can fight: free, capable of violence, not downed.</summary>
    public int Fighters { get; init; }

    /// <summary>Their average gear, 0 (none) to 1 (the best armor and weapons), from armor ratings and weapon quality.</summary>
    public float Gear { get; init; }

    /// <summary>Turrets and mortars on home maps, weighted by kind (a mortar counts more than a mini-turret).</summary>
    public float Defenses { get; init; }

    /// <summary>The sum of the colonists' psylink levels.</summary>
    public int PsylinkLevels { get; init; }

    public float Wealth { get; init; }

    public const float PerFighter = 100f;
    public const float PerDefense = 40f;
    public const float PerPsylinkLevel = 30f;

    /// <summary>Strength per 1,000 wealth: vanilla's raid points count wealth far more.</summary>
    public const float PerThousandWealth = 1f;

    /// <summary>A colony of this score faces the base budget (a mid-game colony: 8 fighters in fair gear, a few turrets).</summary>
    public const float Reference = 1500f;

    public float Score =>
        Math.Max(0, Fighters) * PerFighter * (0.5f + Math.Clamp(Gear, 0f, 1f))
        + Math.Max(0, Defenses) * PerDefense
        + Math.Max(0, PsylinkLevels) * PerPsylinkLevel
        + Math.Max(0, Wealth) / 1000f * PerThousandWealth;

    public override string ToString() =>
        $"{Fighters} fighters, gear {Gear:0.00}, defenses {Defenses:0.#}, psylink {PsylinkLevels}, wealth {Wealth:0}: score {Score:0}";
}

/// <summary>The crisis's own threat budget (crisis spec §6): <c>base(stage) × difficulty × clamp(strength)</c>. It is
/// separate from the storyteller's raid points. The numbers are first guesses for the balance pass (C14).</summary>
public static class ThreatBudget
{
    public const float MinStrengthFactor = 0.6f;
    public const float MaxStrengthFactor = 1.8f;

    public const float MinDifficulty = 0.1f;
    public const float MaxDifficulty = 5f;

    /// <summary>Budget points at the reference strength and difficulty 1, by stage.</summary>
    public static float Base(CrisisStage stage) => stage switch
    {
        CrisisStage.Probing => 800f,
        CrisisStage.Reclamation => 1800f,
        CrisisStage.War => 3500f,
        CrisisStage.Finale => 6000f,
        _ => 0f,
    };

    /// <summary>How the colony's strength scales the budget, clamped so a weak colony still faces a crisis and a strong
    /// one isn't buried.</summary>
    public static float StrengthFactor(ColonyStrength strength) =>
        Math.Clamp(strength.Score / ColonyStrength.Reference, MinStrengthFactor, MaxStrengthFactor);

    /// <summary>A broken Empire fights with less (tech spec §4.1).</summary>
    public const float BrokenFactor = 0.6f;

    /// <param name="difficulty">The storyteller's threat scale times the crisis difficulty setting.</param>
    public static float Compute(CrisisStage stage, float difficulty, ColonyStrength strength, bool broken = false) =>
        Base(stage) * Math.Clamp(difficulty, MinDifficulty, MaxDifficulty) * StrengthFactor(strength) * (broken ? BrokenFactor : 1f);
}
