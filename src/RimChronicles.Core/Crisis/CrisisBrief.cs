using RimChronicles.Core.Snapshots;

namespace RimChronicles.Core.Crisis;

/// <summary>Builds the snapshot's <see cref="CrisisInfo"/> from the crisis state (crisis C11, tech spec §5).</summary>
public static class CrisisBrief
{
    /// <summary>The operations the brief shows, newest kept.</summary>
    public const int MaxOperations = 4;

    public const int MaxPeople = 4;
    public const int MaxIntel = 3;

    /// <summary>The brief, or null with no crisis. <paramref name="present"/> says whether a captive or defector (by
    /// pawn id) is still in the colony: released, dead or departed ones aren't shown.</summary>
    public static CrisisInfo? From(CrisisState state, CrisisStage stage, int today, Func<string, bool> present)
    {
        if (stage == CrisisStage.None)
        {
            return null;
        }

        var defectors = state.Defectors.Where(d => present(d.PawnId)).ToList();
        var defectorIds = defectors.Select(d => d.PawnId).ToHashSet(StringComparer.Ordinal);
        return new CrisisInfo
        {
            Stage = stage.ToString(),
            Operations = state.History.TakeLast(MaxOperations)
                .Select(h => new CrisisOperationInfo { Day = h.Day, Operation = h.Operation, Outcome = h.Outcome })
                .ToList(),
            Disabled = state.Holds.Where(h => today < h.UntilDay).OrderBy(h => h.Capability, StringComparer.Ordinal)
                .Select(h => new CrisisHoldInfo { Capability = h.Capability, UntilDay = h.UntilDay })
                .ToList(),
            Captives = state.Captives.Where(c => !defectorIds.Contains(c.PawnId) && present(c.PawnId)).TakeLast(MaxPeople)
                .Select(c => Person(c.Name, c.Rank))
                .ToList(),
            Defectors = defectors.TakeLast(MaxPeople).Select(d => Person(d.Name, d.Rank)).ToList(),
            Intel = state.LiveIntel(today).Where(i => i.Text.Length > 0).TakeLast(MaxIntel).Select(i => i.Text).ToList(),
        };
    }

    private static CrisisPersonInfo Person(string name, Captives.CaptiveRank rank) =>
        new() { Name = name, Rank = rank.ToString().ToLowerInvariant() };

    /// <summary>What a stage means, for the director's summary.</summary>
    public static string Meaning(string stage) => stage switch
    {
        nameof(CrisisStage.Probing) => "the Empire is testing the colony's defenses with small raids",
        nameof(CrisisStage.Reclamation) => "the Empire is striking at the colony with breachers, psykers and retrieval squads",
        nameof(CrisisStage.War) => "open war: sieges, orbital strikes and attacks on the colony's leaders",
        nameof(CrisisStage.Finale) => "the Empire's last stand: the final assault can come at any time",
        _ => stage,
    };

    /// <summary>What a stage of the Hollow Choir's crisis means (War in Heaven W12).</summary>
    public static string ChoirMeaning(string stage) => stage switch
    {
        nameof(CrisisStage.Probing) => "the Choir is testing the colony's defenses with small swarms of mechs",
        nameof(CrisisStage.Reclamation) => "the Choir is striking at the colony with tunnelling mechs, the hum and retrieval squads",
        nameof(CrisisStage.War) => "open war: cluster sieges, cluster drops and mechs burrowing up beside the colony's leaders",
        nameof(CrisisStage.Finale) => "the Choir's last stand: the First Voice's final assault can come at any time",
        _ => stage,
    };

    /// <summary>A knocked-out capability of the Choir's, as the summary says it.</summary>
    public static string ChoirHold(CrisisHoldInfo hold) => hold.Capability switch
    {
        CrisisCapability.Psykers => $"the Choir's hum is silenced until day {hold.UntilDay} (a resonator was destroyed)",
        CrisisCapability.Strikes => $"no cluster drops until day {hold.UntilDay} (a signal spire was destroyed)",
        _ => $"{hold.Capability} knocked out until day {hold.UntilDay}",
    };

    /// <summary>A knocked-out capability, as the summary says it.</summary>
    public static string Hold(CrisisHoldInfo hold) => hold.Capability switch
    {
        CrisisCapability.Psykers => $"Imperial psykers are weakened until day {hold.UntilDay} (a psychic relay was destroyed)",
        CrisisCapability.Strikes => $"no orbital strikes until day {hold.UntilDay} (an orbital uplink was destroyed)",
        _ => $"{hold.Capability} knocked out until day {hold.UntilDay}",
    };
}
