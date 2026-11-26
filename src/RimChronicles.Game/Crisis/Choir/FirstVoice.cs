using LudeonTK;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.News;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Crisis.Units;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>
/// The First Voice in game (War in Heaven W9), the High Stellarch's mirror: one pawn, made once and kept between its
/// appearances, leading the final chorus and the Cathedral's bossgroup guard. Its death or capture is announced, and
/// carries the colony's assault on the Cathedral. Unlike the Stellarch it doesn't skip away: it is a mechanitor, and
/// bringing it down silences the mechs it commands. Its record is the Choir crisis's leader fields
/// (<see cref="CrisisState.StellarchPawnId"/> and the rest, on <see cref="RimChroniclesGameComponent.ChoirCrisis"/>).
/// Main thread only.
/// </summary>
public static class FirstVoice
{
    private static CrisisState? State => RimChroniclesGameComponent.Current?.ChoirCrisis;

    public static bool Is(Pawn? pawn) => pawn is not null && State is { StellarchPawnId.Length: > 0 } s && pawn.ThingID == s.StellarchPawnId;

    /// <summary>The First Voice, ready to spawn: the one met before if it's off the maps, a new one the first time; null
    /// once it has fallen, or while it's already on a map.</summary>
    public static Pawn? Ready(Faction choir)
    {
        if (State is not { } state || RimChroniclesGameComponent.Current is not { } component || !StellarchRules.CanLead(state))
        {
            return null;
        }

        if (state.StellarchPawnId.Length > 0 && component.TrackedOrFind(state.StellarchPawnId) is { } known)
        {
            return known.Dead || known.Spawned || known.IsPrisoner ? null : known;
        }

        if (UnitKit.ChoirKinds.FirstOrDefault(k => UnitKit.Unit(k)?.role == UnitRole.FirstVoice) is not { } kind)
        {
            return null;
        }

        var pawn = UnitKit.Generate(kind, choir, CrisisStage.Finale);
        state.StellarchPawnId = pawn.ThingID;
        state.StellarchFate = StellarchFate.Alive;
        component.Track(pawn); // kept between appearances
        return pawn;
    }

    /// <summary>The First Voice died or was taken: once, a letter and the news, and if the colony is storming the
    /// Cathedral, that assault has won.</summary>
    public static void Fell(Pawn pawn, string fate)
    {
        if (!Is(pawn) || State is not { } state || RimChroniclesGameComponent.Current is not { } component
            || !StellarchRules.Fell(state, fate, GenDate.DaysPassed))
        {
            return;
        }

        var choir = ChoirCrisisDriver.Faction;
        var how = fate == StellarchFate.Captured ? "taken prisoner" : "killed";
        var carried = CarryTheAssault(component);
        StoryLetters.ChoirCrisis($"The First Voice is {(fate == StellarchFate.Captured ? "captured" : "dead")}",
            $"{pawn.LabelShort}, the First Voice of the Hollow Choir, has been {how}. The Chorus has lost the voice it sang with."
            + (carried ? "\n\nWith the First Voice fallen, the assault on the Cathedral has won." : ""),
            LetterDefOf.PositiveEvent, pawn);
        component.News.Add(new NewsEvent(GenDate.DaysPassed, Power.Choir.NewsKind,
            $"The First Voice of the Hollow Choir was {how} by {Faction.OfPlayer.Name}", choir?.Name ?? ""));
    }

    /// <summary>Ends the Choir plot's pending assault on the Cathedral in success, if there is one.</summary>
    private static bool CarryTheAssault(RimChroniclesGameComponent component)
    {
        if (Powers.ChoirBeats.Current(component) is not { } plot || plot.Fact(PowerArc.Job) != PowerStep.StormCapital)
        {
            return false;
        }

        var link = component.Plots.Quests.LastOrDefault(q => q.PlotId == plot.Id && q.Pending);
        if (link is null || Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == link.QuestId) is not { State: QuestState.Ongoing } quest)
        {
            return false;
        }

        quest.End(QuestEndOutcome.Success);
        return true;
    }

    [DebugAction("Rim Chronicles", "Choir crisis: First Voice state", allowedGameStates = AllowedGameStates.Playing)]
    public static void Show()
    {
        var state = State;
        var pawn = state is { StellarchPawnId.Length: > 0 } s ? RimChroniclesGameComponent.Current?.TrackedOrFind(s.StellarchPawnId) : null;
        Find.WindowStack.Add(new Dialog_MessageBox(state is null
            ? "No game."
            : $"First Voice: {(pawn?.LabelShort ?? "not met yet")}; fate {(state.StellarchFate.Length == 0 ? "not met" : state.StellarchFate)}"
              + (state.StellarchFellDay >= 0 ? $" on day {state.StellarchFellDay}" : "")
              + $"; {(pawn is { Spawned: true } ? $"on {pawn.Map}" : "off the maps")}."));
    }
}
