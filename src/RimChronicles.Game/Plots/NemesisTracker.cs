using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.Nemesis;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Finds Nemesis hooks (Stage 3 M2): records raiders who kill or maim colonists, notices when they escape,
/// and seeds a Nemesis plot from the strongest escaped grudge when the plot limits allow. Main thread only.</summary>
public static class NemesisTracker
{
    private static float? _handCoverage;

    /// <summary>A lost part counts as maiming if it is at least as big as a hand, the same bar the game uses for a
    /// pawn's "biggest missing part". Fingers and toes don't start a vendetta.</summary>
    public static float HandCoverage => _handCoverage ??=
        ThingDefOf.Human.race.body.GetPartsWithDef(BodyPartDefOf.Hand).FirstOrDefault()?.coverageAbsWithChildren ?? 0.01f;

    /// <summary>Whether this attacker can become a nemesis: a named, humanlike member of a non-player faction, hostile
    /// to the victim.</summary>
    public static bool CanBeNemesis(Pawn attacker, Pawn victim) =>
        attacker.RaceProps.Humanlike && attacker.Name is not null && !attacker.Dead
        && attacker.Faction is { IsPlayer: false } && attacker.HostileTo(victim);

    public static void Record(RimChroniclesGameComponent component, Pawn killer, Pawn victim, string kind, string part)
    {
        var grudge = new Grudge
        {
            KillerId = killer.ThingID,
            KillerName = killer.Name?.ToStringShort ?? killer.LabelShort,
            KillerFaction = killer.Faction?.Name ?? "",
            VictimId = victim.ThingID,
            VictimName = victim.Name?.ToStringShort ?? victim.LabelShort,
            Kind = kind,
            Part = part,
            Day = GenDate.DaysPassed,
        };
        if (component.Grudges.Record(grudge))
        {
            component.Track(killer);
            component.Track(victim);
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] Grudge: {grudge.KillerName} {kind} {grudge.VictimName}{(part.Length > 0 ? $" ({part})" : "")}.");
            }
        }
    }

    /// <summary>Called daily: updates each tracked raider's fate, then seeds a plot if one can start.</summary>
    public static void Daily(RimChroniclesGameComponent component)
    {
        var ledger = component.Grudges;
        var today = GenDate.DaysPassed;

        foreach (var killerId in ledger.TrackedKillers.ToList())
        {
            var killer = component.Tracked(killerId);
            if (killer is null || killer.Destroyed || killer.Dead || killer.IsPrisonerOfColony)
            {
                ledger.Settle(killerId); // dead or caught before the plot began: the score is settled
            }
            else if (!killer.Spawned && killer.IsWorldPawn())
            {
                ledger.MarkEscaped(killerId);
            }
        }

        ledger.Expire(today);
        component.ForgetUntracked();
        TrySeed(component, today, force: false);
    }

    /// <summary>Starts a Nemesis plot from the strongest escaped grudge. With <paramref name="force"/> (Dev Mode) the
    /// plot limits are ignored.</summary>
    public static Plot? TrySeed(RimChroniclesGameComponent component, int today, bool force)
    {
        if (PlotTemplateDef.Find(NemesisPlot.TemplateId) is not { } template || component.Grudges.Strongest(today) is not { } grudge)
        {
            return null;
        }

        if (!force && component.Plots.CannotStart(template, today, PlotTemplateDef.Find) is not null)
        {
            return null;
        }

        var victim = component.TrackedOrFind(grudge.VictimId);
        var colonists = PawnsFinder.AllMaps_FreeColonists.Where(p => p != victim && !p.Dead).ToList();
        var heir = NemesisPlot.Protagonist(grudge, victim is null ? [] : HeirCandidates(victim, colonists), Faction.OfPlayer.Name);
        if (grudge.Kind == GrudgeKind.Maimed && !colonists.Concat(victim is null ? [] : [victim]).Any(p => p.ThingID == heir.Id))
        {
            heir = Inheritance.PickHeir(victim is null ? [] : HeirCandidates(victim, colonists), Faction.OfPlayer.Name); // victim gone
        }

        var protagonist = colonists.Concat(victim is null ? [] : [victim]).FirstOrDefault(p => p.ThingID == heir.Id);
        var relation = protagonist is not null && victim is not null && protagonist != victim
            ? protagonist.GetMostImportantRelation(victim)?.GetGenderSpecificLabel(victim) ?? ""
            : "";
        var plot = NemesisPlot.Seed(component.Plots, template, grudge, heir, relation, today, Rand.RangeInclusive(2, 5));
        if (Prefs.DevMode)
        {
            Log.Message($"[Rim Chronicles] Nemesis plot {plot.Id} started: {plot.Log[0]}; protagonist {heir.Name} ({heir.Rule}).");
        }

        return plot;
    }

    /// <summary>The living colonists and bonded animals who could inherit from <paramref name="victim"/>.</summary>
    public static List<HeirCandidate> HeirCandidates(Pawn victim, List<Pawn> colonists)
    {
        var list = colonists.Select(c =>
        {
            var relations = victim.GetRelations(c).ToList();
            return new HeirCandidate(c.ThingID, c.Name?.ToStringShort ?? c.LabelShort,
                relations.Any(d => d.familyByBloodRelation),
                relations.Any(d => d == PawnRelationDefOf.Spouse || d == PawnRelationDefOf.Fiance || d == PawnRelationDefOf.Lover),
                IsBondedAnimal: false,
                c.relations?.OpinionOf(victim) ?? 0);
        }).ToList();

        var bonded = victim.relations?.DirectRelations
            .Where(r => r.def == PawnRelationDefOf.Bond && r.otherPawn is { Dead: false } a && a.Faction == Faction.OfPlayer)
            .Select(r => new HeirCandidate(r.otherPawn.ThingID, r.otherPawn.Name?.ToStringShort ?? r.otherPawn.LabelShort, false, false, true, 0))
            ?? [];
        list.AddRange(bonded);
        return list;
    }
}
