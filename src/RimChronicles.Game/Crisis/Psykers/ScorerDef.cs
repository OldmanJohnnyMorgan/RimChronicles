using RimChronicles.Core.Crisis.Psykers;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Psykers;

/// <summary>Enables a scorer for a list of psycasts (crisis spec §5.3: "each scored psycast has a scorer class, listed
/// in Defs"). <see cref="scorer"/> is an <see cref="IPsycastScorer.Id"/>.</summary>
public sealed class ScorerDef : Def
{
    public string scorer = "";
    public List<string> psycasts = [];
}

/// <summary>
/// The enabled scorers, by psycast. Checked once after the defs load: a scorer whose id isn't known, or whose psycasts
/// the game doesn't have, is disabled and logged, never thrown on (crisis spec §5.3).
/// </summary>
[StaticConstructorOnStartup]
public static class ScorerRegistry
{
    private static Dictionary<string, List<IPsycastScorer>>? _byPsycast;
    private static List<string> _report = [];

    static ScorerRegistry()
    {
        Load();
        foreach (var line in _report.Where(l => l.StartsWith("disabled", StringComparison.Ordinal)))
        {
            Log.Warning($"[Rim Chronicles] Psyker scorer {line}");
        }
    }

    public static IReadOnlyList<IPsycastScorer> For(string psycast) =>
        (_byPsycast ?? Load()).TryGetValue(psycast, out var list) ? list : [];

    /// <summary>One line per scorer def: enabled with its psycasts, or disabled and why.</summary>
    public static IReadOnlyList<string> Report
    {
        get
        {
            _ = _byPsycast ?? Load();
            return _report;
        }
    }

    private static Dictionary<string, List<IPsycastScorer>> Load()
    {
        var byPsycast = new Dictionary<string, List<IPsycastScorer>>();
        var report = new List<string>();
        foreach (var def in DefDatabase<ScorerDef>.AllDefsListForReading)
        {
            var scorer = PsykerScorers.Find(def.scorer);
            var missing = (def.psycasts ?? []).Where(p => DefDatabase<AbilityDef>.GetNamedSilentFail(p) is null).ToList();
            if (scorer is null)
            {
                report.Add($"disabled {def.defName}: no scorer called '{def.scorer}'");
                continue;
            }

            if (missing.Count > 0 || def.psycasts is not { Count: > 0 })
            {
                report.Add($"disabled {def.defName} ({def.scorer}): psycasts not found: {string.Join(", ", missing.DefaultIfEmpty("none listed"))}");
                continue;
            }

            foreach (var psycast in def.psycasts)
            {
                if (!byPsycast.TryGetValue(psycast, out var list))
                {
                    byPsycast[psycast] = list = [];
                }

                list.Add(scorer);
            }

            report.Add($"enabled {def.defName} ({def.scorer}): {string.Join(", ", def.psycasts)}");
        }

        _report = report;
        _byPsycast = byPsycast;
        return byPsycast;
    }
}
