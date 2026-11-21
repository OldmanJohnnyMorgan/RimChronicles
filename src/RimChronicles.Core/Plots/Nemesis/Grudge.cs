using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Plots.Nemesis;

/// <summary>What a raider did to a colonist.</summary>
public static class GrudgeKind
{
    public const string Killed = "killed";
    public const string Maimed = "maimed";
}

/// <summary>A raider who killed or maimed a colonist. It can seed a Nemesis plot once the raider has escaped.</summary>
public sealed class Grudge : IArchivable
{
    public string KillerId = "";
    public string KillerName = "";
    public string KillerFaction = "";
    public string VictimId = "";
    public string VictimName = "";

    /// <summary><see cref="GrudgeKind.Killed"/> or <see cref="GrudgeKind.Maimed"/>.</summary>
    public string Kind = GrudgeKind.Killed;

    /// <summary>For a maiming, the body part lost ("arm"); "" for a killing.</summary>
    public string Part = "";

    public int Day;

    /// <summary>True once the raider has left the map alive and free.</summary>
    public bool Escaped;

    /// <summary>The plot this grudge seeded, or "" if none yet.</summary>
    public string PlotId = "";

    public void Expose(IArchive archive)
    {
        archive.Value(ref KillerId, "killerId", "");
        archive.Value(ref KillerName, "killerName", "");
        archive.Value(ref KillerFaction, "killerFaction", "");
        archive.Value(ref VictimId, "victimId", "");
        archive.Value(ref VictimName, "victimName", "");
        archive.Value(ref Kind, "kind", GrudgeKind.Killed);
        archive.Value(ref Part, "part", "");
        archive.Value(ref Day, "day");
        archive.Value(ref Escaped, "escaped");
        archive.Value(ref PlotId, "plotId", "");
        KillerId ??= "";
        KillerName ??= "";
        KillerFaction ??= "";
        VictimId ??= "";
        VictimName ??= "";
        Kind ??= GrudgeKind.Killed;
        Part ??= "";
        PlotId ??= "";
    }
}

/// <summary>Every open grudge in the save. Grudges whose raider died or was captured are settled (removed); unused ones
/// expire after <see cref="ExpireDays"/> days.</summary>
public sealed class GrudgeLedger : IArchivable
{
    public const int ExpireDays = 60;

    public List<Grudge> Grudges = [];

    /// <summary>Records a grudge, once per raider, victim and kind. Returns whether it was new.</summary>
    public bool Record(Grudge grudge)
    {
        if (Grudges.Any(g => g.KillerId == grudge.KillerId && g.VictimId == grudge.VictimId && g.Kind == grudge.Kind))
        {
            return false;
        }

        Grudges.Add(grudge);
        return true;
    }

    /// <summary>The raider died or was captured: their unused grudges are settled. Grudges already in a plot stay, so
    /// the plot can resolve.</summary>
    public int Settle(string killerId) => Grudges.RemoveAll(g => g.KillerId == killerId && g.PlotId.Length == 0);

    public void MarkEscaped(string killerId)
    {
        foreach (var g in Grudges.Where(g => g.KillerId == killerId))
        {
            g.Escaped = true;
        }
    }

    public int Expire(int today) => Grudges.RemoveAll(g => g.PlotId.Length == 0 && today - g.Day > ExpireDays);

    /// <summary>How strong a hook the grudge makes, 0 to 1: a killing outweighs a maiming, and both fade with time.</summary>
    public static double Strength(Grudge g, int today)
    {
        var basis = g.Kind == GrudgeKind.Killed ? 0.9 : 0.6;
        return Math.Max(0, basis - 0.005 * Math.Max(0, today - g.Day));
    }

    /// <summary>The strongest escaped grudge not yet in a plot, against a raider who isn't already someone's nemesis.</summary>
    public Grudge? Strongest(int today) =>
        Grudges.Where(g => g.Escaped && g.PlotId.Length == 0 && !Grudges.Any(o => o.KillerId == g.KillerId && o.PlotId.Length > 0))
            .OrderByDescending(g => Strength(g, today))
            .ThenBy(g => g.Day)
            .FirstOrDefault();

    /// <summary>Raiders the game should keep in the world: those with any grudge open.</summary>
    public IEnumerable<string> TrackedKillers => Grudges.Select(g => g.KillerId).Distinct();

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref Grudges, "grudges");
        Grudges.RemoveAll(g => g.KillerId.Length == 0);
    }
}
