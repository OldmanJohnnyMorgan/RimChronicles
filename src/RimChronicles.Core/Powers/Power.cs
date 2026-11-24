namespace RimChronicles.Core.Powers;

/// <summary>
/// One of the major plot's powers (War in Heaven tech spec §2): the Empire and the Hollow Choir. A power's saga,
/// plot and crisis run the same rules; what differs is here: its ids, how its plot and options are named, and its texts.
/// The Empire's values are the ones the mod used before the War in Heaven, so its saves and director cases don't change.
/// </summary>
public sealed class Power
{
    /// <summary>The saga's save label and its plot id in the decision log ("empire").</summary>
    public required string Id { get; init; }

    /// <summary>The news kind its bulletins carry ("Empire").</summary>
    public required string NewsKind { get; init; }

    /// <summary>The plot template of its major plot ("AwakenedEmpire").</summary>
    public required string PlotTemplateId { get; init; }

    /// <summary>The plot template of its crisis ("ImperialCrisis").</summary>
    public required string CrisisTemplateId { get; init; }

    /// <summary>The prefix of its Act 2 job options ("Empire" in "Empire:EscortEnvoy").</summary>
    public required string OptionPrefix { get; init; }

    /// <summary>Act 1's option: the envoy arrives ("Act1:ImperialEnvoy").</summary>
    public required string EnvoyOption { get; init; }

    /// <summary>Act 2's opening option: the power marches ("Act2:TheEmpireMarches").</summary>
    public required string MarchOption { get; init; }

    /// <summary>The awakening trigger for a colonist who draws it (<see cref="AwakeningTrigger.ImperialColonist"/> for the
    /// Empire).</summary>
    public string ColonistTrigger { get; init; } = AwakeningTrigger.ImperialColonist;

    /// <summary>The ending when the colony accepts the power's highest offer (the Empire's governorship: Imperial).</summary>
    public string ServiceEnding { get; init; } = global::RimChronicles.Core.Empire.EmpireEnding.Imperial;

    /// <summary>The ending when the colony takes the power's capital (the Empire's: Resistance).</summary>
    public string DefeatEnding { get; init; } = global::RimChronicles.Core.Empire.EmpireEnding.Resistance;

    public required PowerText Text { get; init; }

    public override string ToString() => Id;

    public static readonly Power Empire = new()
    {
        Id = global::RimChronicles.Core.Empire.EmpireIds.PlotId,
        NewsKind = global::RimChronicles.Core.Empire.EmpireIds.NewsKind,
        PlotTemplateId = global::RimChronicles.Core.Empire.EmpireIds.TemplateId,
        CrisisTemplateId = Crisis.CrisisArc.TemplateId,
        OptionPrefix = "Empire",
        EnvoyOption = "Act1:ImperialEnvoy",
        MarchOption = "Act2:TheEmpireMarches",
        Text = new global::RimChronicles.Core.Empire.EmpirePowerText(),
    };

    /// <summary>The Hollow Choir (War in Heaven spec §3), the Empire's old enemy. Needs Biotech.</summary>
    public static readonly Power Choir = new()
    {
        Id = global::RimChronicles.Core.Choir.ChoirIds.PlotId,
        NewsKind = global::RimChronicles.Core.Choir.ChoirIds.NewsKind,
        PlotTemplateId = global::RimChronicles.Core.Choir.ChoirIds.TemplateId,
        CrisisTemplateId = global::RimChronicles.Core.Choir.ChoirIds.CrisisTemplateId,
        OptionPrefix = "Choir",
        EnvoyOption = "Act1:ChoirEnvoy",
        MarchOption = "Act2:TheChoirMarches",
        ColonistTrigger = AwakeningTrigger.MechanitorColonist,
        ServiceEnding = global::RimChronicles.Core.Choir.ChoirEnding.Choral,
        DefeatEnding = global::RimChronicles.Core.Choir.ChoirEnding.Silenced,
        Text = new global::RimChronicles.Core.Choir.ChoirPowerText(),
    };

    /// <summary>Every power the mod knows, in the order their drivers run. The game runs only those its DLCs allow.</summary>
    public static IReadOnlyList<Power> All { get; } = [Empire, Choir];
}

/// <summary>A power's texts: template news, rumours and letters, and what each plot step does, for the director.</summary>
public abstract class PowerText
{
    public abstract string StirringNews { get; }

    public abstract string AwakenedNews(string name);

    public abstract IReadOnlyList<string> Rumours(string name, IReadOnlyList<string> neighbours);

    /// <summary>Why it stirred, for the log and letters: "Ashgrove's wealth drew the Empire's eye".</summary>
    public abstract string TriggerWhy(string trigger, string detail, string colony);

    public abstract (string Label, string Text) AwakenedLetter(string name);

    /// <summary>What a <see cref="PowerStep"/> would do, as the director reads it.</summary>
    public abstract string Describe(string step);

    public abstract string ExpansionNews(ExpansionResult result, string name, ExpansionTarget target);
}
