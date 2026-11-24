using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Core.Snapshots;
using RimChronicles.Game.Engine;
using RimChronicles.Llm.Chat;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Passes a plot to an heir when its protagonist dies, is taken or leaves (Stage 3 M4), and narrates the
/// handoff: the model writes the scene for a death (the inheritance-handoff prompt), a template covers everything
/// else. Main thread only.</summary>
public static class SuccessionDriver
{
    private static string Colony => Faction.OfPlayer.Name;

    /// <summary>Called daily: a protagonist (or co-lead) who was kidnapped or left the colony passes the plot on.
    /// Deaths are caught at once by the Kill patch.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        foreach (var plot in component.Plots.Active.Where(p => p.Protagonist.Kind != ProtagonistKind.Colony).ToList())
        {
            var pawn = component.TrackedOrFind(plot.Protagonist.PawnId);
            if (pawn is null || pawn.Destroyed)
            {
                ProtagonistLost(component, plot, pawn, ProtagonistLoss.Left);
            }
            else if (!pawn.Dead && IsKidnapped(pawn))
            {
                ProtagonistLost(component, plot, pawn, ProtagonistLoss.Taken);
            }
            else if (!pawn.Dead && pawn.Faction != Faction.OfPlayer)
            {
                ProtagonistLost(component, plot, pawn, ProtagonistLoss.Left);
            }
        }
    }

    private static bool IsKidnapped(Pawn pawn) =>
        Find.FactionManager.AllFactionsListForReading.Any(f => f.kidnapped?.KidnappedPawnsListForReading.Contains(pawn) == true);

    /// <summary>A pawn died: passes on every plot it led, and replaces it as co-lead where it was one.</summary>
    public static void PawnDied(RimChroniclesGameComponent component, Pawn pawn, Thing? killer)
    {
        var id = pawn.ThingID;
        foreach (var plot in component.Plots.Active.ToList())
        {
            if (plot.Protagonist.Kind != ProtagonistKind.Colony && plot.Protagonist.PawnId == id)
            {
                var byNemesis = killer is Pawn k && k.ThingID == plot.Fact(NemesisPlot.NemesisId);
                ProtagonistLost(component, plot, pawn, byNemesis ? ProtagonistLoss.KilledByNemesis : ProtagonistLoss.Died);
            }
            else if (plot.Protagonist.CoLeadId == id)
            {
                var next = PawnsFinder.AllMaps_FreeColonists.Where(p => p != pawn && !p.Dead)
                    .OrderByDescending(p => p.relations?.OpinionOf(pawn) ?? 0).FirstOrDefault();
                plot.Protagonist.CoLeadId = next?.ThingID ?? "";
                plot.Protagonist.CoLeadName = next?.Name?.ToStringShort ?? "";
                plot.Note(GenDate.DaysPassed, $"co-lead {pawn.LabelShort} died; {(next is null ? "no one" : plot.Protagonist.CoLeadName)} takes their place");
            }
        }
    }

    public static void ProtagonistLost(RimChroniclesGameComponent component, Plot plot, Pawn? lost, ProtagonistLoss loss)
    {
        if (plot.Ended || PlotTemplateDef.Find(plot.Template) is not { } template)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var oldName = plot.Protagonist.Name;
        var colonists = PawnsFinder.AllMaps_FreeColonists.Where(p => p != lost && !p.Dead).ToList();
        var heir = Inheritance.PickHeir(lost is null ? [] : NemesisTracker.HeirCandidates(lost, colonists), Colony);
        var heirPawn = heir.Kind == ProtagonistKind.Colony ? null : component.TrackedOrFind(heir.Id) ?? colonists.FirstOrDefault(p => p.ThingID == heir.Id);
        var victim = component.TrackedOrFind(plot.Fact(NemesisPlot.VictimId) ?? plot.Fact(Core.Plots.LostFamily.LostFamilyArc.KinId) ?? "");

        string Label(Pawn? from, Pawn? of) => from is not null && of is not null && from != of
            ? from.GetMostImportantRelation(of)?.GetGenderSpecificLabel(of) ?? ""
            : "";

        var context = new Succession.Context(oldName, plot.Fact(NemesisPlot.ProtagonistPronoun) ?? NemesisTracker.Pronoun(lost),
            Label(heirPawn, lost), Label(heirPawn, victim), NemesisTracker.Pronoun(heirPawn));
        var line = Succession.Pass(plot, template, heir, loss, context, today);
        if (heirPawn is not null)
        {
            component.Track(heirPawn);
        }

        if (loss == ProtagonistLoss.KilledByNemesis && lost is not null)
        {
            plot.SetFact(NemesisPlot.VictimId, lost.ThingID);
        }

        plot.NextBeatDay = Math.Max(plot.NextBeatDay, today + 2); // let the handoff land before the next beat
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] {plot.Id}: {line}");
        }

        Narrate(component, plot, lost, heirPawn, heir, loss, context);
    }

    private static void Narrate(RimChroniclesGameComponent component, Plot plot, Pawn? lost, Pawn? heirPawn, HeirChoice heir,
        ProtagonistLoss loss, Succession.Context context)
    {
        string unfinished;
        string summary;
        if (plot.Template == Core.Plots.LostFamily.LostFamilyArc.TemplateId)
        {
            var family = Core.Plots.LostFamily.LostFamilyLetters.Facts.From(plot, Colony);
            unfinished = Core.Plots.LostFamily.LostFamilyLetters.Unfinished(family);
            summary = $"{unfinished}.";
        }
        else
        {
            var facts = NemesisLetters.Facts.From(plot, Colony);
            unfinished = $"{facts.Raider}, who {facts.Deed}, is still out there";
            summary = $"{facts.Raider} {facts.Deed}.";
        }

        var heirToLost = lost is not null && heirPawn is not null ? lost.GetMostImportantRelation(heirPawn)?.GetGenderSpecificLabel(heirPawn) ?? "" : "";
        if (heir.Kind == ProtagonistKind.Animal)
        {
            heirToLost = "bonded animal";
        }

        var (label, template) = HandoffTask.Template(context.OldName, heir.Name, heirToLost, loss, unfinished, heir.Kind);
        void Deliver(string text)
        {
            plot.Tell(GenDate.DaysPassed, label, text);
            Engine.StoryLetters.Send(label, text, LetterDefOf.NeutralEvent, heirPawn is { Spawned: true } ? new LookTargets(heirPawn) : null);
        }

        var died = loss is ProtagonistLoss.Died or ProtagonistLoss.KilledByNemesis;
        if (!died || lost is null || heirPawn is null)
        {
            Deliver(template);
            return;
        }

        // The handoff scene needs both dossiers: the snapshot's colonists, plus the dead protagonist (and an animal heir).
        var colonists = PawnsFinder.AllMaps_FreeColonists;
        var world = WorldReader.Instance.Read();
        var pawns = world.Pawns.ToList();
        foreach (var p in new[] { lost, heirPawn }.Where(p => pawns.All(x => x.Id != p.ThingID)))
        {
            pawns.Add(WorldReader.ReadPawn(p, colonists));
        }

        var snapshot = world with
        {
            Pawns = pawns,
            Handoff = new HandoffCase
            {
                DeadPawn = lost.ThingID,
                Heir = heirPawn.ThingID,
                Relation = heirToLost.Length > 0 ? heirToLost : "friend",
                PlotSummary = $"{summary} {context.OldName} {Succession.HowLost(loss)}.",
            },
        };
        PlotNarrator.Run(component, "the handoff scene", template, text => Deliver(text),
            (prompts, tier, fallback) => HandoffTask.Build(prompts, snapshot, tier, ThinkingMode.Off, fallback).Definition);
    }
}
