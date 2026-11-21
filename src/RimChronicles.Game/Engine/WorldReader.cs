using RimChronicles.Core.Engine;
using RimChronicles.Core.Snapshots;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>Reads the running game into a <see cref="WorldSnapshot"/> (spike test 5). Main thread only.
/// Stage 1 fills the fields the harness tasks use; plots, reporter and bulletins come with later stages.</summary>
public sealed class WorldReader : IWorldReader
{
    public static readonly WorldReader Instance = new();

    private const int MaxTales = 15;
    private const int TaleWindowDays = 60;
    private const int MaxRecentEvents = 10;
    private const int MaxNewsDays = 15;

    public WorldSnapshot Read()
    {
        var map = Find.AnyPlayerHomeMap;
        var colonists = PawnsFinder.AllMaps_FreeColonists;
        var day = GenDate.DaysPassed;

        var moods = colonists.Select(p => p.needs?.mood?.CurLevel).Where(m => m.HasValue).Select(m => m!.Value).ToList();

        return new WorldSnapshot
        {
            Id = SnapshotIds.ForExport(Faction.OfPlayer.Name, day),
            Day = day,
            Season = map is null ? "Aprimay" : GenLocalDate.Twelfth(map).GetQuadrum().ToString(),
            Year = map is null ? 5500 : GenLocalDate.Year(map),
            Colony = new ColonyInfo
            {
                Name = Faction.OfPlayer.Name,
                Wealth = (int)WealthUtility.PlayerWealth,
                Population = colonists.Count,
                Mood = ColonyMood.FromAverage(moods.Count == 0 ? null : moods.Average()),
            },
            Pawns = colonists.Select(p => ReadPawn(p, colonists)).ToList(),
            Tales = ReadTales(day),
            Factions = ReadFactions(),
            RecentEvents = ReadRecentEvents(),
            PublicEvents = RimChroniclesGameComponent.Current?.News.Since(day - MaxNewsDays).Select(e => e.Text).ToList() ?? [],
        };
    }

    private static PawnInfo ReadPawn(Pawn pawn, List<Pawn> colonists) => new()
    {
        Id = pawn.ThingID,
        Name = pawn.Name?.ToStringShort ?? pawn.LabelShort,
        Age = pawn.ageTracker?.AgeBiologicalYears ?? 0,
        Traits = pawn.story?.traits?.allTraits.Select(t => t.LabelCap).ToList() ?? [],
        Backstory = (pawn.story?.Adulthood ?? pawn.story?.Childhood)?.TitleCapFor(pawn.gender),
        Relations = ReadRelations(pawn, colonists),
        Status = pawn.Dead ? "dead" : "alive",
    };

    /// <summary>"What this pawn is to each other colonist", e.g. "son of Human905". <c>other.GetRelations(pawn)</c>
    /// gives what <paramref name="pawn"/> is to <c>other</c> (the Social tab reads it the same way), including implied
    /// relations such as child or sibling.</summary>
    private static List<string> ReadRelations(Pawn pawn, List<Pawn> colonists) =>
        colonists
            .Where(other => other != pawn)
            .SelectMany(other => other.GetRelations(pawn).Select(def => $"{def.GetGenderSpecificLabel(pawn)} of {other.ThingID}"))
            .Distinct()
            .ToList();

    private static List<TaleInfo> ReadTales(int today)
    {
        var start = Find.TickManager.gameStartAbsTick;
        return Find.TaleManager.AllTalesListForReading
            .Where(t => t.date >= 0)
            .Select(t => (Tale: t, Day: (t.date - start) / GenDate.TicksPerDay))
            .Where(x => x.Day >= today - TaleWindowDays)
            .OrderByDescending(x => x.Tale.date)
            .Select(x => ReadTale(x.Tale, x.Day))
            .OfType<TaleInfo>()
            .Distinct() // a fight logs one Wounded tale per hit
            .Take(MaxTales)
            .ToList();
    }

    private static TaleInfo? ReadTale(Tale tale, int day) => tale switch
    {
        Tale_DoublePawn d when d.firstPawnData is not null => new TaleInfo
        {
            Day = day,
            Type = tale.def.defName,
            Actor = TalePawnName(d.firstPawnData),
            Victim = d.secondPawnData is null ? null : TalePawnName(d.secondPawnData),
        },
        Tale_SinglePawn s when s.pawnData is not null => new TaleInfo
        {
            Day = day,
            Type = tale.def.defName,
            Actor = TalePawnName(s.pawnData),
        },
        _ => null,
    };

    /// <summary>A named pawn's short name, else its kind ("boomalope", "pirate"), for animals and unnamed raiders.</summary>
    private static string TalePawnName(TaleData_Pawn data) =>
        data.name?.ToStringShort ?? data.kind?.label ?? "someone";

    private static List<FactionInfo> ReadFactions()
    {
        var settlements = Find.WorldObjects.Settlements;
        return Find.FactionManager.AllFactionsVisible
            .Where(f => !f.IsPlayer && !f.defeated)
            .Select(f => new FactionInfo
            {
                Name = f.Name,
                Kind = f.def.label,
                Goodwill = f.PlayerGoodwill,
                Settlements = settlements.Count(s => s.Faction == f),
            })
            .ToList();
    }

    private static List<string> ReadRecentEvents()
    {
        var archive = Find.Archive;
        if (archive is null)
        {
            return [];
        }

        return archive.ArchivablesListForReading
            .Where(a => !(a.ArchivedLabel ?? "").StartsWith("Rim Chronicles", StringComparison.Ordinal)) // our own letters
            .OrderByDescending(a => a.CreatedTicksGame)
            .Take(MaxRecentEvents)
            .Select(a => $"{EventText.Shorten(a.ArchivedLabel ?? "")} (day {a.CreatedTicksGame / GenDate.TicksPerDay})")
            .ToList();
    }
}
