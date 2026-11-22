using RimChronicles.Core.Crisis.Battle;

namespace RimChronicles.Core.Crisis.Psykers;

/// <summary>A psyker's decision this round: the cast, or none (hold), with the candidates it weighed for the debug view.</summary>
public sealed record PsykerDecision(ScoredCast? Cast, IReadOnlyList<ScoredCast> Candidates, string Why);

/// <summary>
/// Picks a psyker's cast (crisis spec §5.3): every castable psycast is scored by the scorers that handle it; heat
/// lowers a cast's worth as it nears the limit; the best one wins if it's worth it, otherwise the psyker holds and
/// fights on while its heat falls. Psykers obey neural heat and psyfocus: the game only offers casts they can afford.
/// </summary>
public static class PsykerBrain
{
    /// <summary>Below this, the psyker holds.</summary>
    public const float MinUtility = 1.5f;

    /// <summary>Heat (share of the limit) above which casting costs more and more.</summary>
    public const float HeatComfort = 0.6f;

    public const float HeatPenalty = 4f;

    /// <param name="scorersFor">The scorers that handle a psycast (from the <c>ScorerDef</c>s); none means it is never cast.</param>
    public static PsykerDecision Choose(BattleView view, BattleUnit caster, IReadOnlyList<CastOption> options,
        Func<string, IReadOnlyList<IPsycastScorer>> scorersFor, float psyfocus)
    {
        var candidates = new List<ScoredCast>();
        foreach (var cast in options.Where(o => o.HeatAfter < 1f && o.PsyfocusCost <= psyfocus + 0.0005f))
        {
            foreach (var scorer in scorersFor(cast.Def))
            {
                if (scorer.Score(view, caster, cast) is { } scored)
                {
                    var penalty = cast.HeatAfter > HeatComfort ? HeatPenalty * (cast.HeatAfter - HeatComfort) / (1 - HeatComfort) : 0;
                    candidates.Add(scored with { Utility = scored.Utility - penalty });
                }
            }
        }

        var ranked = candidates.OrderByDescending(c => c.Utility).ThenBy(c => c.Psycast, StringComparer.Ordinal).ToList();
        if (ranked.Count == 0)
        {
            return new PsykerDecision(null, ranked, options.Count == 0 ? "hold: nothing castable (heat, psyfocus or cooldown)" : "hold: no good target");
        }

        return ranked[0].Utility < MinUtility
            ? new PsykerDecision(null, ranked, $"hold: best was {ranked[0].Psycast} at {ranked[0].Utility:0.0}")
            : new PsykerDecision(ranked[0], ranked, ranked[0].Why);
    }
}
