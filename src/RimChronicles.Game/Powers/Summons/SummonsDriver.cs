using LudeonTK;
using RimChronicles.Core.News;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Quests;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Powers.Summons;

/// <summary>
/// The summons, day by day (War in Heaven W6, spec §6.1, tech spec §5): while the colony stands with a power, that power
/// sends for each colonist of the kind it can't keep (the Empire for mechanitors, the Hollow Choir for psycasters), one
/// at a time. Returning converts them; refusing (declining the offer, letting it lapse, or sending the shuttle away
/// empty) sets the colony independent. Template letters only. Main thread only.
/// </summary>
public static class SummonsDriver
{
    public const string ScriptDef = "RimChronicles_Summons";
    public const string Template = "Summons";

    private static string Colony => Faction.OfPlayer.Name;

    public static void Daily(RimChroniclesGameComponent component)
    {
        var today = GenDate.DaysPassed;
        var book = component.Summons;
        foreach (var record in book.Records.Where(r => r.Status == SummonsStatus.Pending).ToList())
        {
            var quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == record.QuestId);
            switch (quest?.State)
            {
                case null:
                    book.Finish(record, SummonsStatus.Ended);
                    break;
                case QuestState.EndedOfferExpired:
                    Refuse(component, record, today); // declined, or left unanswered
                    break;
                case QuestState.EndedSuccess:
                    book.Finish(record, SummonsStatus.Returned);
                    break;
                case QuestState.EndedFailed or QuestState.EndedUnknownOutcome or QuestState.EndedInvalid:
                    book.Finish(record, SummonsStatus.Ended); // a refusal was recorded when the shuttle left
                    break;
            }
        }

        TryStart(component, today);
    }

    private static void TryStart(RimChroniclesGameComponent component, int today)
    {
        var running = PowerFactions.Running;
        var side = component.Allegiance.SideAmong(running);
        if (running.FirstOrDefault(p => p.Id == side) is not { } power
            || PowerFactions.Of(power) is not { } faction || faction.HostileTo(Faction.OfPlayer)
            || AllegianceDriver.PlotOf(component, power) is not { } plot
            || DefDatabase<QuestScriptDef>.GetNamedSilentFail(ScriptDef) is null)
        {
            return;
        }

        var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => p.Map.IsPlayerHome).ToList();
        if (component.Summons.Next(power, colonists.Select(p =>
                new SummonsCandidate(p.ThingID, p.LabelShort, SummonsConversion.IsMechanitor(p), SummonsConversion.IsPsycaster(p))))
            is not { } next || colonists.FirstOrDefault(p => p.ThingID == next.Id) is not { } pawn)
        {
            return;
        }

        var (name, description) = Letter(power, faction, pawn);
        if (QuestStarter.Offer(component, plot, Template, ScriptDef, name, description, slate =>
            {
                slate.Set(QuestNode_Root_ChroniclesSummons.PawnVar, pawn);
                slate.Set(QuestNode_Root_ChroniclesSummons.FactionVar, faction);
                slate.Set(QuestNode_Root_ChroniclesSummons.PowerVar, power.Id);
            }) is { } quest)
        {
            component.Summons.Start(power, next, quest.id, today);
            plot.Note(today, $"summons: {power.Id} sent for {pawn.LabelShort}");
        }
    }

    private static (string Name, string Description) Letter(Power power, Faction faction, Pawn pawn) => power == Power.Empire
        ? ($"The Empire's summons: {pawn.LabelShort}",
            $"{faction.Name} will not have a mechanitor in an allied colony. A shuttle will come for {pawn.LabelShort}: in "
            + $"{SummonsBook.DaysAway} days they will come back a psycaster of matching strength, and their mechs will shut down where they stand.\n\n"
            + $"Declining, or sending the shuttle away without {pawn.LabelShort}, breaks with {faction.Name}: {Colony} would stand alone.")
        : ($"The Choir's summons: {pawn.LabelShort}",
            $"{faction.Name} will not have a psycaster in the Chorus. A shuttle will come for {pawn.LabelShort}: in "
            + $"{SummonsBook.DaysAway} days they will come back a mechanitor of matching strength, their psylink gone.\n\n"
            + $"Declining, or sending the shuttle away without {pawn.LabelShort}, breaks with {faction.Name}: {Colony} would stand alone.");

    /// <summary>The shuttle left without the summoned colonist (<see cref="QuestPart_RcSummonsRefused"/>).</summary>
    public static void Notify_Refused(int questId)
    {
        if (RimChroniclesGameComponent.Current is { } component
            && component.Summons.Records.FirstOrDefault(r => r.QuestId == questId && r.Status == SummonsStatus.Pending) is { } record)
        {
            Refuse(component, record, GenDate.DaysPassed);
        }
    }

    private static void Refuse(RimChroniclesGameComponent component, SummonsRecord record, int today)
    {
        if (!component.Summons.Finish(record, SummonsStatus.Refused))
        {
            return;
        }

        var running = PowerFactions.Running;
        AllegianceDriver.Apply(component, component.Allegiance.DeclareIndependence(running, today, AllegianceCause.Summons), today);
        var summoner = Power.All.FirstOrDefault(p => p.Id == record.Power);
        var name = summoner is null ? "its ally" : PowerFactions.Of(summoner)?.Name ?? summoner.DisplayName;
        var label = "The colony stands alone";
        var text = $"{Colony} refused to give up {record.Name}. {name} counts the colony among its enemies now, and so does its rival: "
            + $"{Colony} stands alone, at war with both.";
        component.News.Add(new NewsEvent(today, summoner?.NewsKind ?? Power.Empire.NewsKind, $"{Colony} refused a summons from {name}", name));
        if (summoner is not null && AllegianceDriver.PlotOf(component, summoner) is { } plot)
        {
            plot.Tell(today, label, text);
        }

        StoryLetters.Send(label, text, LetterDefOf.ThreatBig);
    }

    /// <summary>The summoned colonist came back converted (<see cref="QuestPart_RcSummons"/>).</summary>
    public static void Notify_Converted(Pawn pawn, Power power, string became)
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        if (component.Summons.Records.FirstOrDefault(r => r.PawnId == pawn.ThingID && r.Power == power.Id && r.Status == SummonsStatus.Pending) is { } record)
        {
            component.Summons.Finish(record, SummonsStatus.Returned);
        }

        var name = PowerFactions.Of(power)?.Name ?? power.DisplayName;
        var label = $"{pawn.LabelShort} returns";
        var text = $"{pawn.LabelShort} has come back from {name}, changed: they are {became} now."
            + (power == Power.Empire ? " Their mechs stand silent where they were left; they can be dismantled for parts." : "");
        if (AllegianceDriver.PlotOf(component, power) is { } plot)
        {
            plot.Tell(GenDate.DaysPassed, label, text);
        }

        StoryLetters.Send(label, text, LetterDefOf.NeutralEvent, faction: PowerFactions.Of(power));
    }

    [DebugAction("Rim Chronicles", "War in Heaven: send a summons now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DevSummon()
    {
        if (RimChroniclesGameComponent.Current is { } component)
        {
            var before = component.Summons.Records.Count;
            TryStart(component, GenDate.DaysPassed);
            Messages.Message(component.Summons.Records.Count > before
                ? "Rim Chronicles: a summons is on offer."
                : "Rim Chronicles: no summons (the colony needs a side, and a colonist of the kind that side can't keep, and no summons pending).",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    [DebugAction("Rim Chronicles", "War in Heaven: return the summoned colonist now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DevReturn()
    {
        foreach (var part in Find.QuestManager.QuestsListForReading.Where(q => q.State == QuestState.Ongoing)
                     .SelectMany(q => q.PartsListForReading.OfType<QuestPart_RcSummons>()).Where(p => p.State == QuestPartState.Enabled))
        {
            part.returnLentColonistsInTicks = 0; // it completes on its next tick
        }
    }

    [DebugAction("Rim Chronicles", "War in Heaven: convert the selected colonist", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DevConvert()
    {
        if (Find.Selector.SingleSelectedThing is not Pawn pawn)
        {
            Messages.Message("Rim Chronicles: select one colonist first.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Find.WindowStack.Add(new FloatMenu(
        [
            new FloatMenuOption("As the Empire would (mechanitor → psycaster)", () => Messages.Message(
                $"{pawn.LabelShort} is now {SummonsConversion.ToPsycaster(pawn)}.", pawn, MessageTypeDefOf.NeutralEvent, historical: false)),
            new FloatMenuOption("As the Choir would (psycaster → mechanitor)", () => Messages.Message(
                $"{pawn.LabelShort} is now {SummonsConversion.ToMechanitor(pawn)}.", pawn, MessageTypeDefOf.NeutralEvent, historical: false)),
        ]));
    }
}
