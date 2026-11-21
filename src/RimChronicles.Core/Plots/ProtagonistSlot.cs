using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Plots;

/// <summary>Who a protagonist slot can hold.</summary>
public enum ProtagonistKind
{
    /// <summary>A colonist.</summary>
    Pawn,

    /// <summary>A bonded animal, which gets a narrated role; a human co-lead carries the action.</summary>
    Animal,

    /// <summary>The colony itself, as a collective grudge, when no one is left to inherit.</summary>
    Colony,
}

/// <summary>A plot belongs to a slot, not a fixed pawn: when the protagonist dies, leaves or is captured, the slot
/// passes to an heir (<see cref="Inheritance"/>).</summary>
public sealed class ProtagonistSlot : IArchivable
{
    public ProtagonistKind Kind = ProtagonistKind.Pawn;

    /// <summary>The pawn's ThingID, or "colony".</summary>
    public string PawnId = "";

    public string Name = "";

    /// <summary>For an animal protagonist, the human who carries the action; "" otherwise.</summary>
    public string CoLeadId = "";

    public string CoLeadName = "";

    /// <summary>Earlier protagonists, oldest first, as "Name (how the slot passed on)".</summary>
    public List<string> History = [];

    public static ProtagonistSlot ForPawn(string pawnId, string name) => new() { PawnId = pawnId, Name = name };

    public void Expose(IArchive archive)
    {
        archive.Value(ref Kind, "kind", ProtagonistKind.Pawn);
        archive.Value(ref PawnId, "pawnId", "");
        archive.Value(ref Name, "name", "");
        archive.Value(ref CoLeadId, "coLeadId", "");
        archive.Value(ref CoLeadName, "coLeadName", "");
        archive.Strings(ref History, "history");
        PawnId ??= "";
        Name ??= "";
        CoLeadId ??= "";
        CoLeadName ??= "";
    }
}
