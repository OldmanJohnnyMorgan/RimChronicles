using System.Text.Json.Nodes;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Llm.Chat;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

public class DirectorLabelCommandTests
{
    /// <summary>Answers from the request's own schema: top score to the first option, or a broken label when told to.</summary>
    private sealed class FakeTeacher(Func<int, bool> broken) : IChatProvider
    {
        public int Calls { get; private set; }

        public string ModelId => "fake";

        public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            var order = request.Schema!.Schema["properties"]!["best"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
            var scores = new JsonObject();
            for (var i = 0; i < order.Count; i++)
            {
                scores[order[i]] = i == 0 ? 5 : 2;
            }

            var best = broken(Calls) ? order[^1] : order[0]; // a best that isn't the top score fails the check
            var text = new JsonObject { ["scores"] = scores, ["best"] = best, ["reason"] = "The colony needs this now." }.ToJsonString();
            return Task.FromResult(new ChatResult { Ok = true, Text = text, ModelId = ModelId });
        }
    }

    private static readonly ModelConfig Model = new() { Id = "fake", Tier = "High", Provider = ProviderKind.Stub };

    private static (string Root, LabelOptions Options) Setup(int seed, int cases, int samples = 2)
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-label-" + Guid.NewGuid().ToString("N"));
        var casesDir = Path.Combine(root, "cases");
        Directory.CreateDirectory(casesDir);
        for (var i = 0; i < cases; i++)
        {
            var s = CaseGenerator.Generate(seed, i).Snapshot;
            File.WriteAllText(Path.Combine(casesDir, s.Id + ".json"), SnapshotJson.Serialize(s));
        }

        return (root, new LabelOptions
        {
            CasesDir = casesDir, OutDir = Path.Combine(root, "out"), PromptsRoot = Repo.Prompts,
            CacheDir = Path.Combine(root, "cache"), Count = cases, Samples = samples, EnforceWait = false,
        });
    }

    [Fact]
    public async Task Labels_EveryItem_InThePackFormat_AndResumes()
    {
        var (root, o) = Setup(1, 6);
        try
        {
            var teacher = new FakeTeacher(_ => false);
            Assert.Equal(0, await DirectorLabelCommand.RunAsync(o, Model, teacher, TextWriter.Null, CancellationToken.None));
            var labels = File.ReadAllLines(Path.Combine(o.OutDir, "labels.jsonl"));
            Assert.Equal(12, labels.Length);
            Assert.Equal(12, teacher.Calls);

            // A second run finds nothing left to do, and the pack passes the same check a hand-labelled one does.
            var again = new FakeTeacher(_ => false);
            await DirectorLabelCommand.RunAsync(o, Model, again, TextWriter.Null, CancellationToken.None);
            Assert.Equal(0, again.Calls);
            Assert.Equal(0, DirectorLabels.Check(o.OutDir, o.CasesDir, TextWriter.Null));
            Assert.Equal(6, File.ReadAllLines(Path.Combine(o.OutDir, "labels-final.jsonl")).Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidLabel_IsRetried_ThenRecordedAsAFailure()
    {
        var (root, o) = Setup(1, 2, samples: 1);
        try
        {
            // Call 1 fails and is retried (call 2 succeeds); the second item fails all three attempts.
            var teacher = new FakeTeacher(n => n != 2);
            await DirectorLabelCommand.RunAsync(o, Model, teacher, TextWriter.Null, CancellationToken.None);
            Assert.Single(File.ReadAllLines(Path.Combine(o.OutDir, "labels.jsonl")));
            Assert.Single(File.ReadAllLines(Path.Combine(o.OutDir, "failures.jsonl")));
            Assert.Equal(5, teacher.Calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Recheck_LabelsOnlyTheReferenceCases_AndCompares()
    {
        var (root, o) = Setup(1, 6, samples: 1);
        try
        {
            // The reference: a labelled folder covering 3 of the 6 cases.
            var reference = Path.Combine(root, "reference");
            await DirectorLabelCommand.RunAsync(o with { OutDir = reference, Count = 3 }, Model, new FakeTeacher(_ => false), TextWriter.Null, CancellationToken.None);
            DirectorLabels.Check(reference, o.CasesDir, TextWriter.Null);
            var only = DirectorLabelCommand.LabelledCases(reference);
            Assert.Equal(3, only.Count);

            await DirectorLabelCommand.RunAsync(o with { OnlyCases = only }, Model, new FakeTeacher(_ => false), TextWriter.Null, CancellationToken.None);
            DirectorLabels.Check(o.OutDir, o.CasesDir, TextWriter.Null);
            Assert.Equal(only, DirectorLabelCommand.LabelledCases(o.OutDir));
            var log = new StringWriter();
            Assert.Equal(0, DirectorLabelCommand.Compare(reference, o.OutDir, log));
            Assert.Contains("Agreement with reference:", log.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TestSplitCases_AreRefused()
    {
        var (root, o) = Setup(9001, 2);
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => DirectorLabelCommand.RunAsync(o, Model, new FakeTeacher(_ => false), TextWriter.Null, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WaitMismatch_FollowsTheCasesWaitRule()
    {
        Assert.NotNull(DirectorLabelCommand.WaitMismatch(true, "Complication:XLeadsARaid"));
        Assert.Null(DirectorLabelCommand.WaitMismatch(true, "Wait"));
        Assert.NotNull(DirectorLabelCommand.WaitMismatch(false, "Wait"));
        Assert.Null(DirectorLabelCommand.WaitMismatch(false, "Choice:RescueHun"));
        Assert.Null(DirectorLabelCommand.WaitMismatch(null, "Wait"));
    }

    [Fact]
    public void RubricTalk_CatchesScoringLanguage_NotStories()
    {
        Assert.Matches(DirectorLabelCommand.RubricTalk(), "Since the beat is young, Wait is the highest scoring pacing option.");
        Assert.Matches(DirectorLabelCommand.RubricTalk(), "The colony is reeling from Voud's death.");
        Assert.DoesNotMatch(DirectorLabelCommand.RubricTalk(), "Hesper raided Emberfall 25 days ago; Miana should decide how to answer.");
    }

    [Fact]
    public void SchemaFor_AllowsExactlyTheShownOptions()
    {
        var schema = DirectorLabelCommand.SchemaFor(["Wait", "Choice:RescueHun"]).Schema;
        Assert.Equal(["Wait", "Choice:RescueHun"], schema["properties"]!["best"]!["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.False(schema["properties"]!["scores"]!["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(2, schema["properties"]!["scores"]!["required"]!.AsArray().Count);
    }

    [Fact]
    public void CapWait_KeepsAtMostTheShare_AndIsStable()
    {
        var train = Enumerable.Range(0, 80).Select(i => ($"w{i}", "Wait"))
            .Concat(Enumerable.Range(0, 20).Select(i => ($"a{i}", "Complication:X"))).ToList();
        var dropped = DirectorDataset.CapWait(train, 0.5);
        Assert.Equal(60, dropped.Count); // 20 Wait kept beside 20 others
        Assert.All(dropped, id => Assert.StartsWith("w", id));
        Assert.Equal(dropped, DirectorDataset.CapWait(train, 0.5));
        Assert.Empty(DirectorDataset.CapWait(train, 1.0));
        Assert.Empty(DirectorDataset.CapWait(train.Take(10).Concat(train.Skip(80)).ToList(), 0.5));
    }
}
