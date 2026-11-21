namespace RimChronicles.Core.Snapshots;

/// <summary>
/// One moment in a colony's game: the single input type for everything above the engine adapter.
/// Hand-written for the Stage 0 harness, later exported from real saves. The JSON contract is
/// harness/schema/snapshot.schema.json.
/// </summary>
public sealed record WorldSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public required string Id { get; init; }

    /// <summary>Days since the game started.</summary>
    public required int Day { get; init; }

    /// <summary>The quadrum name, e.g. "Aprimay".</summary>
    public required string Season { get; init; }

    public required int Year { get; init; }
    public required ColonyInfo Colony { get; init; }

    /// <summary>Notable colonists (may be fewer than the population).</summary>
    public IReadOnlyList<PawnInfo> Pawns { get; init; } = [];

    /// <summary>Tale-system records worth a story, oldest first.</summary>
    public IReadOnlyList<TaleInfo> Tales { get; init; } = [];

    public IReadOnlyList<FactionInfo> Factions { get; init; } = [];
    public IReadOnlyList<PlotInfo> ActivePlots { get; init; } = [];

    /// <summary>Colony events, oldest first. Not known to the reporter.</summary>
    public IReadOnlyList<string> RecentEvents { get; init; } = [];

    /// <summary>World events anyone on the rim could hear about, oldest first. The reporter's sources.</summary>
    public IReadOnlyList<string> PublicEvents { get; init; } = [];

    /// <summary>Public news the reporter's earlier bulletins already covered: entries of <see cref="PublicEvents"/>
    /// or a plot's public hint, word for word. In the game the mod records what each bulletin used.</summary>
    public IReadOnlyList<string> ReportedEvents { get; init; } = [];

    /// <summary>The director's legal options right now. Always includes "Wait".</summary>
    public IReadOnlyList<string> AllowedTransitions { get; init; } = [];

    /// <summary>The reporter's last bulletins, oldest first (at most two are used).</summary>
    public IReadOnlyList<string> PreviousBulletins { get; init; } = [];

    public ReporterInfo? Reporter { get; init; }

    /// <summary>An inheritance case for the handoff task, when the snapshot has one.</summary>
    public HandoffCase? Handoff { get; init; }

    /// <summary>A plot letter to write (the plot-letter task), or null.</summary>
    public PlotLetterCase? PlotLetter { get; init; }
}

public sealed record ColonyInfo
{
    public required string Name { get; init; }
    public required int Wealth { get; init; }
    public required int Population { get; init; }

    /// <summary>"content", "strained" or "desperate".</summary>
    public required string Mood { get; init; }
}

public sealed record PawnInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Age { get; init; }
    public IReadOnlyList<string> Traits { get; init; } = [];
    public string? Backstory { get; init; }

    /// <summary>Plain phrases that may name other pawns by id, e.g. "spouse of p5".</summary>
    public IReadOnlyList<string> Relations { get; init; } = [];

    public string? IdeoRole { get; init; }

    /// <summary>"alive", "dead", "captured" or "left".</summary>
    public string Status { get; init; } = "alive";

    /// <summary>Optional one-line description, e.g. "the colony's only doctor".</summary>
    public string? Note { get; init; }
}

public sealed record TaleInfo
{
    public required int Day { get; init; }

    /// <summary>Tale type, e.g. "KilledColonist".</summary>
    public required string Type { get; init; }

    /// <summary>Who did it: a pawn id or an outsider's name.</summary>
    public required string Actor { get; init; }

    /// <summary>Who it happened to: a pawn id or a name.</summary>
    public string? Victim { get; init; }

    public bool? Escaped { get; init; }
}

public sealed record FactionInfo
{
    public required string Name { get; init; }

    /// <summary>e.g. "Empire", "Pirates", "Outlanders", "Tribe".</summary>
    public string? Kind { get; init; }

    /// <summary>Goodwill toward the colony, -100 to 100.</summary>
    public required int Goodwill { get; init; }

    /// <summary>The Empire's major-plot state, for the Empire faction only.</summary>
    public string? PlotState { get; init; }

    public required int Settlements { get; init; }
    public IReadOnlyList<string> AtWarWith { get; init; } = [];
}

public sealed record PlotInfo
{
    public required string Id { get; init; }

    /// <summary>e.g. "Nemesis", "LostFamily", "AwakenedEmpire".</summary>
    public required string Template { get; init; }

    /// <summary>"Hook", "Complication", "Choice" or "Resolution" (or an Empire act/state).</summary>
    public required string Beat { get; init; }

    /// <summary>Protagonist pawn id, or "colony" for a collective plot.</summary>
    public required string Protagonist { get; init; }

    /// <summary>What the plot is about so far, for the director.</summary>
    public string? Summary { get; init; }

    /// <summary>The part of the plot that has become public, if any (the reporter may use it).</summary>
    public string? PublicHint { get; init; }
}

public sealed record ReporterInfo
{
    public required string Name { get; init; }
    public required string Outlet { get; init; }
    public required string Persona { get; init; }
}

/// <summary>A dead protagonist and the heir who takes up their plot.</summary>
public sealed record HandoffCase
{
    public required string DeadPawn { get; init; }
    public required string Heir { get; init; }

    /// <summary>What the heir was to the dead pawn, e.g. "wife", "daughter", "closest friend".</summary>
    public required string Relation { get; init; }

    public required string PlotSummary { get; init; }
}

/// <summary>One plot letter: which arc and beat, and the facts it may use (Stage 3 M3b).</summary>
public sealed record PlotLetterCase
{
    /// <summary>The arc, e.g. "Nemesis".</summary>
    public required string Template { get; init; }

    /// <summary>The beat's step, e.g. "Sighting", "Raid", "Choice", "SecondRaid" or "Ending:KilledByColony".</summary>
    public required string Step { get; init; }

    public required string Protagonist { get; init; }

    /// <summary>The plot's facts by key (nemesisName, nemesisFaction, victimName, relation, grievance, part, place,
    /// bountyCost).</summary>
    public IReadOnlyDictionary<string, string> Facts { get; init; } = new Dictionary<string, string>();
}
