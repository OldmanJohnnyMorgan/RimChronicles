using System.Text.Json.Nodes;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Prompts;
using RimChronicles.Tests.TestSupport;
using static RimChronicles.Eval.DirectorCases.DirectorReportCommand;

namespace RimChronicles.Tests.Harness;

public class DirectorReportTests
{
    private static Label L(string id, string best, bool? wait, params (string Id, double Score)[] scores) =>
        new(id, "Nemesis/Hook", wait, false, best, scores.ToDictionary(s => s.Id, s => s.Score), scores.Select(s => s.Id).ToList());

    private static CaseRun R(string id, string? on, string? o2 = null, string? o3 = null, string? ids = null, string reason = "",
        string briefing = "") =>
        new(id, C(on, reason), C(on, reason), C(o2 ?? on), C(o3 ?? on), C(ids ?? on), ids ?? on, [], briefing);

    private static Call C(string? choice, string reason = "") => new(choice is not null, choice is not null, choice, reason, 0.5);

    [Fact]
    public void Score_CountsAgreementWaitAccuracyAndStability()
    {
        var labels = new[]
        {
            L("a", "Wait", true, ("Wait", 5), ("X", 2)),
            L("b", "X", false, ("Wait", 1), ("X", 5), ("Y", 4)),
            L("c", "Y", null, ("Wait", 3), ("X", 2), ("Y", 4)),
        };
        var runs = new[] { R("a", "Wait"), R("b", "Y", o2: "X"), R("c", "Y", ids: "X") };
        var m = Score("m", labels, runs);
        Assert.Equal(2.0 / 3, m.Top1, 3);          // a and c
        Assert.Equal(1.0, m.Top2, 3);              // b's Y is second best
        Assert.Equal((0 + 1 + 0) / 3.0, m.Regret, 3);
        Assert.Equal(1.0, m.WaitRight);
        Assert.Equal(1.0, m.WaitWrong);            // b didn't wait
        Assert.Equal(2.0 / 3, m.Position!.Value, 3); // b changed with the order
        Assert.Equal(2.0 / 3, m.Ids!.Value, 3);      // c changed with the ids
    }

    [Fact]
    public void AlwaysWait_IsTheBaselineAModelMustBeat()
    {
        var labels = new[] { L("a", "Wait", true, ("Wait", 5), ("X", 2)), L("b", "X", false, ("Wait", 1), ("X", 5)) };
        var m = Score(AlwaysWait, labels, labels.Select(l => R(l.Case, "Wait")).ToList());
        Assert.Equal(0.5, m.Top1);
        Assert.Equal(1.0, m.WaitRight);
        Assert.Equal(0.0, m.WaitWrong);
        Assert.Null(m.Position);
        Assert.Null(m.InventedPer50);
    }

    [Fact]
    public void InventedFacts_CatchD0sStarvingColonies()
    {
        var steelShort = "Recent events:\n- Day 906: Imperial legionaries repelled, two colonists dead\n- Steel nearly gone";
        Assert.Contains("claims food or starving", InventedFacts("Last Lantern is starving and Ysolde is exhausted.", ["Last Lantern", "Ysolde"], steelShort));
        Assert.Empty(InventedFacts("Last Lantern just lost two colonists; a pause fits.", ["Last Lantern"], steelShort));
        var hungry = "Recent events:\n- Day 5: food is running low and two colonists had mental breaks.";
        Assert.Empty(InventedFacts("Thornwick is starving and colonists are breaking; Kit can wait.", ["Thornwick", "Kit"], hungry));
        Assert.Contains("names Brannock", InventedFacts("Thornwick should let Brannock rest.", ["Thornwick"], hungry));
    }

    [Fact]
    public void TopTwoAndRegret_HandleTiesAndInvalidPicks()
    {
        var scores = new Dictionary<string, double> { ["Wait"] = 5, ["X"] = 4, ["Y"] = 4, ["Z"] = 1 };
        Assert.Equal(new HashSet<string> { "Wait", "X", "Y" }, TopTwo(scores));
        Assert.Equal(4, Regret(scores, "Wait", null)); // an invalid pick costs as much as the worst option
        Assert.Equal(1, Regret(scores, "Wait", "Y"));
    }

    [Fact]
    public async Task RunCase_MapsRenamedIdsBack_AndKeysEachVariantApart()
    {
        var s = CaseGenerator.Generate(9001, 7).Snapshot;
        var prompt = PromptLibrary.Load(Repo.Prompts).Get(TaskName);
        var model = new ModelConfig { Id = "fake", Tier = "Minimum", Provider = ProviderKind.Stub };
        var variants = new List<int>();
        var run = await RunCase(prompt, s, model, (req, k) =>
        {
            variants.Add(k);
            // Always pick the last option in the list shown, so a reordering changes the pick.
            var enumIds = req.Schema!.Schema["properties"]!["choice"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
            var text = new JsonObject { ["choice"] = enumIds[^1], ["reason"] = "x" }.ToJsonString();
            return System.Threading.Tasks.Task.FromResult(new ChatResult { Ok = true, Text = text, ModelId = "fake" });
        });
        Assert.Equal([0, 1, 2, 3, 4], variants);
        Assert.Equal(s.AllowedTransitions[^1], run.On.Choice);
        Assert.Equal(s.AllowedTransitions[^1], run.IdsChoice); // C-numbered pick mapped back to the real id
        Assert.Contains("## Deciding now", run.Briefing);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RunCase_TheOtherOrdersReallyDiffer(int options)
    {
        var s = Enumerable.Range(0, 400).Select(i => CaseGenerator.Generate(9001, i).Snapshot).First(c => c.AllowedTransitions.Count == options);
        var prompt = PromptLibrary.Load(Repo.Prompts).Get(TaskName);
        var model = new ModelConfig { Id = "fake", Tier = "Minimum", Provider = ProviderKind.Stub };
        var shown = new Dictionary<int, List<string>>();
        await RunCase(prompt, s, model, (req, k) =>
        {
            shown[k] = req.Schema!.Schema["properties"]!["choice"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
            return System.Threading.Tasks.Task.FromResult(new ChatResult { Ok = true, Text = "{\"choice\":\"Wait\",\"reason\":\"x\"}", ModelId = "fake" });
        });
        Assert.NotEqual(shown[0], shown[2]);
        Assert.NotEqual(shown[0], shown[3]);
        Assert.Equal(shown[0].Order(), shown[2].Order());
    }
}
