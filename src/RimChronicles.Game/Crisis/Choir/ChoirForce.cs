using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>A Choir force: its mechanitors and mechs, not spawned, and which mechanitor commands each mech.</summary>
public sealed class ChoirForceParts
{
    public List<Pawn> Mechanitors { get; } = [];
    public List<Pawn> Mechs { get; } = [];
    public List<(Pawn Mech, Pawn Commander)> Links { get; } = [];

    public List<Pawn> All => [.. Mechanitors, .. Mechs];

    public int Count => Mechanitors.Count + Mechs.Count;
}

/// <summary>
/// Builds the Hollow Choir's forces (War in Heaven W7): Core's <see cref="ChoirMixes"/> and <see cref="ForceMix"/> pick
/// the units for a budget; mechanitors are the Choir's unit kinds (kitted by stage, as the Empire's), mechs are vanilla
/// kinds of the entry's class (<see cref="ChoirRoster"/>). Each mech gets a commander (<see cref="ChoirRoster.Assign"/>).
/// Main thread only.
/// </summary>
public static class ChoirForce
{
    private static bool Exists(string def) => DefDatabase<PawnKindDef>.GetNamedSilentFail(def) is not null;

    private static PawnKindDef? KindFor(UnitRole role) => UnitKit.ChoirKinds.FirstOrDefault(k => UnitKit.Unit(k)?.role == role);

    /// <summary>What an entry costs: its kind's combat power, or for mechs the average of its class at the stage.</summary>
    private static float Cost(MixEntry entry, CrisisStage stage)
    {
        if (entry.Role != UnitRole.Mech)
        {
            return KindFor(entry.Role)?.combatPower ?? 100f;
        }

        var kinds = ChoirRoster.Kinds(entry.Mech, stage, Exists);
        return kinds.Count == 0 ? 100f : kinds.Average(k => DefDatabase<PawnKindDef>.GetNamed(k.Def).combatPower);
    }

    /// <summary>A force for <paramref name="points"/> of <paramref name="mix"/> at <paramref name="stage"/>.</summary>
    public static ChoirForceParts Build(IReadOnlyList<MixEntry> mix, CrisisStage stage, float points, Faction faction, int maxUnits = ForceMix.MaxUnits)
    {
        var usable = mix.Where(e => e.Role == UnitRole.Mech ? ChoirRoster.Kinds(e.Mech, stage, Exists).Count > 0 : KindFor(e.Role) is not null).ToList();
        var parts = new ChoirForceParts();
        foreach (var entry in ForceMix.Build(usable, stage, points, e => Cost(e, stage), maxUnits))
        {
            if (entry.Role == UnitRole.Mech)
            {
                if (ChoirRoster.Pick(entry.Mech, stage, Rand.Value, Exists) is { } def)
                {
                    parts.Mechs.Add(Mech(DefDatabase<PawnKindDef>.GetNamed(def), faction));
                }
            }
            else if (KindFor(entry.Role) is { } kind)
            {
                parts.Mechanitors.Add(UnitKit.Generate(kind, faction, stage));
            }
        }

        Link(parts, parts.Mechs);
        return parts;
    }

    /// <summary>One mech of <paramref name="kind"/> for the Choir.</summary>
    public static Pawn Mech(PawnKindDef kind, Faction faction) =>
        PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));

    /// <summary>The First Voice, for the final chorus (on top of the mix, as the Stellarch leads the final assault).</summary>
    public static Pawn? FirstVoice(Faction faction, CrisisStage stage) =>
        KindFor(UnitRole.FirstVoice) is { } kind ? UnitKit.Generate(kind, faction, stage) : null;

    /// <summary>Gives each of <paramref name="mechs"/> a commander among the force's mechanitors.</summary>
    public static void Link(ChoirForceParts parts, IReadOnlyList<Pawn> mechs)
    {
        var roles = parts.Mechanitors.Select(m => UnitKit.Unit(m.kindDef)?.role ?? UnitRole.Chorister).ToList();
        var owners = ChoirRoster.Assign(roles, mechs.Count);
        for (var i = 0; i < mechs.Count; i++)
        {
            if (owners[i] >= 0)
            {
                parts.Links.Add((mechs[i], parts.Mechanitors[owners[i]]));
            }
        }
    }
}
