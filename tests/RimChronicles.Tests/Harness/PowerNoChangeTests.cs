using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Empire;
using RimChronicles.Eval.DirectorCases;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Harness;

/// <summary>
/// The War in Heaven's W1 proof (tech spec §3): turning the Empire code into a <see cref="Power"/> changed nothing the
/// director or a save can see. The case sets regenerate byte for byte, and the saga's and the crisis's saved labels stay
/// as they were.
/// </summary>
public class PowerNoChangeTests
{
    /// <summary>The seed-1 crisis set isn't committed (it regenerates on the PC); its hash was taken before W1.</summary>
    private const string Seed1CrisisSha = "83ecb1dec50aa36cd78c310f479a690be10d3750ddf7e98d5708a1243e462b52";

    [Theory]
    [InlineData(9001, 500, false, "seed-9001")]
    [InlineData(9001, 240, true, "seed-9001-crisis")]
    [InlineData(1, 5000, false, "seed-1")]
    [InlineData(1, 1200, true, null)]
    public void TheDirectorCaseSets_RegenerateByteForByte(int seed, int count, bool crisis, string? committed)
    {
        var dir = Path.Combine(Path.GetTempPath(), "rc-w1-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sites = crisis ? SiteArchetypeXml.Load(Repo.Path("Defs", "SiteArchetypeDefs")) : null;
            DirectorGenCommand.Run(seed, count, dir, Repo.Path("harness", "schema", "snapshot.schema.json"), TextWriter.Null, sites);
            var sha = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")))!["sha256"]!.GetValue<string>();
            var expected = committed is null
                ? Seed1CrisisSha
                : JsonNode.Parse(File.ReadAllText(Repo.Path("training", "data", "cases", "synthetic", committed, "manifest.json")))!["sha256"]!.GetValue<string>();
            Assert.Equal(expected, sha);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void TheSagaAndCrisisSaves_KeepTheirLabels()
    {
        var saga = new EmpireSaga
        {
            State = EmpireState.Awakened,
            StateDay = 120,
            NextStateDay = -1,
            Trigger = AwakeningTrigger.Wealth,
            TriggerDetail = "160000",
            Log = ["Day 90: stirring", "Day 120: awakened"],
            NextExpansionDay = 140,
            Expansions = [new ExpansionRecord { Day = 125, Settlement = "Ashgrove", Faction = "The Free Rim", Result = ExpansionResult.Conquered }],
            HighTitleHolder = "Ada",
        };
        var crisis = new CrisisState
        {
            History = [new OperationRecord { Day = 130, Operation = "Operation:ProbingRaid", Stage = CrisisStage.Probing, Outcome = OperationOutcome.Repelled, Budget = 800 }],
            AnnouncedStage = CrisisStage.Probing,
            AnnouncedDay = 128,
        };

        var archive = new DictionaryArchive(ArchiveMode.Saving);
        archive.Deep(ref saga, "empire");
        archive.Deep(ref crisis, "crisis");
        Golden.Match("save-layout.empire-crisis.txt", Render(archive.Data, 0));
    }

    private static string Render(object? value, int depth)
    {
        var pad = new string(' ', depth * 2);
        switch (value)
        {
            case Dictionary<string, object?> d:
                var sb = new StringBuilder();
                foreach (var (k, v) in d)
                {
                    sb.Append('\n').Append(pad).Append(k).Append(':').Append(Render(v, depth + 1));
                }

                return sb.ToString();
            case string s:
                return " \"" + s + "\"";
            case IList list:
                var lb = new StringBuilder();
                var i = 0;
                foreach (var item in list)
                {
                    lb.Append('\n').Append(pad).Append('[').Append(i++).Append(']').Append(Render(item, depth + 1));
                }

                return list.Count == 0 ? " []" : lb.ToString();
            case null:
                return " null";
            case IFormattable f:
                return " " + f.ToString(null, CultureInfo.InvariantCulture);
            default:
                return " " + value;
        }
    }
}
