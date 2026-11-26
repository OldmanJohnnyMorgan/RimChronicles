using RimChronicles.Core.Crisis.Pieces;
using Verse;

namespace RimChronicles.Game.Crisis.Pieces;

/// <summary>
/// Marks a vanilla <c>PrefabDef</c> as a crisis piece and says how the planners may use it (tech spec §3.1, R2). In XML:
/// <c>&lt;modExtensions&gt;&lt;li Class="RimChronicles.Game.Crisis.Pieces.PieceExtension"&gt;…</c>.
/// </summary>
public sealed class PieceExtension : DefModExtension
{
    public List<string> tags = [];

    /// <summary>North, East, South or West: where the piece connects, before it's rotated.</summary>
    public List<string> doorSides = [];

    public bool needsPower;
    public int defenseValue;
    public float threatCost;

    /// <summary>For sieges: the stage it is built in, 1 to 5.</summary>
    public int buildOrder = 1;

    /// <summary>Whose piece it is (War in Heaven W8): <c>empire</c>, <c>choir</c>, or empty for a shared piece.</summary>
    public string? power;

    public PieceMeta ToCore() => new()
    {
        Tags = tags ?? [],
        DoorSides = (doorSides ?? []).Select(s => Enum.TryParse<Side>(s, ignoreCase: true, out var side) ? side : (Side?)null)
            .OfType<Side>().ToList(),
        NeedsPower = needsPower,
        DefenseValue = defenseValue,
        ThreatCost = threatCost,
        BuildOrder = buildOrder,
        Power = string.IsNullOrEmpty(power) ? null : power,
    };

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
        {
            yield return error;
        }

        foreach (var side in (doorSides ?? []).Where(s => !Enum.TryParse<Side>(s, ignoreCase: true, out _)))
        {
            yield return $"doorSides: '{side}' isn't North, East, South or West";
        }
    }
}
