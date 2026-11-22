namespace RimChronicles.Core.Crisis;

/// <summary>One crisis operation the commander can run (crisis spec §5.2). <see cref="Step"/> is the operation's own
/// name; <see cref="Id"/> is what the decision log shows, and what the director will see from C11.</summary>
public sealed record OperationSpec(string Step, CrisisStage MinStage, double Weight, int CooldownDays, string Desc)
{
    public string Id => CrisisOperations.Prefix + Step;

    /// <summary>Allowed only while the colony holds an Imperial captive or defector.</summary>
    public bool NeedsCaptive { get; init; }

    /// <summary>Allowed only in the finale.</summary>
    public bool FinaleOnly { get; init; }
}

/// <summary>The crisis spec's operations and the stage each one unlocks at (§6). The weights and cooldowns are first
/// guesses for the balance pass.</summary>
public static class CrisisOperations
{
    public const string Prefix = "Operation:";

    public const string ProbingRaid = "ProbingRaid";
    public const string Breach = "Breach";
    public const string PsychicAssault = "PsychicAssault";
    public const string Retrieval = "Retrieval";
    public const string SiegeWithBombardment = "SiegeWithBombardment";
    public const string Decapitation = "Decapitation";
    public const string FinalAssault = "FinalAssault";

    public static readonly IReadOnlyList<OperationSpec> All =
    [
        new(ProbingRaid, CrisisStage.Probing, 3, 0, "a small Imperial force tests the colony's defenses, then pulls back to report"),
        new(Breach, CrisisStage.Reclamation, 2, 10, "breachers knock out turrets and open the walls; the main force follows"),
        new(PsychicAssault, CrisisStage.Reclamation, 2, 10, "Imperial psykers attack under a Praetorian escort"),
        new(Retrieval, CrisisStage.Reclamation, 3, 12, "a strike force comes for an Imperial captive or defector the colony holds") { NeedsCaptive = true },
        new(SiegeWithBombardment, CrisisStage.War, 2, 20, "the Empire digs in a siege camp near the colony and bombards it"),
        new(Decapitation, CrisisStage.War, 1, 20, "drop pods land near the colony's leader"),
        new(FinalAssault, CrisisStage.Finale, 4, 15, "the High Stellarch leads every arm of the Empire against the colony") { FinaleOnly = true },
    ];

    public static OperationSpec? Find(string step) => All.FirstOrDefault(o => o.Step == step);
}
