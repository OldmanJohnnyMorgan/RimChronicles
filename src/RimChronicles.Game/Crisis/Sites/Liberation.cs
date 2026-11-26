using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.News;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Crisis.Sites;

/// <summary>
/// A Reclaimed Holding broken (crisis C11): the settlement the Empire conquered goes back to the faction it took it
/// from, which thanks the colony. The Empire's conquest stays in its saga (its stage doesn't move back); the
/// liberation is news, a letter in the Empire plot's story, and a note in the saga. Main thread only.
/// </summary>
public static class Liberation
{
    public const int Goodwill = 15;

    /// <summary>Frees <paramref name="settlement"/> if the Empire still holds it and <paramref name="former"/> still
    /// stands. Returns false (and does nothing) otherwise.</summary>
    public static bool Free(Settlement settlement, Faction former)
    {
        // The Empire's or, since War in Heaven W8, the Hollow Choir's.
        var choir = Game.Powers.PowerFactions.Of(Power.Choir);
        var power = settlement.Faction is { } holder && holder == choir ? Power.Choir : Power.Empire;
        if (RimChroniclesGameComponent.Current is not { } component || Game.Powers.PowerFactions.Of(power) is not { } empire
            || settlement.Destroyed || settlement.Faction != empire || former.defeated)
        {
            return false;
        }

        var today = GenDate.DaysPassed;
        var colony = Faction.OfPlayer.Name;
        var garrison = power == Power.Choir ? "the Chorus garrison" : "the Imperial garrison";
        settlement.SetFaction(former);
        EmpireDriver.Repaint(settlement);
        former.TryAffectGoodwillWith(Faction.OfPlayer, Goodwill, canSendMessage: true, canSendHostilityLetter: false);
        component.SagaOf(power).Note(today, $"{settlement.Label} liberated by {colony}, back to {former.Name}");
        component.News.Add(new NewsEvent(today, power.NewsKind, $"{colony} drove {garrison} out of {settlement.Label}; it is {former.Name}'s again", empire.Name));
        var (label, text) = ($"{settlement.Label} liberated",
            $"{char.ToUpperInvariant(garrison[0])}{garrison[1..]} holding {settlement.Label} is broken. The settlement is {former.Name}'s again, and they won't forget who freed it.");
        if (power == Power.Choir)
        {
            StoryLetters.ChoirCrisis(label, text, LetterDefOf.PositiveEvent, new LookTargets(settlement));
        }
        else
        {
            StoryLetters.Crisis(label, text, LetterDefOf.PositiveEvent, new LookTargets(settlement), former);
        }

        return true;
    }
}

/// <summary>Frees the held settlement when the garrison's objective is destroyed (the quest's success signal).</summary>
public sealed class QuestPart_ChroniclesLiberate : QuestPart
{
    public string inSignal = "";
    public Settlement? settlement;
    public Faction? former;

    public override IEnumerable<GlobalTargetInfo> QuestLookTargets
    {
        get
        {
            if (settlement is not null)
            {
                yield return settlement;
            }
        }
    }

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal && settlement is not null && former is not null)
        {
            Liberation.Free(settlement, former);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal", "");
        Scribe_References.Look(ref settlement, "settlement");
        Scribe_References.Look(ref former, "former");
    }
}
