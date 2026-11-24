using RimChronicles.Core.Crisis.Sites;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

/// <summary>The War in Heaven's director cases (W12).</summary>
public class WarCaseTests
{
    private static readonly IReadOnlyList<SiteArchetype> ChoirSites = SiteArchetypeXml.Load(Repo.Path("Defs", "SiteArchetypeDefs"), Power.Choir.Id);

    private static readonly int Cycle = CaseGenerator.WarScenarios.Length * CaseGenerator.Moods.Length * CaseGenerator.Situations.Length;

    private static List<DirectorCaseResult> OneCycle(int seed = 7) =>
        Enumerable.Range(0, Cycle).Select(i => CaseGenerator.GenerateWar(seed, i, ChoirSites)).ToList();

    [Fact]
    public void EveryCase_HasAChoice_AWarId_AndTheWarBlock()
    {
        var cases = OneCycle();
        Assert.All(cases, c =>
        {
            Assert.Matches(@"^dir-s7-w\d{5}$", c.Snapshot.Id);
            Assert.True(c.Snapshot.AllowedTransitions.Count >= 2, c.Scenario);
            Assert.NotNull(c.Snapshot.War);
            Assert.Null(c.Snapshot.Crisis); // the Imperial crisis block never appears in these cases
        });
        Assert.Equal(cases.Select(c => c.Snapshot.Id).Distinct().Count(), cases.Count);
    }

    [Fact]
    public void TheCases_AreTheChoirsAndTheWars_WithTheirOwnIds()
    {
        var cases = OneCycle();
        string Template(DirectorCaseResult c) => c.Snapshot.ActivePlots.Single(p => p.Id == c.Snapshot.Director!.PlotId).Template;
        Assert.Equal(["ChoirCrisis", "HollowChoir", "WarInHeaven"], cases.Select(Template).Distinct().Order());
        var ids = cases.SelectMany(c => c.Snapshot.AllowedTransitions).Distinct().ToList();
        Assert.DoesNotContain(ids, o => o.StartsWith("Empire:", StringComparison.Ordinal));
        Assert.DoesNotContain("Act3:OfferGovernorship", ids);
        Assert.DoesNotContain("Act3:StormTheCapital", ids);
        Assert.Contains("Act3:FirstVoiceOffer", ids);
        Assert.Contains("Choir:SabotageChoir", ids);
        Assert.Contains("Operation:TheHum", ids);
        Assert.Contains("Site:Resonator", ids);
        Assert.Contains("War:FinalPush", ids);
        Assert.Contains("War:TreatyOffer", ids);
        Assert.DoesNotContain(ids, o => o is "Operation:PsychicAssault" or "Operation:FinalAssault" or "Site:PsychicRelay");
    }

    [Fact]
    public void ChoirCrisisCases_CarryTheChoirsCrisisBlock()
    {
        var crisis = OneCycle().Where(c => c.Scenario.StartsWith("ChoirCrisis:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(crisis);
        Assert.All(crisis, c => Assert.NotNull(c.Snapshot.ChoirCrisis));
    }

    [Fact]
    public void TheyRegenerateTheSame_AndDifferBySeed()
    {
        Assert.Equal(SnapshotJson.Serialize(CaseGenerator.GenerateWar(9001, 42, ChoirSites).Snapshot),
            SnapshotJson.Serialize(CaseGenerator.GenerateWar(9001, 42, ChoirSites).Snapshot));
        Assert.NotEqual(SnapshotJson.Serialize(CaseGenerator.GenerateWar(9001, 42, ChoirSites).Snapshot),
            SnapshotJson.Serialize(CaseGenerator.GenerateWar(1, 42, ChoirSites).Snapshot));
    }

    [Fact]
    public void TheDatasetKnowsAWarCase() =>
        Assert.True(DatasetOptions.IsWarCase("dir-s9001-w00012") && !DatasetOptions.IsWarCase("dir-s9001-c00012") && !DatasetOptions.IsCrisisCase("dir-s9001-w00012"));
}
