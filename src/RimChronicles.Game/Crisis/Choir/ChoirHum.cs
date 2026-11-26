using RimChronicles.Core.Choir;
using RimChronicles.Core.Crisis.Pieces;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Crisis.Choir;

/// <summary>
/// The Hum in game (War in Heaven W7, tech spec §4.3): resonators the Choir drops near the colony. While one stands
/// (and for at most two days), every colonist in its reach who can feel it carries the hum: slower at work and worse in
/// mood, more so the more psychically sensitive. Destroying the resonators ends it. Rules in Core's <see cref="HumRules"/>.
/// </summary>
public static class ChoirHum
{
    public static ThingDef? ResonatorDef => DefDatabase<ThingDef>.GetNamedSilentFail("RimChronicles_ChoirResonator");

    public static HediffDef? HumDef => DefDatabase<HediffDef>.GetNamedSilentFail("RimChronicles_Hum");

    /// <summary>Drops a resonator at <paramref name="cell"/> in a drop pod, as vanilla drops a mech cluster's buildings.</summary>
    public static void Drop(ThingDef def, IntVec3 cell, Map map, Faction faction)
    {
        var resonator = ThingMaker.MakeThing(def);
        resonator.SetFactionDirect(faction);
        var info = new ActiveTransporterInfo { openDelay = 60, leaveSlag = false, despawnPodBeforeSpawningThing = true, moveItemsAsideBeforeSpawning = true };
        info.innerContainer.TryAdd(resonator, 1);
        info.spawnWipeMode = WipeMode.VanishOrMoveAside;
        DropPodUtility.MakeDropPodAt(cell, map, info, faction);
    }

    /// <summary>Gives <paramref name="pawn"/> the hum at a resonator's strength, or refreshes it.</summary>
    public static void Apply(Pawn pawn, float severity)
    {
        if (HumDef is not { } def || severity <= 0)
        {
            return;
        }

        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) ?? pawn.health.AddHediff(def);
        hediff.Severity = Math.Max(hediff.Severity, severity);
        if (hediff.TryGetComp<HediffComp_Disappears>() is { } disappears)
        {
            disappears.ticksToDisappear = HumRules.EffectTicks;
        }
    }
}

public sealed class CompProperties_RcResonator : CompProperties
{
    public CompProperties_RcResonator() => compClass = typeof(CompRcResonator);
}

/// <summary>A Choir resonator: hums over the colony while it stands, for at most <see cref="HumRules.MaxTicks"/>.</summary>
public sealed class CompRcResonator : ThingComp
{
    private int _since = -1;

    public bool Silent => _since >= 0 && Find.TickManager.TicksGame - _since >= HumRules.MaxTicks;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (_since < 0)
        {
            _since = Find.TickManager.TicksGame;
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        if (!parent.Spawned || !parent.IsHashIntervalTick(HumRules.RefreshTicks) || Silent || parent.Faction is not { } faction || !faction.HostileTo(Faction.OfPlayer))
        {
            return;
        }

        var at = new Cell(parent.Position.x, parent.Position.z);
        foreach (var pawn in parent.Map.mapPawns.FreeColonistsSpawned.ToList())
        {
            if (HumRules.Reaches(at, new Cell(pawn.Position.x, pawn.Position.z)))
            {
                ChoirHum.Apply(pawn, HumRules.Severity(pawn.GetStatValue(StatDefOf.PsychicSensitivity)));
            }
        }
    }

    public override string CompInspectStringExtra() =>
        Silent ? "Silent: its hum has died away." : "Humming: colonists within its reach who can feel it work slower and feel worse. Destroy it to end the hum.";

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _since, "humSince", -1);
    }
}
