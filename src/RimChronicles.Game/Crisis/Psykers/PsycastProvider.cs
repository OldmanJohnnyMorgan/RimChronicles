using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Psykers;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimChronicles.Game.Crisis.Psykers;

/// <summary>
/// Where psycasts come from (tech spec §4.3): lists what a pawn can cast right now and turns a scored cast into a job.
/// Iteration 1 has <see cref="VanillaProvider"/> (Royalty psycasts); a later <c>VpeProvider</c> implements the same
/// interface, so the psyker AI doesn't change (crisis spec §12).
/// </summary>
public interface IPsycastProvider
{
    IReadOnlyList<CastOption> Options(Pawn pawn);

    /// <summary>The casting job for <paramref name="cast"/>, or null if the game won't allow it now.</summary>
    Job? JobFor(Pawn pawn, ScoredCast cast, IReadOnlyDictionary<int, Thing> things);
}

/// <summary>Royalty's psycasts through the vanilla ability system. Psykers obey heat and psyfocus: a psycast is only
/// offered when <c>Psycast.GizmoDisabled</c> lets the pawn cast it (psychic sensitivity, psylink level, psyfocus and its
/// band, heat, cooldown). <c>CanCast</c> alone misses psyfocus for psycasts that make heat (C0, R20).</summary>
public sealed class VanillaProvider : IPsycastProvider
{
    public static readonly VanillaProvider Instance = new();

    public IReadOnlyList<CastOption> Options(Pawn pawn)
    {
        if (pawn.abilities is null || pawn.psychicEntropy is not { } entropy)
        {
            return [];
        }

        var options = new List<CastOption>();
        foreach (var ability in pawn.abilities.abilities.OfType<Psycast>())
        {
            if (ability.GizmoDisabled(out _) || !ability.CanCast)
            {
                continue;
            }

            var max = Math.Max(1f, entropy.MaxEntropy);
            options.Add(new CastOption
            {
                Def = ability.def.defName,
                Range = ability.verb?.verbProps.range ?? 0,
                Radius = ability.def.EffectRadius,
                DestRange = ability.CompOfType<CompAbilityEffect_WithDest>()?.Props.range ?? 0,
                HeatAfter = (entropy.EntropyValue + ability.def.EntropyGain) / max,
                PsyfocusCost = ability.def.PsyfocusCost,
                Psychic = ability.EffectComps.Any(c => c.Props.psychic),
            });
        }

        return options;
    }

    public Job? JobFor(Pawn pawn, ScoredCast cast, IReadOnlyDictionary<int, Thing> things)
    {
        if (DefDatabase<AbilityDef>.GetNamedSilentFail(cast.Psycast) is not { } def || pawn.abilities.GetAbility(def) is not Psycast ability
            || ability.GizmoDisabled(out _))
        {
            return null;
        }

        var map = pawn.Map;
        LocalTargetInfo target = cast.Target.UnitId is { } id
            ? things.TryGetValue(id, out var thing) && thing.Spawned ? thing : LocalTargetInfo.Invalid
            : new IntVec3(cast.Target.Cell.X, 0, cast.Target.Cell.Z);
        if (!target.IsValid || !target.Cell.InBounds(map) || !ability.CanApplyOn(target) || !ability.verb.CanHitTarget(target))
        {
            return null;
        }

        var dest = LocalTargetInfo.Invalid;
        if (cast.Target.Dest is { } d)
        {
            var cell = new IntVec3(d.X, 0, d.Z);
            var comp = ability.CompOfType<CompAbilityEffect_WithDest>();
            // Vanilla measures a skip's destination from the target, with a line of sight from it (CompAbilityEffect_WithDest).
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstPawn(map) is not null
                || (comp is not null && comp.Props.range > 0 && cell.DistanceTo(target.Cell) > comp.Props.range)
                || (comp is not null && comp.Props.requiresLineOfSight && !GenSight.LineOfSight(target.Cell, cell, map)))
            {
                return null;
            }

            dest = cell;
        }

        return ability.GetJob(target, dest);
    }
}
