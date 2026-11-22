using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Empire;

/// <summary>The Awakened Empire's states (design spec, Stage 5).</summary>
public enum EmpireState
{
    /// <summary>Few settlements, no expansion, normal Royalty quests.</summary>
    Dormant,

    /// <summary>Rumours, envoys and demands on nearby factions, for 20–40 days.</summary>
    Stirring,

    /// <summary>Expanding by force.</summary>
    Awakened,

    /// <summary>Controls most of the region and makes final demands.</summary>
    Ascendant,

    /// <summary>Collapsing after heavy losses.</summary>
    Broken,
}

/// <summary>What woke the Empire. The ids are what the decision log shows.</summary>
public static class AwakeningTrigger
{
    public const string Wealth = "Wealth";
    public const string SiteDestroyed = "SiteDestroyed";
    public const string ImperialColonist = "ImperialColonist";
    public const string TitleRefused = "TitleRefused";
    public const string Deadline = "Deadline";
    public const string Dev = "Dev";
}

/// <summary>What the game sees today that could wake the Empire. <see cref="ImperialColonist"/> names a colonist with
/// an Imperial backstory, or is null. One-off signs (a destroyed site, a renounced title) are latched on the saga.</summary>
public sealed record EmpireSigns(float ColonyWealth, string? ImperialColonist)
{
    public static readonly EmpireSigns None = new(0, null);
}

/// <summary>Something the Empire did today that the game should show: news for the reporter, and optionally a letter
/// to the player.</summary>
public sealed record EmpireEvent(string NewsText, string? LetterLabel = null, string? LetterText = null);

/// <summary>
/// The Awakened Empire, the game-long major plot (design spec, Stage 5), as plain decisions: the state, what woke
/// it and when, and the rumours its stirring spreads. Random numbers come in from the caller so the rules are testable.
/// Saved with the game. M1 covers Dormant → Stirring → Awakened, M2 the expansion; the acts come later.
/// </summary>
public sealed class EmpireSaga : IArchivable
{
    public const string PlotId = "empire";
    public const string NewsKind = "Empire";

    /// <summary>No trigger wakes the Empire before this day, so a new colony settles first.</summary>
    public const int EarliestAwakeningDay = 30;

    /// <summary>The Empire wakes on this day at the latest.</summary>
    public const int DeadlineDay = 300;

    /// <summary>Colony wealth that draws the Empire's eye.</summary>
    public const float WealthThreshold = 150_000f;

    public const int MinStirringDays = 20;
    public const int MaxStirringDays = 40;
    public const int MinRumourGapDays = 5;
    public const int MaxRumourGapDays = 8;

    public EmpireState State = EmpireState.Dormant;

    /// <summary>The day the Empire entered <see cref="State"/>.</summary>
    public int StateDay;

    /// <summary>The day the current state ends on its own, or -1 when another event moves it on.</summary>
    public int NextStateDay = -1;

    public int NextRumourDay = -1;

    /// <summary>What woke it (an <see cref="AwakeningTrigger"/> id), and who or what, for letters and the log.</summary>
    public string Trigger = "";

    public string TriggerDetail = "";

    /// <summary>Signs that happen once and are latched until the Empire wakes: a destroyed Imperial site, a renounced title.</summary>
    public string SiteDestroyed = "";

    public string TitleRefusedBy = "";

    /// <summary>"Day N: what happened", oldest first.</summary>
    public List<string> Log = [];

    /// <summary>The day of the next expansion attack, or -1 while the Empire isn't expanding.</summary>
    public int NextExpansionDay = -1;

    /// <summary>Every expansion attack, oldest first.</summary>
    public List<ExpansionRecord> Expansions = [];

    public int Conquests => Expansions.Count(e => e.Result != ExpansionResult.Repelled);

    public int Defeats => Expansions.Count(e => e.Result == ExpansionResult.Repelled);

    public bool ExpansionDue(int today) => State is EmpireState.Awakened or EmpireState.Ascendant && NextExpansionDay >= 0 && today >= NextExpansionDay;

    /// <summary>Records an attack's result and schedules the next one <paramref name="nextGapDays"/> days on.</summary>
    public EmpireEvent Expanded(ExpansionTarget target, ExpansionResult result, int today, int nextGapDays, string empire)
    {
        Expansions.Add(new ExpansionRecord { Day = today, Settlement = target.Name, Faction = target.Faction, Result = result });
        NextExpansionDay = today + Math.Max(1, nextGapDays);
        var news = EmpireExpansion.News(result, empire, target);
        Note(today, $"expansion: {news}");
        return new EmpireEvent(news);
    }

    public bool IsAwake => State != EmpireState.Dormant;

    public void Note(int day, string line) => Log.Add($"Day {day}: {line}");

    /// <summary>The awakening triggers present today, with their weights: the more pointed the provocation, the heavier.</summary>
    public IReadOnlyList<PlotTransition> Triggers(EmpireSigns signs, int today)
    {
        var options = new List<PlotTransition>();
        if (State != EmpireState.Dormant)
        {
            return options;
        }

        if (today >= EarliestAwakeningDay)
        {
            if (SiteDestroyed.Length > 0)
            {
                options.Add(new($"Awaken:{AwakeningTrigger.SiteDestroyed}", AwakeningTrigger.SiteDestroyed, 4));
            }

            if (TitleRefusedBy.Length > 0)
            {
                options.Add(new($"Awaken:{AwakeningTrigger.TitleRefused}", AwakeningTrigger.TitleRefused, 3));
            }

            if (signs.ImperialColonist is not null)
            {
                options.Add(new($"Awaken:{AwakeningTrigger.ImperialColonist}", AwakeningTrigger.ImperialColonist, 2));
            }

            if (signs.ColonyWealth >= WealthThreshold)
            {
                options.Add(new($"Awaken:{AwakeningTrigger.Wealth}", AwakeningTrigger.Wealth, 1));
            }
        }

        if (options.Count == 0 && today >= DeadlineDay)
        {
            options.Add(new($"Awaken:{AwakeningTrigger.Deadline}", AwakeningTrigger.Deadline, 1));
        }

        return options;
    }

    /// <summary>The detail a trigger carries ("Ashgrove", "Ada"), from the latched signs and today's.</summary>
    public string DetailFor(string trigger, EmpireSigns signs) => trigger switch
    {
        AwakeningTrigger.SiteDestroyed => SiteDestroyed,
        AwakeningTrigger.TitleRefused => TitleRefusedBy,
        AwakeningTrigger.ImperialColonist => signs.ImperialColonist ?? "",
        AwakeningTrigger.Wealth => ((int)signs.ColonyWealth).ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "",
    };

    /// <summary>Dormant → Stirring, for <paramref name="trigger"/>. The stirring lasts
    /// <paramref name="stirringDays"/> days (pick it from the spec's 20–40).</summary>
    public EmpireEvent Awaken(string trigger, string detail, int today, int stirringDays, string colony)
    {
        State = EmpireState.Stirring;
        StateDay = today;
        NextStateDay = today + Math.Clamp(stirringDays, 1, MaxStirringDays);
        NextRumourDay = today + MinRumourGapDays;
        Trigger = trigger;
        TriggerDetail = detail;
        Note(today, $"stirring: {EmpireText.TriggerWhy(trigger, detail, colony)}");
        return new EmpireEvent(EmpireText.StirringNews);
    }

    /// <summary>Stirring → Awakened: the Empire starts expanding by force.</summary>
    public EmpireEvent Wake(int today, string empire)
    {
        State = EmpireState.Awakened;
        StateDay = today;
        NextStateDay = -1;
        NextRumourDay = -1;
        NextExpansionDay = today + EmpireExpansion.FirstAttackDays;
        Note(today, "awakened: expanding by force");
        var (label, text) = EmpireText.AwakenedLetter(empire);
        return new EmpireEvent(EmpireText.AwakenedNews(empire), label, text);
    }

    /// <summary>A change of territory the colony caused (a sabotaged target falling, a siege broken or lost), recorded
    /// like an expansion but without moving the expansion timer.</summary>
    public void RecordAssisted(string settlement, string faction, ExpansionResult result, int today, string why)
    {
        Expansions.Add(new ExpansionRecord { Day = today, Settlement = settlement, Faction = faction, Result = result });
        Note(today, $"colony's doing: {why}");
    }

    /// <summary>Act 3: Awakened → Ascendant or Broken.</summary>
    public void Reckon(bool ascendant, int today)
    {
        State = ascendant ? EmpireState.Ascendant : EmpireState.Broken;
        StateDay = today;
        NextExpansionDay = ascendant ? NextExpansionDay : -1;
        Note(today, ascendant ? "ascendant: final demands" : "broken: collapsing after its losses");
    }

    /// <summary>A rumour of the stirring, if one is due today (and schedules the next). <paramref name="neighbours"/>
    /// are the factions the Empire might make demands of; <paramref name="pick"/> chooses an index below its argument.</summary>
    public EmpireEvent? Rumour(int today, string empire, IReadOnlyList<string> neighbours, Func<int, int> pick, int gapDays)
    {
        if (State != EmpireState.Stirring || NextRumourDay < 0 || today < NextRumourDay)
        {
            return null;
        }

        NextRumourDay = today + Math.Max(1, gapDays);
        var lines = EmpireText.Rumours(empire, neighbours);
        var line = lines[pick(lines.Count)];
        Note(today, $"rumour: {line}");
        return new EmpireEvent(line);
    }

    /// <summary>True once the stirring has run its course.</summary>
    public bool StirringOver(int today) => State == EmpireState.Stirring && NextStateDay >= 0 && today >= NextStateDay;

    public void Expose(IArchive archive)
    {
        archive.Value(ref State, "state", EmpireState.Dormant);
        archive.Value(ref StateDay, "stateDay");
        archive.Value(ref NextStateDay, "nextStateDay", -1);
        archive.Value(ref NextRumourDay, "nextRumourDay", -1);
        archive.Value(ref Trigger, "trigger", "");
        archive.Value(ref TriggerDetail, "triggerDetail", "");
        archive.Value(ref SiteDestroyed, "siteDestroyed", "");
        archive.Value(ref TitleRefusedBy, "titleRefusedBy", "");
        archive.Strings(ref Log, "log");
        archive.Value(ref NextExpansionDay, "nextExpansionDay", -1);
        archive.DeepList(ref Expansions, "expansions");
        Trigger ??= "";
        TriggerDetail ??= "";
        SiteDestroyed ??= "";
        TitleRefusedBy ??= "";
    }
}

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
        AwakeningTrigger.Dev => "woken from Dev Mode",
        _ => trigger,
    };

    public static (string Label, string Text) AwakenedLetter(string empire) => (
        "The Empire awakens",
        $"{empire} has woken from its long quiet. Its envoys no longer ask: they announce. Word along the trade roads is that "
        + "the Empire means to bring the whole region under its rule, settlement by settlement.\n\n"
        + "No one yet knows what it wants of your colony.");
}
