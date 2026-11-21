namespace RimChronicles.Core.Plots.Nemesis;

/// <summary>The Nemesis arc's fact keys and setup (design spec, Stage 3).</summary>
public static class NemesisPlot
{
    public const string TemplateId = "Nemesis";

    public const string NemesisId = "nemesis";
    public const string NemesisName = "nemesisName";
    public const string NemesisFaction = "nemesisFaction";
    public const string VictimId = "victim";
    public const string VictimName = "victimName";
    public const string Grievance = "grievance";
    public const string Part = "part";

    /// <summary>What the victim was to the protagonist ("husband"), or "" when the protagonist is the victim.</summary>
    public const string Relation = "relation";

    /// <summary>The protagonist for a grudge. A maimed colonist carries their own grudge; for a killing, the slot goes to
    /// whoever inherits from the victim (blood kin first, see <see cref="Inheritance"/>).</summary>
    public static HeirChoice Protagonist(Grudge grudge, IReadOnlyList<HeirCandidate> candidatesForVictim, string colonyName) =>
        grudge.Kind == GrudgeKind.Maimed
            ? new HeirChoice(ProtagonistKind.Pawn, grudge.VictimId, grudge.VictimName, "the victim")
            : Inheritance.PickHeir(candidatesForVictim, colonyName);

    /// <summary>One line for the plot's log: "Vex of the Venom Gang killed Mara's husband Tomas".</summary>
    public static string Describe(Grudge grudge, string protagonistName, string relation)
    {
        var raider = grudge.KillerFaction.Length > 0 ? $"{grudge.KillerName} of {grudge.KillerFaction}" : grudge.KillerName;
        var whom = relation.Length > 0 ? $"{protagonistName}'s {relation} {grudge.VictimName}" : grudge.VictimName;
        return grudge.Kind == GrudgeKind.Killed
            ? $"{raider} killed {whom} and escaped"
            : $"{raider} cost {whom} their {(grudge.Part.Length > 0 ? grudge.Part : "limb")} and escaped";
    }

    /// <summary>Starts a Nemesis plot from a grudge, recording its facts and marking the grudge as used.</summary>
    public static Plot Seed(PlotBook book, PlotTemplate template, Grudge grudge, HeirChoice protagonist, string relation, int today, int hookDelayDays)
    {
        var slot = new ProtagonistSlot
        {
            Kind = protagonist.Kind,
            PawnId = protagonist.Id,
            Name = protagonist.Name,
            CoLeadId = protagonist.CoLeadId,
            CoLeadName = protagonist.CoLeadName,
        };
        var plot = book.Start(template, slot, today, hookDelayDays, Describe(grudge, protagonist.Name, relation));
        plot.SetFact(NemesisId, grudge.KillerId);
        plot.SetFact(NemesisName, grudge.KillerName);
        plot.SetFact(NemesisFaction, grudge.KillerFaction);
        plot.SetFact(VictimId, grudge.VictimId);
        plot.SetFact(VictimName, grudge.VictimName);
        plot.SetFact(Grievance, grudge.Kind);
        plot.SetFact(Part, grudge.Part);
        plot.SetFact(Relation, relation);
        grudge.PlotId = plot.Id;
        return plot;
    }
}
