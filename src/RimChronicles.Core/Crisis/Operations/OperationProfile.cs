namespace RimChronicles.Core.Crisis.Operations;

/// <summary>What a phase of an operation does. The game turns each into a lord toil.</summary>
public enum PhaseKind
{
    /// <summary>Attack the colony.</summary>
    Assault,

    /// <summary>Attack, but only to learn the defenses; it pulls back early.</summary>
    Probe,

    /// <summary>Go for one pawn (the decapitation target).</summary>
    Hunt,

    /// <summary>Breachers open a way through the walls and the rest follow (vanilla's breaching assault).</summary>
    Breach,

    /// <summary>Fall back to the staging point; reinforcements join at the end if the reserve allows.</summary>
    Regroup,

    /// <summary>Leave the map, carrying downed allies where it's safe.</summary>
    Retreat,
}

/// <summary>What moves an operation from one phase to the next.</summary>
public enum PhaseTrigger
{
    /// <summary>The share of the force lost (dead, downed or fled) passes <see cref="PhaseLink.Value"/>.</summary>
    LossesOver,

    /// <summary><see cref="PhaseLink.Value"/> ticks in the phase.</summary>
    TimeUp,

    /// <summary>The hunted pawn is down, dead or gone.</summary>
    TargetDown,

    /// <summary>The crisis is over, or the colony made peace: every raid pulls out.</summary>
    Abandoned,
}

public sealed record PhaseLink(PhaseTrigger Trigger, string To, float Value = 0)
{
    /// <summary>How this ending counts: reaching the goal, being beaten back, or calling it off.</summary>
    public string Outcome { get; init; } = Trigger switch
    {
        PhaseTrigger.LossesOver => OperationOutcome.Repelled,
        PhaseTrigger.Abandoned => OperationOutcome.Aborted,
        _ => OperationOutcome.Succeeded,
    };
}

public sealed record Phase(string Name, PhaseKind Kind, IReadOnlyList<PhaseLink> Links);

/// <summary>
/// An operation's shape (crisis spec §5.2): its phases and what moves it between them, the share of the budget that
/// arrives at once, and the reserve held back for reinforcements. The game builds the lord's state graph from this, so
/// the retreat rules are checked here, on plain data (fairness rule 6).
/// </summary>
public sealed record OperationProfile(string Operation, string Start, IReadOnlyList<Phase> Phases, float ForceShare = 1, float ReserveShare = 0)
{
    public Phase? Find(string name) => Phases.FirstOrDefault(p => p.Name == name);

    /// <summary>The phases reachable from the start.</summary>
    public IReadOnlyList<Phase> Reachable()
    {
        var seen = new List<Phase>();
        var queue = new Queue<string>([Start]);
        while (queue.Count > 0)
        {
            if (Find(queue.Dequeue()) is not { } phase || seen.Contains(phase))
            {
                continue;
            }

            seen.Add(phase);
            foreach (var link in phase.Links)
            {
                queue.Enqueue(link.To);
            }
        }

        return seen;
    }

    /// <summary>True when every reachable phase can still fall back to a retreat after losses (fairness rule 6: no
    /// raid fights to the last pawn, except the final assault).</summary>
    public bool RetreatsOnLosses() => Reachable().All(p => p.Kind == PhaseKind.Retreat || LossPathToRetreat(p.Name, []));

    private bool LossPathToRetreat(string from, HashSet<string> visited)
    {
        if (!visited.Add(from) || Find(from) is not { } phase)
        {
            return false;
        }

        return phase.Links.Any(l => Find(l.To) is { } next
            && ((l.Trigger == PhaseTrigger.LossesOver && next.Kind == PhaseKind.Retreat) || (l.Trigger == PhaseTrigger.LossesOver && LossPathToRetreat(next.Name, visited))));
    }
}

/// <summary>The crisis operations' shapes. Numbers are first guesses for the balance pass (C14).</summary>
public static class OperationProfiles
{
    public const string Retreat = "retreat";

    /// <summary>A probing raid looks for about two in-game hours, then leaves to report.</summary>
    public const int ProbeTicks = 5000;

    public const int HuntTicks = 10000;
    public const int RegroupTicks = 2500;

    private static readonly Phase RetreatPhase = new(Retreat, PhaseKind.Retreat, []);

    public static readonly OperationProfile ProbingRaid = new(CrisisOperations.ProbingRaid, "probe",
    [
        new("probe", PhaseKind.Probe,
        [
            new(PhaseTrigger.LossesOver, Retreat, 0.25f),
            new(PhaseTrigger.TimeUp, Retreat, ProbeTicks),
            new(PhaseTrigger.Abandoned, Retreat),
        ]),
        RetreatPhase,
    ], ForceShare: 0.5f);

    public static readonly OperationProfile Decapitation = new(CrisisOperations.Decapitation, "hunt",
    [
        new("hunt", PhaseKind.Hunt,
        [
            new(PhaseTrigger.TargetDown, Retreat),
            new(PhaseTrigger.LossesOver, Retreat, 0.5f),
            new(PhaseTrigger.TimeUp, Retreat, HuntTicks),
            new(PhaseTrigger.Abandoned, Retreat),
        ]),
        RetreatPhase,
    ], ForceShare: 0.8f);

    /// <summary>The shape most operations share: attack (or breach); regroup once after heavy losses and come back
    /// with the reserve; leave after heavier losses.</summary>
    public static OperationProfile Standard(string operation, PhaseKind first = PhaseKind.Assault) => new(operation, "assault",
    [
        new("assault", first,
        [
            new(PhaseTrigger.LossesOver, "regroup", 0.4f),
            new(PhaseTrigger.Abandoned, Retreat),
        ]),
        new("regroup", PhaseKind.Regroup,
        [
            new(PhaseTrigger.TimeUp, "second", RegroupTicks) { Outcome = OperationOutcome.Running },
            new(PhaseTrigger.LossesOver, Retreat, 0.6f),
            new(PhaseTrigger.Abandoned, Retreat),
        ]),
        new("second", PhaseKind.Assault,
        [
            new(PhaseTrigger.LossesOver, Retreat, 0.6f),
            new(PhaseTrigger.Abandoned, Retreat),
        ]),
        RetreatPhase,
    ], ForceShare: 0.75f, ReserveShare: 0.25f);

    /// <summary>The finale's assault fights to the end; it only pulls out if the crisis itself ends.</summary>
    public static readonly OperationProfile FinalAssault = new(CrisisOperations.FinalAssault, "assault",
    [
        new("assault", PhaseKind.Assault, [new(PhaseTrigger.Abandoned, Retreat)]),
        RetreatPhase,
    ]);

    public static OperationProfile For(string operation) => operation switch
    {
        CrisisOperations.ProbingRaid => ProbingRaid,
        CrisisOperations.Decapitation => Decapitation,
        CrisisOperations.FinalAssault => FinalAssault,
        CrisisOperations.Breach => Standard(operation, PhaseKind.Breach),
        _ => Standard(operation),
    };
}
