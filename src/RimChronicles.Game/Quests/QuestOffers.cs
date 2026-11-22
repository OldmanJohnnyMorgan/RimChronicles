using RimChronicles.Core.Plots;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>Offers each quest template for a plot (Stage 4 M3). Rewards come from the storyteller's threat points
/// (<see cref="QuestTemplates.RewardSilver"/>), never from the model. Main thread only.</summary>
public static class QuestOffers
{
    public static Quest? Hunt(RimChroniclesGameComponent component, Plot plot, Pawn target, string name, string description)
    {
        component.Track(target);
        return QuestStarter.Offer(component, plot, QuestTemplates.Hunt, QuestTemplates.ScriptDef(QuestTemplates.Hunt), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesHunt.TargetVar, target);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, QuestTemplates.RewardSilver(slate.Get("points", 300f)));
        });
    }

    public static Quest? Rescue(RimChroniclesGameComponent component, Plot plot, Pawn captive, Faction captor, string name, string description)
    {
        component.Track(captive);
        return QuestStarter.Offer(component, plot, QuestTemplates.Rescue, QuestTemplates.ScriptDef(QuestTemplates.Rescue), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesRescue.CaptiveVar, captive);
            slate.Set(QuestNode_Root_ChroniclesRescue.CaptorVar, captor);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, QuestTemplates.RewardSilver(slate.Get("points", 300f)) / 2);
        });
    }

    public static Quest? Deliver(RimChroniclesGameComponent component, Plot plot, Settlement settlement, ThingDef thing, int count,
        string name, string description) =>
        QuestStarter.Offer(component, plot, QuestTemplates.Deliver, QuestTemplates.ScriptDef(QuestTemplates.Deliver), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesDeliver.SettlementVar, settlement);
            slate.Set(QuestNode_Root_ChroniclesDeliver.ThingVar, thing);
            slate.Set(QuestNode_Root_ChroniclesDeliver.CountVar, count);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, (int)(thing.BaseMarketValue * count * 1.5f));
        });

    public static Quest? Escort(RimChroniclesGameComponent component, Plot plot, Faction faction, Settlement destination, int count,
        string name, string description) =>
        QuestStarter.Offer(component, plot, QuestTemplates.Escort, QuestTemplates.ScriptDef(QuestTemplates.Escort), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesEscort.FactionVar, faction);
            slate.Set(QuestNode_Root_ChroniclesEscort.DestinationVar, destination);
            slate.Set(QuestNode_Root_ChroniclesEscort.CountVar, count);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, QuestTemplates.RewardSilver(slate.Get("points", 300f)) / 2);
        });

    public static Quest? DefendAlly(RimChroniclesGameComponent component, Plot plot, Settlement ally, Faction attacker, string name, string description) =>
        QuestStarter.Offer(component, plot, QuestTemplates.DefendAlly, QuestTemplates.ScriptDef(QuestTemplates.DefendAlly), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesDefendAlly.AllyVar, ally);
            slate.Set(QuestNode_Root_ChroniclesDefendAlly.AttackerVar, attacker);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, QuestTemplates.RewardSilver(slate.Get("points", 300f)));
        });

    public static Quest? Sabotage(RimChroniclesGameComponent component, Plot plot, Settlement target, ThingDef? objective, string name, string description) =>
        QuestStarter.Offer(component, plot, QuestTemplates.Sabotage, QuestTemplates.ScriptDef(QuestTemplates.Sabotage), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesSabotage.SettlementVar, target);
            slate.Set(QuestNode_Root_ChroniclesSabotage.ObjectiveVar, objective ?? ThingDefOf.CommsConsole);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, QuestTemplates.RewardSilver(slate.Get("points", 300f)));
        });

    /// <summary>The Empire's capital as a Sabotage quest with no objective: only destroying the settlement succeeds,
    /// within <see cref="Core.Empire.EmpireArc.CapitalDays"/> days.</summary>
    public static Quest? Capital(RimChroniclesGameComponent component, Plot plot, Settlement capital, string name, string description) =>
        QuestStarter.Offer(component, plot, QuestTemplates.Sabotage, QuestTemplates.ScriptDef(QuestTemplates.Sabotage), name, description, slate =>
        {
            slate.Set(QuestNode_Root_ChroniclesSabotage.SettlementVar, capital);
            slate.Set(QuestNode_Root_ChroniclesSabotage.NoObjectiveVar, true);
            slate.Set(QuestNode_Root_ChroniclesSabotage.DaysVar, Core.Empire.EmpireArc.CapitalDays);
            slate.Set(QuestNode_Root_ChroniclesSite.RewardVar, 0);
        });

    /// <summary>The nearest settlement (other than the player's, with no map open) matching <paramref name="filter"/>, or null.</summary>
    public static Settlement? NearestSettlement(Func<Settlement, bool> filter)
    {
        if (Find.AnyPlayerHomeMap?.Tile is not { } home)
        {
            return null;
        }

        return Find.WorldObjects.Settlements
            .Where(s => s.Faction is { IsPlayer: false } && !s.HasMap && s.Tile.Layer == home.Layer && filter(s))
            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(home, s.Tile))
            .FirstOrDefault();
    }

    /// <summary>The nearest settlement of a faction that isn't hostile and can take trade requests, or null.</summary>
    public static Settlement? NearestFriendlySettlement(Faction? only = null)
    {
        if (Find.AnyPlayerHomeMap?.Tile is not { } home)
        {
            return null;
        }

        return Find.WorldObjects.Settlements
            .Where(s => s.Faction is { IsPlayer: false } f && !f.HostileTo(Faction.OfPlayer) && (only is null || f == only)
                        && s.GetComponent<TradeRequestComp>() is { ActiveRequest: false })
            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(home, s.Tile))
            .FirstOrDefault();
    }
}

/// <summary>One option in a plot's choice letter.</summary>
/// <param name="Label">The button text.</param>
/// <param name="Answer">The value written to the plot fact when chosen.</param>
/// <param name="Silver">Silver paid when chosen (the button is greyed out without it), or 0.</param>
/// <param name="ActNow">Whether the plot acts on the answer at the next daily check, rather than on its own schedule.</param>
public sealed record PlotChoice(string Label, string Answer, int Silver = 0, bool ActNow = true);

/// <summary>The Choice letter template: 2–3 options, each mapped to an answer the plot's transitions read from a fact.
/// An unanswered letter times out and the plot carries on without an answer. UI code.</summary>
public sealed class ChoiceLetter_Plot : ChoiceLetter
{
    public string plotId = "";
    public string factKey = "";
    public List<string> labels = [];
    public List<string> answers = [];
    public List<int> costs = [];
    public List<bool> actNow = [];

    public static void Send(Plot plot, string factKey, string label, string text, IReadOnlyList<PlotChoice> options, int timeoutDays)
    {
        var letter = (ChoiceLetter_Plot)LetterMaker.MakeLetter(label, text, PlotLetterDefOf.RimChronicles_PlotChoice);
        letter.plotId = plot.Id;
        letter.factKey = factKey;
        letter.labels = options.Select(o => o.Label).ToList();
        letter.answers = options.Select(o => o.Answer).ToList();
        letter.costs = options.Select(o => o.Silver).ToList();
        letter.actNow = options.Select(o => o.ActNow).ToList();
        letter.StartTimeout(timeoutDays * GenDate.TicksPerDay);
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

            for (var i = 0; i < labels.Count; i++)
            {
                var index = i;
                var option = new DiaOption(labels[i]) { action = () => Answer(index), resolveTree = true };
                if (costs[i] > 0 && Plots.Silver.Available() < costs[i])
                {
                    option.Disable($"needs {costs[i]} silver on a home map");
                }

                yield return option;
            }

            yield return Option_Postpone;
        }
    }

    private void Answer(int index)
    {
        if (RimChroniclesGameComponent.Current?.Plots.Active.FirstOrDefault(p => p.Id == plotId) is { } plot)
        {
            if (costs[index] > 0 && !Plots.Silver.TryPay(costs[index]))
            {
                return;
            }

            plot.SetFact(factKey, answers[index]);
            plot.Note(GenDate.DaysPassed, $"the colony chose: {labels[index]}");
            if (actNow[index])
            {
                plot.NextBeatDay = GenDate.DaysPassed;
            }
        }

        Find.LetterStack.RemoveLetter(this);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref plotId, "plotId", "");
        Scribe_Values.Look(ref factKey, "factKey", "");
        Scribe_Collections.Look(ref labels, "labels", LookMode.Value);
        Scribe_Collections.Look(ref answers, "answers", LookMode.Value);
        Scribe_Collections.Look(ref costs, "costs", LookMode.Value);
        Scribe_Collections.Look(ref actNow, "actNow", LookMode.Value);
        labels ??= [];
        answers ??= [];
        costs ??= [];
        actNow ??= [];
    }
}

[DefOf]
public static class PlotLetterDefOf
{
    public static LetterDef? RimChronicles_PlotChoice;

    static PlotLetterDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PlotLetterDefOf));
}
