using RimChronicles.Core.Crisis.Battle;
using RimChronicles.Core.Crisis.Pieces;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Psykers;

/// <summary>
/// Reads a fight into a <see cref="BattleView"/> (tech spec §4.3): the faction's pawns on the map, everyone hostile to
/// it, and the colony's turrets. One view per map and faction is kept for <see cref="RefreshTicks"/> ticks and shared by
/// every psyker there. Main thread only.
/// </summary>
public static class BattleViewBuilder
{
    public const int RefreshTicks = 250;

    private static readonly string[] Effects = [BattleEffect.Painblock, BattleEffect.Invisible, BattleEffect.Burden, BattleEffect.Vertigo, BattleEffect.Blind];

    private static readonly Dictionary<(int Map, int Faction), (int Tick, BattleView View, Dictionary<int, Thing> Things)> Cache = [];

    /// <summary>The view for <paramref name="side"/> on <paramref name="map"/>, and the things behind its unit ids.</summary>
    public static (BattleView View, IReadOnlyDictionary<int, Thing> Things) For(Map map, Faction side)
    {
        var now = Find.TickManager.TicksGame;
        var key = (map.uniqueID, side.loadID);
        if (Cache.TryGetValue(key, out var cached) && now - cached.Tick < RefreshTicks)
        {
            return (cached.View, cached.Things);
        }

        var (view, things) = Build(map, side);
        if (Cache.Count > 32)
        {
            Cache.Clear();
        }

        Cache[key] = (now, view, things);
        return (view, things);
    }

    private static (BattleView, Dictionary<int, Thing>) Build(Map map, Faction side)
    {
        var pawns = map.mapPawns.AllPawnsSpawned
            .Where(p => !p.Dead && (p.Faction == side || (p.HostileTo(side) && !p.IsPrisoner)) && (p.RaceProps.Humanlike || p.RaceProps.IsMechanoid || p.Faction == side))
            .ToList();
        var turrets = map.listerBuildings.allBuildingsColonist.OfType<Building_TurretGun>()
            .Where(t => t.HostileTo(side) && t.Active && !t.IsBrokenDown())
            .ToList();
        var enemies = pawns.Where(p => p.Faction != side && !p.Downed).Cast<Thing>().Concat(turrets).ToList();
        var shooters = enemies.Where(e => Range(e) > 2).ToList();
        var friends = pawns.Where(p => p.Faction == side && !p.Downed).ToList();

        var units = new List<BattleUnit>();
        var things = new Dictionary<int, Thing>();
        foreach (var pawn in pawns)
        {
            var ours = pawn.Faction == side;
            var foes = ours ? enemies : friends.Cast<Thing>().ToList();
            var role = UnitKit.Unit(pawn.kindDef)?.role;
            var effects = Effects.Where(e => pawn.health.hediffSet.hediffs.Any(h => h.def.defName == e)).ToHashSet();
            if (pawn.stances?.stunner?.Stunned == true)
            {
                effects.Add(BattleEffect.Stunned);
            }

            if (pawn.InMentalState)
            {
                effects.Add(BattleEffect.Mental);
            }

            var ranged = pawn.equipment?.Primary?.def.IsRangedWeapon == true;
            units.Add(new BattleUnit
            {
                Id = pawn.thingIDNumber,
                Side = ours ? BattleSide.Empire : BattleSide.Colony,
                Pos = new Cell(pawn.Position.x, pawn.Position.z),
                Name = pawn.LabelShort,
                Role = role,
                Downed = pawn.Downed,
                Health = pawn.health.summaryHealth.SummaryHealthPercent,
                Pain = pawn.health.hediffSet.PainTotal,
                Dps = Dps(pawn),
                Ranged = ranged,
                WeaponRange = Range(pawn),
                InCover = InCover(pawn.Position, map),
                UnderFire = ours && shooters.Any(s => s.Position.DistanceTo(pawn.Position) <= Range(s) && GenSight.LineOfSight(s.Position, pawn.Position, map)),
                Seen = !ours || foes.Any(f => f.Position.DistanceTo(pawn.Position) <= 40 && GenSight.LineOfSight(f.Position, pawn.Position, map)),
                MeleeEnemies = foes.OfType<Pawn>().Count(f => !f.Downed && f.Position.AdjacentTo8WayOrInside(pawn.Position)),
                Value = Value(role),
                PsychicSensitivity = pawn.GetStatValue(StatDefOf.PsychicSensitivity),
                Effects = effects,
            });
            things[pawn.thingIDNumber] = pawn;
        }

        foreach (var turret in turrets)
        {
            units.Add(new BattleUnit
            {
                Id = turret.thingIDNumber,
                Side = BattleSide.Colony,
                Pos = new Cell(turret.Position.x, turret.Position.z),
                Name = turret.LabelShort,
                IsTurret = true,
                Dps = Dps(turret),
                Ranged = true,
                WeaponRange = Range(turret),
                PsychicSensitivity = 0,
                Value = 1.5f,
            });
            things[turret.thingIDNumber] = turret;
        }

        var view = new BattleView(units,
            c => new IntVec3(c.X, 0, c.Z) is var v && v.InBounds(map) && v.Standable(map) && v.GetFirstPawn(map) is null && v.GetEdifice(map) is null,
            (a, b) => new IntVec3(a.X, 0, a.Z).InBounds(map) && new IntVec3(b.X, 0, b.Z).InBounds(map)
                && GenSight.LineOfSight(new IntVec3(a.X, 0, a.Z), new IntVec3(b.X, 0, b.Z), map));
        return (view, things);
    }

    private static float Value(UnitRole? role) => role switch
    {
        UnitRole.Stellarch => 3,
        UnitRole.Herald => 2,
        UnitRole.Adept => 1.5f,
        _ => 1,
    };

    private static bool InCover(IntVec3 cell, Map map) =>
        GenAdj.CellsAdjacent8Way(new TargetInfo(cell, map)).Any(c => c.InBounds(map) && c.GetCover(map) is { } cover && cover.def.fillPercent >= 0.4f);

    private static Verb? VerbOf(Thing thing) => thing switch
    {
        Building_TurretGun turret => turret.AttackVerb,
        Pawn pawn => pawn.equipment?.PrimaryEq?.PrimaryVerb,
        _ => null,
    };

    private static float Range(Thing thing) => VerbOf(thing) is { verbProps: { } props } && !props.IsMeleeAttack ? props.range : 1.5f;

    /// <summary>Rough damage per second: one burst's damage over its warmup, cooldown and burst time; melee DPS if no gun.</summary>
    private static float Dps(Thing thing)
    {
        if (VerbOf(thing) is { verbProps: { IsMeleeAttack: false, defaultProjectile.projectile: { } projectile } props } verb)
        {
            var weapon = verb.EquipmentSource ?? (thing as Building_TurretGun)?.gun;
            var burst = Math.Max(1, props.burstShotCount);
            var damage = weapon is null ? projectile.GetDamageAmount((Thing?)null) : projectile.GetDamageAmount(weapon);
            var cooldown = weapon?.GetStatValue(StatDefOf.RangedWeapon_Cooldown) ?? 1f;
            var cycle = props.warmupTime + cooldown + ((burst - 1) * props.ticksBetweenBurstShots / 60f);
            return cycle <= 0 ? 0 : damage * burst / cycle;
        }

        return thing is Pawn pawn ? pawn.GetStatValue(StatDefOf.MeleeDPS) : 0;
    }
}
