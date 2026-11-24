using RimChronicles.Core.Powers;
using RimChronicles.Core.Snapshots;

namespace RimChronicles.Core.War;

/// <summary>Builds the snapshot's <see cref="WarInfo"/> and its lines in the director's summary (War in Heaven W12).</summary>
public static class WarBrief
{
    public static WarInfo From(WarInHeaven war, Allegiance allegiance, IReadOnlyList<Power> powers) => new()
    {
        Started = war.State == WarState.Started,
        StartDay = war.StartDay,
        Side = allegiance.SideAmong(powers),
        EmpireStance = allegiance.StanceToward(Power.Empire),
        ChoirStance = allegiance.StanceToward(Power.Choir),
        Front = war.Front,
        Destroyed = war.Destroyed.ToList(),
    };

    private static string Name(string id) => Power.All.FirstOrDefault(p => p.Id == id)?.DisplayName ?? id;

    public static IEnumerable<string> Lines(WarInfo w)
    {
        yield return w.Started
            ? $"The Empire and the Hollow Choir have been fighting in full since day {w.StartDay}."
            : "The Empire and the Hollow Choir are old enemies, at war in name; the War in Heaven hasn't begun.";
        yield return w.Side switch
        {
            "" => $"The colony has taken no side (the Empire: {w.EmpireStance}; the Hollow Choir: {w.ChoirStance}).",
            Side.Independent => "The colony stands alone, at war with both powers.",
            var side => $"The colony stands with {Name(side)}, and is at war with {Name(side == Power.Empire.Id ? Power.Choir.Id : Power.Empire.Id)}.",
        };
        if (w.Started && w.Side is { Length: > 0 } && w.Side != Side.Independent)
        {
            yield return w.Front switch
            {
                > 0 => $"The front favours the colony's side ({w.Front:+0}).",
                < 0 => $"The front is turning against the colony's side ({w.Front}).",
                _ => "The front is even.",
            };
        }

        foreach (var id in w.Destroyed)
        {
            yield return id == Power.Empire.Id
                ? "The Empire is destroyed: its Citadel has fallen."
                : "The Hollow Choir is destroyed: its Cathedral has fallen.";
        }
    }
}
