using RimChronicles.Core.Plots.Nemesis;

namespace RimChronicles.Core.Plots;

/// <summary>How the protagonist was lost.</summary>
public enum ProtagonistLoss
{
    /// <summary>Died, at the nemesis's hand.</summary>
    KilledByNemesis,

    /// <summary>Died some other way.</summary>
    Died,

    /// <summary>Kidnapped by another faction.</summary>
    Taken,

    /// <summary>Left the colony (banished, joined another faction).</summary>
    Left,
}

/// <summary>Passes a plot's protagonist slot to an heir and changes the arc to match (design spec, Stage 3:
/// "inheriting changes the arc"). For Nemesis, a protagonist killed by the nemesis becomes the new victim, so the hunt
/// becomes revenge for them.</summary>
public static class Succession
{
    /// <summary>What the arc's facts need to know about the old protagonist and the heir.</summary>
    /// <param name="OldName">The lost protagonist's name.</param>
    /// <param name="OldPronoun">"he", "she" or "they".</param>
    /// <param name="OldToHeir">What the lost protagonist was to the heir ("husband"), or "".</param>
    /// <param name="VictimToHeir">What the arc's original victim was to the heir, or "".</param>
    /// <param name="HeirPronoun">"he", "she" or "they".</param>
    public sealed record Context(string OldName, string OldPronoun, string OldToHeir, string VictimToHeir, string HeirPronoun);

    public static string HowLost(ProtagonistLoss loss) => loss switch
    {
        ProtagonistLoss.KilledByNemesis => "killed by the nemesis",
        ProtagonistLoss.Died => "died",
        ProtagonistLoss.Taken => "was taken",
        _ => "left the colony",
    };

    /// <summary>Moves the slot to <paramref name="heir"/> and rewrites the facts from the heir's side. Returns the line
    /// the plot's log and the handoff use for how the story turned.</summary>
    public static string Pass(Plot plot, PlotTemplate template, HeirChoice heir, ProtagonistLoss loss, Context c, int today)
    {
        Inheritance.PassTo(plot.Protagonist, heir, HowLost(loss));
        plot.SetFact(NemesisPlot.ProtagonistPronoun, heir.Kind == ProtagonistKind.Colony ? "they" : c.HeirPronoun);

        if (plot.Template == NemesisPlot.TemplateId)
        {
            if (loss == ProtagonistLoss.KilledByNemesis)
            {
                // The fallen protagonist is the nemesis's newest victim: the heir now hunts for them.
                plot.SetFact(NemesisPlot.VictimName, c.OldName);
                plot.SetFact(NemesisPlot.VictimPronoun, c.OldPronoun);
                plot.SetFact(NemesisPlot.Grievance, GrudgeKind.Killed);
                plot.SetFact(NemesisPlot.Part, "");
                plot.SetFact(NemesisPlot.Relation, c.OldToHeir);
            }
            else
            {
                plot.SetFact(NemesisPlot.Relation, c.VictimToHeir);
            }
        }

        var turn = template.InheritanceMutation.Length > 0 ? $"; {template.InheritanceMutation}" : "";
        var line = $"{c.OldName} {HowLost(loss)}; the story passes to {heir.Name} ({heir.Rule}){turn}";
        plot.Note(today, $"inherited: {line}");
        return line;
    }
}
