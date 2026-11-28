using LudeonTK;
using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.News;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Game.Plots;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Empire;

/// <summary>Minor plots promoted into the major one (Stage 5 M6): a nemesis becomes an Imperial officer, and a Lost
/// Family relative is caught in a settlement the Empire conquers. Each is noted on the Empire plot too. Main thread only.</summary>
public static class EmpirePromotions
{
    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>Called daily: tells each Nemesis plot whether the Empire can take its nemesis in (awake, at war with
    /// the colony, the story not over). The director then offers the promotion as an option.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var empire = EmpireDriver.Available ? EmpireDriver.EmpireFaction : null;
        var saga = component.Empire;
        var open = empire is not null && saga.State is PowerState.Awakened or PowerState.Ascendant && saga.Ending.Length == 0
                   && empire.HostileTo(Faction.OfPlayer);
        foreach (var plot in component.Plots.Active.Where(p => p.Template == NemesisPlot.TemplateId))
        {
            plot.SetFact(Promotion.EmpireOpen, open ? "yes" : "no");
            plot.SetFact(Promotion.EmpireName, empire?.Name ?? "");
        }
    }

    /// <summary>The nemesis joins the Empire: their faction changes, so the raids they lead are Imperial from now on.</summary>
    public static bool TryMakeOfficer(RimChroniclesGameComponent component, Plot plot, Pawn nemesis, int today)
    {
        if (EmpireDriver.EmpireFaction is not { } empire || !empire.HostileTo(Faction.OfPlayer) || nemesis.Faction == empire)
        {
            return false;
        }

        var oldFaction = nemesis.Faction?.Name ?? plot.Fact(NemesisPlot.NemesisFaction) ?? "the raiders";
        nemesis.SetFaction(empire);
        plot.SetFact(Promotion.Fact, Promotion.ImperialOfficer);
        plot.SetFact(NemesisPlot.NemesisFaction, empire.Name);
        var name = plot.Fact(NemesisPlot.NemesisName) ?? nemesis.LabelShort;
        var (label, text) = Promotion.OfficerLetter(name, oldFaction, empire.Name, plot.Protagonist.Name);
        plot.Tell(today, label, text);
        StoryLetters.Send(label, text, LetterDefOf.ThreatSmall, nemesis.Spawned ? new LookTargets(nemesis) : null, empire);
        plot.PublicHint = Promotion.OfficerNews(name, oldFaction, empire.Name);
        component.News.Add(new NewsEvent(today, "PlotHint", plot.PublicHint, empire.Name));
        NoteOnEmpire(component, today, $"promoted {plot.Id}: {name} became an Imperial officer");
        return true;
    }

    /// <summary>Called when the Empire takes a settlement from <paramref name="owner"/>: a Lost Family relative of that
    /// faction, still writing letters, is caught in the fall, and the Empire becomes the captor. The plot moves to its
    /// Complication at once, since the conquest is the beat.</summary>
    public static void OnConquest(RimChroniclesGameComponent component, string owner, string settlement, Faction empire, int today)
    {
        foreach (var plot in component.Plots.Active.Where(p => Promotion.KinCaughtBy(p, owner)).ToList())
        {
            var kin = plot.Fact(LostFamilyArc.KinName) ?? "a relative";
            plot.SetFact(LostFamilyArc.Captor, empire.Name);
            plot.SetFact(Promotion.Fact, Promotion.Conquered);
            plot.SetFact(Promotion.ConqueredAt, settlement);
            var (label, text) = Promotion.CaughtLetter(kin, settlement, empire.Name, plot.Protagonist.Name);
            plot.Tell(today, label, text);
            StoryLetters.Send(label, text, LetterDefOf.NegativeEvent, StoryLetters.For(component, plot), empire);
            plot.PublicHint = $"{empire.Name} took captives when {settlement} fell";
            var template = PlotTemplateDef.Find(LostFamilyArc.TemplateId);
            component.Plots.Advance(plot, PlotBeat.Complication, today,
                template is null ? 5 : PlotGaps.Roll(template), $"promoted: {kin} caught in the fall of {settlement}");
            NoteOnEmpire(component, today, $"promoted {plot.Id}: {kin} caught in the fall of {settlement}");
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] Promotion: {plot.Id}, {kin} caught in the fall of {settlement}.");
            }
        }
    }

    private static void NoteOnEmpire(RimChroniclesGameComponent component, int today, string line)
    {
        component.Empire.Note(today, line);
        EmpireBeats.Current(component)?.Note(today, line);
    }

    [DebugAction("Rim Chronicles", "Empire: promote a minor plot", allowedGameStates = AllowedGameStates.Playing)]
    public static void DevPromote()
    {
        if (RimChroniclesGameComponent.Current is not { } component || EmpireDriver.EmpireFaction is not { } empire || !component.Empire.IsAwake)
        {
            Messages.Message("Rim Chronicles: wake the Empire first (Empire: trigger awakening).", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var today = GenDate.DaysPassed;
        var options = new List<FloatMenuOption>();
        foreach (var plot in component.Plots.Active.Where(p => p.Template == NemesisPlot.TemplateId && !Promotion.IsPromoted(p)))
        {
            options.Add(new FloatMenuOption($"{plot.Id}: nemesis joins the Empire", () =>
            {
                if (component.Tracked(plot.Fact(NemesisPlot.NemesisId) ?? "") is not { Spawned: false } nemesis || !TryMakeOfficer(component, plot, nemesis, today))
                {
                    Messages.Message("Rim Chronicles: the nemesis must be off the map, and the Empire at war with the colony.", MessageTypeDefOf.RejectInput, historical: false);
                }
            }));
        }

        foreach (var plot in component.Plots.Active.Where(p => p.Template == LostFamilyArc.TemplateId && !Promotion.IsPromoted(p)))
        {
            var faction = plot.Fact(LostFamilyArc.KinFaction) ?? "";
            options.Add(new FloatMenuOption($"{plot.Id}: the Empire takes a settlement of {faction}", () =>
            {
                var target = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction?.Name == faction && !s.HasMap);
                if (target is null || !Promotion.KinCaughtBy(plot, faction))
                {
                    Messages.Message("Rim Chronicles: needs a settlement of the relative's faction, and the plot past its first letter but not yet taken.",
                        MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                EmpireDriver.Conquer(component, target, empire, today, "Dev Mode");
            }));
        }

        if (options.Count == 0)
        {
            Messages.Message("Rim Chronicles: no Nemesis or Lost Family plot to promote.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }
}
