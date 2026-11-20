using System.Globalization;
using System.Text;
using System.Text.Json;
using RimChronicles.Eval.Running;

namespace RimChronicles.Eval.Judging;

/// <summary>Judge-human agreement on the hand-scored sample.</summary>
public sealed record Agreement(int Sampled, int Scored, double? OverallExact, double? OverallWithinOne, double? MeanAbsoluteDifference,
    double? InventedAgreement, double? MeanJudgeOverall, double? MeanHumanOverall);

/// <summary>
/// The design spec's hand check of the judge. "create" writes a blind sample (no model names, no judge scores) to
/// spotcheck.csv for a person to fill in; "score" compares the filled-in scores with the judge's.
/// </summary>
public static class SpotCheck
{
    public const string SampleFile = "spotcheck.csv";
    public const string KeyFile = "spotcheck_key.csv";
    public const string AgreementFile = "spotcheck_agreement.json";

    private static readonly string[] Header =
        ["sample", "task", "task_given", "briefing", "output", "human_invented_count", "human_overall", "human_notes"];

    /// <summary>Samples judged outputs (at least one), stratified by task so every task is checked.</summary>
    public static int Create(string runFolder, double rate, int seed, TextWriter log)
    {
        var judged = JudgeFiles.Read(Path.Combine(runFolder, "judge.csv")).Where(j => j.Judged).ToList();
        if (judged.Count == 0)
        {
            throw new InvalidOperationException("This run has no judged outputs. Run the judge first.");
        }

        var random = new Random(seed);
        var sample = judged.GroupBy(j => j.Task)
            .SelectMany(g =>
            {
                var take = Math.Max(1, (int)Math.Ceiling(g.Count() * rate));
                return g.OrderBy(j => j.Cell, StringComparer.Ordinal).OrderBy(_ => random.Next()).Take(take);
            })
            .OrderBy(_ => random.Next())
            .ToList();

        var samples = new StringBuilder();
        var key = new StringBuilder();
        Csv.AppendRow(samples, Header);
        Csv.AppendRow(key, ["sample", "cell", "raw_file"]);
        for (var i = 0; i < sample.Count; i++)
        {
            var id = $"S{i + 1:000}";
            var raw = RawOutput.Load(Path.Combine(runFolder, sample[i].RawFile));
            Csv.AppendRow(samples, [id, sample[i].Task, raw.System, raw.User, raw.Text, "", "", ""]);
            Csv.AppendRow(key, [id, sample[i].Cell, sample[i].RawFile]);
        }

        File.WriteAllText(Path.Combine(runFolder, SampleFile), samples.ToString());
        File.WriteAllText(Path.Combine(runFolder, KeyFile), key.ToString());
        log.WriteLine($"Wrote {sample.Count} blind samples ({rate:P0} of {judged.Count} judged outputs, at least one per task) to {Path.Combine(runFolder, SampleFile)}.");
        log.WriteLine("Fill in human_invented_count (0 or more) and human_overall (1-5) using the rubric in prompts/judge, then run spotcheck again to score agreement.");
        return sample.Count;
    }

    /// <summary>Compares the filled-in sample with the judge. Rows without a human_overall are skipped.</summary>
    public static Agreement Score(string runFolder, TextWriter log)
    {
        var samples = Csv.ReadTable(File.ReadAllText(Path.Combine(runFolder, SampleFile)));
        var key = Csv.ReadTable(File.ReadAllText(Path.Combine(runFolder, KeyFile))).ToDictionary(r => r["sample"], r => r["raw_file"], StringComparer.Ordinal);
        var judge = JudgeFiles.Read(Path.Combine(runFolder, "judge.csv")).ToDictionary(j => j.RawFile, StringComparer.Ordinal);

        var pairs = new List<(int JudgeOverall, int HumanOverall, int JudgeInvented, int HumanInvented)>();
        foreach (var s in samples)
        {
            var human = Csv.ParseInt(s["human_overall"].Trim());
            if (human is null)
            {
                continue;
            }

            if (human is < 1 or > 5)
            {
                throw new InvalidDataException($"{s["sample"]}: human_overall must be 1-5.");
            }

            var j = judge[key[s["sample"]]];
            pairs.Add((j.Overall!.Value, human.Value, j.InventedCount!.Value, Csv.ParseInt(s["human_invented_count"].Trim()) ?? 0));
        }

        double? Rate(Func<(int JudgeOverall, int HumanOverall, int JudgeInvented, int HumanInvented), bool> f) =>
            pairs.Count == 0 ? null : pairs.Count(f) / (double)pairs.Count;

        var agreement = new Agreement(
            samples.Count,
            pairs.Count,
            Rate(p => p.JudgeOverall == p.HumanOverall),
            Rate(p => Math.Abs(p.JudgeOverall - p.HumanOverall) <= 1),
            pairs.Count == 0 ? null : pairs.Average(p => Math.Abs(p.JudgeOverall - p.HumanOverall)),
            Rate(p => (p.JudgeInvented == 0) == (p.HumanInvented == 0)),
            pairs.Count == 0 ? null : pairs.Average(p => p.JudgeOverall),
            pairs.Count == 0 ? null : pairs.Average(p => p.HumanOverall));

        File.WriteAllText(Path.Combine(runFolder, AgreementFile), JsonSerializer.Serialize(agreement, new JsonSerializerOptions { WriteIndented = true }));
        static string P(double? v) => v is null ? "-" : v.Value.ToString("P0", CultureInfo.InvariantCulture);
        static string N(double? v) => v is null ? "-" : v.Value.ToString("0.00", CultureInfo.InvariantCulture);
        log.WriteLine($"Scored {pairs.Count} of {samples.Count} samples.");
        log.WriteLine($"Overall: exact {P(agreement.OverallExact)}, within one point {P(agreement.OverallWithinOne)}, mean difference {N(agreement.MeanAbsoluteDifference)} (judge mean {N(agreement.MeanJudgeOverall)}, yours {N(agreement.MeanHumanOverall)}).");
        log.WriteLine($"Invented facts: agree on whether there were any in {P(agreement.InventedAgreement)} of samples.");
        return agreement;
    }
}
