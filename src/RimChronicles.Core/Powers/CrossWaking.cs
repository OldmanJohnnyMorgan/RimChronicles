namespace RimChronicles.Core.Powers;

/// <summary>
/// When one power wakes the other (War in Heaven spec §4, §5): once a power is awake and has attacked anyone (taken a
/// settlement, sent an operation or a raid against the colony), a rival that is still dormant or stirring wakes at once,
/// skipping its stirring. A power whose story has ended wakes no one and isn't woken.
/// </summary>
public static class CrossWaking
{
    public static bool RivalShouldWake(PowerSaga attacker, bool attackerHasAttacked, PowerSaga rival) =>
        attackerHasAttacked
        && attacker.State is PowerState.Awakened or PowerState.Ascendant
        && attacker.Ending.Length == 0
        && rival.State is PowerState.Dormant or PowerState.Stirring
        && rival.Ending.Length == 0;

    /// <summary>Whether a power has attacked anyone yet: an expansion, a raid against a ship launch or a Royal Ascent,
    /// or (from the game) a crisis operation.</summary>
    public static bool HasAttacked(PowerSaga saga, bool crisisOperations = false) =>
        saga.Expansions.Count > 0 || saga.NextShipRaidDay >= 0 || crisisOperations;
}
