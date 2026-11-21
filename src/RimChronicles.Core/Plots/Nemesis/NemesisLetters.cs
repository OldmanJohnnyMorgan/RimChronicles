namespace RimChronicles.Core.Plots.Nemesis;

/// <summary>Template letters and news lines for the Nemesis arc (rule 7: the arc runs with no model). Stage 3 M3b lets
/// the model write the letters, with these as the fallback.</summary>
public static class NemesisLetters
{
    /// <summary>The plain facts a letter uses, read from the plot.</summary>
    public sealed record Facts(string Nemesis, string Faction, string Protagonist, string Victim, string Relation,
        string Grievance, string Part, string Place, string Colony, string VictimPronoun = "they")
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
            colony,
            plot.Fact(NemesisPlot.VictimPronoun) is { Length: > 0 } pronoun ? pronoun : "they");

        /// <summary>The facts from a plot letter case (the harness, and the game's letter writer).</summary>
        public static Facts From(IReadOnlyDictionary<string, string> f, string protagonist, string colony)
        {
            string Get(string key, string fallback) => f.TryGetValue(key, out var v) && v.Length > 0 ? v : fallback;
            return new(Get(NemesisPlot.NemesisName, "the raider"), Get(NemesisPlot.NemesisFaction, ""), protagonist,
                Get(NemesisPlot.VictimName, ""), Get(NemesisPlot.Relation, ""), Get(NemesisPlot.Grievance, GrudgeKind.Killed),
                Get(NemesisPlot.Part, ""), Get(NemesisArc.Place, "the wastes"), colony, Get(NemesisPlot.VictimPronoun, "they"));
        }

        public bool ProtagonistIsVictim => Victim == Protagonist;

        /// <summary>"Mara's husband Tomas", "Ana", or the victim's name.</summary>
        public string Whom => ProtagonistIsVictim ? Protagonist : Relation.Length > 0 ? $"{Protagonist}'s {Relation} {Victim}" : Victim;

        public string Deed => Grievance == GrudgeKind.Maimed
            ? $"cost {Whom} {(ProtagonistIsVictim ? Possessive(VictimPronoun) : "a")} {(Part.Length > 0 ? Part : "limb")}"
            : $"killed {Whom}";

        public string Raider => Faction.Length > 0 ? $"{Nemesis} of {Faction}" : Nemesis;

        private static string Possessive(string pronoun) => pronoun switch { "he" => "his", "she" => "her", _ => "their" };
    }

    public static (string Label, string Text) Sighting(Facts f) => (
        $"Nemesis: {f.Nemesis} sighted",
        $"Travellers bring word of {f.Raider}, who {f.Deed}. {f.Nemesis} was seen near {f.Place}, alive and unpunished, "
        + "and in no hurry to hide.\n\n"
        + $"{f.Protagonist} has not forgotten. Neither, it seems, has {f.Nemesis}. The colony would do well to keep its walls manned.");

    public static (string Label, string Text) Raid(Facts f) => (
        $"Nemesis: {f.Nemesis} leads the raid",
        $"{f.Raider} rides at the head of this raid: the same raider who {f.Deed}. This time {f.Nemesis} came to the colony's door.\n\n"
        + $"{f.Protagonist} knows the face. This may be the chance to end it, if the colony can hold.");

    public static (string Label, string Text) Choice(Facts f, int cost) => (
        $"Nemesis: what to do about {f.Nemesis}",
        $"{f.Nemesis} escaped again. Word is that bounty hunters would take the job for {cost} silver, "
        + $"though it would be their kill, not {f.Protagonist}'s.\n\nOr the colony can wait: {f.Nemesis} will come back, "
        + "and next time there will be no one else to blame.");

    public const string BountyOption = "Post a bounty ({0} silver)";
    public const string WaitOption = "Wait for them to return";

    public static (string Label, string Text) SecondRaid(Facts f) => (
        $"Nemesis: {f.Nemesis} returns",
        $"{f.Raider} has come back, as promised, at the head of another raid. The raider who {f.Deed} wants to finish what they started.\n\n"
        + $"{f.Protagonist} is ready this time, or will have to be. There is no one else to wait for now.");

    public static (string Label, string Text) Ending(Facts f, NemesisEnd end) => end switch
    {
        NemesisEnd.KilledByColony => ($"Nemesis: {f.Nemesis} is dead",
            $"{f.Raider}, who {f.Deed}, is dead, killed by the people of {f.Colony}. The raider's run is over, and it ended here.\n\n"
            + $"{f.Protagonist} can finally rest."),
        NemesisEnd.KilledByBounty => ($"Nemesis: the bounty is collected",
            $"The bounty hunters caught up with {f.Raider} near {f.Place}. The raider who {f.Deed} is dead, and the hunters have their silver.\n\n"
            + $"It was not {f.Protagonist}'s hand, but {f.Protagonist} paid for it, and can finally rest."),
        NemesisEnd.KilledOtherwise => ($"Nemesis: {f.Nemesis} is dead",
            $"{f.Raider} is dead, though no one at {f.Colony} struck the blow. The raider who {f.Deed} will not be coming back.\n\n"
            + $"{f.Protagonist} feels something like peace, if not quite satisfaction."),
        NemesisEnd.Captured => ($"Nemesis: {f.Nemesis} captured",
            $"{f.Raider}, who {f.Deed}, is now a prisoner of {f.Colony}. This time there is no escape.\n\n"
            + $"What happens to the prisoner next is {f.Protagonist}'s to decide, and the whole colony is watching."),
        NemesisEnd.Vanished => ($"Nemesis: {f.Nemesis} vanished",
            $"{f.Raider} slipped away once more, and this time the roads have gone quiet. No one has seen the raider who {f.Deed} since.\n\n"
            + $"{f.Protagonist} will carry it, unfinished."),
        _ => ($"Nemesis: the feud fades",
            $"{f.Faction} is no longer at war with {f.Colony}, and {f.Nemesis} has gone to ground. The raider who {f.Deed} is beyond reach for now.\n\n"
            + $"The feud fades, unfinished. {f.Protagonist} will have to live with that."),
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

    /// <summary>The template letter for a step name ("Sighting", "Raid", "Choice", "SecondRaid", "Ending:Captured").
    /// It is both the fallback and the plain version the model rewrites.</summary>
    public static (string Label, string Text) ForStep(string step, Facts f, int bountyCost) => step switch
    {
        "Sighting" => Sighting(f),
        "Raid" => Raid(f),
        "Choice" => Choice(f, bountyCost),
        "SecondRaid" => SecondRaid(f),
        _ when step.StartsWith("Ending:", StringComparison.Ordinal) && Enum.TryParse<NemesisEnd>(step["Ending:".Length..], out var end) => Ending(f, end),
        _ => throw new ArgumentException($"Unknown Nemesis letter step '{step}'."),
    };

    /// <summary>What the letter is for, in a line, for the prompt.</summary>
    public static string Purpose(string step) => step switch
    {
        "Sighting" => "The hook: news reaches the colony that the raider was seen. It should unsettle, and remind the reader what the raider did.",
        "Raid" => "The complication: the raider leads a raid on the colony right now. It should be urgent.",
        "Choice" => "The choice: the raider escaped again, and the colony must decide between paying bounty hunters and waiting for the raider to return. Present both options fairly, with the price.",
        "SecondRaid" => "The raider returns with a second raid, as the colony expected. It should feel like a reckoning.",
        _ when step.EndsWith("Vanished", StringComparison.Ordinal) || step.EndsWith("FeudFaded", StringComparison.Ordinal)
            => "The end: the raider is gone for good, unpunished. It should feel unfinished and bitter.",
        _ => "The end: the raider's story is over. It should feel like closure for the protagonist.",
    };
}
