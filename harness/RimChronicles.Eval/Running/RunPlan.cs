using System.Text.RegularExpressions;
using RimChronicles.Core.Snapshots;
using RimChronicles.Eval.Tasks;

namespace RimChronicles.Eval.Running;

/// <summary>Expands the options into the list of cells, in model order (so a local server loads each model once).</summary>
public static class RunPlan
{
    public static (IReadOnlyList<Cell> Cells, IReadOnlyDictionary<string, WorldSnapshot> Snapshots) Build(RunOptions o)
    {
        var models = o.Config.Models.Where(m => o.Models is null || o.Models.Contains(m.Id)).ToList();
        if (o.Models?.Except(o.Config.Models.Select(m => m.Id)).ToList() is { Count: > 0 } unknownModels)
        {
            throw new ArgumentException($"Unknown model(s): {string.Join(", ", unknownModels)}");
        }

        if (o.Tasks?.Except(EvalTasks.All).ToList() is { Count: > 0 } unknownTasks)
        {
            throw new ArgumentException($"Unknown task(s): {string.Join(", ", unknownTasks)}. Tasks: {string.Join(", ", EvalTasks.All)}");
        }

        var tasks = EvalTasks.All.Where(t => o.Tasks is null || o.Tasks.Contains(t)).ToList();
        var patterns = (o.Snapshots ?? ["*"]).Select(Glob).ToList();
        var snapshots = Directory.GetFiles(o.SnapshotsDir, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(SnapshotJson.Load)
            .Where(s => patterns.Any(p => p.IsMatch(s.Id)))
            .ToDictionary(s => s.Id, StringComparer.Ordinal);

        if (snapshots.Count == 0)
        {
            throw new ArgumentException("No snapshots match.");
        }

        var repeats = o.Repeats ?? o.Config.Repeats;
        var cells = new List<Cell>();
        foreach (var model in models)
        {
            foreach (var task in tasks)
            {
                IReadOnlyList<string> modes = EvalTasks.UsesSchema(task)
                    ? model.SupportsGrammar ? ["on", "off"] : ["off"]
                    : ["n/a"];

                foreach (var snapshot in snapshots.Values.Where(s => EvalTasks.AppliesTo(task, s)))
                {
                    foreach (var mode in modes)
                    {
                        for (var r = 1; r <= repeats; r++)
                        {
                            cells.Add(new Cell(model, task, snapshot.Id, mode, r));
                        }
                    }
                }
            }
        }

        return (cells, snapshots);
    }

    private static Regex Glob(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.CultureInvariant);
}
