namespace RimChronicles.Core.Plots.Nemesis;

/// <summary>Template letters and news lines for the Nemesis arc (rule 7: the arc runs with no model). Stage 3 M3b lets
/// the model write the letters, with these as the fallback.</summary>
public static class NemesisLetters
{
    /// <summary>The plain facts a letter uses, read from the plot.</summary>
    public sealed record Facts(string Nemesis, string Faction, string Protagonist, string Victim, string Relation,
        string Grievance, string Part, string Place, string Colony)
    {
        public static Facts From(Plot plot, string colony) => new(
            plot.Fact(NemesisPlot.NemesisName) ?? "the raider",
            plot.Fact(NemesisPlot.NemesisFaction) ?? "",
            plot.Protagonist.Name,
            plot.Fact(NemesisPlot.VictimName) ?? "",
            plot.Fact(NemesisPlot.Relation) ?? "",
            plot.Fact(NemesisPlot.Grievance) ?? GrudgeKind.Killed,
            plot.Fact(NemesisPlot.Part) ?? "",
            plot.Fact(NemesisArc.Place) ?? "the wastes",
            colony);

        public bool ProtagonistIsVictim => Victim == Protagonist;

        /// <summary>"Mara's husband Tomas", "Ana", or the victim's name.</summary>
        public string Whom => ProtagonistIsVictim ? Protagonist : Relation.Length > 0 ? $"{Protagonist}'s {Relation} {Victim}" : Victim;

        public string Deed => Grievance == GrudgeKind.Maimed
            ? $"cost {Whom} {(ProtagonistIsVictim ? "their" : "a")} {(Part.Length > 0 ? Part : "limb")}"
            : $"killed {Whom}";

        public string Raider => Faction.Length > 0 ? $"{Nemesis} of {Faction}" : Nemesis;
    }

    public static (string Label, string Text) Sighting(Facts f) => (
        $"Nemesis: {f.Nemesis} sighted",
        $"Travellers bring word of {f.Raider}, who {f.Deed}. {f.Nemesis} was seen near {f.Place}, alive and unpunished.\n\n"
        + $"{f.Protagonist} has not forgotten. Neither, it seems, has {f.Nemesis}.");

    public static (string Label, string Text) Raid(Facts f) => (
        $"Nemesis: {f.Nemesis} leads the raid",
        $"{f.Nemesis} rides at the head of this raid, the same raider who {f.Deed}.\n\n{f.Protagonist} knows the face. This may be the chance to end it.");

    public static (string Label, string Text) Choice(Facts f, int cost) => (
        $"Nemesis: what to do about {f.Nemesis}",
        $"{f.Nemesis} escaped again. Word is that the bounty hunters at {f.Place} would take the job for {cost} silver, "
        + $"though it would be their kill, not {f.Protagonist}'s.\n\nOr the colony can wait: {f.Nemesis} will come back, "
        + "and next time there will be no one else to blame.");

    public const string BountyOption = "Post a bounty ({0} silver)";
    public const string WaitOption = "Wait for them to return";

    public static (string Label, string Text) SecondRaid(Facts f) => (
        $"Nemesis: {f.Nemesis} returns",
        $"{f.Nemesis} has come back, as promised. {f.Protagonist} is ready this time, or will have to be.");

    public static (string Label, string Text) Ending(Facts f, NemesisEnd end) => end switch
    {
        NemesisEnd.KilledByColony => ($"Nemesis: {f.Nemesis} is dead",
            $"{f.Raider}, who {f.Deed}, is dead, killed by the people of {f.Colony}.\n\n{f.Protagonist} can finally rest."),
        NemesisEnd.KilledByBounty => ($"Nemesis: the bounty is collected",
            $"The bounty hunters caught up with {f.Raider} near {f.Place}. It's done.\n\n{f.Protagonist} paid for it, and can finally rest."),
        NemesisEnd.KilledOtherwise => ($"Nemesis: {f.Nemesis} is dead",
            $"{f.Raider} is dead, though no one at {f.Colony} struck the blow.\n\n{f.Protagonist} feels something like peace."),
        NemesisEnd.Captured => ($"Nemesis: {f.Nemesis} captured",
            $"{f.Raider}, who {f.Deed}, is {f.Colony}'s prisoner now.\n\nWhat happens next is {f.Protagonist}'s to decide."),
        NemesisEnd.Vanished => ($"Nemesis: {f.Nemesis} vanished",
            $"{f.Nemesis} slipped away once more, and this time the roads have gone quiet. No one has seen them since.\n\n"
            + $"{f.Protagonist} will carry it."),
        _ => ($"Nemesis: the feud fades",
            $"{f.Faction} is no longer at war with {f.Colony}, and {f.Nemesis} has gone to ground. The feud fades, unfinished."),
    };

    /// <summary>Public news lines for the reporter.</summary>
    public static string SightingNews(Facts f) => $"The raider {f.Raider} was seen near {f.Place}";

    public static string? EndingNews(Facts f, NemesisEnd end) => end switch
    {
        NemesisEnd.KilledByColony => $"The raider {f.Raider} was killed by {f.Colony}",
        NemesisEnd.KilledByBounty => $"Bounty hunters killed the raider {f.Raider} near {f.Place}",
        NemesisEnd.KilledOtherwise => $"The raider {f.Raider} is dead",
        NemesisEnd.Captured => $"The raider {f.Raider} is a prisoner of {f.Colony}",
        _ => null,
    };
}
