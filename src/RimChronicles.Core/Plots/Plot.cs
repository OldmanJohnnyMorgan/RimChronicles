using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;

namespace RimChronicles.Core.Plots;

/// <summary>A key-value fact a plot remembers (e.g. "nemesis" → a pawn id), saved with it.</summary>
public sealed class PlotFact : IArchivable
{
    public string Key = "";
    public string Value = "";

    public void Expose(IArchive archive)
    {
        archive.Value(ref Key, "key", "");
        archive.Value(ref Value, "value", "");
        Key ??= "";
        Value ??= "";
    }
}

/// <summary>One running arc, saved with the game.</summary>
public sealed class Plot : IArchivable
{
    /// <summary>"nemesis-1", unique within the save.</summary>
    public string Id = "";

    public string Template = "";

    public PlotBeat Beat = PlotBeat.Hook;

    /// <summary>The day the current beat fired, or -1 if the hook hasn't fired yet.</summary>
    public int BeatDay = -1;

    /// <summary>The earliest day the next beat may fire.</summary>
    public int NextBeatDay;

    public int StartDay;

    public ProtagonistSlot Protagonist = new();

    public List<PlotFact> Facts = [];

    /// <summary>"Day N: what happened and why", oldest first. Doubles as the director's decision log.</summary>
    public List<string> Log = [];

    /// <summary>How it ended ("nemesis killed", "dropped"), once <see cref="Beat"/> is <see cref="PlotBeat.Ended"/>.</summary>
    public string Outcome = "";

    /// <summary>What has become public about the plot, for the reporter; "" while it's private.</summary>
    public string PublicHint = "";

    public bool Ended => Beat == PlotBeat.Ended;

    public string? Fact(string key) => Facts.FirstOrDefault(f => f.Key == key)?.Value;

    public void SetFact(string key, string value)
    {
        if (Facts.FirstOrDefault(f => f.Key == key) is { } fact)
        {
            fact.Value = value;
        }
        else
        {
            Facts.Add(new PlotFact { Key = key, Value = value });
        }
    }

    public void Note(int day, string line) => Log.Add($"Day {day}: {line}");

    /// <summary>The snapshot view of this plot, for the director and the reporter.</summary>
    public PlotInfo ToInfo() => new()
    {
        Id = Id,
        Template = Template,
        Beat = Beat.ToString(),
        Protagonist = Protagonist.Kind == ProtagonistKind.Colony ? Inheritance.ColonyId : Protagonist.PawnId,
        Summary = Log.Count == 0 ? null : string.Join(" ", Log.TakeLast(3)),
        PublicHint = PublicHint.Length == 0 ? null : PublicHint,
    };

    public void Expose(IArchive archive)
    {
        archive.Value(ref Id, "id", "");
        archive.Value(ref Template, "template", "");
        archive.Value(ref Beat, "beat", PlotBeat.Hook);
        archive.Value(ref BeatDay, "beatDay", -1);
        archive.Value(ref NextBeatDay, "nextBeatDay");
        archive.Value(ref StartDay, "startDay");
        archive.Deep(ref Protagonist, "protagonist");
        archive.DeepList(ref Facts, "facts");
        archive.Strings(ref Log, "log");
        archive.Value(ref Outcome, "outcome", "");
        archive.Value(ref PublicHint, "publicHint", "");
        Id ??= "";
        Template ??= "";
        Outcome ??= "";
        PublicHint ??= "";
    }
}
