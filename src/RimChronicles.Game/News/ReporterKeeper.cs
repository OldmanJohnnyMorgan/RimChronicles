using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.News;

/// <summary>Finds the Rim Dispatch a reporter (Stage 2 M4): a pawn generated from a neutral outlander faction and
/// kept as a world pawn, so it is saved with the world and can later be met, kidnapped or killed. Main thread only.</summary>
public static class ReporterKeeper
{
    /// <summary>Called daily. Recruits a reporter if there is none yet; does nothing once one exists (M5 handles deaths).</summary>
    public static void EnsureReporter(RimChroniclesGameComponent component)
    {
        if (component.ReporterPawn is not null)
        {
            return;
        }

        if (PickFaction() is not { } faction || ReporterKind(faction) is not { } kind)
        {
            if (Prefs.DevMode)
            {
                Log.WarningOnce("[Rim Chronicles] No faction can supply a reporter yet (all hostile, hidden or without humanlike "
                    + "pawn kinds); the default reporter writes until one can.", 0x52434e03);
            }

            return; // the default reporter writes, and we try again tomorrow
        }

        Pawn pawn;
        try
        {
            pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, allowPregnant: false,
                developmentalStages: DevelopmentalStage.Adult));
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"[Rim Chronicles] Could not generate a reporter in {faction.Name}: {ex}", 0x52434e02);
            return;
        }

        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        component.ReporterPawn = pawn;

        var reporter = component.Desk.Reporter;
        reporter.Name = DisplayName(pawn);
        reporter.Faction = faction.Name;
        LetterSink.Instance.Send($"{reporter.Outlet.CapitalizeFirst()}: new correspondent",
            $"{reporter.Name} of {faction.Name} will be reporting for {reporter.Outlet}, with a bulletin every "
            + $"{RimChroniclesMod.Settings.NewsCadenceDays} days. Past bulletins are kept in the Dispatch tab.");
    }

    /// <summary>"Kael Dunmore" for a normal name, else the short name.</summary>
    public static string DisplayName(Pawn pawn) => pawn.Name switch
    {
        NameTriple { First.Length: > 0, Last.Length: > 0 } triple => $"{triple.First} {triple.Last}",
        { } name => name.ToStringShort,
        _ => pawn.LabelShort,
    };

    /// <summary>A visible, undefeated humanlike faction that isn't hostile to the colony, preferring civil outlanders,
    /// then the most advanced.</summary>
    private static Faction? PickFaction() =>
        Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction && !f.def.permanentEnemy
                        && ReporterKind(f) is not null && f.PlayerRelationKind != FactionRelationKind.Hostile)
            .OrderByDescending(f => f.def == FactionDefOf.OutlanderCivil)
            .ThenByDescending(f => (int)f.def.techLevel)
            .ThenBy(f => f.loadID)
            .FirstOrDefault();

    /// <summary>A humanlike pawn kind from the faction's group makers (traders, guards, members), preferring non-fighters
    /// such as villagers and traders. Falls back to <c>basicMemberKind</c>, which most NPC factions leave unset: the
    /// game's own <c>Faction.RandomPawnKind</c> does the same.</summary>
    private static PawnKindDef? ReporterKind(Faction faction)
    {
        var kinds = (faction.def.pawnGroupMakers ?? [])
            .SelectMany(g => g.options.Concat(g.traders).Concat(g.guards))
            .Select(o => o.kind)
            .Where(k => k?.RaceProps?.Humanlike == true)
            .Distinct()
            .ToList();
        return kinds.OrderBy(k => k.isFighter).ThenByDescending(k => k.trader).ThenBy(k => k.defName).FirstOrDefault()
               ?? faction.def.basicMemberKind;
    }
}
