using RimChronicles.Core.Empire;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;

namespace RimChronicles.Core.Powers;

/// <summary>A power's states (design spec, Stage 5; the Hollow Choir mirrors them, War in Heaven spec §3.1).</summary>
public enum PowerState
{
    /// <summary>Few settlements, no expansion, normal Royalty quests.</summary>
    Dormant,

    /// <summary>Rumours, envoys and demands on nearby factions, for 10–20 days.</summary>
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

    /// <summary>The Hollow Choir's mirror of <see cref="ImperialColonist"/>: a mechanitor among the colonists.</summary>
    public const string MechanitorColonist = "MechanitorColonist";
    public const string TitleRefused = "TitleRefused";
    public const string Deadline = "Deadline";

    /// <summary>The rival power woke and attacked (War in Heaven spec §4): this one wakes at once.</summary>
    public const string Rival = "Rival";

    /// <summary>The colony started a ship reactor: the Empire wakes at once (spec §5).</summary>
    public const string ShipLaunch = "ShipLaunch";

    /// <summary>The colony accepted the Royal Ascent: the Hollow Choir wakes at once (spec §5).</summary>
    public const string RoyalAscent = "RoyalAscent";
    public const string Dev = "Dev";
}

/// <summary>What the game sees today that could wake a power. <see cref="ImperialColonist"/> names the colonist who
/// draws it (an Imperial backstory for the Empire, a mechlink for the Choir), or is null. One-off signs (a destroyed site, a renounced title) are latched on the saga.</summary>
public sealed record PowerSigns(float ColonyWealth, string? ImperialColonist)
{
    public static readonly PowerSigns None = new(0, null);
}

/// <summary>Something the Empire did today that the game should show: news for the reporter, and optionally a letter
/// to the player.</summary>
public sealed record PowerEvent(string NewsText, string? LetterLabel = null, string? LetterText = null);

/// <summary>
/// A power's saga (the Awakened Empire, design spec Stage 5; the Hollow Choir from W4), as plain decisions: the state,
/// what woke it and when, the rumours its stirring spreads and its expansion. Random numbers come in from the caller so
/// the rules are testable. Saved with the game; <see cref="Power"/> isn't saved, the owner sets it.
/// </summary>
public sealed class PowerSaga : IArchivable
{
    /// <summary>Which power this saga is. Not saved: the owner sets it (the Empire's saga is the default).</summary>
    public Power Power = Power.Empire;

    /// <summary>The opening hook (decided 2026-11-26): the story's first power stirs on this day, whatever the signs;
    /// they only say why. The Empire's day, or the Choir's when it runs alone (no Royalty).</summary>
    public const int HookDay = 20;

    /// <summary>The second power's earliest day to wake on its own signs (the Choir beside the Empire): it usually wakes
    /// sooner, when the first power attacks (War in Heaven spec §4).</summary>
    public const int LateEarliestDay = 30;

    /// <summary>The second power's deadline.</summary>
    public const int LateDeadlineDay = 120;

    /// <summary>Colony wealth that draws the Empire's eye.</summary>
    public const float WealthThreshold = 80_000f;

    public const int MinStirringDays = 10;
    public const int MaxStirringDays = 20;
    public const int MinRumourGapDays = 5;
    public const int MaxRumourGapDays = 8;

    public PowerState State = PowerState.Dormant;

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

    /// <summary>The settlement the power took last (its front advances from there), or null.</summary>
    public string? LastConquest => Expansions.LastOrDefault(e => e.Result == ExpansionResult.Conquered)?.Settlement;

    public int Defeats => Expansions.Count(e => e.Result == ExpansionResult.Repelled);

    public bool ExpansionDue(int today) => State is PowerState.Awakened or PowerState.Ascendant && NextExpansionDay >= 0 && today >= NextExpansionDay;

    /// <summary>Records an attack's result and schedules the next one <paramref name="nextGapDays"/> days on.</summary>
    public PowerEvent Expanded(ExpansionTarget target, ExpansionResult result, int today, int nextGapDays, string empire)
    {
        Expansions.Add(new ExpansionRecord { Day = today, Settlement = target.Name, Faction = target.Faction, Result = result });
        NextExpansionDay = today + Math.Max(1, nextGapDays);
        var news = Power.Text.ExpansionNews(result, empire, target);
        Note(today, $"expansion: {news}");
        return new PowerEvent(news);
    }

    public bool IsAwake => State != PowerState.Dormant;

    /// <summary>A colonist holding a high Imperial title (Knight or above), read daily; "" if none. Opens the
    /// governorship in Act 3 for an allied colony.</summary>
    public string HighTitleHolder = "";

    /// <summary>The ending reached (an <see cref="EmpireEnding"/>), or "" while the plot runs.</summary>
    public string Ending = "";

    /// <summary>The ship-launch reaction (Stage 5 M5): the next Imperial raid while a ship reactor starts up, or -1.</summary>
    public int NextShipRaidDay = -1;

    public void Note(int day, string line) => Log.Add($"Day {day}: {line}");

    /// <summary>The awakening triggers present today, with their weights: the more pointed the provocation, the heavier.
    /// <paramref name="deadlineDay"/> overrides the power's own deadline (the Choir running alone takes the hook day).</summary>
    public IReadOnlyList<PlotTransition> Triggers(PowerSigns signs, int today, int? deadlineDay = null)
    {
        var deadline = deadlineDay ?? Power.DeadlineDay;
        var earliest = Math.Min(Power.EarliestWakeDay, deadline);
        var options = new List<PlotTransition>();
        if (State != PowerState.Dormant)
        {
            return options;
        }

        if (today >= earliest)
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
                options.Add(new($"Awaken:{Power.ColonistTrigger}", Power.ColonistTrigger, 2));
            }

            if (signs.ColonyWealth >= WealthThreshold)
            {
                options.Add(new($"Awaken:{AwakeningTrigger.Wealth}", AwakeningTrigger.Wealth, 1));
            }
        }

        if (options.Count == 0 && today >= deadline)
        {
            options.Add(new($"Awaken:{AwakeningTrigger.Deadline}", AwakeningTrigger.Deadline, 1));
        }

        return options;
    }

    /// <summary>The detail a trigger carries ("Ashgrove", "Ada"), from the latched signs and today's.</summary>
    public string DetailFor(string trigger, PowerSigns signs) => trigger switch
    {
        AwakeningTrigger.SiteDestroyed => SiteDestroyed,
        AwakeningTrigger.TitleRefused => TitleRefusedBy,
        AwakeningTrigger.ImperialColonist or AwakeningTrigger.MechanitorColonist => signs.ImperialColonist ?? "",
        AwakeningTrigger.Wealth => ((int)signs.ColonyWealth).ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "",
    };

    /// <summary>Dormant → Stirring, for <paramref name="trigger"/>. The stirring lasts
    /// <paramref name="stirringDays"/> days (pick it from <see cref="MinStirringDays"/>–<see cref="MaxStirringDays"/>, scaled by the
    /// story intensity; a quiet story may double it).</summary>
    public PowerEvent Awaken(string trigger, string detail, int today, int stirringDays, string colony)
    {
        State = PowerState.Stirring;
        StateDay = today;
        NextStateDay = today + Math.Clamp(stirringDays, 1, StoryIntensity.Scale(MaxStirringDays, StoryIntensity.Min));
        NextRumourDay = today + MinRumourGapDays;
        Trigger = trigger;
        TriggerDetail = detail;
        Note(today, $"stirring: {Power.Text.TriggerWhy(trigger, detail, colony)}");
        return new PowerEvent(Power.Text.StirringNews);
    }

    /// <summary>Dormant or Stirring → Awakened at once, skipping the stirring (War in Heaven spec §4–§5: a rival's attack,
    /// the ship launch, the Royal Ascent). Returns null if it is already awake.</summary>
    public PowerEvent? WakeAtOnce(string trigger, string detail, int today, string name, string colony)
    {
        if (State is not (PowerState.Dormant or PowerState.Stirring))
        {
            return null;
        }

        if (State == PowerState.Dormant)
        {
            Trigger = trigger;
            TriggerDetail = detail;
        }

        Note(today, $"woken at once: {Power.Text.TriggerWhy(trigger, detail, colony)}");
        return Wake(today, name);
    }

    /// <summary>Stirring → Awakened: the Empire starts expanding by force.</summary>
    public PowerEvent Wake(int today, string empire)
    {
        State = PowerState.Awakened;
        StateDay = today;
        NextStateDay = -1;
        NextRumourDay = -1;
        NextExpansionDay = today + PowerExpansion.FirstAttackDays;
        Note(today, "awakened: expanding by force");
        var (label, text) = Power.Text.AwakenedLetter(empire);
        return new PowerEvent(Power.Text.AwakenedNews(empire), label, text);
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
        State = ascendant ? PowerState.Ascendant : PowerState.Broken;
        StateDay = today;
        NextExpansionDay = ascendant ? NextExpansionDay : -1;
        Note(today, ascendant ? "ascendant: final demands" : "broken: collapsing after its losses");
    }

    /// <summary>A rumour of the stirring, if one is due today (and schedules the next). <paramref name="neighbours"/>
    /// are the factions the Empire might make demands of; <paramref name="pick"/> chooses an index below its argument.</summary>
    public PowerEvent? Rumour(int today, string empire, IReadOnlyList<string> neighbours, Func<int, int> pick, int gapDays)
    {
        if (State != PowerState.Stirring || NextRumourDay < 0 || today < NextRumourDay)
        {
            return null;
        }

        NextRumourDay = today + Math.Max(1, gapDays);
        var lines = Power.Text.Rumours(empire, neighbours);
        var line = lines[pick(lines.Count)];
        Note(today, $"rumour: {line}");
        return new PowerEvent(line);
    }

    /// <summary>True once the stirring has run its course.</summary>
    public bool StirringOver(int today) => State == PowerState.Stirring && NextStateDay >= 0 && today >= NextStateDay;

    public void Expose(IArchive archive)
    {
        archive.Value(ref State, "state", PowerState.Dormant);
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
        archive.Value(ref HighTitleHolder, "highTitleHolder", "");
        archive.Value(ref Ending, "ending", "");
        archive.Value(ref NextShipRaidDay, "nextShipRaidDay", -1);
        HighTitleHolder ??= "";
        Ending ??= "";
        Trigger ??= "";
        TriggerDetail ??= "";
        SiteDestroyed ??= "";
        TitleRefusedBy ??= "";
    }
}
