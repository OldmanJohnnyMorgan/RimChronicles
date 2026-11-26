using LudeonTK;
using RimChronicles.Core.Choir;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Plots;
using RimChronicles.Game.Quests;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Powers;

/// <summary>
/// The Hollow Choir's acts in game (War in Heaven W4b), the mirror of <see cref="EmpireBeats"/>: the Act 1 envoy and
/// the colony's stance, Act 2 jobs that fit it (quests from the templates), tribute, the Act 3 reckoning, the First
/// Voice's offer to a colonist of high Chorus rank (spec D13), the assault on the Cathedral, the final demand and the
/// treaty. The rules are <see cref="PowerArc"/>'s with <see cref="Power.Choir"/>. The director decides it only when its
/// model knows the War in Heaven (<see cref="ChoirDriver.DirectorDecides"/>); otherwise it picks by weight and stays out
/// of the snapshot. Template letters only;
/// victory screens and epilogues for its endings come with W11. Main thread only.
/// </summary>
public static class ChoirBeats
{
    private static Power Choir => Power.Choir;

    public static void Register() => PlotDriver.Register(Choir.PlotTemplateId,
        plot => RimChroniclesGameComponent.Current is { } c ? PowerArc.Transitions(plot, c.Choir, GenDate.DaysPassed) : [], Fire,
        useDirector: () => ChoirDriver.DirectorDecides, subOptions: SiteOptions);

    /// <summary>The Choir's sites a sabotage job may target (War in Heaven W8), for the director's second call, the
    /// mirror of <see cref="EmpireBeats.SiteOptions"/>. Empty unless the director may decide the Choir's crisis.</summary>
    public static IReadOnlyList<PlotTransition> SiteOptions(Plot plot, PlotTransition option) =>
        option.Step == PowerStep.SabotageImperial && Crisis.Choir.ChoirCrisisDriver.DirectorDecides
            && RimChroniclesGameComponent.Current is { } component && PowerFactions.Of(Choir) is { } choir
            ? Crisis.Sites.SiteOffers.Allowed(component, choir).Select(a => new PlotTransition(a.Id, a.Name, a.Weight, a.Desc)).ToList()
            : [];

    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>Starts the Choir plot once the Choir stirs, if it hasn't one yet, taking the colony's stored stance (an
    /// Imperial ally is already its enemy). The envoy comes a few days later.</summary>
    public static void EnsurePlot(RimChroniclesGameComponent component, int today)
    {
        var book = component.Plots;
        if (book.Active.Concat(book.Finished).Any(p => p.Template == Choir.PlotTemplateId)
            || PlotTemplateDef.Find(Choir.PlotTemplateId) is not { } template)
        {
            return;
        }

        var plot = book.Start(template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = Colony },
            today, Rand.RangeInclusive(2, 5), "the Choir is stirring");
        if (component.Allegiance.StanceToward(Choir) is var stance && stance != PowerStance.Neutral)
        {
            plot.SetFact(PowerArc.Stance, stance);
        }
    }

    public static Plot? Current(RimChroniclesGameComponent component) =>
        component.Plots.Active.FirstOrDefault(p => p.Template == Choir.PlotTemplateId);

    public static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        if (ChoirFaction.Existing is not { } choir)
        {
            return false; // still stirring: the faction appears when it wakes
        }

        var gap = Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays);
        switch (transition.Step)
        {
            case PowerStep.Envoy:
            {
                var (label, text) = ChoirLetters.Envoy(choir.Name, Colony);
                ChoiceLetter_Plot.Send(plot, PowerArc.Stance, label, text,
                [
                    new PlotChoice("Stand with the Choir", PowerStance.Ally),
                    new PlotChoice("Stand against it", PowerStance.Oppose),
                    new PlotChoice("Keep out of it", PowerStance.Neutral),
                ], PowerArc.EnvoyTimeoutDays, "the colony keeps out of it", choir);
                component.Plots.Advance(plot, PlotBeat.Hook, today, PowerArc.EnvoyTimeoutDays + 1, "Act 1: the envoy asks the colony's stance");
                return true;
            }

            case PowerStep.Expansion:
            {
                var stance = PowerArc.StanceOf(plot);
                plot.SetFact(PowerArc.Stance, stance);
                PowerFactions.ApplyStance(choir, stance);
                Send(plot, ChoirLetters.Marches(stance, choir.Name, Colony), stance == PowerStance.Oppose ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent, choir);
                component.Plots.Advance(plot, PlotBeat.Complication, today, gap, $"Act 2: the Choir marches; the colony is {stance}");
                return true;
            }

            case PowerStep.EscortEnvoy:
            {
                if (choir.HostileTo(Faction.OfPlayer) || QuestOffers.NearestSettlement(s => s.Faction == choir) is not { } destination)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, destination.Label, () => QuestOffers.Escort(component, plot, choir, destination,
                    Rand.RangeInclusive(1, 2), $"Escort Chorus envoys to {destination.Label}",
                    $"{choir.Name} asks its ally {Colony} to see its envoys safely to {destination.Label}. Take them there by caravan within "
                    + $"{QuestTemplates.EscortDays} days."));
            }

            case PowerStep.ConquestJob:
            {
                if (NextTarget(choir, friendlyToPlayer: null) is not { } target)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, target.Label, () => QuestOffers.Sabotage(component, plot, target, null,
                    $"Weaken {target.Label}",
                    $"{choir.Name} means to take {target.Label}, held by {target.Faction.Name}, and asks its ally {Colony} to destroy the comms "
                    + $"console there first. Do it within {QuestTemplates.SabotageDays} days, and {target.Label} will fall to the Choir."));
            }

            case PowerStep.DefendAlly:
            {
                if (!choir.HostileTo(Faction.OfPlayer) || NextTarget(choir, friendlyToPlayer: true) is not { } ally)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, ally.Label, () => QuestOffers.DefendAlly(component, plot, ally, choir,
                    $"Break the siege of {ally.Label}",
                    $"A Choir host has camped outside {ally.Label}, held by {ally.Faction.Name}, humming day and night. Clear the camp, or "
                    + $"{ally.Label} will fall to the Choir."));
            }

            case PowerStep.SabotageImperial:
            {
                if (!choir.HostileTo(Faction.OfPlayer))
                {
                    return false;
                }

                // While the Choir's crisis runs, the sabotage is one of its sites (W8); otherwise a Choir settlement.
                if (Crisis.Sites.SiteOffers.Pick(component, choir, transition.Sub) is { } archetype)
                {
                    if (archetype.needsHolding)
                    {
                        if (Crisis.Sites.SiteOffers.Holding(component, choir) is not { } held)
                        {
                            return false;
                        }

                        var town = held.Settlement.Label;
                        return StartQuest(component, plot, today, gap, transition.Step, town, () => QuestOffers.ImperialSite(component, plot, archetype, choir,
                            $"Free {town}",
                            $"{choir.Name} took {town} from {held.Former.Name}, and a Chorus garrison holds it near {Colony}. Destroy the comms "
                            + $"console deepest inside the garrison's walls, and {town} goes back to {held.Former.Name}.", held));
                    }

                    var near = QuestOffers.NearestSettlement(s => s.Faction == choir)?.Label ?? archetype.label;
                    return StartQuest(component, plot, today, gap, transition.Step, near, () => QuestOffers.ImperialSite(component, plot, archetype, choir,
                        $"Destroy the {archetype.label}",
                        $"{choir.Name} has raised {Find.ActiveLanguageWorker.WithIndefiniteArticle(archetype.label)} near {Colony}: "
                        + $"{archetype.description}. Destroy its objective before the site is abandoned, and the Choir's advance will falter."));
                }

                if (QuestOffers.NearestSettlement(s => s.Faction == choir) is not { } target)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, target.Label, () => QuestOffers.Sabotage(component, plot, target, null,
                    $"Sabotage {target.Label}",
                    $"Destroy the comms console in the Choir settlement of {target.Label} within {QuestTemplates.SabotageDays} days, "
                    + "and the Choir's advance will falter."));
            }

            case PowerStep.Tribute:
            {
                var silver = EmpireBeats.TributeSilver(WealthUtility.PlayerWealth);
                PowerArc.StartJob(plot, PowerStep.Tribute, "", today);
                var (label, text) = ChoirLetters.TributeDemand(choir.Name, Colony, silver);
                ChoiceLetter_Plot.Send(plot, PowerArc.Answer, label, text,
                [
                    new PlotChoice($"Pay {silver} silver", PowerArc.TributePaid, Silver: silver),
                    new PlotChoice("Refuse", PowerArc.TributeRefused),
                    new PlotChoice("Join the Chorus", PowerArc.TributeFealty),
                ], PowerArc.LetterTimeoutDays, "the tribute goes unpaid, though the Choir doesn't go to war over it", choir);
                component.Plots.Advance(plot, plot.Beat, today, PowerArc.LetterTimeoutDays, "the Choir demands tribute");
                return true;
            }

            case PowerStep.Aftermath:
                Aftermath(component, plot, choir, today);
                component.Plots.Advance(plot, plot.Beat, today, plot.Fact(PowerArc.EndingFact) is { Length: > 0 } ? 1 : gap, "the job's aftermath");
                return true;

            case PowerStep.Reckoning:
            {
                var ascendant = PowerArc.ReckoningScore(plot, component.Choir) > 0;
                component.Choir.Reckon(ascendant, today);
                Send(plot, ChoirLetters.Reckoning(ascendant, choir.Name, Colony), LetterDefOf.ThreatBig, choir);
                component.News.Add(new NewsEvent(today, Choir.NewsKind,
                    ascendant ? $"{choir.Name} sent its final demands to every settlement on the rim" : $"{choir.Name} fell back after heavy losses",
                    choir.Name));
                component.Plots.Advance(plot, PlotBeat.Choice, today, gap, ascendant ? "Act 3: the Choir is ascendant" : "Act 3: the Choir is faltering");
                return true;
            }

            case PowerStep.Governorship:
            {
                var holder = component.Choir.HighTitleHolder;
                if (holder.Length == 0)
                {
                    return false;
                }

                PowerArc.StartJob(plot, transition.Step, holder, today);
                var (label, text) = ChoirLetters.FirstVoiceOffer(choir.Name, Colony, holder);
                ChoiceLetter_Plot.Send(plot, PowerArc.Answer, label, text,
                    [new PlotChoice("Join the Chorus", PowerArc.Accept), new PlotChoice("Not yet", PowerArc.Decline)], PowerArc.LetterTimeoutDays,
                    "it counts as not yet", choir);
                component.Plots.Advance(plot, PlotBeat.Choice, today, PowerArc.LetterTimeoutDays, "Act 3: the First Voice's offer");
                return true;
            }

            case PowerStep.FinalDemand:
            case PowerStep.Treaty:
            {
                PowerArc.StartJob(plot, transition.Step, "", today);
                var final = transition.Step == PowerStep.FinalDemand;
                var (label, text) = final ? ChoirLetters.FinalDemand(choir.Name, Colony) : ChoirLetters.Treaty(choir.Name, Colony);
                ChoiceLetter_Plot.Send(plot, PowerArc.Answer, label, text, final
                    ? [new PlotChoice("Join the Chorus", PowerArc.Accept), new PlotChoice("Refuse", PowerArc.Decline)]
                    : [new PlotChoice("Sign the treaty", PowerArc.Accept), new PlotChoice("Fight on", PowerArc.Decline)],
                    PowerArc.LetterTimeoutDays, final ? "it counts as a refusal: war" : "no treaty is signed", choir);
                component.Plots.Advance(plot, PlotBeat.Choice, today, PowerArc.LetterTimeoutDays, final ? "Act 3: the final demand" : "Act 3: a treaty is offered");
                return true;
            }

            case PowerStep.StormCapital:
            {
                if (!choir.HostileTo(Faction.OfPlayer) || EmpireBeats.Capital(choir) is not { } cathedral)
                {
                    return false;
                }

                var allies = string.Join(" and ", Find.FactionManager.AllFactionsVisible
                    .Where(f => f != choir && !f.IsPlayer && !f.defeated && f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally)
                    .Select(f => f.Name).Take(3));
                var (name, description) = ChoirLetters.CathedralQuest(choir.Name, cathedral.Label, allies);
                PowerArc.StartJob(plot, transition.Step, cathedral.Label, today);
                if (QuestOffers.Capital(component, plot, cathedral, name, description) is null)
                {
                    plot.SetFact(PowerArc.Job, "");
                    return false;
                }

                component.Plots.Advance(plot, PlotBeat.Choice, today, gap, $"Act 3: silence {cathedral.Label}");
                return true;
            }

            case PowerStep.Ending:
                if (plot.Fact(PowerArc.EndingFact) == Choir.DefeatEnding && War.WarDriver.CapitalFallDestroys(component))
                {
                    War.WarDriver.Destroy(component, Choir, today, $"its Cathedral fell to {Colony}"); // R11: not the Choir silenced
                    return true;
                }

                Finish(component, plot, plot.Fact(PowerArc.EndingFact) ?? Core.Empire.EmpireEnding.Independence, choir, today);
                return true;

            default:
                return false;
        }
    }

    private static bool StartQuest(RimChroniclesGameComponent component, Plot plot, int today, int gap, string step, string target, Func<Quest?> offer)
    {
        PowerArc.StartJob(plot, step, target, today);
        if (offer() is null)
        {
            plot.SetFact(PowerArc.Job, "");
            return false;
        }

        component.Plots.Advance(plot, plot.Beat, today, gap, $"job: {step} at {target}");
        return true;
    }

    /// <summary>The settlement the Choir would most likely attack next, optionally only those of factions friendly (or
    /// not) to the colony.</summary>
    private static Settlement? NextTarget(Faction choir, bool? friendlyToPlayer) =>
        EmpireDriver.Targets(choir)
            .Where(t => friendlyToPlayer is not { } friendly || t.Value.Faction.HostileTo(Faction.OfPlayer) != friendly)
            .OrderBy(t => t.Key.Distance)
            .Select(t => t.Value)
            .FirstOrDefault();

    /// <summary>Applies a finished job to the world: settlements change hands, goodwill moves, rank rises, the stance
    /// may change.</summary>
    private static void Aftermath(RimChroniclesGameComponent component, Plot plot, Faction choir, int today)
    {
        var r = PowerArc.FinishJob(plot, Choir);
        var settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Label == r.Target);
        switch (r.Job, r.Success)
        {
            case (PowerStep.EscortEnvoy, var ok):
                choir.TryAffectGoodwillWith(Faction.OfPlayer, ok ? 12 : -8, canSendMessage: true, canSendHostilityLetter: true);
                if (ok)
                {
                    GrantRank(component, plot, choir);
                }

                break;
            case (PowerStep.ConquestJob, true):
                if (settlement is { HasMap: false } && settlement.Faction != choir && !settlement.Faction.IsPlayer)
                {
                    var owner = settlement.Faction.Name;
                    settlement.SetFaction(choir);
                    EmpireDriver.Repaint(settlement);
                    component.Choir.RecordAssisted(settlement.Label, owner, ExpansionResult.Conquered, today, $"{r.Target} fell after the colony's sabotage");
                }

                GrantRank(component, plot, choir);
                break;
            case (PowerStep.DefendAlly, true):
                component.Choir.RecordAssisted(r.Target, settlement?.Faction.Name ?? "", ExpansionResult.Repelled, today, $"the colony broke the siege of {r.Target}");
                settlement?.Faction.TryAffectGoodwillWith(Faction.OfPlayer, 10, canSendMessage: true, canSendHostilityLetter: false);
                break;
            case (PowerStep.DefendAlly, false):
                if (settlement is { HasMap: false } && settlement.Faction != choir && !settlement.Faction.IsPlayer)
                {
                    var owner = settlement.Faction.Name;
                    settlement.SetFaction(choir);
                    EmpireDriver.Repaint(settlement);
                    component.Choir.RecordAssisted(settlement.Label, owner, ExpansionResult.Conquered, today, $"{r.Target} fell after the siege");
                }

                break;
            case (PowerStep.SabotageImperial, true):
                component.Choir.RecordAssisted(r.Target, choir.Name, ExpansionResult.Repelled, today, $"the colony sabotaged {r.Target}");
                break;
            case (PowerStep.Tribute or PowerStep.FinalDemand or PowerStep.Treaty, _):
                if (r.NewStance is { } stance)
                {
                    PowerFactions.ApplyStance(choir, stance);
                    Send(plot, ChoirLetters.StanceTaken(stance, choir.Name, Colony), stance == PowerStance.Oppose ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent, choir);
                }
                else if (r.Job == PowerStep.Tribute && !r.Success)
                {
                    choir.TryAffectGoodwillWith(Faction.OfPlayer, -10, canSendMessage: true, canSendHostilityLetter: true); // ignored
                }

                break;
        }

        if (ChoirLetters.AftermathNews(r, choir.Name, Colony) is { Length: > 0 } news)
        {
            component.News.Add(new NewsEvent(today, Choir.NewsKind, news, choir.Name));
        }
    }

    /// <summary>Chorus favor for an ally's service (spec D13), to the colonist who already has the most; a letter
    /// when they rise a rank.</summary>
    private static void GrantRank(RimChroniclesGameComponent component, Plot plot, Faction choir)
    {
        var colonists = PawnsFinder.AllMaps_FreeColonists.Select(p => (p.ThingID, p.LabelShort)).ToList();
        if (component.ChoirRanks.GrantForJob(colonists) is { } rank)
        {
            var holder = component.ChoirRanks.Standings.OrderByDescending(s => s.Favor).First().Name;
            Send(plot, ("A rise in the Chorus", $"{choir.Name} has raised {holder} to {rank} for the colony's service."), LetterDefOf.PositiveEvent, choir);
        }
    }

    /// <summary>The colonist of high Chorus rank who may receive the First Voice's offer, or "". Read daily.</summary>
    public static string HighRankHolder(RimChroniclesGameComponent component) =>
        component.ChoirRanks.HighRankHolder(PawnsFinder.AllMaps_FreeColonists.Select(p => p.ThingID).ToHashSet());

    /// <summary>The plot reached an ending: news, a letter and the plot's end. The victory screen and epilogue come with
    /// the War in Heaven's endings (W11).</summary>
    private static void Finish(RimChroniclesGameComponent component, Plot plot, string ending, Faction choir, int today)
    {
        var saga = component.Choir;
        saga.Ending = ending;
        saga.Note(today, $"ending: {ending}");
        if (ending == ChoirEnding.Silenced)
        {
            saga.Reckon(ascendant: false, today);
        }

        var news = ChoirLetters.EndingNews(ending, choir.Name, Colony);
        component.News.Add(new NewsEvent(today, Choir.NewsKind, news, choir.Name));
        Send(plot, ("The Choir's story ends", news + "."), LetterDefOf.PositiveEvent, choir);
        component.Plots.End(plot, today, ending);
    }

    private static void Send(Plot plot, (string Label, string Text) letter, LetterDef def, Faction choir)
    {
        plot.Tell(GenDate.DaysPassed, letter.Label, letter.Text);
        StoryLetters.Send(letter.Label, letter.Text, def, faction: choir);
    }

    [DebugAction("Rim Chronicles", "Hollow Choir: grant rank favor", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevGrant()
    {
        if (RimChroniclesGameComponent.Current is not { } component || Current(component) is not { } plot || ChoirFaction.Existing is not { } choir)
        {
            Messages.Message("Rim Chronicles: no Choir plot yet (Hollow Choir: wake now).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        GrantRank(component, plot, choir);
        component.Choir.HighTitleHolder = HighRankHolder(component);
        Messages.Message($"Rim Chronicles: Chorus favor granted. High-rank holder: {(component.Choir.HighTitleHolder is { Length: > 0 } h ? h : "none yet")}.",
            MessageTypeDefOf.NeutralEvent, historical: false);
    }
}
