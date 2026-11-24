using RimChronicles.Core.News;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RimChronicles.Game.News;

/// <summary>The reporter's life in game (Stage 2 M5): visits to the colony, being held prisoner or recruited, and
/// dying. Main thread only.</summary>
public static class ReporterLife
{
    /// <summary>Called daily: keeps the reporter's status in step with the pawn, and starts a visit when one is due.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var desk = component.Desk;
        if (component.ReporterPawn is not { } pawn)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        if (pawn.Dead || pawn.Destroyed)
        {
            // Normally caught by the Kill patch; this covers deaths it couldn't see.
            Died(component, pawn, killerIsColony: null, held: false, recruited: false);
            return;
        }

        if (pawn.Faction == Faction.OfPlayer)
        {
            desk.Silence(today); // recruited: the outlet loses its reporter to the colony
        }
        else if (pawn.IsPrisonerOfColony)
        {
            desk.Held();
        }
        else
        {
            desk.Freed();
        }

        if (desk.Reporter.NextVisitDay < 0 && desk.Reporter.Status == ReporterStatus.Active)
        {
            desk.ScheduleVisit(today, Rand.RangeInclusive(NewsDesk.MinVisitGapDays, NewsDesk.MaxVisitGapDays));
        }

        if (desk.VisitDue(today))
        {
            // Retry tomorrow if the colony isn't fit for visitors today.
            desk.ScheduleVisit(today, TryVisit(component) ? Rand.RangeInclusive(NewsDesk.MinVisitGapDays, NewsDesk.MaxVisitGapDays) : 1);
        }
    }

    /// <summary>Brings the reporter to the colony as a visitor for about a day. False if they can't come now.</summary>
    public static bool TryVisit(RimChroniclesGameComponent component)
    {
        if (component.ReporterPawn is not { } pawn || pawn.Dead || pawn.Spawned || !pawn.IsWorldPawn()
            || pawn.Faction is not { } faction || faction.IsPlayer || faction.HostileTo(Faction.OfPlayer)
            || Find.AnyPlayerHomeMap is not { } map || GenHostility.AnyHostileActiveThreatToPlayer(map)
            || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Neutral))
        {
            return false;
        }

        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 5), map);
        if (!RCellFinder.TryFindRandomSpotJustOutsideColony(pawn, out var chillSpot))
        {
            chillSpot = map.Center;
        }

        LordMaker.MakeNewLord(faction, new LordJob_VisitColony(faction, chillSpot, GenDate.TicksPerDay), map, [pawn]);
        var reporter = component.Desk.Reporter;
        StoryLetters.Send($"{reporter.Outlet.CapitalizeFirst()}: correspondent visiting",
            $"{reporter.Name}, who writes the bulletins for {reporter.Outlet}, has come by to gather news. They'll stay for about a day.",
            LetterDefOf.NeutralEvent, new LookTargets(pawn), faction);
        return true;
    }

    /// <summary>Records the reporter's death. A death at the colony's hands silences the news with no notice; any
    /// other death is public news, and the outlet sends a replacement in 5–10 days.</summary>
    public static void Died(RimChroniclesGameComponent component, Pawn pawn, bool? killerIsColony, bool held, bool recruited)
    {
        var desk = component.Desk;
        if (desk.Reporter.Status is ReporterStatus.Silenced or ReporterStatus.Replacing)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        component.ReporterPawn = null;
        if (desk.KilledByColony(killerIsColony, held, recruited, Find.TickManager.TicksGame))
        {
            desk.Silence(today);
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] The colony killed the reporter {desk.Reporter.Name}; the news stops.");
            }

            return;
        }

        component.News.Add(new NewsEvent(today, NewsKind.ReporterDied, NewsPhrasing.ReporterDied(desk.Reporter.Name, desk.Reporter.Outlet), desk.Reporter.Faction));
        desk.ReporterLost(today, Rand.RangeInclusive(NewsDesk.MinReplaceDays, NewsDesk.MaxReplaceDays));
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] The reporter {desk.Reporter.Name} died; a replacement is due on day {desk.Reporter.ReplaceDay}.");
        }
    }
}
