using RimChronicles.Core.Crisis;
using RimChronicles.Core.Plots.Nemesis;

namespace RimChronicles.Core.Plots;

/// <summary>The pawns the mod needs to keep: one list, so the game's tracked-pawn list and the world-pawn cleanup
/// can't drift apart. Ids are RimWorld thing ids.</summary>
public static class KeptPawns
{
    /// <summary>Every pawn the mod still refers to, living or dead: grudge killers and victims, any pawn an active
    /// plot names (facts, protagonist, co-lead), and each crisis's finale leader once made (the High Stellarch, the
    /// First Voice). The game's tracked-pawn list keeps these and drops the rest.</summary>
    public static HashSet<string> Referenced(PlotBook plots, GrudgeLedger grudges, params CrisisState[] crises)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grudge in grudges.Grudges)
        {
            ids.Add(grudge.KillerId);
            ids.Add(grudge.VictimId);
        }

        foreach (var plot in plots.Active)
        {
            ids.UnionWith(plot.Facts.Select(f => f.Value));
            ids.Add(plot.Protagonist.PawnId);
            ids.Add(plot.Protagonist.CoLeadId);
        }

        ids.UnionWith(Leaders(crises, fallenToo: true));
        ids.Remove("");
        return ids;
    }

    /// <summary>The living pawns the world-pawn cleanup must keep: raiders with an open grudge, any pawn an active plot
    /// names, and each crisis's finale leader while still in the war (a leader who escapes leaves the map as a world
    /// pawn and must be there to be met again).</summary>
    public static HashSet<string> KeepAlive(PlotBook plots, GrudgeLedger grudges, params CrisisState[] crises)
    {
        var ids = new HashSet<string>(grudges.TrackedKillers, StringComparer.Ordinal);
        foreach (var plot in plots.Active)
        {
            ids.UnionWith(plot.Facts.Select(f => f.Value));
        }

        ids.UnionWith(Leaders(crises, fallenToo: false));
        ids.Remove("");
        return ids;
    }

    /// <summary>The finale leaders' ids: those still in the war, or every one ever made.</summary>
    public static IEnumerable<string> Leaders(IEnumerable<CrisisState> crises, bool fallenToo) =>
        crises.Where(c => c.StellarchPawnId.Length > 0 && (fallenToo || !StellarchFate.Fallen(c.StellarchFate)))
            .Select(c => c.StellarchPawnId);
}
