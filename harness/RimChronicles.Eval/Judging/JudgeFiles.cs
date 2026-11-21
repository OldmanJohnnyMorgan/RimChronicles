using System.Text;
using RimChronicles.Eval.Running;

namespace RimChronicles.Eval.Judging;

/// <summary>Reads and writes judge.csv.</summary>
public static class JudgeFiles
{
    private static readonly string[] Header =
    [
        "cell", "model", "tier", "task", "snapshot", "repeat", "raw_file", "judge_prompt_version", "judge_outcome",
        "invented_count", "invented_major", "invented_minor", "invented", "voice", "clarity", "tone", "overall", "comment", "error",
    ];

    public static void Write(string path, IEnumerable<JudgeRow> rows)
    {
        var sb = new StringBuilder();
        Csv.AppendRow(sb, Header);
        foreach (var r in rows)
        {
            Csv.AppendRow(sb,
            [
                r.Cell, r.Model, r.Tier, r.Task, r.Snapshot, Csv.Num(r.Repeat), r.RawFile, r.JudgePromptVersion, r.JudgeOutcome,
                Csv.Num(r.InventedCount), Csv.Num(r.MajorCount), Csv.Num(r.MinorCount), r.Invented,
                Csv.Num(r.Voice), Csv.Num(r.Clarity), Csv.Num(r.Tone), Csv.Num(r.Overall), r.Comment, r.Error,
            ]);
        }

        File.WriteAllText(path, sb.ToString());
    }

    public static IReadOnlyList<JudgeRow> Read(string path) =>
        Csv.ReadTable(File.ReadAllText(path)).Select(r =>
        {
            var count = Csv.ParseInt(r["invented_count"]);
            // judge.csv from judge v1 has no severity columns: every invented fact counted as major.
            var major = r.TryGetValue("invented_major", out var mj) ? Csv.ParseInt(mj) : count;
            var minor = r.TryGetValue("invented_minor", out var mn) ? Csv.ParseInt(mn) : count is null ? null : 0;
            return new JudgeRow
            {
                Cell = r["cell"], Model = r["model"], Tier = r["tier"], Task = r["task"], Snapshot = r["snapshot"],
                Repeat = Csv.ParseInt(r["repeat"]) ?? 0, RawFile = r["raw_file"], JudgePromptVersion = r["judge_prompt_version"],
                JudgeOutcome = r["judge_outcome"], InventedCount = count, MajorCount = major, MinorCount = minor, Invented = r["invented"],
                Voice = Csv.ParseInt(r["voice"]), Clarity = Csv.ParseInt(r["clarity"]), Tone = Csv.ParseInt(r["tone"]),
                Overall = Csv.ParseInt(r["overall"]), Comment = r["comment"], Error = Csv.NullIfEmpty(r["error"]),
            };
        }).ToList();
}
