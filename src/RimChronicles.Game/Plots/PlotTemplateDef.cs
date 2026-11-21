using RimChronicles.Core.Plots;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>An arc template in XML (Defs/PlotTemplateDefs). The defName is the template id ("Nemesis").</summary>
public sealed class PlotTemplateDef : Def
{
    public bool countsTowardCap = true;
    public int minGapDays = 5;
    public int maxGapDays = 15;
    public string inheritanceMutation = "";

    public PlotTemplate ToCore() => new()
    {
        Id = defName,
        CountsTowardCap = countsTowardCap,
        MinGapDays = minGapDays,
        MaxGapDays = Math.Max(minGapDays, maxGapDays),
        InheritanceMutation = inheritanceMutation ?? "",
    };

    /// <summary>The core template for an id, or null if no such def is loaded (e.g. a plot from a removed mod version).</summary>
    public static PlotTemplate? Find(string id) => DefDatabase<PlotTemplateDef>.GetNamedSilentFail(id)?.ToCore();
}
