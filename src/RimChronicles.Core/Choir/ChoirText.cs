using RimChronicles.Core.Powers;

namespace RimChronicles.Core.Choir;

/// <summary>The Hollow Choir's ids (War in Heaven spec §3, W3). Its plot and crisis templates come with W4 and W7.</summary>
public static class ChoirIds
{
    public const string PlotId = "choir";
    public const string NewsKind = "HollowChoir";
    public const string TemplateId = "HollowChoir";
    public const string CrisisTemplateId = "ChoirCrisis";

    /// <summary>The faction's defName (Defs/FactionDefs/RimChronicles_HollowChoir.xml).</summary>
    public const string FactionDefName = "RimChronicles_HollowChoir";
}

/// <summary>The Hollow Choir's news lines and letters (template text). Its people call themselves the Chorus; the hum
/// of their mechs is the sign that they are near (spec §3).</summary>
public static class ChoirText
{
    public const string StirringNews = "Travellers on the rim report a low hum at night, coming from nowhere they can find";

    public static string AwakenedNews(string choir) => $"{choir} has come out of hiding, and its mechs are on the march";

    public static IReadOnlyList<string> Rumours(string choir, IReadOnlyList<string> neighbours)
    {
        var lines = new List<string>
        {
            "Caravan guards swear the old mech ruins are humming again",
            "Mechanoid husks were found stripped of their cores, with no sign of a fight",
        };
        foreach (var n in neighbours)
        {
            lines.Add($"The people of {n} hear a hum in the hills at night");
            lines.Add($"Strangers with mechlinks were seen watching {n}");
        }

        return lines;
    }

    public static string TriggerWhy(string trigger, string detail, string colony) => trigger switch
    {
        AwakeningTrigger.SiteDestroyed => $"{colony} destroyed the Choir's site {detail}",
        AwakeningTrigger.ImperialColonist => $"{detail}, a mechanitor of {colony}, was heard by the Choir",
        AwakeningTrigger.Wealth => $"{colony}'s wealth ({detail} silver) drew the Choir out of the dark",
        AwakeningTrigger.Deadline => "the Choir's long silence ended",
        AwakeningTrigger.Dev => "woken from Dev Mode",
        _ => trigger,
    };

    public static (string Label, string Text) AwakenedLetter(string choir) => (
        "The Hollow Choir awakens",
        $"{choir} has come out of the dark. Its mechs march in step, humming in one voice, and the mechanitors who lead them "
        + "call themselves the Chorus. They say the Empire is a usurper, and that the rim will be remade in war.\n\n"
        + "No one yet knows what they want of your colony.");

    public static string Describe(string step) => step switch
    {
        PowerStep.Envoy => "a Chorus envoy asks the colony to stand with the Choir, against it, or aside",
        PowerStep.Expansion => "the Choir starts taking settlements by force (Act 2)",
        PowerStep.EscortEnvoy => "the Choir asks its ally to escort its envoys to a Choir settlement",
        PowerStep.ConquestJob => "the Choir asks its ally to sabotage a settlement it means to take",
        PowerStep.DefendAlly => "a neighbour under Choir siege asks the colony to break the siege",
        PowerStep.SabotageImperial => "the colony is offered a chance to sabotage a Choir settlement",
        PowerStep.Tribute => "the Choir demands tribute: pay, refuse (war), or join the Chorus",
        PowerStep.Aftermath => "the last job's outcome takes effect",
        PowerStep.Reckoning => "Act 3: the Choir becomes ascendant or breaks, and the endings open",
        PowerStep.Governorship => "the First Voice offers the colony a place in the Chorus; accepting ends the story in Choral victory",
        PowerStep.StormCapital => "the colony is offered the assault on the Choir's Cathedral",
        PowerStep.FinalDemand => "the Choir's final demand: join the Chorus, or be counted among its enemies",
        PowerStep.Treaty => "the Choir offers a treaty of independence; signing ends the story",
        PowerStep.Ending => "the story reaches its ending",
        _ => step,
    };

    public static string ExpansionNews(ExpansionResult result, string choir, ExpansionTarget t) => result switch
    {
        ExpansionResult.Conquered => $"{choir} took {t.Name} from {t.Faction}",
        ExpansionResult.Razed => $"{choir} razed {t.Name}, a settlement of {t.Faction}",
        _ => $"{t.Faction} drove off a Choir attack on {t.Name}",
    };
}

/// <summary>The Hollow Choir's <see cref="PowerText"/>: <see cref="ChoirText"/>.</summary>
public sealed class ChoirPowerText : PowerText
{
    public override string StirringNews => ChoirText.StirringNews;

    public override string AwakenedNews(string name) => ChoirText.AwakenedNews(name);

    public override IReadOnlyList<string> Rumours(string name, IReadOnlyList<string> neighbours) => ChoirText.Rumours(name, neighbours);

    public override string TriggerWhy(string trigger, string detail, string colony) => ChoirText.TriggerWhy(trigger, detail, colony);

    public override (string Label, string Text) AwakenedLetter(string name) => ChoirText.AwakenedLetter(name);

    public override string Describe(string step) => ChoirText.Describe(step);

    public override string ExpansionNews(ExpansionResult result, string name, ExpansionTarget target) => ChoirText.ExpansionNews(result, name, target);
}
