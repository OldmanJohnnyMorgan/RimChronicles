using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Psykers;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimChronicles.Game.Crisis.Psykers;

/// <summary>
/// The psyker AI's think node (tech spec §4.3), inserted into every humanlike think tree after mental states
/// (<c>Humanlike_PostMentalState</c>), so it works under any lord duty. For a hostile crisis psyker, at most every
/// <see cref="CheckTicks"/> ticks, it scores the castable psycasts on the shared <see cref="BattleView"/> and returns the
/// best cast's job; otherwise it returns nothing and the pawn fights as usual.
/// </summary>
public sealed class JobGiver_RcPsykerCast : ThinkNode_JobGiver
{
    public const int CheckTicks = 250;

    private static readonly Dictionary<int, int> NextCheck = [];

    public static IPsycastProvider Provider { get; set; } = VanillaProvider.Instance;

    protected override Job? TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Downed || pawn.InMentalState || pawn.IsPrisoner || pawn.Faction is not { } faction || faction == Faction.OfPlayer
            || UnitKit.Unit(pawn.kindDef) is not { } unit || !PsycastKits.IsPsyker(unit.role)
            || !GenHostility.AnyHostileActiveThreatTo(pawn.Map, faction))
        {
            return null;
        }

        var now = Find.TickManager.TicksGame;
        if (NextCheck.TryGetValue(pawn.thingIDNumber, out var next) && now < next)
        {
            return null;
        }

        NextCheck[pawn.thingIDNumber] = now + CheckTicks;
        if (NextCheck.Count > 500)
        {
            NextCheck.Clear();
        }

        var (view, things) = BattleViewBuilder.For(pawn.Map, faction);
        if (view.Find(pawn.thingIDNumber) is not { } caster)
        {
            return null;
        }

        var options = Provider.Options(pawn);
        var decision = PsykerBrain.Choose(view, caster, options, ScorerRegistry.For, pawn.psychicEntropy?.CurrentPsyfocus ?? 0);
        Job? job = null;
        ScoredCast? chosen = null;
        foreach (var candidate in decision.Cast is null ? [] : decision.Candidates.Where(c => c.Utility >= PsykerBrain.MinUtility))
        {
            job = Provider.JobFor(pawn, candidate, things);
            if (job is not null)
            {
                chosen = candidate;
                break;
            }
        }

        PsykerLog.Record(pawn, decision, chosen);
        return job;
    }
}

/// <summary>What each psyker last decided, and every cast a crisis unit started, for the Dev Mode views and the
/// "no cast its scorers didn't choose" check (crisis spec §9). Not saved.</summary>
public static class PsykerLog
{
    public sealed record Entry(int Tick, string Pawn, string Decision, float Heat, float Psyfocus, IReadOnlyList<ScoredCast> Top);

    private static readonly Dictionary<int, Entry> Last = [];
    private static readonly Dictionary<int, string> Chosen = [];

    /// <summary>Dev Mode: show each decision as text over the psyker.</summary>
    public static bool Debug { get; set; }

    public static int ChosenCasts { get; private set; }
    public static List<string> UnchosenCasts { get; } = [];

    public static IEnumerable<Entry> Entries => Last.Values.OrderBy(e => e.Pawn);

    /// <summary>Forgets the last game's decisions and casts (a new game reuses thing id numbers). Keeps <see cref="Debug"/>.</summary>
    public static void Reset()
    {
        Last.Clear();
        Chosen.Clear();
        ChosenCasts = 0;
        UnchosenCasts.Clear();
    }

    public static void Record(Pawn pawn, PsykerDecision decision, ScoredCast? chosen)
    {
        var heat = pawn.psychicEntropy is { } e ? e.EntropyValue / Math.Max(1f, e.MaxEntropy) : 0;
        var psyfocus = pawn.psychicEntropy?.CurrentPsyfocus ?? 0;
        var text = chosen is null ? decision.Why : $"{chosen.Psycast}: {chosen.Why} ({chosen.Utility:0.0})";
        Last[pawn.thingIDNumber] = new Entry(Find.TickManager.TicksGame, pawn.LabelShort, text, heat, psyfocus, decision.Candidates.Take(3).ToList());
        if (chosen is not null)
        {
            Chosen[pawn.thingIDNumber] = chosen.Psycast;
        }

        if (Debug && (chosen is not null || decision.Candidates.Count > 0))
        {
            // Dev Mode drawing may use the Unity stub types it needs (decided 2026-11-22, Q2; ENGINE_NOTES).
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"{text}\nheat {heat:P0} focus {psyfocus:P0}", 6f);
        }
    }

    /// <summary>A crisis unit started casting <paramref name="psycast"/>: it must be the one its scorers chose.</summary>
    public static void Cast(Pawn pawn, string psycast)
    {
        if (Chosen.TryGetValue(pawn.thingIDNumber, out var chosen) && chosen == psycast)
        {
            Chosen.Remove(pawn.thingIDNumber);
            ChosenCasts++;
            return;
        }

        var line = $"tick {Find.TickManager.TicksGame}: {pawn.LabelShort} cast {psycast}, which its scorers didn't choose";
        UnchosenCasts.Add(line);
        Log.Warning($"[Rim Chronicles] {line}");
    }
}
