namespace RimChronicles.Core.Plots;

/// <summary>A possible heir, as plain data read from the game.</summary>
/// <param name="Id">ThingID.</param>
/// <param name="IsBloodKin">Parent, child, sibling, grandparent, grandchild, cousin, aunt or uncle of the old protagonist.</param>
/// <param name="IsPartner">Spouse, fiancé(e) or lover.</param>
/// <param name="IsBondedAnimal">An animal bonded to the old protagonist.</param>
/// <param name="Opinion">This pawn's opinion of the old protagonist (humans only; -100 to 100).</param>
public sealed record HeirCandidate(string Id, string Name, bool IsBloodKin, bool IsPartner, bool IsBondedAnimal, int Opinion);

/// <summary>Who inherits, and by which rule.</summary>
public sealed record HeirChoice(ProtagonistKind Kind, string Id, string Name, string Rule, string CoLeadId = "", string CoLeadName = "");

/// <summary>The design spec's inheritance order: blood kin, then spouse or lover, then a bonded animal (with a human
/// co-lead picked by the next rule), then the colonist with the highest opinion of the protagonist, then the colony.</summary>
public static class Inheritance
{
    public const string ColonyId = "colony";

    /// <summary>Picks the heir among living, free colonists and the protagonist's bonded animals. Ties break by highest
    /// opinion, then by id, so the result is stable.</summary>
    public static HeirChoice PickHeir(IReadOnlyList<HeirCandidate> candidates, string colonyName)
    {
        var humans = candidates.Where(c => !c.IsBondedAnimal).OrderByDescending(c => c.Opinion).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

        if (humans.FirstOrDefault(c => c.IsBloodKin) is { } kin)
        {
            return new(ProtagonistKind.Pawn, kin.Id, kin.Name, "blood kin");
        }

        if (humans.FirstOrDefault(c => c.IsPartner) is { } partner)
        {
            return new(ProtagonistKind.Pawn, partner.Id, partner.Name, "spouse or lover");
        }

        var coLead = humans.FirstOrDefault();
        if (candidates.Where(c => c.IsBondedAnimal).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault() is { } animal && coLead is not null)
        {
            return new(ProtagonistKind.Animal, animal.Id, animal.Name, "bonded animal", coLead.Id, coLead.Name);
        }

        if (coLead is not null)
        {
            return new(ProtagonistKind.Pawn, coLead.Id, coLead.Name, "highest opinion");
        }

        return new(ProtagonistKind.Colony, ColonyId, colonyName, "the colony");
    }

    /// <summary>Moves the slot to the heir, recording the old protagonist in its history.</summary>
    public static void PassTo(ProtagonistSlot slot, HeirChoice heir, string howLost)
    {
        slot.History.Add($"{slot.Name} ({howLost})");
        slot.Kind = heir.Kind;
        slot.PawnId = heir.Id;
        slot.Name = heir.Name;
        slot.CoLeadId = heir.CoLeadId;
        slot.CoLeadName = heir.CoLeadName;
    }
}
