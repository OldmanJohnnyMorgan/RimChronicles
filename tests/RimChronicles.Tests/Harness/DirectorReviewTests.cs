using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Llm.Chat;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class DirectorReviewTests
{
    /// <summary>Always picks the first option it may, with a reason.</summary>
    private sealed class FirstOption : IChatProvider
    {
        public string ModelId => "fake";

        public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            var first = request.Schema!.Schema["properties"]!["choice"]!["enum"]![0]!.GetValue<string>();
            var text = new JsonObject { ["choice"] = first, ["reason"] = "The model's <reason> & more." }.ToJsonString();
            return Task.FromResult(new ChatResult { Ok = true, Text = text, ModelId = ModelId });
        }
    }

    [Fact]
    public async Task Prepare_WritesABlindPageAndAKey_ThenScoreRevealsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-review-" + Guid.NewGuid().ToString("N"));
        var cases = Path.Combine(root, "cases");
        var labels = Path.Combine(root, "labels");
        Directory.CreateDirectory(cases);
        Directory.CreateDirectory(labels);
        try
        {
            var lines = new List<string>();
            for (var i = 0; i < 8; i++)
            {
                var s = CaseGenerator.Generate(9001, i * 37).Snapshot;
                File.WriteAllText(Path.Combine(cases, s.Id + ".json"), SnapshotJson.Serialize(s));
                var best = s.AllowedTransitions[^1];
                lines.Add(new JsonObject
                {
                    ["case"] = s.Id, ["best"] = best, ["reason"] = "The teacher's reason.",
                    ["scores"] = new JsonObject(s.AllowedTransitions.Select(t => KeyValuePair.Create(t, (JsonNode?)JsonValue.Create(t == best ? 5.0 : 2.0)))),
                }.ToJsonString());
            }

            File.WriteAllLines(Path.Combine(labels, "labels-final.jsonl"), lines);
            var o = new ReviewOptions
            {
                CasesDir = cases, LabelsDir = labels, PromptsRoot = Repo.Prompts, CacheDir = Path.Combine(root, "cache"),
                OutDir = Path.Combine(root, "review"), Count = 8,
            };
            var model = new ModelConfig { Id = "fake", Tier = "Minimum", Provider = ProviderKind.Stub };
            Assert.Equal(0, await DirectorReviewCommand.PrepareAsync(o, model, new FirstOption(), TextWriter.Null, CancellationToken.None));

            var html = File.ReadAllText(Path.Combine(o.OutDir, "review.html"));
            Assert.Equal(8, html.Split("class=\"item\"").Length - 1);
            Assert.Contains("The model&#39;s &lt;reason&gt; &amp; more.", html); // escaped
            Assert.DoesNotContain("\"model\"", html);                       // the key isn't on the page
            var key = JsonNode.Parse(File.ReadAllText(Path.Combine(o.OutDir, "key.json")))!["items"]!.AsArray();
            Assert.Equal(8, key.Count);

            // Pick the model's side for the first 3, "same" for 1, the teacher for the rest.
            var picks = new JsonObject();
            foreach (var k in key)
            {
                var n = k!["item"]!.GetValue<int>();
                var side = k["model"]!.GetValue<string>();
                picks[n.ToString(System.Globalization.CultureInfo.InvariantCulture)] = n <= 3 ? side : n == 4 ? "same" : side == "A" ? "B" : "A";
            }

            var answers = Path.Combine(root, "review-answers.json");
            File.WriteAllText(answers, new JsonObject { ["review"] = "review", ["picks"] = picks }.ToJsonString());
            var log = new StringWriter();
            Assert.Equal(0, DirectorReviewCommand.Score(o.OutDir, answers, log));
            Assert.Contains("better 3, same 1, teacher better 4 of 8; picked or tied 50%", log.ToString());
            Assert.Contains("✅ passes", File.ReadAllText(Path.Combine(o.OutDir, "review.md")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
