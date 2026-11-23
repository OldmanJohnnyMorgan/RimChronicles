using RimChronicles.Core.Crisis.Units;

namespace RimChronicles.Core.Crisis.Operations;

/// <summary>One kind of unit in an operation's mix, and its share (crisis spec §5.3.1, "operation mix").</summary>
public sealed record MixEntry(UnitRole Role, Specialty Specialty, float Weight, CrisisStage MinStage = CrisisStage.Probing);

/// <summary>
/// Builds an operation's force from its mix and a points budget: units are added one at a time, always the kind
/// furthest below its share, while the budget lasts. Units that can't appear at the stage (a Mind Adept before stage 3)
/// are left out. Plain and deterministic; the game supplies each unit's cost (its pawn kind's combat power).
/// </summary>
public static class ForceMix
{
    public const int MaxUnits = 40;

    /// <summary>A raid never arrives as fewer than this many, even on a tiny budget.</summary>
    public const int MinUnits = 2;

    public static readonly IReadOnlyList<MixEntry> ProbingRaid =
    [
        new(UnitRole.Praetorian, Specialty.None, 4),
        new(UnitRole.Breacher, Specialty.None, 1, CrisisStage.Reclamation),
        new(UnitRole.Adept, Specialty.Ward, 1, CrisisStage.Reclamation),
        new(UnitRole.Herald, Specialty.None, 1, CrisisStage.War),
    ];

    public static readonly IReadOnlyList<MixEntry> Decapitation =
    [
        new(UnitRole.Praetorian, Specialty.None, 3),
        new(UnitRole.Adept, Specialty.Skip, 1),
        new(UnitRole.Adept, Specialty.Ward, 1),
        new(UnitRole.Adept, Specialty.Control, 1),
    ];

    /// <summary>Breachers (frag grenades to open walls, EMP grenades for turrets) lead; the main force follows.</summary>
    public static readonly IReadOnlyList<MixEntry> Breach =
    [
        new(UnitRole.Praetorian, Specialty.None, 6),
        new(UnitRole.Breacher, Specialty.None, 3),
        new(UnitRole.Adept, Specialty.Ward, 1),
        new(UnitRole.Adept, Specialty.Skip, 1),
        new(UnitRole.Herald, Specialty.None, 1, CrisisStage.War),
    ];

    /// <summary>Psykers under a Praetorian escort (crisis spec §5.3.1: "Mind 2, Control 2, Ward 1 per 6 Praetorians").
    /// Before Mind Adepts appear (stage 3) a Skip Adept takes part of their place.</summary>
    public static readonly IReadOnlyList<MixEntry> PsychicAssault =
    [
        new(UnitRole.Praetorian, Specialty.None, 6),
        new(UnitRole.Adept, Specialty.Mind, 2),
        new(UnitRole.Adept, Specialty.Control, 2),
        new(UnitRole.Adept, Specialty.Ward, 1),
        new(UnitRole.Adept, Specialty.Skip, 1),
    ];

    /// <summary>The siege: Praetorians build and hold the camp, with a breacher for the assault, Control and Ward
    /// Adepts to hold off sallies, and a Herald to call strikes until the uplink is up.</summary>
    public static readonly IReadOnlyList<MixEntry> SiegeWithBombardment =
    [
        new(UnitRole.Praetorian, Specialty.None, 7),
        new(UnitRole.Breacher, Specialty.None, 1),
        new(UnitRole.Adept, Specialty.Control, 1),
        new(UnitRole.Adept, Specialty.Ward, 1),
        new(UnitRole.Herald, Specialty.None, 1, CrisisStage.War),
    ];

    /// <summary>A general assault, for operations whose own mix comes in a later milestone.</summary>
    public static readonly IReadOnlyList<MixEntry> Assault =
    [
        new(UnitRole.Praetorian, Specialty.None, 6),
        new(UnitRole.Breacher, Specialty.None, 1),
        new(UnitRole.Herald, Specialty.None, 1, CrisisStage.War),
        new(UnitRole.Adept, Specialty.Control, 1),
        new(UnitRole.Adept, Specialty.Ward, 1),
    ];

    /// <summary>A site's garrison (C9): Praetorians on the walls, with what the site guards. An outpost has a Herald
    /// from stage 3; a relay keeps its psykers; an uplink its Heralds.</summary>
    public static IReadOnlyList<MixEntry> ForSite(string archetype) => archetype switch
    {
        "PsychicRelay" =>
        [
            new(UnitRole.Praetorian, Specialty.None, 4),
            new(UnitRole.Adept, Specialty.Control, 1),
            new(UnitRole.Adept, Specialty.Ward, 1),
            new(UnitRole.Adept, Specialty.Mind, 1),
        ],
        "OrbitalUplink" =>
        [
            new(UnitRole.Praetorian, Specialty.None, 5),
            new(UnitRole.Herald, Specialty.None, 2),
            new(UnitRole.Adept, Specialty.Ward, 1),
        ],
        _ =>
        [
            new(UnitRole.Praetorian, Specialty.None, 4),
            new(UnitRole.Breacher, Specialty.None, 1),
            new(UnitRole.Herald, Specialty.None, 1, CrisisStage.War),
        ],
    };

    public static IReadOnlyList<MixEntry> For(string operation) => operation switch
    {
        CrisisOperations.ProbingRaid => ProbingRaid,
        CrisisOperations.Decapitation => Decapitation,
        CrisisOperations.Breach => Breach,
        CrisisOperations.PsychicAssault => PsychicAssault,
        CrisisOperations.SiegeWithBombardment => SiegeWithBombardment,
        _ => Assault,
    };

    public static IReadOnlyList<MixEntry> Build(IReadOnlyList<MixEntry> mix, CrisisStage stage, float points, Func<MixEntry, float> cost)
    {
        var allowed = mix.Where(e => e.Weight > 0 && stage >= e.MinStage && stage >= PsycastKits.FirstStage(e.Role, e.Specialty)).ToList();
        var force = new List<MixEntry>();
        if (allowed.Count == 0)
        {
            return force;
        }

        var counts = allowed.ToDictionary(e => e, _ => 0);
        var left = points;
        while (force.Count < MaxUnits)
        {
            var next = allowed
                .Where(e => cost(e) <= left || force.Count < MinUnits)
                .OrderBy(e => counts[e] / e.Weight).ThenByDescending(e => e.Weight).ThenBy(e => allowed.IndexOf(e))
                .FirstOrDefault();
            if (next is null)
            {
                break;
            }

            force.Add(next);
            counts[next]++;
            left -= cost(next);
        }

        return force;
    }
}
