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

    // The Hollow Choir's operations (War in Heaven spec §4.3, tech spec §4.3): the mirror of the Empire's, with mechs
    // and mechanitors. Retrieval keeps its name; the rest are the Choir's own.
    public const string ProbingSwarm = "ProbingSwarm";
    public const string TunnelBreach = "TunnelBreach";
    public const string TheHum = "TheHum";
    public const string ClusterSiege = "ClusterSiege";
    public const string Burrowing = "Burrowing";
    public const string FinalChorus = "FinalChorus";

    public static readonly IReadOnlyList<OperationSpec> Choir =
    [
        new(ProbingSwarm, CrisisStage.Probing, 3, 0, "a small swarm of Choir mechs tests the colony's defenses, then pulls back to report"),
        new(TunnelBreach, CrisisStage.Reclamation, 2, 10, "tunnelling mechs come up under the walls and turrets; the main force follows"),
        new(TheHum, CrisisStage.Reclamation, 2, 10, "Choir resonators fill the air with the hum, and its mechanitors attack while the colony reels"),
        new(Retrieval, CrisisStage.Reclamation, 3, 12, "a strike force comes for a Choir captive or defector the colony holds") { NeedsCaptive = true },
        new(ClusterSiege, CrisisStage.War, 2, 20, "the Choir drops a mech cluster near the colony and grows it into a siege"),
        new(Burrowing, CrisisStage.War, 1, 20, "mechs burrow up near the colony's leader"),
        new(FinalChorus, CrisisStage.Finale, 4, 15, "the First Voice leads every mech of the Choir against the colony") { FinaleOnly = true },
    ];

    /// <summary>True for a siege, which a second director call gives a <c>Goal:*</c> (crisis tech spec R8): the Empire's
    /// siege with bombardment, or the Choir's cluster siege.</summary>
    public static bool IsSiege(string step) => step is SiegeWithBombardment or ClusterSiege;

    /// <summary>The operations of <paramref name="power"/>'s crisis.</summary>
    public static IReadOnlyList<OperationSpec> For(Powers.Power power) => power == Powers.Power.Choir ? Choir : All;

    public static OperationSpec? Find(string step, Powers.Power power) => For(power).FirstOrDefault(o => o.Step == step);
}
