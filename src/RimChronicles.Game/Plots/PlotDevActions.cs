using LudeonTK;
using RimChronicles.Game.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Dev Mode actions for minor plots, under "Rim Chronicles". Arcs add their own (force seed, advance beat).</summary>
public static class PlotDevActions
{
    [DebugAction("Rim Chronicles", "Show plots", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowPlots()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var book = component.Plots;
        var today = GenDate.DaysPassed;
        var gate = ColonyPacingGate.Instance.BlockReason() ?? "open";
        var lines = new List<string>
        {
            $"Day {today}. Active: {book.Active.Count}. Last plot started day {book.LastStartDay}; last beat day {book.LastBeatDay}. Pacing gate: {gate}.",
        };
        foreach (var plot in book.Active.Concat(book.Finished.TakeLast(3)))
        {
            var who = plot.Protagonist.CoLeadName.Length > 0 ? $"{plot.Protagonist.Name} with {plot.Protagonist.CoLeadName}" : plot.Protagonist.Name;
            lines.Add("");
            lines.Add($"{plot.Id} ({plot.Template}), {plot.Beat}, protagonist {who}, next beat day {plot.NextBeatDay}");
            if (plot.Facts.Count > 0)
            {
                lines.Add("  facts: " + string.Join(", ", plot.Facts.Where(f => f.Value.Length > 0).Select(f => $"{f.Key}={f.Value}")));
            }

            lines.AddRange(plot.Log.TakeLast(6).Select(l => "  " + l));
        }

        LetterSink.Instance.Send($"Rim Chronicles: plots ({book.Active.Count} active)", string.Join("\n", lines));
    }
}

/// <summary>Dev Mode actions for the Nemesis arc.</summary>
public static class NemesisDevActions
{
    /// <summary>Makes a raider from a hostile faction "maim" a random colonist (no real injury), marks them escaped, and
    /// seeds a Nemesis plot now, ignoring the plot limits.</summary>
    [DebugAction("Rim Chronicles", "Nemesis: force seed", allowedGameStates = AllowedGameStates.Playing)]
    public static void ForceSeed()
    {
        if (RimChroniclesGameComponent.Current is not { } component || PawnsFinder.AllMaps_FreeColonists.RandomElementWithFallback() is not { } victim)
        {
            return;
        }

        var faction = Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && f.RandomPawnKind() is not null)
            .RandomElementWithFallback();
        if (faction?.RandomPawnKind() is not { } kind)
        {
            Messages.Message("Rim Chronicles: no hostile humanlike faction to draw a nemesis from.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var raider = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
        Find.WorldPawns.PassToWorld(raider, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
        NemesisTracker.Record(component, raider, victim, Core.Plots.Nemesis.GrudgeKind.Maimed, "arm (Dev Mode test)");
        component.Grudges.MarkEscaped(raider.ThingID);
        if (NemesisTracker.TrySeed(component, GenDate.DaysPassed, force: true) is { } plot)
        {
            Messages.Message($"Rim Chronicles: {plot.Id} started. {plot.Log[0]}", MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    [DebugAction("Rim Chronicles", "Nemesis: show grudges", allowedGameStates = AllowedGameStates.Playing)]
    public static void ShowGrudges()
    {
        if (RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var today = GenDate.DaysPassed;
        var grudges = component.Grudges.Grudges;
        var text = grudges.Count == 0
            ? "No grudges. A named raider has to kill a colonist, or take off a part at least as big as a hand, then escape."
            : string.Join("\n", grudges.Select(g =>
            {
                var killer = component.Tracked(g.KillerId);
                var where = killer is null ? "lost track" : killer.Dead ? "dead" : killer.Spawned ? "on a map" : "in the world";
                return $"Day {g.Day}: {g.KillerName} ({g.KillerFaction}) {g.Kind} {g.VictimName}{(g.Part.Length > 0 ? $" ({g.Part})" : "")}; "
                    + $"{(g.Escaped ? "escaped" : "not escaped")}, {where}; strength {Core.Plots.Nemesis.GrudgeLedger.Strength(g, today):0.00}"
                    + (g.PlotId.Length > 0 ? $"; in {g.PlotId}" : "");
            }));
        LetterSink.Instance.Send($"Rim Chronicles: grudges ({grudges.Count})", text);
    }

    /// <summary>Fires the oldest active plot's next beat now, ignoring its wait, the beat spacing and the pacing gate.</summary>
    [DebugAction("Rim Chronicles", "Plot: advance beat", allowedGameStates = AllowedGameStates.Playing)]
    public static void AdvanceBeat()
    {
        if (RimChroniclesGameComponent.Current is not { } component || component.Plots.Active.OrderBy(p => p.StartDay).FirstOrDefault() is not { } plot)
        {
            Messages.Message("Rim Chronicles: no active plot.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var today = GenDate.DaysPassed;
        plot.NextBeatDay = today;
        component.Plots.LastBeatDay = -1;
        if (!PlotDriver.FireNow(component, plot, today))
        {
            Messages.Message($"Rim Chronicles: {plot.Id}'s beat couldn't fire now (see Show plots).", MessageTypeDefOf.RejectInput, historical: false);
        }
    }

    [DebugAction("Rim Chronicles", "Plot: kill protagonist (by nemesis)", allowedGameStates = AllowedGameStates.Playing)]
    public static void KillProtagonistByNemesis() => KillProtagonist(byNemesis: true);

    [DebugAction("Rim Chronicles", "Plot: kill protagonist (other)", allowedGameStates = AllowedGameStates.Playing)]
    public static void KillProtagonistOther() => KillProtagonist(byNemesis: false);

    /// <summary>Kills the oldest plot's protagonist through the real Kill path, so the succession patch is what runs.</summary>
    private static void KillProtagonist(bool byNemesis)
    {
        if (RimChroniclesGameComponent.Current is not { } component
            || component.Plots.Active.Where(p => p.Protagonist.Kind != Core.Plots.ProtagonistKind.Colony).OrderBy(p => p.StartDay).FirstOrDefault() is not { } plot
            || component.TrackedOrFind(plot.Protagonist.PawnId) is not { Dead: false } pawn)
        {
            Messages.Message("Rim Chronicles: no plot with a living protagonist.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        var killer = byNemesis ? component.Tracked(plot.Fact(Core.Plots.Nemesis.NemesisPlot.NemesisId) ?? "") : null;
        pawn.Kill(new DamageInfo(DamageDefOf.Cut, 9999f, instigator: killer));
    }
}
