using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.Plots;

/// <summary>Fires the Nemesis arc's beats in game (Stage 3 M3a): the sighting, the raid the nemesis leads, the bounty
/// choice, and the endings with their payoffs and news. Letters use the templates in <see cref="NemesisLetters"/>.
/// Main thread only.</summary>
public static class NemesisBeats
{
    public static void Register() => PlotDriver.Register(NemesisPlot.TemplateId, NemesisArc.Transitions, Fire);

    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>The plot driver's beat handler: fires the step the director chose. Returns false when it couldn't fire
    /// today (it is decided again tomorrow).</summary>
    public static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        var nemesis = component.Tracked(plot.Fact(NemesisPlot.NemesisId) ?? "");
        if (nemesis is null || nemesis.Dead || nemesis.Destroyed)
        {
            End(component, plot, today, NemesisEnd.KilledOtherwise);
            return true;
        }

        var step = Enum.TryParse<NemesisStep>(transition.Step, out var chosen) ? chosen : NemesisArc.NextStep(plot);
        var gap = PlotGaps.Roll(template);
        var facts = NemesisLetters.Facts.From(plot, Colony);
        switch (step)
        {
            case NemesisStep.Sighting:
                plot.SetFact(NemesisArc.Place, SightingPlace(nemesis));
                var sightings = int.TryParse(plot.Fact(NemesisArc.Sightings), out var n) ? n + 1 : 1;
                plot.SetFact(NemesisArc.Sightings, sightings.ToString(System.Globalization.CultureInfo.InvariantCulture));
                facts = NemesisLetters.Facts.From(plot, Colony);
                Narrate(component, plot, "Sighting", NemesisLetters.Sighting(facts), LetterDefOf.NeutralEvent);
                plot.PublicHint = NemesisLetters.SightingNews(facts);
                component.News.Add(new NewsEvent(today, "PlotHint", plot.PublicHint, facts.Faction));
                break;

            case NemesisStep.Raid:
            case NemesisStep.SecondRaid:
                if (nemesis.Faction is not { IsPlayer: false } faction || !faction.HostileTo(Faction.OfPlayer))
                {
                    End(component, plot, today, NemesisEnd.FeudFaded);
                    return true;
                }

                if (!TryRaid(nemesis, faction))
                {
                    return false; // no home map, or the raid couldn't start: try again tomorrow
                }

                Narrate(component, plot, step == NemesisStep.Raid ? "Raid" : "SecondRaid",
                    step == NemesisStep.Raid ? NemesisLetters.Raid(facts) : NemesisLetters.SecondRaid(facts), LetterDefOf.ThreatBig);
                if (step == NemesisStep.SecondRaid)
                {
                    plot.SetFact(NemesisArc.SecondRaidDone, "yes");
                }

                break;

            case NemesisStep.Choice:
                if (nemesis.Spawned)
                {
                    return false; // still on the map after the raid: wait until they're gone
                }

                var cost = NemesisArc.BountyCost(WealthUtility.PlayerWealth);
                plot.SetFact(NemesisArc.BountyCostFact, cost.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var (choiceLabel, choiceTemplate) = NemesisLetters.Choice(facts, cost);
                var plotId = plot.Id;
                PlotNarrator.Write(component, PlotNarrator.CaseFor(plot, "Choice"), Colony, choiceTemplate,
                    text => ChoiceLetter_Nemesis.Send(plotId, choiceLabel, text, cost));
                gap = NemesisArc.ChoiceTimeoutDays;
                break;

            case NemesisStep.BountyKill:
                if (nemesis.Spawned)
                {
                    return false;
                }

                plot.SetFact("killedBy", "bounty");
                nemesis.Kill(null); // the death patch ends the plot as KilledByBounty
                return true;

            case NemesisStep.Hunt:
                if (nemesis.Spawned)
                {
                    return false;
                }

                var hunt = Quests.QuestOffers.Hunt(component, plot, nemesis, $"Hunt {facts.Nemesis}",
                    $"{facts.Raider}, who {facts.Deed}, is holed up in a camp near {facts.Place}. {facts.Protagonist} means to end it there. "
                    + "Kill or capture the raider before they slip away again.");
                if (hunt is null)
                {
                    plot.SetFact(NemesisArc.HuntQuestFact, Core.Plots.QuestOutcome.Other); // no camp could be placed: the raider comes back instead
                }

                gap = NemesisArc.HuntWaitDays;
                break;

            case NemesisStep.ImperialOfficer:
                if (nemesis.Spawned || !Empire.EmpirePromotions.TryMakeOfficer(component, plot, nemesis, today))
                {
                    return false;
                }

                component.Plots.Advance(plot, plot.Beat, today, gap, "promoted: the nemesis joined the Empire");
                return true;

            case NemesisStep.Vanished:
                if (nemesis.Spawned)
                {
                    return false;
                }

                End(component, plot, today, NemesisEnd.Vanished);
                return true;
        }

        component.Plots.Advance(plot, NemesisArc.BeatAfter(step), today, gap, step.ToString());
        return true;
    }

    /// <summary>Ends the plot with its payoff (a memory for the protagonist), its letter, and public news.</summary>
    public static void End(RimChroniclesGameComponent component, Plot plot, int today, NemesisEnd end)
    {
        if (plot.Ended)
        {
            return;
        }

        var facts = NemesisLetters.Facts.From(plot, Colony);
        Narrate(component, plot, $"Ending:{end}", NemesisLetters.Ending(facts, end),
            end is NemesisEnd.Vanished or NemesisEnd.FeudFaded ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent);
        if (NemesisLetters.EndingNews(facts, end) is { } news)
        {
            component.News.Add(new NewsEvent(today, "PlotResolved", news, facts.Faction));
        }

        GivePayoff(component, plot, end);
        component.Plots.End(plot, today, end.ToString());
        component.Grudges.Grudges.RemoveAll(g => g.PlotId == plot.Id);
        component.ForgetUntracked();
    }

    /// <summary>The nemesis died: classifies who killed them and ends the plot.</summary>
    public static void NemesisDied(RimChroniclesGameComponent component, Plot plot, Thing? killer)
    {
        var end = plot.Fact("killedBy") == "bounty" ? NemesisEnd.KilledByBounty
            : killer?.Faction == Faction.OfPlayer ? NemesisEnd.KilledByColony
            : NemesisEnd.KilledOtherwise;
        End(component, plot, GenDate.DaysPassed, end);
    }

    private static void GivePayoff(RimChroniclesGameComponent component, Plot plot, NemesisEnd end)
    {
        var thought = end switch
        {
            NemesisEnd.KilledByColony => NemesisThoughts.Avenged,
            NemesisEnd.KilledByBounty or NemesisEnd.KilledOtherwise or NemesisEnd.Captured => NemesisThoughts.Settled,
            NemesisEnd.Vanished => NemesisThoughts.Escaped,
            _ => null,
        };
        if (thought is null)
        {
            return;
        }

        var slot = plot.Protagonist;
        var pawns = slot.Kind == ProtagonistKind.Colony
            ? PawnsFinder.AllMaps_FreeColonists
            : new[] { slot.PawnId, slot.CoLeadId }.Where(id => id.Length > 0).Select(component.TrackedOrFind).OfType<Pawn>().ToList();
        foreach (var pawn in pawns.Where(p => !p.Dead && p.needs?.mood is not null))
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }
    }

    private static string SightingPlace(Pawn nemesis) =>
        Find.WorldObjects.Settlements.Where(s => s.Faction == nemesis.Faction).Select(s => s.Label).RandomElementWithFallback()
        ?? "the wastes";

    /// <summary>Starts a raid by the nemesis's faction on a home map and puts the nemesis in it.</summary>
    private static bool TryRaid(Pawn nemesis, Faction faction)
    {
        if (nemesis.Spawned || Find.AnyPlayerHomeMap is not { } map)
        {
            return false;
        }

        var before = map.lordManager.lords.ToList();
        var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.faction = faction;
        parms.forced = true;
        // The storyteller only picks factions that can field a group at these points; we force the faction, so
        // lift the points to its minimum (as vanilla's AdjustedRaidPoints does), or no raid strategy fits.
        parms.points = System.Math.Max(parms.points, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat) * 1.05f);
        if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
        {
            return false;
        }

        var lord = map.lordManager.lords.LastOrDefault(l => l.faction == faction && !before.Contains(l));
        var anchor = lord?.ownedPawns.FirstOrDefault(p => p.Spawned);
        IntVec3 cell;
        if (anchor is not null)
        {
            cell = CellFinder.RandomClosewalkCellNear(anchor.Position, map, 4);
        }
        else if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile))
        {
            return false;
        }

        GenSpawn.Spawn(nemesis, cell, map);
        lord?.AddPawn(nemesis);
        return true;
    }

    /// <summary>Sends a beat's letter: written by the model when one is set up, else the template. The label is fixed.</summary>
    private static void Narrate(RimChroniclesGameComponent component, Plot plot, string step, (string Label, string Text) template, LetterDef def) =>
        PlotNarrator.Write(component, PlotNarrator.CaseFor(plot, step), Colony, template.Text,
            text => StoryLetters.Send(component, plot, template.Label, text, def));
}

/// <summary>The payoff memories, defined in Defs/ThoughtDefs/RimChronicles_Thoughts.xml.</summary>
[DefOf]
public static class NemesisThoughts
{
    [DefAlias("RimChronicles_NemesisAvenged")]
    public static ThoughtDef? Avenged;

    [DefAlias("RimChronicles_NemesisSettled")]
    public static ThoughtDef? Settled;

    [DefAlias("RimChronicles_NemesisEscaped")]
    public static ThoughtDef? Escaped;

    static NemesisThoughts() => DefOfHelper.EnsureInitializedInCtor(typeof(NemesisThoughts));
}
