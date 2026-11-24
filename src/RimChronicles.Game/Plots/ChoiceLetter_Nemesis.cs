using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>The Nemesis choice letter: post a bounty (paid in silver from the colony's maps) or wait for the nemesis to
/// return. The answer is stored in the plot; an unanswered letter times out and the colony waits. UI code.</summary>
public sealed class ChoiceLetter_Nemesis : ChoiceLetter
{
    public string plotId = "";
    public int cost;

    public static void Send(string plotId, string label, string text, int cost)
    {
        text += StoryLetters.IfUnanswered(NemesisArc.ChoiceTimeoutDays, "the colony waits for them to return");
        var letter = (ChoiceLetter_Nemesis)LetterMaker.MakeLetter(label, text, NemesisLetterDefOf.RimChronicles_NemesisChoice);
        StoryLetters.Point(letter, plotId);
        StoryLetters.Record(plotId, label, text);
        letter.plotId = plotId;
        letter.cost = cost;
        letter.StartTimeout(NemesisArc.ChoiceTimeoutDays * GenDate.TicksPerDay);
        Find.LetterStack.ReceiveLetter(letter);
    }

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            if (ArchivedOnly)
            {
                yield return Option_Close;
                yield break;
            }

            var bounty = new DiaOption(string.Format(NemesisLetters.BountyOption, cost))
            {
                action = () => Answer(NemesisArc.ChoiceBounty),
                resolveTree = true,
            };
            if (Silver.Available() < cost)
            {
                bounty.Disable($"needs {cost} silver on a home map");
            }

            yield return bounty;
            yield return new DiaOption(NemesisLetters.HuntOption) { action = () => Answer(NemesisArc.ChoiceHunt), resolveTree = true };
            yield return new DiaOption(NemesisLetters.WaitOption) { action = () => Answer(NemesisArc.ChoiceWait), resolveTree = true };
            yield return Option_Postpone;
        }
    }

    private void Answer(string choice)
    {
        if (RimChroniclesGameComponent.Current?.Plots.Active.FirstOrDefault(p => p.Id == plotId) is { } plot && plot.Fact(NemesisArc.Choice) is null or "")
        {
            if (choice == NemesisArc.ChoiceBounty && !Silver.TryPay(cost))
            {
                return;
            }

            plot.SetFact(NemesisArc.Choice, choice);
            plot.Note(GenDate.DaysPassed, choice switch
            {
                NemesisArc.ChoiceBounty => $"the colony posted a {cost}-silver bounty",
                NemesisArc.ChoiceHunt => "the colony chose to hunt them down",
                _ => "the colony chose to wait",
            });
            if (choice == NemesisArc.ChoiceHunt)
            {
                plot.NextBeatDay = GenDate.DaysPassed; // offer the Hunt quest at the next daily check
            }

            StoryLetters.Answered(plot, choice switch
            {
                NemesisArc.ChoiceBounty => $"Bounty posted: {cost} silver paid.",
                NemesisArc.ChoiceHunt => "The colony will hunt them down. A quest will follow.",
                _ => "The colony waits for them to return.",
            });
        }

        Find.LetterStack.RemoveLetter(this);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref plotId, "plotId", "");
        Scribe_Values.Look(ref cost, "cost");
    }
}

/// <summary>Silver anywhere on the colony's home maps (the game's trade helpers only see silver near a trade beacon).</summary>
public static class Silver
{
    private static IEnumerable<Thing> Stacks() =>
        Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.listerThings.ThingsOfDef(ThingDefOf.Silver))
            .Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer));

    public static int Available() => Stacks().Sum(t => t.stackCount);

    public static bool TryPay(int amount)
    {
        if (Available() < amount)
        {
            return false;
        }

        foreach (var stack in Stacks().ToList())
        {
            var take = Math.Min(amount, stack.stackCount);
            stack.SplitOff(take).Destroy();
            amount -= take;
            if (amount <= 0)
            {
                break;
            }
        }

        return true;
    }
}

[DefOf]
public static class NemesisLetterDefOf
{
    public static LetterDef? RimChronicles_NemesisChoice;

    static NemesisLetterDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NemesisLetterDefOf));
}
