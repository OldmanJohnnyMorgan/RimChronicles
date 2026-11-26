using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Units;

namespace RimChronicles.Core.Choir;

/// <summary>A vanilla mech kind the Choir fields, the first stage it appears at, and how often.</summary>
public sealed record MechKind(string Def, MechClass Class, CrisisStage MinStage, float Weight);

/// <summary>
/// The Hollow Choir's mechs (War in Heaven tech spec §4.1, W7): vanilla Core and Biotech mech kinds by weight class and
/// stage. Kinds the game doesn't have are skipped, and a class with none left falls back to a lighter one, so a
/// missing def never stops a raid.
/// </summary>
public static class ChoirRoster
{
    public static readonly IReadOnlyList<MechKind> Mechs =
    [
        new("Mech_Militor", MechClass.Light, CrisisStage.Probing, 4),
        new("Mech_Scorcher", MechClass.Light, CrisisStage.Probing, 2),
        new("Mech_Pikeman", MechClass.Light, CrisisStage.Probing, 2),
        new("Mech_Scyther", MechClass.Light, CrisisStage.Reclamation, 2),
        new("Mech_Lancer", MechClass.Line, CrisisStage.Probing, 3),
        new("Mech_Legionary", MechClass.Line, CrisisStage.Reclamation, 2),
        new("Mech_Tesseron", MechClass.Line, CrisisStage.Reclamation, 2),
        new("Mech_CentipedeGunner", MechClass.Heavy, CrisisStage.Reclamation, 2),
        new("Mech_CentipedeBlaster", MechClass.Heavy, CrisisStage.War, 2),
        new("Mech_CentipedeBurner", MechClass.Heavy, CrisisStage.War, 1),
        new("Mech_Centurion", MechClass.Heavy, CrisisStage.War, 1),
        new("Mech_Tunneler", MechClass.Tunneler, CrisisStage.Probing, 1),
        new("Mech_Warqueen", MechClass.Boss, CrisisStage.Finale, 1),
        new("Mech_Diabolus", MechClass.Boss, CrisisStage.Finale, 1),
        new("Mech_Apocriton", MechClass.Boss, CrisisStage.Finale, 1),
    ];

    /// <summary>The class to try when a class has no kind the game knows: tunnelers and heavies fall back to the line,
    /// bosses to heavies, the line to light mechs.</summary>
    public static MechClass Fallback(MechClass c) => c switch
    {
        MechClass.Boss => MechClass.Heavy,
        MechClass.Heavy or MechClass.Tunneler => MechClass.Line,
        MechClass.Line => MechClass.Light,
        _ => MechClass.None,
    };

    /// <summary>The kinds of a class available at a stage that the game has.</summary>
    public static IReadOnlyList<MechKind> Kinds(MechClass c, CrisisStage stage, Func<string, bool> exists)
    {
        var s = stage == CrisisStage.None ? CrisisStage.Probing : stage;
        for (var cls = c; cls != MechClass.None; cls = Fallback(cls))
        {
            var kinds = Mechs.Where(m => m.Class == cls && m.MinStage <= s && exists(m.Def)).ToList();
            if (kinds.Count > 0)
            {
                return kinds;
            }
        }

        return [];
    }

    /// <summary>A kind by weight, from a roll in [0, 1) supplied by the caller; null if the game has none.</summary>
    public static string? Pick(MechClass c, CrisisStage stage, double roll, Func<string, bool> exists)
    {
        var kinds = Kinds(c, stage, exists);
        if (kinds.Count == 0)
        {
            return null;
        }

        var target = Math.Clamp(roll, 0, 0.999999) * kinds.Sum(k => k.Weight);
        foreach (var kind in kinds)
        {
            target -= kind.Weight;
            if (target < 0)
            {
                return kind.Def;
            }
        }

        return kinds[^1].Def;
    }

    /// <summary>True for the Choir's mechanitors, who command mechs.</summary>
    public static bool IsMechanitor(UnitRole? role) => role is UnitRole.Chorister or UnitRole.Cantor or UnitRole.Choirmaster or UnitRole.FirstVoice;

    /// <summary>How many mechs one mechanitor of a role can command (its bandwidth, in mechs); the rest of a force's
    /// mechs follow the strongest.</summary>
    public static int Command(UnitRole role) => role switch
    {
        UnitRole.FirstVoice => 12,
        UnitRole.Choirmaster => 8,
        UnitRole.Cantor => 4,
        _ => 5,
    };

    /// <summary>
    /// Shares a force's mechs out among its mechanitors (spec: killing a mechanitor shuts its mechs down, R4): each
    /// takes mechs up to its command, strongest first; any left over go to the strongest. Returns each mech's
    /// commander index, or -1 with no mechanitor at all.
    /// </summary>
    public static int[] Assign(IReadOnlyList<UnitRole> mechanitors, int mechs)
    {
        var owner = new int[mechs];
        if (mechanitors.Count == 0)
        {
            Array.Fill(owner, -1);
            return owner;
        }

        var order = Enumerable.Range(0, mechanitors.Count).OrderByDescending(i => Command(mechanitors[i])).ThenBy(i => i).ToList();
        var load = new int[mechanitors.Count];
        for (var m = 0; m < mechs; m++)
        {
            var pick = order.FirstOrDefault(i => load[i] < Command(mechanitors[i]), order[0]);
            owner[m] = pick;
            load[pick]++;
        }

        return owner;
    }
}

/// <summary>The Choir's operation mixes (War in Heaven W7): mechs by class, and the mechanitors who command them.</summary>
public static class ChoirMixes
{
    private static MixEntry Mechs(MechClass c, float weight, CrisisStage from = CrisisStage.Probing) =>
        new(UnitRole.Mech, Specialty.None, weight, from) { Mech = c };

    private static MixEntry Of(UnitRole role, float weight, CrisisStage from = CrisisStage.Probing) => new(role, Specialty.None, weight, from);

    /// <summary>A light swarm with one mechanitor to report back.</summary>
    public static readonly IReadOnlyList<MixEntry> ProbingSwarm =
    [
        Mechs(MechClass.Light, 4),
        Mechs(MechClass.Line, 1, CrisisStage.Reclamation),
        Of(UnitRole.Chorister, 1),
    ];

    /// <summary>Tunnelers come up under the defenses (they arrive apart, by burrowing); the line follows them in.</summary>
    public static readonly IReadOnlyList<MixEntry> TunnelBreach =
    [
        Mechs(MechClass.Tunneler, 2),
        Mechs(MechClass.Line, 4),
        Mechs(MechClass.Light, 2),
        Of(UnitRole.Chorister, 1),
        Of(UnitRole.Cantor, 1, CrisisStage.War),
    ];

    /// <summary>While the resonators hum, mechanitors push in behind a mixed line.</summary>
    public static readonly IReadOnlyList<MixEntry> TheHum =
    [
        Mechs(MechClass.Light, 3),
        Mechs(MechClass.Line, 2),
        Of(UnitRole.Chorister, 2),
        Of(UnitRole.Choirmaster, 1, CrisisStage.War),
        Of(UnitRole.Cantor, 1),
    ];

    /// <summary>A strike force that burrows up beside a Choir captive.</summary>
    public static readonly IReadOnlyList<MixEntry> Retrieval =
    [
        Mechs(MechClass.Light, 2),
        Mechs(MechClass.Line, 2),
        Mechs(MechClass.Tunneler, 1),
        Of(UnitRole.Chorister, 1),
    ];

    /// <summary>Mechs that burrow up beside the colony's leader.</summary>
    public static readonly IReadOnlyList<MixEntry> Burrowing =
    [
        Mechs(MechClass.Line, 3),
        Mechs(MechClass.Light, 1),
        Mechs(MechClass.Heavy, 1, CrisisStage.War),
        Of(UnitRole.Chorister, 1),
    ];

    /// <summary>The final chorus: every class of mech, a boss mech in the finale, and the First Voice on top of the mix.</summary>
    public static readonly IReadOnlyList<MixEntry> FinalChorus =
    [
        Mechs(MechClass.Line, 4),
        Mechs(MechClass.Heavy, 2),
        Mechs(MechClass.Light, 2),
        Mechs(MechClass.Boss, 1, CrisisStage.Finale),
        Of(UnitRole.Chorister, 1),
        Of(UnitRole.Choirmaster, 1),
        Of(UnitRole.Cantor, 1),
    ];

    /// <summary>A general Choir assault, for an operation without its own mix (the cluster siege until W8).</summary>
    public static readonly IReadOnlyList<MixEntry> Assault =
    [
        Mechs(MechClass.Line, 3),
        Mechs(MechClass.Light, 2),
        Of(UnitRole.Chorister, 1),
        Of(UnitRole.Cantor, 1, CrisisStage.War),
    ];

    /// <summary>The cluster siege (W8): a mechanitor to hold the camp for every few mechs, a cantor to call drops.</summary>
    public static readonly IReadOnlyList<MixEntry> ClusterSiege =
    [
        Mechs(MechClass.Line, 4),
        Mechs(MechClass.Light, 2),
        Mechs(MechClass.Heavy, 1, CrisisStage.War),
        Of(UnitRole.Chorister, 2),
        Of(UnitRole.Cantor, 1),
    ];

    /// <summary>A Choir site's garrison (W8), by archetype: what the site guards.</summary>
    public static IReadOnlyList<MixEntry> ForSite(string archetype) => archetype switch
    {
        "Resonator" =>
        [
            Mechs(MechClass.Line, 3),
            Mechs(MechClass.Light, 2),
            Of(UnitRole.Chorister, 1),
            Of(UnitRole.Choirmaster, 1, CrisisStage.War),
        ],
        "SignalSpire" =>
        [
            Mechs(MechClass.Line, 3),
            Mechs(MechClass.Heavy, 1, CrisisStage.War),
            Of(UnitRole.Cantor, 2),
            Of(UnitRole.Chorister, 1),
        ],
        "ChoirHolding" =>
        [
            Mechs(MechClass.Line, 4),
            Mechs(MechClass.Light, 2),
            Mechs(MechClass.Heavy, 1, CrisisStage.War),
            Of(UnitRole.Chorister, 2),
        ],
        _ =>
        [
            Mechs(MechClass.Light, 3),
            Mechs(MechClass.Line, 2),
            Of(UnitRole.Chorister, 1),
        ],
    };

    /// <summary>The Cathedral's garrisons (W9), ring by ring: the outer works' mech line with a cantor; the cloister's
    /// heavier line under a choirmaster; the sanctum's bossgroup guard, which the First Voice commands.</summary>
    public static IReadOnlyList<MixEntry> Cathedral(int ring) => ring switch
    {
        0 =>
        [
            Mechs(MechClass.Line, 5),
            Mechs(MechClass.Light, 3),
            Of(UnitRole.Chorister, 1),
            Of(UnitRole.Cantor, 1),
        ],
        1 =>
        [
            Mechs(MechClass.Line, 4),
            Mechs(MechClass.Heavy, 2),
            Of(UnitRole.Chorister, 1),
            Of(UnitRole.Choirmaster, 1),
        ],
        _ =>
        [
            Mechs(MechClass.Boss, 2, CrisisStage.Finale),
            Mechs(MechClass.Heavy, 2),
            Of(UnitRole.Choirmaster, 1),
        ],
    };

    /// <summary>What a Cantor's cluster drop brings: a few mechs of the stage's light and line classes.</summary>
    public static readonly IReadOnlyList<MixEntry> ClusterDrop =
    [
        Mechs(MechClass.Light, 2),
        Mechs(MechClass.Line, 1),
        Mechs(MechClass.Heavy, 1, CrisisStage.War),
    ];

    public static IReadOnlyList<MixEntry> For(string operation) => operation switch
    {
        CrisisOperations.ProbingSwarm => ProbingSwarm,
        CrisisOperations.TunnelBreach => TunnelBreach,
        CrisisOperations.TheHum => TheHum,
        CrisisOperations.Retrieval => Retrieval,
        CrisisOperations.Burrowing => Burrowing,
        CrisisOperations.FinalChorus => FinalChorus,
        CrisisOperations.ClusterSiege => ClusterSiege,
        _ => Assault,
    };
}

/// <summary>
/// The Choir's cluster siege (War in Heaven W8, tech spec §4.3 row 4): the mirror of the Imperial siege. It walks to the
/// same kind of camp spot and lays out the same five stages (<see cref="Crisis.Siege.CampLayout"/>, with the Choir's
/// pieces), but its pieces aren't built: each stage drops in by pod <see cref="StageTicks"/> after the last, while a
/// mechanitor stands at the camp. Kill the mechanitors and the camp stops growing; lose most of them and the siege
/// must attack or leave (<see cref="Crisis.Siege.SiegeRules.AfterBuildersFall"/>, with mechanitors as its builders).
/// </summary>
public static class ClusterSiegeRules
{
    /// <summary>A stage drops in this long after the last: all five in under two days, as the Imperial camp is built.</summary>
    public const int StageTicks = 18000;

    /// <summary>A mechanitor this close to the camp's centre keeps it growing.</summary>
    public const float CampReach = 20f;

    /// <summary>The stage to be on: the first at once, then the next once <see cref="StageTicks"/> have passed since the
    /// last landed, while a mechanitor is at the camp; never past the last.</summary>
    public static int Next(int current, int ticksSinceLast, bool mechanitorAtCamp) =>
        current < Crisis.Siege.BuildStages.First ? Crisis.Siege.BuildStages.First
        : current >= Crisis.Siege.BuildStages.Last || !mechanitorAtCamp || ticksSinceLast < StageTicks ? current
        : current + 1;

    /// <summary>What each stage gives the Choir, as the player is told when it lands.</summary>
    public static string Landed(int stage) => stage switch
    {
        1 => "The Choir's cluster has landed: sandbag lines and shelters around its mechanitors.",
        2 => "The Choir's cluster has grown auto-mortars: the shelling starts now.",
        3 => "The Choir's cluster has raised a signal spire: its cluster drops need no cantor now.",
        4 => "The Choir's cluster has a shield and a resonator: shells won't reach it, and the hum has begun.",
        _ => "The Choir's cluster has lit a drop beacon: more mechs will land there.",
    };
}
