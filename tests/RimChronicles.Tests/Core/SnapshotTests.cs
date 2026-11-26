using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RimChronicles.Core.Snapshots;
using RimChronicles.Llm.Validation;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public partial class SnapshotTests
{
    [GeneratedRegex(@"\bp\d+\b")]
    private static partial Regex PawnId();

    private static readonly string[] Quadrums = ["Aprimay", "Jugust", "Septober", "Decembary"];

    public static TheoryData<string> SnapshotFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Repo.Snapshots, "*.json").Order())
        {
            data.Add(System.IO.Path.GetFileName(file));
        }

        return data;
    }

    private static WorldSnapshot Load(string file) => SnapshotJson.Load(System.IO.Path.Combine(Repo.Snapshots, file));

    private static List<WorldSnapshot> All() =>
        Directory.GetFiles(Repo.Snapshots, "*.json").Order().Select(SnapshotJson.Load).ToList();

    [Theory]
    [MemberData(nameof(SnapshotFiles))]
    public void Snapshot_MatchesSchema(string file)
    {
        var schema = (JsonObject)JsonNode.Parse(File.ReadAllText(Repo.SnapshotSchema))!;
        var json = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(Repo.Snapshots, file)));

        Assert.Empty(JsonSchemaValidator.Validate(json, schema));
    }

    [Theory]
    [MemberData(nameof(SnapshotFiles))]
    public void Snapshot_IsInternallyConsistent(string file)
    {
        var s = Load(file);
        var ids = s.Pawns.Select(p => p.Id).ToList();

        Assert.Equal(System.IO.Path.GetFileNameWithoutExtension(file), s.Id);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        if (IsRealExport(s))
        {
            // Exported from a real game, which may start in any quadrum: just a real quadrum and a plausible year.
            Assert.Contains(s.Season, Quadrums);
            Assert.InRange(s.Year, 5500, 5500 + s.Day / 60 + 1);
        }
        else
        {
            Assert.Equal(5500 + s.Day / 60, s.Year);
            Assert.Equal(Quadrums[s.Day % 60 / 15], s.Season);
        }

        Assert.Contains("Wait", s.AllowedTransitions);
        Assert.True(s.Colony.Population >= s.Pawns.Count(p => p.Status == "alive"), "population is below the living pawns listed");

        // Every pawn id mentioned anywhere must exist.
        var mentioned = s.Pawns.SelectMany(p => p.Relations)
            .Concat(s.Tales.SelectMany(t => new[] { t.Actor, t.Victim ?? "" }))
            .Concat(s.ActivePlots.SelectMany(p => new[] { p.Protagonist, p.Summary ?? "" }))
            .Concat(s.AllowedTransitions)
            .SelectMany(text => PawnId().Matches(text).Select(m => m.Value));
        Assert.All(mentioned, id => Assert.Contains(id, ids));

        Assert.All(s.ActivePlots, p => Assert.True(p.Protagonist == "colony" || ids.Contains(p.Protagonist), $"unknown protagonist {p.Protagonist}"));

        if (s.Handoff is { } h)
        {
            Assert.Equal("dead", s.Pawns.Single(p => p.Id == h.DeadPawn).Status);
            Assert.Equal("alive", s.Pawns.Single(p => p.Id == h.Heir).Status);
        }
    }

    [Fact]
    public void SnapshotSet_HasTheCoverageTheSpecRequires()
    {
        var all = All().Where(s => !IsRealExport(s) && !IsCrisis(s) && !IsWar(s) && !IsEnding(s)).ToList();
        string Phase(WorldSnapshot s) => s.Id.Split('-')[2];

        Assert.Equal(20, all.Count);
        Assert.Equal(6, all.Count(s => Phase(s) == "early"));
        Assert.Equal(8, all.Count(s => Phase(s) == "mid"));
        Assert.Equal(6, all.Count(s => Phase(s) == "late"));
        Assert.Equal(7, all.Count(s => s.Colony.Mood == "content"));
        Assert.Equal(7, all.Count(s => s.Colony.Mood == "strained"));
        Assert.Equal(6, all.Count(s => s.Colony.Mood == "desperate"));
        Assert.True(all.Count(s => s.ActivePlots.Any(p => p.Template == "AwakenedEmpire")) >= 3);
        Assert.True(all.Count(s => s.ActivePlots.Any(p => p.Template == "Nemesis")) >= 4);
        Assert.True(all.Count(s => s.Handoff is not null) >= 3);
        Assert.All(all, s => Assert.EndsWith(s.Colony.Mood, s.Id));
    }

    /// <summary>Snapshots exported from real games (Stage 2 M6) are named snap-NN-real-*.</summary>
    private static bool IsRealExport(WorldSnapshot s) => s.Id.Split('-')[2] == "real";

    /// <summary>Imperial crisis snapshots (crisis C11) are named snap-NN-crisis-*.</summary>
    private static bool IsCrisis(WorldSnapshot s) => s.Id.Split('-')[2] == "crisis";

    private static bool IsWar(WorldSnapshot s) => s.Id.Split('-')[2] == "war";

    /// <summary>The War in Heaven's endings for the epilogue (W11) are named snap-NN-ending-*.</summary>
    private static bool IsEnding(WorldSnapshot s) => s.Id.Split('-')[2] == "ending";

    [Fact]
    public void TheEndingSnapshots_CoverTheNewEndings()
    {
        var endings = All().Where(IsEnding).Select(s => s.Epilogue!).ToList();
        Assert.Contains(endings, e => e.Ending == "Choral" && e.Rival is not null);
        Assert.Contains(endings, e => e.Ending == "IndependenceTotal" && e.Rival is not null);
    }

    [Fact]
    public void TheWarSnapshot_HasTheWarBlock_AndTheWarPlotDecides()
    {
        var war = All().Where(IsWar).ToList();
        Assert.Single(war);
        Assert.NotNull(war[0].War);
        Assert.Equal("WarInHeaven", war[0].ActivePlots.Single(p => p.Id == war[0].Director!.PlotId).Template);
        Assert.All(war[0].AllowedTransitions.Where(o => o != "Wait"), o => Assert.StartsWith("War:", o));
    }

    [Fact]
    public void CrisisSnapshots_HaveACrisisBlock_AndTheCrisisPlotDecides()
    {
        var crisis = All().Where(IsCrisis).ToList();
        Assert.NotEmpty(crisis);
        Assert.All(crisis, s =>
        {
            Assert.NotNull(s.Crisis);
            Assert.Equal("ImperialCrisis", s.ActivePlots.Single(p => p.Id == s.Director!.PlotId).Template);
        });
    }

    [Fact]
    public void RealExports_CoverAQuietWeekAndRealNews()
    {
        var real = All().Where(IsRealExport).ToList();

        Assert.Equal(3, real.Count);
        Assert.Contains(real, s => s.PublicEvents.Count == 0);
        Assert.Contains(real, s => s.PublicEvents.Any(e => e.Contains("correspondent", StringComparison.Ordinal)));
        Assert.All(real, s => Assert.Null(s.Handoff));
    }

    [Fact]
    public void RoundTrip_PreservesTheSnapshot()
    {
        var original = Load("snap-07-mid-strained.json");

        var copy = SnapshotJson.Parse(SnapshotJson.Serialize(original));

        Assert.Equal(SnapshotJson.Serialize(original), SnapshotJson.Serialize(copy));
        Assert.Equal("Mara", copy.Pawns[0].Name);
        Assert.True(copy.Tales[1].Escaped);
    }

    [Fact]
    public void Parse_UnknownField_IsRejected() =>
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => SnapshotJson.Parse(
            """{"id":"x","day":1,"season":"Aprimay","year":5500,"colony":{"name":"A","wealth":1,"population":1,"mood":"content"},"pawnz":[]}"""));

    [Fact]
    public void Parse_MinimalSnapshot_DefaultsTheLists()
    {
        var s = SnapshotJson.Parse(
            """{"id":"x","day":1,"season":"Aprimay","year":5500,"colony":{"name":"A","wealth":1,"population":1,"mood":"content"}}""");

        Assert.Empty(s.Pawns);
        Assert.Empty(s.AllowedTransitions);
        Assert.Null(s.Handoff);
        Assert.Equal(1, s.SchemaVersion);
    }
}
