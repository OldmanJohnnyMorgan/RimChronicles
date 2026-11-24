using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Sites;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>One kind of piece a site asks for (XML): its tag, how many if required, and its weight and cap if optional.</summary>
public sealed class PieceRequestDef
{
    public string tag = "";
    public int count = 1;
    public float weight = 1;
    public int max = 1;

    public PieceRequest ToCore() => new(tag, count, weight, max);
}

/// <summary>
/// An Imperial site archetype (crisis spec §4.1, tech spec §3.3): the world-map site part it makes, the objective
/// building and the room holding it, and what Core's <see cref="SitePlanner"/> needs to plan it. The description is
/// what the director sees for its <c>Site:*</c> id from C11.
/// </summary>
public sealed class SiteArchetypeDef : Def
{
    /// <summary>The archetype's name ("PsychicRelay"): its id is <c>Site:PsychicRelay</c>.</summary>
    public string archetype = "";

    public SitePartDef? sitePart;
    public ThingDef? objectiveThing;
    public string objectiveTag = "";
    public IntVec2 size = new(40, 40);
    public List<PieceRequestDef> required = [];
    public List<PieceRequestDef> optional = [];
    public string defenseTag = "Line";
    public int gates = 1;
    public FloatRange budget = new(500, 3000);
    public float pieceShare = 0.5f;
    public float optionalChance = 0.7f;
    public CrisisStage minStage = CrisisStage.Probing;
    public float weight = 1;

    /// <summary><c>psykers</c> or <c>strikes</c> (<see cref="CrisisCapability"/>): what destroying the objective
    /// knocks out, for <see cref="capabilityDays"/> days.</summary>
    public string? capability;

    public int capabilityDays;

    /// <summary>Offered only while the Empire holds a settlement it conquered near the colony (Reclaimed Holding).</summary>
    public bool needsHolding;

    public SiteArchetype ToCore() => new()
    {
        Name = archetype,
        Desc = description ?? "",
        Width = size.x,
        Height = size.z,
        ObjectiveTag = objectiveTag,
        Required = (required ?? []).Select(r => r.ToCore()).ToList(),
        Optional = (optional ?? []).Select(r => r.ToCore()).ToList(),
        DefenseTag = defenseTag,
        Gates = gates,
        MinBudget = budget.min,
        MaxBudget = budget.max,
        PieceShare = pieceShare,
        OptionalChance = optionalChance,
        MinStage = minStage,
        Weight = weight,
        Capability = string.IsNullOrEmpty(capability) ? null : capability,
        CapabilityDays = capabilityDays,
        NeedsHolding = needsHolding,
    };

    public static SiteArchetypeDef? For(SiteArchetype archetype) =>
        DefDatabase<SiteArchetypeDef>.AllDefsListForReading.FirstOrDefault(d => d.archetype == archetype.Name);

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
        {
            yield return error;
        }

        if (archetype.NullOrEmpty())
        {
            yield return "archetype is empty";
        }

        if (sitePart is null)
        {
            yield return "sitePart is missing";
        }

        if (objectiveThing is null || objectiveTag.NullOrEmpty())
        {
            yield return "objectiveThing and objectiveTag are both needed";
        }

        if (capability is { Length: > 0 } c && c != CrisisCapability.Psykers && c != CrisisCapability.Strikes)
        {
            yield return $"capability '{c}' isn't {CrisisCapability.Psykers} or {CrisisCapability.Strikes}";
        }
    }
}
