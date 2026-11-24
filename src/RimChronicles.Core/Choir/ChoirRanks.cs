using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Choir;

/// <summary>One colonist's standing in the Chorus.</summary>
public sealed class ChoirStanding : IArchivable
{
    public string PawnId = "";
    public string Name = "";
    public int Favor;

    public void Expose(IArchive archive)
    {
        archive.Value(ref PawnId, "pawnId", "");
        archive.Value(ref Name, "name", "");
        archive.Value(ref Favor, "favor");
        PawnId ??= "";
        Name ??= "";
    }
}

/// <summary>
/// The Chorus's rank ladder (War in Heaven spec §3, D13): the mirror of Royal titles. Each job a colony allied with the
/// Choir finishes for it gives favor to the colonist who already has the most (so service builds toward a rank, as with
/// Imperial favor). A colonist at <see cref="HighRank"/> or above opens the First Voice's offer in Act 3, as a Knight
/// opens the Empire's governorship. Saved with the game.
/// </summary>
public sealed class ChoirRanks : IArchivable
{
    /// <summary>The ranks and the favor each needs, lowest first.</summary>
    public static readonly IReadOnlyList<(string Name, int Favor)> Ladder =
    [
        ("Listener", 0),
        ("Chorister", 4),
        ("Choirmaster", 12),
        ("Cantor", 24),
        ("Voice of the Choir", 40),
    ];

    /// <summary>The rank that opens the First Voice's offer: Choirmaster, the mirror of a Knight.</summary>
    public const int HighRank = 2;

    /// <summary>Favor per job done for the Choir, as the Empire grants per job.</summary>
    public const int FavorPerJob = 4;

    public List<ChoirStanding> Standings = [];

    public static int RankFor(int favor)
    {
        var rank = 0;
        for (var i = 0; i < Ladder.Count; i++)
        {
            if (favor >= Ladder[i].Favor)
            {
                rank = i;
            }
        }

        return rank;
    }

    public int RankOf(string pawnId) => Standings.FirstOrDefault(s => s.PawnId == pawnId) is { } s ? RankFor(s.Favor) : 0;

    /// <summary>Gives favor for a job to the colonist among <paramref name="colonists"/> (id, name) who already has the
    /// most (ties: the first by id). Returns the new rank's name if that colonist rose a rank, else null.</summary>
    public string? GrantForJob(IReadOnlyList<(string Id, string Name)> colonists, int amount = FavorPerJob)
    {
        if (colonists.Count == 0)
        {
            return null;
        }

        var best = colonists
            .OrderByDescending(c => Standings.FirstOrDefault(s => s.PawnId == c.Id)?.Favor ?? 0)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .First();
        var standing = Standings.FirstOrDefault(s => s.PawnId == best.Id);
        if (standing is null)
        {
            standing = new ChoirStanding { PawnId = best.Id };
            Standings.Add(standing);
        }

        standing.Name = best.Name;
        var before = RankFor(standing.Favor);
        standing.Favor += amount;
        var after = RankFor(standing.Favor);
        return after > before ? Ladder[after].Name : null;
    }

    /// <summary>The name of the highest-ranked colonist at <see cref="HighRank"/> or above among
    /// <paramref name="colonistIds"/> (alive and in the colony), or "".</summary>
    public string HighRankHolder(IReadOnlyCollection<string> colonistIds) =>
        Standings
            .Where(s => colonistIds.Contains(s.PawnId) && RankFor(s.Favor) >= HighRank)
            .OrderByDescending(s => s.Favor).ThenBy(s => s.PawnId, StringComparer.Ordinal)
            .Select(s => s.Name)
            .FirstOrDefault() ?? "";

    public void Expose(IArchive archive)
    {
        archive.DeepList(ref Standings, "standings");
        Standings ??= [];
    }
}
