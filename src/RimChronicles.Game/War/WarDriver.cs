using LudeonTK;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;
using RimChronicles.Core.War;
using RimChronicles.Game.Empire;
using RimChronicles.Game.Engine;
using RimChronicles.Game.Plots;
using RimChronicles.Game.Powers;
using RimChronicles.Game.Quests;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.War;

/// <summary>
/// The War in Heaven in game (War in Heaven W10, spec §7, tech spec §6): once a day, while both powers are awake and the
/// colony is at war, it rolls Core's odds (certain from the setting's day, 600 by default); when the war starts it is
/// announced and its plot, <c>WarInHeaven</c>, gives the colony's side jobs against the rival (Core's <see cref="WarArc"/>):
/// defend the front, strike, escort, sabotage, and the final push on the rival's capital, which destroys it. A power
/// whose capital falls is destroyed (tech spec R11), not an ending; the endings are the war's (the side's offer, the
/// survivor's treaty, total independence). The victory screens and epilogues for them come with W11. Main thread only.
/// </summary>
public static class WarDriver
{
    public static bool Available => RimChroniclesMod.Settings.MajorPlot && RimChroniclesMod.Settings.WarInHeaven
        && ChoirDriver.Available && EmpireDriver.Available;

    private static string Colony => Faction.OfPlayer.Name;

    public static void Register() => PlotDriver.Register(WarInHeaven.TemplateId,
        plot => RimChroniclesGameComponent.Current is { } c && Available ? WarArc.Transitions(plot, Context(c), GenDate.DaysPassed) : [],
        Fire, useDirector: () => ChoirDriver.DirectorDecides);

    /// <summary>The colony's side and enemies, as the war arc reads them.</summary>
    public static WarContext Context(RimChroniclesGameComponent component) => new()
    {
        Side = component.Allegiance.SideAmong(Power.All),
        War = component.War,
        Opposed = Power.All.Where(p => component.Allegiance.StanceToward(p) == PowerStance.Oppose).ToList(),
    };

    public static Plot? Plot(RimChroniclesGameComponent component) =>
        component.Plots.Active.FirstOrDefault(p => p.Template == WarInHeaven.TemplateId);

    public static void Daily(RimChroniclesGameComponent component)
    {
        if (!Available)
        {
            return;
        }

        var war = component.War;
        var today = GenDate.DaysPassed;
        if (war.State == WarState.NotYet)
        {
            var since = component.Allegiance.WarSince();
            if (war.CanStart(component.Empire, component.Choir, since >= 0)
                && WarInHeaven.StartsToday(today, today - since, Rand.Value, PlotGaps.WarSteepness(), RimChroniclesMod.Settings.WarCertainDay))
            {
                Begin(component, today, today >= RimChroniclesMod.Settings.WarCertainDay ? $"day {today}" : $"the colony's war went on for {today - since} days");
            }
        }
        else if (war.State == WarState.Started && component.Plots.Active.Concat(component.Plots.Finished).All(p => p.Template != WarInHeaven.TemplateId))
        {
            StartPlot(component, today, war.Cause); // a save from before W10's game side
        }
    }

    /// <summary>The war begins: the log, the news, a letter, and its plot.</summary>
    public static void Begin(RimChroniclesGameComponent component, int today, string cause)
    {
        if (component.War.State != WarState.NotYet)
        {
            return;
        }

        component.War.Start(today, cause);
        var empire = EmpireDriver.EmpireFaction?.Name ?? "the Empire";
        var choir = PowerFactions.Of(Power.Choir)?.Name ?? "the Hollow Choir";
        component.News.Add(new NewsEvent(today, WarInHeaven.NewsKind, $"{empire} and {choir} have turned their armies on each other: the War in Heaven has begun", empire));
        var side = component.Allegiance.SideAmong(Power.All);
        var where = side == Power.Empire.Id ? $"{Colony} stands with {empire}: its victory is the colony's."
            : side == Power.Choir.Id ? $"{Colony} stands with {choir}: its victory is the colony's."
            : $"{Colony} stands with neither. Bring one power down, and the other may offer the colony its freedom.";
        StoryLetters.Send("The War in Heaven",
            $"{empire} and {choir} have stopped circling each other: they are at war in full, across the whole rim. Each will take the other's "
            + $"settlements, and both will send less against {Colony} while the front holds them.\n\n{where}",
            LetterDefOf.ThreatBig);
        StartPlot(component, today, cause);
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] The War in Heaven began on day {today}: {cause}.");
        }
    }

    private static void StartPlot(RimChroniclesGameComponent component, int today, string why)
    {
        if (PlotTemplateDef.Find(WarInHeaven.TemplateId) is not { } template)
        {
            return;
        }

        component.Plots.Start(template, new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = Colony },
            today, Rand.RangeInclusive(2, 5), why);
    }

    /// <summary>
    /// A power's capital fell (to the colony's storming, or its side's final push): the power is destroyed, its plot and
    /// crisis end, and the war goes on to its endings. If the war hadn't begun, this begins it.
    /// </summary>
    public static void Destroy(RimChroniclesGameComponent component, Power power, int today, string how)
    {
        var war = component.War;
        if (war.IsDestroyed(power))
        {
            return;
        }

        war.Destroy(power, today, how);
        var saga = component.SagaOf(power);
        saga.Ending = WarInHeaven.DestroyedEnding;
        saga.NextExpansionDay = -1; // no more expansions
        saga.Note(today, $"destroyed: {how}");
        foreach (var plot in component.Plots.Active.Where(p => p.Template == power.PlotTemplateId || p.Template == power.CrisisTemplateId).ToList())
        {
            component.Plots.End(plot, today, "destroyed");
        }

        var name = PowerFactions.Of(power)?.Name ?? power.DisplayName;
        component.News.Add(new NewsEvent(today, WarInHeaven.NewsKind, $"{name} is no more: {how}", name));
        StoryLetters.Send($"{name} is destroyed",
            $"{name} is finished: {how}. Its armies are scattered and its crisis against {Colony} is over.\n\nThe War in Heaven goes on to its end.",
            LetterDefOf.PositiveEvent);
        if (war.State == WarState.NotYet)
        {
            Begin(component, today, $"the fall of {name}");
        }
    }

    /// <summary>True when taking <paramref name="power"/>'s capital destroys it rather than ending the game (R11).</summary>
    public static bool CapitalFallDestroys(RimChroniclesGameComponent component) =>
        Available && component.War.CapitalFallDestroys(component.Empire, component.Choir);

    // ---- the plot's beats

    private static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        var c = Context(component);
        var gap = PlotGaps.Roll(template);
        var sideFaction = c.SidePower is { } s ? PowerFactions.Of(s) : null;
        var rivalFaction = c.Rival is { } r ? PowerFactions.Of(r) : null;
        switch (transition.Step)
        {
            case WarStep.DefendFront when sideFaction is not null && rivalFaction is not null:
            {
                if (QuestOffers.NearestSettlement(s2 => s2.Faction == sideFaction && s2 != EmpireBeats.Capital(sideFaction)) is not { } ally)
                {
                    return false;
                }

                return Job(component, plot, today, transition.Step, ally.Label, () => QuestOffers.DefendAlly(component, plot, ally, rivalFaction,
                    $"Hold {ally.Label}",
                    $"{rivalFaction.Name} has camped outside {ally.Label}, a settlement of {sideFaction.Name}, in the War in Heaven. Break the camp, or "
                    + $"{ally.Label} falls to {rivalFaction.Name}."));
            }

            case WarStep.StrikeRival when rivalFaction is not null:
                return Strike(component, plot, today, transition.Step, rivalFaction, sideFaction);
            case WarStep.StrikeEmpire when PowerFactions.Of(Power.Empire) is { } empire:
                return Strike(component, plot, today, transition.Step, empire, null);
            case WarStep.StrikeChoir when PowerFactions.Of(Power.Choir) is { } choir:
                return Strike(component, plot, today, transition.Step, choir, null);

            case WarStep.EscortConvoy when sideFaction is not null:
            {
                if (QuestOffers.NearestSettlement(s2 => s2.Faction == sideFaction) is not { } destination)
                {
                    return false;
                }

                return Job(component, plot, today, transition.Step, destination.Label, () => QuestOffers.Escort(component, plot, sideFaction, destination, 3,
                    $"A convoy to {destination.Label}",
                    $"{sideFaction.Name} is moving supplies to the front through {destination.Label}. Escort its convoy there safely."));
            }

            case WarStep.SabotageRival when rivalFaction is not null:
                return Sabotage(component, plot, today, transition.Step, rivalFaction);
            case WarStep.SabotageEmpire when PowerFactions.Of(Power.Empire) is { } empire:
                return Sabotage(component, plot, today, transition.Step, empire);
            case WarStep.SabotageChoir when PowerFactions.Of(Power.Choir) is { } choir:
                return Sabotage(component, plot, today, transition.Step, choir);

            case WarStep.FinalPush when rivalFaction is not null && sideFaction is not null:
            {
                if (EmpireBeats.Capital(rivalFaction) is not { } capital)
                {
                    return false;
                }

                return Job(component, plot, today, transition.Step, capital.Label, () => QuestOffers.Capital(component, plot, capital,
                    $"The final push on {capital.Label}",
                    $"{sideFaction.Name} is making its final push on {capital.Label}, the capital of {rivalFaction.Name}, and wants {Colony} at its head. "
                    + $"Take {capital.Label}, and {rivalFaction.Name} is finished."));
            }

            case WarStep.VictoryOffer when sideFaction is not null && c.SidePower is { } side:
            {
                WarArc.StartJob(plot, transition.Step, "", today);
                var reward = side == Power.Choir ? "a place in the Chorus for the colony, and the rim as the Choir's" : "a governorship for the colony, and the rim as the Empire's";
                ChoiceLetter_Plot.Send(plot, PowerArc.Answer, "Victory in the War in Heaven",
                    $"With {(rivalFaction?.Name ?? "its rival")} destroyed, {sideFaction.Name} has won the War in Heaven, with {Colony} at its side. It offers "
                    + $"{reward}. Accepting ends the story.",
                    [new PlotChoice("Accept", PowerArc.Accept), new PlotChoice("Not yet", PowerArc.Decline)], PowerArc.LetterTimeoutDays,
                    "it counts as not yet", sideFaction);
                component.Plots.Advance(plot, PlotBeat.Choice, today, PlotBook.JobCheckDays, "the side's victory offer");
                return true;
            }

            case WarStep.TreatyOffer:
            {
                var survivor = Power.All.FirstOrDefault(p => !component.War.IsDestroyed(p));
                if (survivor is null || PowerFactions.Of(survivor) is not { } faction)
                {
                    return false;
                }

                WarArc.StartJob(plot, transition.Step, "", today);
                ChoiceLetter_Plot.Send(plot, PowerArc.Answer, "A treaty of independence",
                    $"With its rival destroyed, {faction.Name} offers {Colony} a treaty: a free state, recognised by the last power on the rim. "
                    + "Signing ends the story. Refusing means fighting on until it falls too.",
                    [new PlotChoice("Sign the treaty", PowerArc.Accept), new PlotChoice("Fight on", PowerArc.Decline)], PowerArc.LetterTimeoutDays,
                    "no treaty is signed", faction);
                component.Plots.Advance(plot, PlotBeat.Choice, today, PlotBook.JobCheckDays, "the survivor's treaty");
                return true;
            }

            case WarStep.Aftermath:
                Aftermath(component, plot, c, today);
                component.Plots.Advance(plot, plot.Beat, today, plot.Fact(PowerArc.EndingFact) is { Length: > 0 } ? 1 : gap, "the job's aftermath");
                return true;

            case WarStep.Ending:
                Finish(component, plot, plot.Fact(PowerArc.EndingFact) ?? (transition.Id.StartsWith("Ending:", StringComparison.Ordinal) ? transition.Id["Ending:".Length..] : ""), today);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Strike a settlement of <paramref name="target"/>: a sabotage of its comms console. Never the capital
    /// (that's the final push, or the power's own Act 3).</summary>
    private static bool Strike(RimChroniclesGameComponent component, Plot plot, int today, string step, Faction target, Faction? side)
    {
        if (QuestOffers.NearestSettlement(s => s.Faction == target && s != EmpireBeats.Capital(target)) is not { } settlement)
        {
            return false;
        }

        return Job(component, plot, today, step, settlement.Label, () => QuestOffers.Sabotage(component, plot, settlement, null,
            $"Strike {settlement.Label}",
            $"Destroy the comms console in {settlement.Label}, a settlement of {target.Name}, within {QuestTemplates.SabotageDays} days."
            + (side is not null ? $" {side.Name} will take {settlement.Label} once it's blind." : " The War in Heaven is the colony's chance to hurt it.")));
    }

    /// <summary>Sabotage <paramref name="target"/>'s war machine: one of its crisis sites while its crisis runs, else a settlement.</summary>
    private static bool Sabotage(RimChroniclesGameComponent component, Plot plot, int today, string step, Faction target)
    {
        if (Crisis.Sites.SiteOffers.Pick(component, target) is { needsHolding: false } archetype)
        {
            var near = QuestOffers.NearestSettlement(s => s.Faction == target)?.Label ?? archetype.label;
            return Job(component, plot, today, step, near, () => QuestOffers.ImperialSite(component, plot, archetype, target,
                $"Destroy the {archetype.label}",
                $"{target.Name} has {Find.ActiveLanguageWorker.WithIndefiniteArticle(archetype.label)} near {Colony}: {archetype.description}. "
                + "Destroy its objective, and its war effort falters."));
        }

        return Strike(component, plot, today, step, target, null);
    }

    private static bool Job(RimChroniclesGameComponent component, Plot plot, int today, string step, string target, Func<Quest?> offer)
    {
        WarArc.StartJob(plot, step, target, today);
        if (offer() is null)
        {
            plot.SetFact(PowerArc.Job, "");
            return false;
        }

        component.Plots.Advance(plot, PlotBeat.Complication, today, PlotBook.JobCheckDays, $"job: {step} at {target}");
        return true;
    }

    /// <summary>A finished job's effects on the rim: settlements change hands, the front moves (Core), a capital taken
    /// destroys its power.</summary>
    private static void Aftermath(RimChroniclesGameComponent component, Plot plot, WarContext c, int today)
    {
        var r = WarArc.FinishJob(plot, c);
        var settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Label == r.Target);
        var side = c.SidePower is { } sp ? PowerFactions.Of(sp) : null;
        var rival = c.Rival is { } rp ? PowerFactions.Of(rp) : null;
        switch (r.Job, r.Success)
        {
            case (WarStep.DefendFront, false) when settlement is { HasMap: false } && rival is not null && c.Rival is { } rivalPower:
                Conquer(component, settlement, rivalPower, rival, today, $"{r.Target} fell to {rival.Name} in the War in Heaven");
                break;
            case (WarStep.StrikeRival, true) when settlement is { HasMap: false } && side is not null && c.SidePower is { } sidePower:
                Conquer(component, settlement, sidePower, side, today, $"{side.Name} took {r.Target} after the colony's strike");
                break;
        }

        if (r.Destroys is { } destroyed)
        {
            Destroy(component, destroyed, today, $"its capital fell to the final push of {side?.Name ?? "its rival"}, with {Colony} at its head");
        }

        var line = (r.Job, r.Success) switch
        {
            (WarStep.DefendFront, true) => $"{Colony} broke the siege of {r.Target} in the War in Heaven",
            (WarStep.DefendFront, false) => $"{r.Target} fell in the War in Heaven",
            (WarStep.StrikeRival or WarStep.StrikeEmpire or WarStep.StrikeChoir, true) => $"{Colony} struck {r.Target} in the War in Heaven",
            (WarStep.EscortConvoy, true) => $"a convoy reached {r.Target} under {Colony}'s guard",
            (WarStep.SabotageRival or WarStep.SabotageEmpire or WarStep.SabotageChoir, true) => $"{Colony} sabotaged the war machine near {r.Target}",
            _ => "",
        };
        if (line.Length > 0)
        {
            component.News.Add(new NewsEvent(today, WarInHeaven.NewsKind, line, ""));
        }

        component.War.Note(today, $"{r.Job} at {r.Target}: {(r.Success ? "won" : "lost")}; front {component.War.Front}");
    }

    private static void Conquer(RimChroniclesGameComponent component, Settlement settlement, Power power, Faction faction, int today, string why)
    {
        var owner = settlement.Faction?.Name ?? "";
        settlement.SetFaction(faction);
        EmpireDriver.Repaint(settlement);
        component.SagaOf(power).RecordAssisted(settlement.Label, owner, ExpansionResult.Conquered, today, why);
        component.News.Add(new NewsEvent(today, power.NewsKind, $"{faction.Name} took {settlement.Label} from {owner}", faction.Name));
    }

    /// <summary>The war reached its ending: the war, both sagas and the news record it, every power's story ends, and the
    /// victory screen shows the epilogue (W11).</summary>
    private static void Finish(RimChroniclesGameComponent component, Plot plot, string ending, int today)
    {
        component.War.End(today, ending);
        foreach (var power in Power.All.Where(p => !component.War.IsDestroyed(p)))
        {
            var saga = component.SagaOf(power);
            if (saga.Ending.Length == 0)
            {
                saga.Ending = ending;
                saga.Note(today, $"ending: {ending}");
            }
        }

        var line = ending switch
        {
            Core.Empire.EmpireEnding.Imperial => $"The Empire has won the War in Heaven, with {Colony} at its side",
            Core.Choir.ChoirEnding.Choral => $"The Hollow Choir has won the War in Heaven, with {Colony} at its side",
            Core.Empire.EmpireEnding.Independence => $"{Colony} signed a treaty of independence with the last power on the rim",
            WarEnding.IndependenceTotal => $"Both powers are destroyed: {Colony} is free",
            _ => $"The War in Heaven is over ({ending})",
        };
        component.News.Add(new NewsEvent(today, WarInHeaven.NewsKind, line, ""));
        plot.Tell(today, "The War in Heaven is over", line + ".");
        foreach (var story in component.Plots.Active.Where(p => Power.All.Any(pw => p.Template == pw.PlotTemplateId || p.Template == pw.CrisisTemplateId)).ToList())
        {
            component.Plots.End(story, today, ending);
        }

        var c = CaseFor(component, plot, ending, line);
        component.Plots.End(plot, today, ending);
        if (c is not null)
        {
            EmpireEndings.Epilogue(component, plot, c);
        }
        else
        {
            StoryLetters.Send("The War in Heaven is over", line + ".", LetterDefOf.PositiveEvent);
        }
    }

    /// <summary>The epilogue's facts for a war ending: the power it ended with (the side, the survivor, or for total
    /// independence the Empire), the other as the rival, and the war's own events.</summary>
    private static Core.Snapshots.EpilogueCase? CaseFor(RimChroniclesGameComponent component, Plot plot, string ending, string closing)
    {
        var war = component.War;
        var (power, rival) = ending switch
        {
            Core.Empire.EmpireEnding.Imperial => (Power.Empire, Power.Choir),
            Core.Choir.ChoirEnding.Choral => (Power.Choir, Power.Empire),
            WarEnding.IndependenceTotal => (Power.Empire, Power.Choir),
            _ => war.IsDestroyed(Power.Empire) ? (Power.Choir, Power.Empire) : (Power.Empire, Power.Choir),
        };
        if (PowerFactions.Of(power) is not { } main)
        {
            return null; // its faction is gone (defeated by vanilla): the letter stands in for the screen
        }

        var rivalName = PowerFactions.Of(rival)?.Name ?? rival.DisplayName;
        var events = new List<string> { $"The War in Heaven began on day {war.StartDay}." };
        events.AddRange(war.Destroyed.Select(id => Power.All.FirstOrDefault(p => p.Id == id)).OfType<Power>()
            .Select(p => $"{PowerFactions.Of(p)?.Name ?? p.DisplayName} was destroyed."));
        return EmpireEndings.CaseFor(component, plot, ending, main, power, rivalName, events, closing);
    }

    // ---- Dev Mode (rule 7)

    [DebugAction("Rim Chronicles", "War in Heaven: show", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevShow()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var war = component.War;
        var today = GenDate.DaysPassed;
        var since = component.Allegiance.WarSince();
        var c = Context(component);
        var lines = new List<string>
        {
            $"Running: {(Available ? "yes" : "no (needs the major plot, the War in Heaven and Hollow Choir settings, Royalty and Biotech)")}.",
            $"State: {war.State}{(war.StartDay >= 0 ? $" since day {war.StartDay} ({war.Cause})" : "")}. Front: {war.Front}. Destroyed: {string.Join(", ", war.Destroyed.DefaultIfEmpty("none"))}.",
            $"Can start: {war.CanStart(component.Empire, component.Choir, since >= 0)}; the colony's war since day {since}; "
                + $"odds today {WarInHeaven.Odds(since < 0 ? 0 : today - since, PlotGaps.WarSteepness()):P1}; certain on day {RimChroniclesMod.Settings.WarCertainDay}.",
            $"Side: {c.Side}; opposed: {string.Join(", ", c.Opposed.Select(p => p.Id).DefaultIfEmpty("none"))}. "
                + $"Crises send {war.BudgetFactor(RimChroniclesMod.Settings.WarFrontFactor):P0} at the colony.",
            $"Plot: {(Plot(component) is { } plot ? $"{plot.Id}, {plot.Beat}, next beat day {plot.NextBeatDay}, job {plot.Fact(PowerArc.Job)}" : "none")}.",
            "",
        };
        lines.AddRange(war.Log.TakeLast(15));
        Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", lines)));
    }

    [DebugAction("Rim Chronicles", "War in Heaven: start now", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevStart()
    {
        if (RimChroniclesGameComponent.Current is not { } component || !Available || component.War.State != WarState.NotYet)
        {
            Messages.Message("Rim Chronicles: the War in Heaven isn't running in this game, or has already begun.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Begin(component, GenDate.DaysPassed, "forced from Dev Mode");
    }

    [DebugAction("Rim Chronicles", "War in Heaven: destroy a power", allowedGameStates = AllowedGameStates.Playing)]
    public static List<DebugActionNode> DevDestroy() => Power.All.Select(power => new DebugActionNode(power.DisplayName, DebugActionType.Action, () =>
    {
        if (RimChroniclesGameComponent.Current is { } component && Available)
        {
            Destroy(component, power, GenDate.DaysPassed, "destroyed from Dev Mode");
        }
    })).ToList();

    /// <summary>Ends the war in one of its endings at once (W11): the victory screen and the epilogue. Starts the war first
    /// if it hasn't begun.</summary>
    [DebugAction("Rim Chronicles", "War in Heaven: reach an ending", allowedGameStates = AllowedGameStates.Playing)]
    public static List<DebugActionNode> DevEnding() => WarArc.Endings.Select(ending => new DebugActionNode(Core.Empire.EpilogueTask.Headline(ending), DebugActionType.Action, () =>
    {
        if (RimChroniclesGameComponent.Current is not { } component || !Available)
        {
            Messages.Message("Rim Chronicles: the War in Heaven isn't running in this game.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var today = GenDate.DaysPassed;
        Begin(component, today, "forced from Dev Mode");
        if (Plot(component) is not { } plot || component.War.State != WarState.Started)
        {
            Messages.Message("Rim Chronicles: the war plot couldn't start, or the war is already over.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var lost = ending switch
        {
            Core.Empire.EmpireEnding.Imperial => [Power.Choir],
            Core.Choir.ChoirEnding.Choral => [Power.Empire],
            WarEnding.IndependenceTotal => Power.All,
            _ => new[] { Power.Choir },
        };
        foreach (var power in lost)
        {
            component.War.Destroy(power, today, "from Dev Mode");
        }

        plot.SetFact(PowerArc.EndingFact, ending);
        Finish(component, plot, ending, today);
    })).ToList();

    [DebugAction("Rim Chronicles", "War in Heaven: move the front", allowedGameStates = AllowedGameStates.Playing)]
    public static List<DebugActionNode> DevFront() =>
    [
        new("+1", DebugActionType.Action, () => { if (RimChroniclesGameComponent.Current is { } c) c.War.Front++; }),
        new("-1", DebugActionType.Action, () => { if (RimChroniclesGameComponent.Current is { } c) c.War.Front--; }),
        new($"to {WarArc.FrontForPush} (the final push opens)", DebugActionType.Action, () => { if (RimChroniclesGameComponent.Current is { } c) c.War.Front = WarArc.FrontForPush; }),
    ];
}
