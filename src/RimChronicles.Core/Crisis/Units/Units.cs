namespace RimChronicles.Core.Crisis.Units;

/// <summary>The crisis's unit roles (crisis spec §5.1).</summary>
public enum UnitRole
{
    /// <summary>Line troops in cataphract armor.</summary>
    Praetorian,

    /// <summary>A psyker with one specialty's psycasts.</summary>
    Adept,

    /// <summary>Calls orbital strikes (C5). Not a psyker.</summary>
    Herald,

    /// <summary>EMP grenades; knocks out turrets and opens walls.</summary>
    Breacher,

    /// <summary>The finale's commander.</summary>
    Stellarch,
}

/// <summary>An adept's specialty (crisis spec §5.3.1).</summary>
public enum Specialty
{
    None,
    Skip,
    Control,
    Mind,
    Ward,
}

/// <summary>A psycast in a kit and the psylink level it needs.</summary>
public sealed record KitPsycast(string Def, int Level);

/// <summary>Item quality, named and ordered as RimWorld's <c>QualityCategory</c>.</summary>
public enum GearQuality
{
    Awful,
    Poor,
    Normal,
    Good,
    Excellent,
    Masterwork,
    Legendary,
}

/// <summary>What a unit gets at one crisis stage (crisis spec §6): psylink range, gear quality, bionics and starting
/// psyfocus. Non-psykers have psylink 0.</summary>
public sealed record UnitTier(int PsylinkMin, int PsylinkMax, GearQuality Gear, int Bionics, float Psyfocus);

/// <summary>
/// Fixed psycast kits by specialty, from the real Royalty defs (crisis spec §5.3.1, fixed in the C0 survey), the enemy
/// ban list (§5.3, fairness rule 9), and how units scale with the crisis stage. Plain rules; the game applies them.
/// </summary>
public static class PsycastKits
{
    public const int MaxPsylink = 6;

    public static readonly IReadOnlyList<KitPsycast> Skip =
        [new("ChaosSkip", 3), new("Beckon", 3), new("Skip", 4), new("MassChaosSkip", 6)];

    public static readonly IReadOnlyList<KitPsycast> Control =
        [new("Stun", 1), new("Burden", 1), new("BlindingPulse", 2), new("VertigoPulse", 3)];

    public static readonly IReadOnlyList<KitPsycast> Mind =
        [new("Berserk", 5), new("BerserkPulse", 6)];

    public static readonly IReadOnlyList<KitPsycast> Ward =
        [new("Painblock", 1), new("Chunkskip", 1), new("Smokepop", 4), new("Wallraise", 4), new("Invisibility", 5), new("BulletShield", 6)];

    /// <summary>The High Stellarch: the Skip and Ward kits, and Farskip to escape once (C13).</summary>
    public static readonly IReadOnlyList<KitPsycast> Stellarch = [.. Skip, .. Ward, new("Farskip", 5)];

    /// <summary>Psycasts no Empire pawn may ever have or cast (fairness rule 9). The game's ban list def adds to these;
    /// it can't take any away.</summary>
    public static readonly IReadOnlyList<string> RequiredBans = ["Neuroquake", "ManhunterPulse", "Flashstorm", "EntropyDump"];

    public static IReadOnlyList<KitPsycast> For(UnitRole role, Specialty specialty) => role switch
    {
        UnitRole.Adept => specialty switch
        {
            Specialty.Skip => Skip,
            Specialty.Control => Control,
            Specialty.Mind => Mind,
            Specialty.Ward => Ward,
            _ => [],
        },
        UnitRole.Stellarch => Stellarch,
        _ => [],
    };

    public static bool IsPsyker(UnitRole role) => role is UnitRole.Adept or UnitRole.Stellarch;

    /// <summary>The first stage a unit appears at: Mind Adepts need psylink 5 (stage 3), the Stellarch leads the finale.</summary>
    public static CrisisStage FirstStage(UnitRole role, Specialty specialty) => role switch
    {
        UnitRole.Stellarch => CrisisStage.Finale,
        UnitRole.Adept when specialty == Specialty.Mind => CrisisStage.War,
        _ => CrisisStage.Probing,
    };

    /// <summary>The psycasts a unit is granted: its kit up to its psylink level, never a banned one, and only those the
    /// game has (<paramref name="exists"/>; a missing one is left out, never thrown on).</summary>
    public static IReadOnlyList<string> Grant(UnitRole role, Specialty specialty, int psylink, IEnumerable<string> banned, Func<string, bool>? exists = null)
    {
        var bans = new HashSet<string>(banned.Concat(RequiredBans), StringComparer.Ordinal);
        return For(role, specialty)
            .Where(p => p.Level <= psylink && !bans.Contains(p.Def) && (exists?.Invoke(p.Def) ?? true))
            .Select(p => p.Def)
            .Distinct()
            .ToList();
    }

    /// <summary>A unit's tier at a stage. Stage None counts as Probing (a Dev Mode spawn outside a crisis).</summary>
    public static UnitTier Tier(UnitRole role, CrisisStage stage)
    {
        var s = stage == CrisisStage.None ? CrisisStage.Probing : stage;
        var (gear, psyfocus) = s switch
        {
            CrisisStage.Probing => (GearQuality.Good, 0.6f),
            CrisisStage.Reclamation => (GearQuality.Excellent, 0.7f),
            CrisisStage.War => (GearQuality.Masterwork, 0.8f),
            _ => (GearQuality.Masterwork, 0.9f),
        };
        var step = (int)s - (int)CrisisStage.Probing; // 0 to 3
        return role switch
        {
            UnitRole.Stellarch => new UnitTier(MaxPsylink, MaxPsylink, GearQuality.Legendary, 3, 1f),
            UnitRole.Adept => s switch
            {
                CrisisStage.Probing => new UnitTier(3, 3, gear, 0, psyfocus),
                CrisisStage.Reclamation => new UnitTier(3, 4, gear, 0, psyfocus),
                CrisisStage.War => new UnitTier(4, 5, gear, 1, psyfocus),
                _ => new UnitTier(5, 6, GearQuality.Legendary, 2, psyfocus), // adepts lead in the finale
            },
            UnitRole.Praetorian => new UnitTier(0, 0, gear, step, 0),
            _ => new UnitTier(0, 0, gear, Math.Max(0, step - 1), 0),
        };
    }

    /// <summary>A psylink level in the tier's range, from a roll in [0, 1) supplied by the caller.</summary>
    public static int PsylinkFor(UnitTier tier, double roll) =>
        tier.PsylinkMin + (int)(Math.Clamp(roll, 0, 0.999999) * (tier.PsylinkMax - tier.PsylinkMin + 1));

    /// <summary>A psyker's psylink level at a stage: the tier's roll, but never below its kit's first psycast, so every
    /// psyker has something to cast (a Mind Adept at stage 3 is psylink 5, not 4). 0 for non-psykers.</summary>
    public static int Psylink(UnitRole role, Specialty specialty, CrisisStage stage, double roll)
    {
        if (!IsPsyker(role))
        {
            return 0;
        }

        var kit = For(role, specialty);
        var floor = kit.Count == 0 ? 0 : kit.Min(p => p.Level);
        return Math.Clamp(Math.Max(PsylinkFor(Tier(role, stage), roll), floor), 0, MaxPsylink);
    }
}
