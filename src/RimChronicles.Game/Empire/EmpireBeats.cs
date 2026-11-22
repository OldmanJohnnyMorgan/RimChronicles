using LudeonTK;
using RimChronicles.Core.Empire;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Game.Plots;
using RimChronicles.Game.Quests;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Empire;

/// <summary>The Empire's acts in game (Stage 5 M4), as a plot the director runs: the Act 1 envoy and the colony's
/// stance, Act 2 jobs that fit the stance (quests built from the templates) and their aftermath on the world map, and
/// the Act 3 reckoning. Template letters only. Main thread only.</summary>
public static class EmpireBeats
{
    public static void Register() => PlotDriver.Register(EmpireArc.TemplateId,
        plot => RimChroniclesGameComponent.Current is { } c ? EmpireArc.Transitions(plot, c.Empire, GenDate.DaysPassed) : [], Fire);

    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>Starts the Empire plot once the Empire is awake, if it hasn't one yet. The envoy comes a few days later.</summary>
    public static void EnsurePlot(RimChroniclesGameComponent component, int today)
    {
        var book = component.Plots;
        if (book.Active.Concat(book.Finished).Any(p => p.Template == EmpireArc.TemplateId)
            || PlotTemplateDef.Find(EmpireArc.TemplateId) is not { } template)
        {
            return;
        }

        book.Start(template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = Colony },
            today, Rand.RangeInclusive(2, 5), "the Empire is stirring");
    }

    public static Plot? Current(RimChroniclesGameComponent component) =>
        component.Plots.Active.FirstOrDefault(p => p.Template == EmpireArc.TemplateId);

    public static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        if (EmpireDriver.EmpireFaction is not { } empire)
        {
            return false;
        }

        var gap = Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays);
        switch (transition.Step)
        {
            case EmpireStep.Envoy:
            {
                var (label, text) = EmpireLetters.Envoy(empire.Name, Colony);
                ChoiceLetter_Plot.Send(plot, EmpireArc.Stance, label, text,
                [
                    new PlotChoice("Stand with the Empire", EmpireStance.Ally),
                    new PlotChoice("Stand against it", EmpireStance.Oppose),
                    new PlotChoice("Keep out of it", EmpireStance.Neutral),
                ], EmpireArc.EnvoyTimeoutDays);
                component.Plots.Advance(plot, PlotBeat.Hook, today, EmpireArc.EnvoyTimeoutDays + 1, "Act 1: the envoy asks the colony's stance");
                return true;
            }

            case EmpireStep.Expansion:
            {
                var stance = EmpireArc.StanceOf(plot);
                plot.SetFact(EmpireArc.Stance, stance);
                ApplyStance(empire, stance);
                Send(EmpireLetters.Marches(stance, empire.Name, Colony), stance == EmpireStance.Oppose ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent);
                component.Plots.Advance(plot, PlotBeat.Complication, today, gap, $"Act 2: the Empire marches; the colony is {stance}");
                return true;
            }

            case EmpireStep.EscortEnvoy:
            {
                if (empire.HostileTo(Faction.OfPlayer) || QuestOffers.NearestSettlement(s => s.Faction == empire) is not { } destination)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, destination.Label, () => QuestOffers.Escort(component, plot, empire, destination,
                    Rand.RangeInclusive(1, 2), $"Escort Imperial envoys to {destination.Label}",
                    $"{empire.Name} asks its ally {Colony} to see its envoys safely to {destination.Label}. Take them there by caravan within "
                    + $"{QuestTemplates.EscortDays} days."));
            }

            case EmpireStep.ConquestJob:
            {
                if (NextImperialTarget(empire, friendlyToPlayer: null) is not { } target)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, target.Label, () => QuestOffers.Sabotage(component, plot, target, null,
                    $"Weaken {target.Label}",
                    $"{empire.Name} means to take {target.Label}, held by {target.Faction.Name}, and asks its ally {Colony} to destroy the comms "
                    + $"console there first. Do it within {QuestTemplates.SabotageDays} days, and {target.Label} will fall to the Empire."));
            }

            case EmpireStep.DefendAlly:
            {
                if (!empire.HostileTo(Faction.OfPlayer) || NextImperialTarget(empire, friendlyToPlayer: true) is not { } ally)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, ally.Label, () => QuestOffers.DefendAlly(component, plot, ally, empire,
                    $"Break the siege of {ally.Label}",
                    $"An Imperial column has camped outside {ally.Label}, held by {ally.Faction.Name}. Clear the camp, or {ally.Label} will fall to the Empire."));
            }

            case EmpireStep.SabotageImperial:
            {
                if (!empire.HostileTo(Faction.OfPlayer) || QuestOffers.NearestSettlement(s => s.Faction == empire) is not { } target)
                {
                    return false;
                }

                return StartQuest(component, plot, today, gap, transition.Step, target.Label, () => QuestOffers.Sabotage(component, plot, target, null,
                    $"Sabotage {target.Label}",
                    $"Destroy the comms console in the Imperial settlement of {target.Label} within {QuestTemplates.SabotageDays} days, "
                    + "and the Empire's advance will falter."));
            }

            case EmpireStep.Tribute:
            {
                var silver = TributeSilver(WealthUtility.PlayerWealth);
                EmpireArc.StartJob(plot, EmpireStep.Tribute, "", today);
                var (label, text) = EmpireLetters.TributeDemand(empire.Name, Colony, silver);
                ChoiceLetter_Plot.Send(plot, EmpireArc.Answer, label, text,
                [
                    new PlotChoice($"Pay {silver} silver", EmpireArc.TributePaid, Silver: silver),
                    new PlotChoice("Refuse", EmpireArc.TributeRefused),
                    new PlotChoice("Swear fealty", EmpireArc.TributeFealty),
                ], EmpireArc.LetterTimeoutDays);
                component.Plots.Advance(plot, plot.Beat, today, EmpireArc.LetterTimeoutDays, "the Empire demands tribute");
                return true;
            }

            case EmpireStep.Aftermath:
                Aftermath(component, plot, empire, today);
                component.Plots.Advance(plot, plot.Beat, today, plot.Fact(EmpireArc.EndingFact) is { Length: > 0 } ? 1 : gap, "the job's aftermath");
                return true;

            case EmpireStep.Reckoning:
            {
                var ascendant = EmpireArc.ReckoningScore(plot, component.Empire) > 0;
                component.Empire.Reckon(ascendant, today);
                Send(EmpireLetters.Reckoning(ascendant, empire.Name, Colony), LetterDefOf.ThreatBig);
                component.News.Add(new NewsEvent(today, EmpireSaga.NewsKind,
                    ascendant ? $"{empire.Name} issued final demands to every settlement on the rim" : $"{empire.Name} pulled back its columns after heavy losses",
                    empire.Name));
                component.Plots.Advance(plot, PlotBeat.Choice, today, gap, ascendant ? "Act 3: the Empire is ascendant" : "Act 3: the Empire is breaking");
                return true;
            }

            case EmpireStep.Governorship:
            {
                var holder = component.Empire.HighTitleHolder;
                if (holder.Length == 0)
                {
                    return false;
                }

                EmpireArc.StartJob(plot, transition.Step, holder, today);
                var (label, text) = EmpireLetters.Governorship(empire.Name, Colony, holder);
                ChoiceLetter_Plot.Send(plot, EmpireArc.Answer, label, text,
                    [new PlotChoice("Accept the governorship", EmpireArc.Accept), new PlotChoice("Not yet", EmpireArc.Decline)], EmpireArc.LetterTimeoutDays);
                component.Plots.Advance(plot, PlotBeat.Choice, today, EmpireArc.LetterTimeoutDays, "Act 3: the Empire offers a governorship");
                return true;
            }

            case EmpireStep.FinalDemand:
            case EmpireStep.Treaty:
            {
                EmpireArc.StartJob(plot, transition.Step, "", today);
                var final = transition.Step == EmpireStep.FinalDemand;
                var (label, text) = final ? EmpireLetters.FinalDemand(empire.Name, Colony) : EmpireLetters.Treaty(empire.Name, Colony);
                ChoiceLetter_Plot.Send(plot, EmpireArc.Answer, label, text, final
                    ? [new PlotChoice("Submit to the Empire", EmpireArc.Accept), new PlotChoice("Refuse", EmpireArc.Decline)]
                    : [new PlotChoice("Sign the treaty", EmpireArc.Accept), new PlotChoice("Fight on", EmpireArc.Decline)],
                    EmpireArc.LetterTimeoutDays);
                component.Plots.Advance(plot, PlotBeat.Choice, today, EmpireArc.LetterTimeoutDays, final ? "Act 3: the final demand" : "Act 3: a treaty is offered");
                return true;
            }

            case EmpireStep.StormCapital:
            {
                if (!empire.HostileTo(Faction.OfPlayer) || Capital(empire) is not { } capital)
                {
                    return false;
                }

                var allies = string.Join(" and ", Find.FactionManager.AllFactionsVisible
                    .Where(f => f != empire && !f.IsPlayer && !f.defeated && f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally)
                    .Select(f => f.Name).Take(3));
                var (name, description) = EmpireLetters.CapitalQuest(empire.Name, capital.Label, allies);
                EmpireArc.StartJob(plot, transition.Step, capital.Label, today);
                if (QuestOffers.Capital(component, plot, capital, name, description) is null)
                {
                    plot.SetFact(EmpireArc.Job, "");
                    return false;
                }

                component.Plots.Advance(plot, PlotBeat.Choice, today, gap, $"Act 3: storm {capital.Label}");
                return true;
            }

            case EmpireStep.Ending:
                EmpireEndings.Finish(component, plot, plot.Fact(EmpireArc.EndingFact) ?? EmpireEnding.Independence, empire, today);
                return true;

            default:
                return false;
        }
    }

    /// <summary>The Imperial capital: the Empire's oldest settlement (lowest id), with no map open.</summary>
    public static Settlement? Capital(Faction empire) =>
        Find.WorldObjects.Settlements.Where(s => s.Faction == empire && !s.HasMap).OrderBy(s => s.ID).FirstOrDefault();

    /// <summary>Tribute: 1% of colony wealth, rounded to 50, between 200 and 2,000 silver.</summary>
    public static int TributeSilver(float wealth) => (int)Math.Clamp(Math.Round(wealth * 0.01f / 50f) * 50f, 200f, 2000f);

    private static bool StartQuest(RimChroniclesGameComponent component, Plot plot, int today, int gap, string step, string target, Func<Quest?> offer)
    {
        EmpireArc.StartJob(plot, step, target, today);
        if (offer() is null)
        {
            plot.SetFact(EmpireArc.Job, "");
            return false;
        }

        component.Plots.Advance(plot, plot.Beat, today, gap, $"job: {step} at {target}");
        return true;
    }

    /// <summary>The settlement the Empire would most likely attack next (nearest to an Imperial one), optionally only
    /// those of factions friendly (or not) to the colony.</summary>
    private static Settlement? NextImperialTarget(Faction empire, bool? friendlyToPlayer) =>
        EmpireDriver.Targets(empire)
            .Where(t => friendlyToPlayer is not { } friendly || t.Value.Faction.HostileTo(Faction.OfPlayer) != friendly)
            .OrderBy(t => t.Key.Distance)
            .Select(t => t.Value)
            .FirstOrDefault();

    /// <summary>Ally: goodwill up to at least +25. Oppose: war (goodwill down to -100). Neutral: no change.</summary>
    private static void ApplyStance(Faction empire, string stance)
    {
        var goodwill = empire.PlayerGoodwill;
        var change = stance switch
        {
            EmpireStance.Ally => Math.Max(0, 25 - goodwill),
            EmpireStance.Oppose => -100 - goodwill,
            _ => 0,
        };
        if (change != 0)
        {
            empire.TryAffectGoodwillWith(Faction.OfPlayer, change, canSendMessage: true, canSendHostilityLetter: true);
        }
    }

    /// <summary>Applies a finished job to the world: settlements change hands, goodwill moves, the stance may change.</summary>
    private static void Aftermath(RimChroniclesGameComponent component, Plot plot, Faction empire, int today)
    {
        var r = EmpireArc.FinishJob(plot);
        var settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Label == r.Target);
        switch (r.Job, r.Success)
        {
            case (EmpireStep.EscortEnvoy, var ok):
                empire.TryAffectGoodwillWith(Faction.OfPlayer, ok ? 12 : -8, canSendMessage: true, canSendHostilityLetter: true);
                if (ok)
                {
                    GrantFavor(empire);
                }

                break;
            case (EmpireStep.ConquestJob, true):
                if (settlement is { HasMap: false } && settlement.Faction != empire && !settlement.Faction.IsPlayer)
                {
                    EmpireDriver.Conquer(component, settlement, empire, today, $"{r.Target} fell after the colony's sabotage", news: false);
                }

                GrantFavor(empire);
                break;
            case (EmpireStep.DefendAlly, true):
                component.Empire.RecordAssisted(r.Target, settlement?.Faction.Name ?? "", ExpansionResult.Repelled, today, $"the colony broke the siege of {r.Target}");
                settlement?.Faction.TryAffectGoodwillWith(Faction.OfPlayer, 10, canSendMessage: true, canSendHostilityLetter: false);
                break;
            case (EmpireStep.DefendAlly, false):
                if (settlement is { HasMap: false } && settlement.Faction != empire && !settlement.Faction.IsPlayer)
                {
                    EmpireDriver.Conquer(component, settlement, empire, today, $"{r.Target} fell after the siege", news: false);
                }

                break;
            case (EmpireStep.SabotageImperial, true):
                component.Empire.RecordAssisted(r.Target, empire.Name, ExpansionResult.Repelled, today, $"the colony sabotaged {r.Target}");
                break;
            case (EmpireStep.Tribute or EmpireStep.FinalDemand or EmpireStep.Treaty, _):
                if (r.NewStance is { } stance)
                {
                    ApplyStance(empire, stance);
                    Send(EmpireLetters.StanceTaken(stance, empire.Name, Colony), stance == EmpireStance.Oppose ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent);
                }
                else if (r.Job == EmpireStep.Tribute && !r.Success)
                {
                    empire.TryAffectGoodwillWith(Faction.OfPlayer, -10, canSendMessage: true, canSendHostilityLetter: true); // ignored
                }

                break;
        }

        if (EmpireLetters.AftermathNews(r, empire.Name, Colony) is { Length: > 0 } news)
        {
            component.News.Add(new NewsEvent(today, EmpireSaga.NewsKind, news, empire.Name));
        }

        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Empire job {r.Job} at {r.Target}: {(r.Success ? "success" : "failure")}{(r.NewStance is { } s ? $"; stance now {s}" : "")}.");
        }
    }

    /// <summary>Royal favor for an ally's service: 4 favor to the colonist who already has the most with the Empire
    /// (so service builds toward a title), then the title is updated as vanilla does after a favor reward.</summary>
    public const int FavorPerJob = 4;

    private static void GrantFavor(Faction empire)
    {
        if (!ModsConfig.RoyaltyActive || PawnsFinder.AllMaps_FreeColonists.Where(p => p.royalty is not null)
                .OrderByDescending(p => p.royalty.GetFavor(empire)).ThenBy(p => p.ThingID, StringComparer.Ordinal).FirstOrDefault() is not { } pawn)
        {
            return;
        }

        pawn.royalty.GainFavor(empire, FavorPerJob);
        pawn.royalty.TryUpdateTitle(empire);
    }

    /// <summary>The colonist with the highest Imperial title at Knight or above, or "".</summary>
    public static string HighTitleHolder(Faction empire) =>
        RoyalTitleDefOf.Knight is not { } knight ? "" :
        PawnsFinder.AllMaps_FreeColonists
            .Select(p => (Pawn: p, Title: p.royalty?.GetCurrentTitle(empire)))
            .Where(x => x.Title is not null && x.Title.seniority >= knight.seniority)
            .OrderByDescending(x => x.Title!.seniority).ThenBy(x => x.Pawn.ThingID, StringComparer.Ordinal)
            .Select(x => x.Pawn.LabelShort)
            .FirstOrDefault() ?? "";

    private static void Send((string Label, string Text) letter, LetterDef def) => Find.LetterStack.ReceiveLetter(letter.Label, letter.Text, def);

    [DebugAction("Rim Chronicles", "Empire: set stance", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevStance()
    {
        if (RimChroniclesGameComponent.Current is not { } component || Current(component) is not { } plot || EmpireDriver.EmpireFaction is not { } empire)
        {
            Messages.Message("Rim Chronicles: no Empire plot yet (Empire: trigger awakening).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Find.WindowStack.Add(new FloatMenu(new[] { EmpireStance.Ally, EmpireStance.Oppose, EmpireStance.Neutral }
            .Select(stance => new FloatMenuOption(stance, () =>
            {
                plot.SetFact(EmpireArc.Stance, stance);
                plot.Note(GenDate.DaysPassed, $"stance set to {stance} (Dev Mode)");
                if (plot.Beat != PlotBeat.Hook)
                {
                    ApplyStance(empire, stance);
                }
            }))
            .ToList()));
    }
}
