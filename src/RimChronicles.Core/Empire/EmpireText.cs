using RimChronicles.Core.Powers;

namespace RimChronicles.Core.Empire;

/// <summary>The Empire's news lines and letters (template text: M1 has no model-written Empire text).</summary>
public static class EmpireText
{
    public const string StirringNews = "Imperial couriers have been seen riding hard between the Empire's settlements";

    public static string AwakenedNews(string empire) => $"{empire} declared that it will bring the region under its rule";

    public static IReadOnlyList<string> Rumours(string empire, IReadOnlyList<string> neighbours)
    {
        var lines = new List<string>
        {
            $"{empire} recalled its officers to the capital",
            $"Imperial shuttles were seen in numbers over the hills",
        };
        foreach (var n in neighbours)
        {
            lines.Add($"Imperial envoys arrived at the gates of {n}");
            lines.Add($"{empire} demanded tribute from {n}");
        }

        return lines;
    }

    public static string TriggerWhy(string trigger, string detail, string colony) => trigger switch
    {
        AwakeningTrigger.SiteDestroyed => $"{colony} destroyed the Imperial site {detail}",
        AwakeningTrigger.TitleRefused => $"{detail} of {colony} renounced an Imperial title",
        AwakeningTrigger.ImperialColonist => $"{detail}, a colonist of {colony}, was recognised as one of the Empire's own",
        AwakeningTrigger.Wealth => $"{colony}'s wealth ({detail} silver) drew the Empire's eye",
        AwakeningTrigger.Deadline => "the Empire's long quiet ran out",
        AwakeningTrigger.Rival => "its old enemy, the Hollow Choir, came out of the dark and attacked",
        AwakeningTrigger.ShipLaunch => $"{colony} started a ship reactor, and the Empire will not let it leave",
        AwakeningTrigger.RoyalAscent => $"{colony} began the Royal Ascent",
        AwakeningTrigger.Dev => "woken from Dev Mode",
        _ => trigger,
    };

    public static (string Label, string Text) AwakenedLetter(string empire) => (
        "The Empire awakens",
        $"{empire} has woken from its long quiet. Its envoys no longer ask: they announce. Word along the trade roads is that "
        + "the Empire means to bring the whole region under its rule, settlement by settlement.\n\n"
        + "No one yet knows what it wants of your colony.");

    /// <summary>What each step would do, for the director.</summary>
    public static string Describe(string step) => step switch
    {
        PowerStep.Envoy => "an Imperial envoy asks the colony to stand with the Empire, against it, or aside",
        PowerStep.Expansion => "the Empire starts taking settlements by force (Act 2)",
        PowerStep.EscortEnvoy => "the Empire asks its ally to escort its envoys to an Imperial settlement",
        PowerStep.ConquestJob => "the Empire asks its ally to sabotage a settlement it means to take",
        PowerStep.DefendAlly => "a neighbour under Imperial siege asks the colony to break the siege",
        PowerStep.SabotageImperial => "the colony is offered a chance to sabotage an Imperial settlement",
        PowerStep.Tribute => "the Empire demands tribute: pay, refuse (war), or swear fealty",
        PowerStep.Aftermath => "the last job's outcome takes effect",
        PowerStep.Reckoning => "Act 3: the Empire becomes ascendant or breaks, and the endings open",
        PowerStep.Governorship => "the Empire offers a governorship; accepting ends the story in Imperial victory",
        PowerStep.StormCapital => "the colony is offered the assault on the Imperial capital",
        PowerStep.FinalDemand => "the Empire's final demand: submit, or be counted among its enemies",
        PowerStep.Treaty => "the Empire offers a treaty of independence; signing ends the story",
        PowerStep.Ending => "the story reaches its ending",
        _ => step,
    };

    public static string ExpansionNews(ExpansionResult result, string empire, ExpansionTarget t) => result switch
    {
        ExpansionResult.Conquered => $"{empire} took {t.Name} from {t.Faction}",
        ExpansionResult.Razed => $"{empire} razed {t.Name}, a settlement of {t.Faction}",
        _ => $"{t.Faction} drove off an Imperial attack on {t.Name}",
    };
}

/// <summary>The Empire's ids, as they were before the War in Heaven (saves, the decision log and director cases use
/// them).</summary>
public static class EmpireIds
{
    public const string PlotId = "empire";
    public const string NewsKind = "Empire";
    public const string TemplateId = "AwakenedEmpire";
}

/// <summary>The Empire's <see cref="PowerText"/>: <see cref="EmpireText"/>.</summary>
public sealed class EmpirePowerText : PowerText
{
    public override string StirringNews => EmpireText.StirringNews;

    public override string AwakenedNews(string name) => EmpireText.AwakenedNews(name);

    public override IReadOnlyList<string> Rumours(string name, IReadOnlyList<string> neighbours) => EmpireText.Rumours(name, neighbours);

    public override string TriggerWhy(string trigger, string detail, string colony) => EmpireText.TriggerWhy(trigger, detail, colony);

    public override (string Label, string Text) AwakenedLetter(string name) => EmpireText.AwakenedLetter(name);

    public override string Describe(string step) => EmpireText.Describe(step);

    public override string ExpansionNews(ExpansionResult result, string name, ExpansionTarget target) => EmpireText.ExpansionNews(result, name, target);
}
