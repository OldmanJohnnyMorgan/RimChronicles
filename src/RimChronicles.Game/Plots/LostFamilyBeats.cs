using LudeonTK;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>The Lost Family arc in game (Stage 4 M5): seeds from a colonist's living kin in another faction, then
/// letters, the kin taken captive, a Rescue quest, and reunion or loss. Main thread only.</summary>
public static class LostFamilyBeats
{
    public static void Register() => PlotDriver.Register(LostFamilyArc.TemplateId, LostFamilyArc.Transitions, Fire);

    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>Called daily: seeds a Lost Family plot from the closest living kin in another faction, when the limits allow.</summary>
    public static void Daily(RimChroniclesGameComponent component) => TrySeed(component, GenDate.DaysPassed, force: false);

    public static Plot? TrySeed(RimChroniclesGameComponent component, int today, bool force)
    {
        if (PlotTemplateDef.Find(LostFamilyArc.TemplateId) is not { } template
            || (!force && (today < LostFamilyArc.EarliestDay || !RimChroniclesMod.Settings.ArcEnabled(LostFamilyArc.TemplateId)
                           || component.Plots.CannotStart(template, today, PlotTemplateDef.Find, RimChroniclesMod.Settings.StoryIntensity) is not null)))
        {
            return null;
        }

        var inPlay = component.Plots.Active.Concat(component.Plots.Finished)
            .Select(p => p.Fact(LostFamilyArc.KinId)).OfType<string>().ToHashSet();
        var candidate = PawnsFinder.AllMaps_FreeColonists
            .SelectMany(colonist => colonist.relations?.FamilyByBlood
                .Where(kin => IsFarAway(kin) && !inPlay.Contains(kin.ThingID))
                .Select(kin => (Colonist: colonist, Kin: kin, Def: colonist.GetMostImportantRelation(kin)))
                ?? [])
            .Where(c => c.Def is not null)
            .OrderByDescending(c => LostFamilyArc.Strength(c.Def!.defName))
            .ThenBy(c => c.Kin.ThingID, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate.Kin is null)
        {
            return null;
        }

        var (colonist, kin, def) = candidate;
        var plot = component.Plots.Start(template, ProtagonistSlot.ForPawn(colonist.ThingID, Name(colonist)), today,
            Rand.RangeInclusive(2, 6), $"{Name(colonist)} has a living {def!.GetGenderSpecificLabel(kin)}, {Name(kin)}, among {kin.Faction!.Name}");
        plot.SetFact(LostFamilyArc.KinId, kin.ThingID);
        plot.SetFact(LostFamilyArc.KinName, Name(kin));
        plot.SetFact(LostFamilyArc.KinFaction, kin.Faction.Name);
        plot.SetFact(LostFamilyArc.Relation, def.GetGenderSpecificLabel(kin));
        plot.SetFact(LostFamilyArc.KinPronoun, NemesisTracker.Pronoun(kin));
        plot.SetFact(LostFamilyArc.ProtagonistPronoun, NemesisTracker.Pronoun(colonist));
        component.Track(kin);
        component.Track(colonist);
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Lost Family plot {plot.Id} started: {plot.Log[0]}.");
        }

        return plot;
    }

    /// <summary>Alive, free, in another (visible) faction, and not on any map: someone a letter would have to reach.</summary>
    private static bool IsFarAway(Pawn kin) =>
        !kin.Dead && kin.RaceProps.Humanlike && kin.Faction is { IsPlayer: false, Hidden: false } && !kin.Spawned && kin.IsWorldPawn()
        && !kin.IsPrisoner;

    private static string Name(Pawn pawn) => pawn.Name?.ToStringShort ?? pawn.LabelShort;

    public static bool Fire(RimChroniclesGameComponent component, Plot plot, PlotTemplate template, int today, PlotTransition transition)
    {
        var kin = component.TrackedOrFind(plot.Fact(LostFamilyArc.KinId) ?? "");
        var step = Enum.TryParse<LostFamilyStep>(transition.Step, out var s) ? s : LostFamilyStep.Lost;
        if ((kin is null || kin.Dead) && step != LostFamilyStep.Reunited)
        {
            step = LostFamilyStep.Lost;
        }

        var gap = Rand.RangeInclusive(template.MinGapDays, template.MaxGapDays);
        var f = LostFamilyLetters.Facts.From(plot, Colony);
        switch (step)
        {
            case LostFamilyStep.Letter:
            {
                var letters = int.TryParse(plot.Fact(LostFamilyArc.Letters), out var n) ? n + 1 : 1;
                plot.SetFact(LostFamilyArc.Letters, letters.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Narrate(component, plot, letters == 1 ? "Letter" : "AnotherLetter", LostFamilyLetters.Letter(f, letters == 1), LetterDefOf.PositiveEvent);
                break;
            }

            case LostFamilyStep.Taken:
            {
                var captor = Find.FactionManager.AllFactionsListForReading
                    .Where(x => !x.IsPlayer && !x.Hidden && !x.defeated && x.def.humanlikeFaction && x.HostileTo(Faction.OfPlayer) && x != kin!.Faction)
                    .RandomElementWithFallback();
                if (captor is null)
                {
                    return false; // no one hostile to take them: try again another day
                }

                plot.SetFact(LostFamilyArc.Captor, captor.Name);
                f = LostFamilyLetters.Facts.From(plot, Colony);
                Narrate(component, plot, "Taken", LostFamilyLetters.Taken(f), LetterDefOf.NegativeEvent);
                plot.PublicHint = LostFamilyLetters.TakenNews(f);
                component.News.Add(new NewsEvent(today, "PlotHint", plot.PublicHint, captor.Name));
                break;
            }

            case LostFamilyStep.OfferRescue:
            {
                var captor = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.Name == plot.Fact(LostFamilyArc.Captor));
                var (name, description) = LostFamilyLetters.RescueQuest(f);
                if (kin is null || kin.Spawned || captor is null || Quests.QuestOffers.Rescue(component, plot, kin, captor, name, description) is null)
                {
                    plot.SetFact(LostFamilyArc.RescueQuestFact, QuestOutcome.Other); // no rescue possible: the trail goes cold
                }

                gap = QuestTemplates.SiteDays + 10;
                break;
            }

            case LostFamilyStep.Reunited:
                End(component, plot, today, reunited: true);
                return true;

            default:
                End(component, plot, today, reunited: false);
                return true;
        }

        component.Plots.Advance(plot, LostFamilyArc.BeatAfter(step), today, gap, step.ToString());
        return true;
    }

    private static void End(RimChroniclesGameComponent component, Plot plot, int today, bool reunited)
    {
        var f = LostFamilyLetters.Facts.From(plot, Colony);
        Narrate(component, plot, reunited ? "Ending:Reunited" : "Ending:Lost", reunited ? LostFamilyLetters.Reunited(f) : LostFamilyLetters.Lost(f),
            reunited ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent);
        if (reunited)
        {
            component.News.Add(new NewsEvent(today, "PlotResolved", LostFamilyLetters.ReunitedNews(f), f.Captor));
        }

        var thought = reunited ? LostFamilyThoughts.Reunited : LostFamilyThoughts.Lost;
        foreach (var id in new[] { plot.Protagonist.PawnId, plot.Protagonist.CoLeadId }.Where(x => x.Length > 0))
        {
            if (component.TrackedOrFind(id) is { Dead: false } pawn && pawn.needs?.mood is { } mood && thought is not null)
            {
                mood.thoughts.memories.TryGainMemory(thought);
            }
        }

        component.Plots.End(plot, today, reunited ? "reunited" : "lost");
        component.ForgetUntracked();
    }

    private static void Narrate(RimChroniclesGameComponent component, Plot plot, string step, (string Label, string Text) template, LetterDef def) =>
        PlotNarrator.Write(component, PlotNarrator.CaseFor(plot, step), Colony, template.Text,
            text => StoryLetters.Send(component, plot, template.Label, text, def));

    [DebugAction("Rim Chronicles", "Lost Family: force seed", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevSeed()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        if (TrySeed(component, GenDate.DaysPassed, force: true) is null && MakeKin() && TrySeed(component, GenDate.DaysPassed, force: true) is null)
        {
            Messages.Message("Rim Chronicles: no colonist has living kin in another faction.", MessageTypeDefOf.RejectInput, historical: false);
        }
    }

    /// <summary>Dev Mode: gives a random colonist a parent in a non-hostile faction, so there is someone to write.</summary>
    private static bool MakeKin()
    {
        var colonist = PawnsFinder.AllMaps_FreeColonists.Where(p => p.RaceProps.Humanlike).RandomElementWithFallback();
        var faction = Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer) && f.RandomPawnKind() is not null)
            .RandomElementWithFallback();
        if (colonist is null || faction?.RandomPawnKind() is not { } kind)
        {
            return false;
        }

        var age = Math.Min(80f, colonist.ageTracker.AgeBiologicalYearsFloat + 25f);
        var parent = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true, fixedBiologicalAge: age, fixedChronologicalAge: age));
        Find.WorldPawns.PassToWorld(parent, PawnDiscardDecideMode.KeepForever);
        colonist.relations.AddDirectRelation(PawnRelationDefOf.Parent, parent);
        return true;
    }
}

/// <summary>The Lost Family payoffs, defined in Defs/ThoughtDefs/RimChronicles_Thoughts.xml.</summary>
[DefOf]
public static class LostFamilyThoughts
{
    [DefAlias("RimChronicles_FamilyReunited")]
    public static ThoughtDef? Reunited;

    [DefAlias("RimChronicles_FamilyLost")]
    public static ThoughtDef? Lost;

    static LostFamilyThoughts() => DefOfHelper.EnsureInitializedInCtor(typeof(LostFamilyThoughts));
}
