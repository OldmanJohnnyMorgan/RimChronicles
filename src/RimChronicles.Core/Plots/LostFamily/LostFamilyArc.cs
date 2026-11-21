namespace RimChronicles.Core.Plots.LostFamily;

/// <summary>What the Lost Family arc does when its next beat is due.</summary>
public enum LostFamilyStep
{
    /// <summary>A letter from the kin.</summary>
    Letter,

    /// <summary>A hostile faction takes the kin captive.</summary>
    Taken,

    /// <summary>A Rescue quest to free them.</summary>
    OfferRescue,

    /// <summary>Freed: the kin joins the colony (resolution).</summary>
    Reunited,

    /// <summary>Not freed in time: the kin is lost (resolution).</summary>
    Lost,
}

/// <summary>The Lost Family arc (design spec, Stage 3: "a colonist with living kin in another faction"), as plain
/// decisions over the plot's beat and facts.</summary>
public static class LostFamilyArc
{
    public const string TemplateId = "LostFamily";

    public const string KinId = "kin";
    public const string KinName = "kinName";
    public const string KinFaction = "kinFaction";
    public const string KinPronoun = "kinPronoun";

    /// <summary>What the kin is to the protagonist ("sister").</summary>
    public const string Relation = "relation";

    public const string ProtagonistPronoun = "protagonistPronoun";
    public const string Captor = "captor";
    public const string Letters = "letters";

    public const int MaxLetters = 2;

    /// <summary>No Lost Family plot starts before this day, so a new colony settles first.</summary>
    public const int EarliestDay = 10;

    public static readonly string RescueQuestFact = QuestLink.FactFor(QuestTemplates.Rescue);

    public static IReadOnlyList<PlotTransition> Transitions(Plot plot)
    {
        var kin = NemesisCompact(plot.Fact(KinName) ?? "Kin");
        var options = new List<PlotTransition>();
        switch (plot.Beat)
        {
            case PlotBeat.Hook when plot.BeatDay < 0:
                return [new($"Hook:LetterFrom{kin}", nameof(LostFamilyStep.Letter), 1)];
            case PlotBeat.Hook:
                options.Add(new($"Complication:{kin}TakenCaptive", nameof(LostFamilyStep.Taken), 3));
                if ((int.TryParse(plot.Fact(Letters), out var n) ? n : 1) < MaxLetters)
                {
                    options.Add(new($"Hook:AnotherLetterFrom{kin}", nameof(LostFamilyStep.Letter), 1));
                }

                break;
            case PlotBeat.Complication:
                options.Add(new($"Choice:Rescue{kin}", nameof(LostFamilyStep.OfferRescue), 3));
                break;
            default:
                var outcome = plot.Fact(RescueQuestFact);
                if (outcome is null or "pending")
                {
                    return []; // the Rescue quest is offered or under way: its outcome decides
                }

                return outcome == QuestOutcome.Success
                    ? [new($"Resolution:{kin}Reunited", nameof(LostFamilyStep.Reunited), 1)]
                    : [new($"Resolution:{kin}Lost", nameof(LostFamilyStep.Lost), 1)];
        }

        if (!PlotBook.HasWaitedThisBeat(plot))
        {
            options.Add(new(Director.WaitId, Director.WaitId, 1));
        }

        return options;
    }

    public static PlotBeat BeatAfter(LostFamilyStep step) => step switch
    {
        LostFamilyStep.Letter => PlotBeat.Hook,
        LostFamilyStep.Taken => PlotBeat.Complication,
        _ => PlotBeat.Choice,
    };

    /// <summary>How strong a hook a relative makes: close family more than distant.</summary>
    public static double Strength(string relationDefName) => relationDefName is "Parent" or "Child" or "Sibling" or "HalfSibling" ? 0.8 : 0.5;

    private static string NemesisCompact(string name) => Nemesis.NemesisArc.Compact(name);
}

/// <summary>Template letters and news for Lost Family: the fallback, and the plain letters the model rewrites.</summary>
public static class LostFamilyLetters
{
    public sealed record Facts(string Protagonist, string Kin, string Relation, string KinFaction, string Captor, string Colony)
    {
        public static Facts From(Plot plot, string colony) => new(
            plot.Protagonist.Name,
            plot.Fact(LostFamilyArc.KinName) ?? "a relative",
            plot.Fact(LostFamilyArc.Relation) ?? "",
            plot.Fact(LostFamilyArc.KinFaction) ?? "another faction",
            plot.Fact(LostFamilyArc.Captor) ?? "raiders",
            colony);

        public static Facts From(IReadOnlyDictionary<string, string> f, string protagonist, string colony)
        {
            string Get(string key, string fallback) => f.TryGetValue(key, out var v) && v.Length > 0 ? v : fallback;
            return new(protagonist, Get(LostFamilyArc.KinName, "a relative"), Get(LostFamilyArc.Relation, ""),
                Get(LostFamilyArc.KinFaction, "another faction"), Get(LostFamilyArc.Captor, "raiders"), colony);
        }

        /// <summary>"Saskia, Anouk's sister" or just the kin's name.</summary>
        public string KinWho => Relation.Length > 0 ? $"{Kin}, {Protagonist}'s {Relation}" : Kin;

        /// <summary>The kin as the subject of a verb: "Saskia, Anouk's sister," or "Saskia".</summary>
        public string KinSubject => Relation.Length > 0 ? $"{Kin}, {Protagonist}'s {Relation}," : Kin;
    }

    public static (string Label, string Text) Letter(Facts f, bool first) => first
        ? ($"Lost Family: a letter from {f.Kin}",
            $"A letter has come for {f.Protagonist}, carried by a passing trader. It is from {f.KinWho}, who lives among {f.KinFaction}.\n\n"
            + $"{f.Kin} is alive, and well enough to write. {f.Protagonist} reads it twice, then puts it away somewhere safe.")
        : ($"Lost Family: another letter",
            $"Another letter from {f.KinWho} has reached {f.Colony}. Life among {f.KinFaction} goes on, the letter says, "
            + $"though it asks after {f.Protagonist} more than it says about {f.Kin}.\n\n{f.Protagonist} keeps this one too.");

    public static (string Label, string Text) Taken(Facts f) => (
        $"Lost Family: {f.Kin} taken",
        $"Word comes that {f.Captor} have taken captives from {f.KinFaction}. {f.KinSubject} is among them, held at a camp "
        + $"not far from {f.Colony}.\n\n{f.Protagonist} has not slept since the news came.");

    public static (string Name, string Description) RescueQuest(Facts f) => (
        $"Rescue {f.Kin}",
        $"{f.Captor} hold {f.KinWho} at a camp near {f.Colony}. {f.Protagonist} will not leave family in their hands. "
        + $"Clear the guards, and have a colonist offer {f.Kin} help: {f.Kin} will come home to {f.Colony}.");

    public static (string Label, string Text) Reunited(Facts f) => (
        $"Lost Family: {f.Kin} is home",
        $"{f.KinSubject} is free, and home at {f.Colony}. {f.Protagonist} has hardly let {f.Kin} out of sight since the gates closed behind them.\n\n"
        + "The family the rim scattered is, for now, together again.");

    public static (string Label, string Text) Lost(Facts f) => (
        $"Lost Family: no word of {f.Kin}",
        $"No more word has come about {f.KinWho}. {f.Captor} moved on, and the trail has gone cold.\n\n"
        + $"{f.Protagonist} still keeps the letters, and some nights reads them again.");

    public static (string Label, string Text) ForStep(string step, Facts f) => step switch
    {
        "Letter" => Letter(f, first: true),
        "AnotherLetter" => Letter(f, first: false),
        "Taken" => Taken(f),
        "Ending:Reunited" => Reunited(f),
        "Ending:Lost" => Lost(f),
        _ => throw new ArgumentException($"Unknown Lost Family letter step '{step}'."),
    };

    public static string Purpose(string step) => step switch
    {
        "Letter" => "The hook: the first letter from a relative the protagonist thought they had lost touch with. It should be warm, with a hint of distance.",
        "AnotherLetter" => "A second letter from the same relative. It should feel familiar, and a little wistful.",
        "Taken" => "The complication: the relative has been taken captive. It should feel urgent and personal.",
        "Ending:Reunited" => "The end: the relative is rescued and joins the colony. It should feel like relief and quiet joy.",
        _ => "The end: the relative was not rescued, and the trail has gone cold. It should feel like a loss the protagonist carries.",
    };

    public static string TakenNews(Facts f) => $"{f.Captor} took captives from {f.KinFaction}";

    public static string ReunitedNews(Facts f) => $"{f.Colony} freed captives held by {f.Captor}";

    /// <summary>What the heir inherits, for the handoff.</summary>
    public static string Unfinished(Facts f) => f.Captor != "raiders"
        ? $"{f.KinSubject} is still held by {f.Captor}"
        : $"{f.KinSubject} is still out there among {f.KinFaction}";
}
