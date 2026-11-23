using RimChronicles.Core.Crisis.Siege;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Game.Crisis.Operations;
using RimChronicles.Game.Crisis.Units;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimChronicles.Game.Crisis.Siege;

/// <summary>
/// The siege's dig-in phase (crisis C8): on arrival it places the camp's first stage, picks its builders once (they
/// build, then man the mortars), and keeps everyone else defending the camp. Every 500 ticks the camp moves on to its
/// next stage when the current one is done. Builders who fall aren't replaced: losing them is what breaks the siege
/// (<see cref="SiegeRules.AfterBuildersFall"/>).
/// </summary>
public sealed class LordToil_RcSiege : LordToil
{
    private const int TickEvery = 500;

    private SiegeCamp? Camp => (lord.LordJob as LordJob_RcOperation)?.siege;

    public override IntVec3 FlagLoc => Camp?.spot ?? IntVec3.Invalid;

    public override bool ForceHighStoryDanger => true;

    public override bool AllowSatisfyLongNeeds => true;

    public override void Init()
    {
        base.Init();
        if (Camp is not { } camp || camp.Started)
        {
            return;
        }

        camp.stage = BuildStages.First;
        camp.PlaceStage(lord, camp.stage);
        var meals = new List<Thing>();
        for (var i = 0; i < lord.ownedPawns.Count; i++)
        {
            meals.Add(ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack));
        }

        DropPodUtility.DropThingsNear(camp.spot, Map, meals, canRoofPunch: false, allowFogged: true);
    }

    public override void UpdateAllDuties()
    {
        if (Camp is not { } camp)
        {
            return;
        }

        if (camp.startBuilders == 0)
        {
            ChooseBuilders(camp);
        }

        var radius = CampLayout.Radius + 3f;
        foreach (var pawn in lord.ownedPawns)
        {
            if (pawn.mindState is null)
            {
                continue;
            }

            if (camp.builders.Contains(pawn))
            {
                SetAsBuilder(pawn, camp.spot, radius);
            }
            else
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, camp.spot, radius);
            }
        }
    }

    /// <summary>Picks the builders from those who can build, Praetorians and Breachers first: psykers and Heralds
    /// stay free to fight.</summary>
    private void ChooseBuilders(SiegeCamp camp)
    {
        var can = lord.ownedPawns
            .Where(p => !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Firefighter))
            .OrderBy(p => UnitKit.Unit(p.kindDef)?.role is UnitRole.Praetorian or UnitRole.Breacher ? 0 : 1)
            .ThenBy(p => p.thingIDNumber)
            .ToList();
        var mortars = CampLayout.Slots.Count(s => s.Tag == "Artillery");
        var count = SiegeRules.Builders(lord.ownedPawns.Count, can.Count, mortars);
        camp.builders = can.Take(count).ToList();
        camp.startBuilders = camp.builders.Count;
    }

    /// <summary>As <c>LordToil_Siege.SetAsBuilder</c>: the Build duty (build, fight fires, man mortars), with enough
    /// skill for every piece of the camp and only construction work on.</summary>
    private static void SetAsBuilder(Pawn pawn, IntVec3 spot, float radius)
    {
        pawn.mindState.duty = new PawnDuty(DutyDefOf.Build, spot, radius);
        var needed = new[] { ThingDefOf.Sandbags, ThingDefOf.Turret_Mortar, SiegeDefs.Uplink, SiegeDefs.Shield, SiegeDefs.Relay, SiegeDefs.Beacon }
            .OfType<ThingDef>().Max(d => d.constructionSkillPrerequisite);
        pawn.skills?.GetSkill(SkillDefOf.Construction).EnsureMinLevelWithMargin(needed);
        if (pawn.workSettings is null)
        {
            return;
        }

        pawn.workSettings.EnableAndInitialize();
        foreach (var work in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if (work == WorkTypeDefOf.Construction)
            {
                pawn.workSettings.SetPriority(work, 1);
            }
            else
            {
                pawn.workSettings.Disable(work);
            }
        }
    }

    public override void Notify_PawnLost(Pawn victim, PawnLostCondition cond)
    {
        base.Notify_PawnLost(victim, cond);
        UpdateAllDuties();
    }

    public override void Notify_ConstructionFailed(Pawn pawn, Frame frame, Blueprint_Build newBlueprint)
    {
        base.Notify_ConstructionFailed(pawn, frame, newBlueprint);
        if (frame.Faction == lord.faction && newBlueprint is not null && Camp is { } camp)
        {
            camp.blueprints.Add(newBlueprint);
        }
    }

    public override void LordToilTick()
    {
        base.LordToilTick();
        if (lord.ticksInToil % TickEvery != 0 || Camp is not { } camp)
        {
            return;
        }

        camp.SiegeTick(lord);
        UpdateAllDuties(); // reinforcements from the beacon join as defenders
    }
}
