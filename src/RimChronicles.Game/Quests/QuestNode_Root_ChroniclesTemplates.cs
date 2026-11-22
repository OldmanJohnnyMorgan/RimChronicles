using RimChronicles.Core.Plots;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RimChronicles.Game.Quests;

/// <summary>Shared pieces for the site-based templates: the site, the reward drop, and the end conditions.</summary>
public abstract class QuestNode_Root_ChroniclesSite : QuestNode_Root_Chronicles
{
    public const string RewardVar = "rcReward";

    /// <summary>A site 2–10 tiles from the colony, or 1–4 tiles from <paramref name="near"/> when given.</summary>
    protected static Site MakeSite(Quest quest, SitePartDef part, Faction faction, float points, PlanetTile? near = null)
    {
        var found = near is { } root ? TileFinder.TryFindNewSiteTile(out var tile, root, 1, 4) : TileFinder.TryFindNewSiteTile(out tile, 2, 10);
        if (!found)
        {
            throw new InvalidOperationException("No free tile for a quest site.");
        }

        var site = QuestGen_Sites.GenerateSite([new SitePartDefWithParams(part, new SitePartParams { threatPoints = points })], tile, faction);
        quest.SpawnWorldObject(site); // placed on the world map when the quest is accepted, as in vanilla
        quest.AddPart(new QuestPart_ChroniclesSiteMarker { tile = tile });
        var slate = QuestGen.slate;
        slate.Set("resolvedQuestDescription", slate.Get(DescriptionVar, "")
            + "\n\nThe camp appears on the world map once you accept; until then, the location points to where it will be.");
        QuestUtility.AddQuestTag(ref site.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID("site"));
        return site;
    }

    /// <summary>On <paramref name="successSignal"/>: drop the reward silver at home and end in success. On a timeout, the
    /// site's removal or any of <paramref name="failSignals"/>: fail.</summary>
    protected static void Outcomes(Quest quest, Slate slate, Site? site, string successSignal, params string[] failSignals)
    {
        var reward = slate.Get(RewardVar, 0);
        if (reward > 0 && slate.Get<Map>("map") is { } home)
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = reward;
            quest.DropPods(home.Parent, [silver], inSignal: successSignal);
        }

        quest.End(QuestEndOutcome.Success, 0, null, successSignal);
        foreach (var fail in failSignals)
        {
            quest.End(QuestEndOutcome.Fail, 0, null, fail);
        }

        var timeout = QuestTemplates.SiteDays * GenDate.TicksPerDay;
        if (site is not null)
        {
            quest.WorldObjectTimeout(site, timeout);
            quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("site.MapRemoved"));
        }

        quest.Delay(timeout, () => QuestGen_End.End(quest, QuestEndOutcome.Fail));
    }

    protected static string Tag(Pawn pawn, string name)
    {
        QuestUtility.AddQuestTag(ref pawn.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID(name));
        return name;
    }

    protected static string Signal(string name) => QuestGenUtility.HardcodedSignalWithQuestID(name);
}

/// <summary>Hunt target: a camp of the target's faction appears nearby, with the target among its defenders. Killing or
/// capturing the target succeeds; letting the camp vanish, leaving it, or the target escaping fails.</summary>
public sealed class QuestNode_Root_ChroniclesHunt : QuestNode_Root_ChroniclesSite
{
    public const string TargetVar = "rcTarget";

    protected override void Build(Quest quest, Slate slate)
    {
        var target = slate.Get<Pawn>(TargetVar) ?? throw new InvalidOperationException("Hunt needs a target pawn.");
        var faction = target.Faction ?? Find.FactionManager.RandomEnemyFaction();
        var site = MakeSite(quest, SitePartDefOf.BanditCamp, faction, slate.Get("points", 300f));
        Tag(target, "target");
        quest.AddPart(new QuestPart_ChroniclesSpawnPawn
        {
            inSignal = Signal("site.MapGenerated"), pawn = target, mapParent = site, joinDefenders = true,
        });

        var success = Signal("huntSucceeded");
        quest.SignalPass(null, Signal("target.Killed"), success);
        quest.SignalPass(null, Signal("target.Arrested"), success);
        Outcomes(quest, slate, site, success, Signal("target.LeftMap"));
    }
}

/// <summary>Rescue: the captor faction holds the captive at a camp nearby. Freeing them (a colonist offers help, and
/// they join the colony) succeeds; the captive dying, or the camp vanishing, fails. Uses vanilla's prisoner site part
/// when it exists, which locks the captive in a cell.</summary>
public sealed class QuestNode_Root_ChroniclesRescue : QuestNode_Root_ChroniclesSite
{
    public const string CaptiveVar = "rcCaptive";
    public const string CaptorVar = "rcCaptor";

    protected override void Build(Quest quest, Slate slate)
    {
        var captive = slate.Get<Pawn>(CaptiveVar) ?? throw new InvalidOperationException("Rescue needs a captive pawn.");
        var captor = slate.Get<Faction>(CaptorVar) ?? Find.FactionManager.RandomEnemyFaction();
        var prisonPart = DefDatabase<SitePartDef>.GetNamedSilentFail("PrisonerWillingToJoin");
        var site = MakeSite(quest, prisonPart ?? SitePartDefOf.BanditCamp, captor, slate.Get("points", 300f));

        if (site.parts.FirstOrDefault(p => p.def == prisonPart) is { } part)
        {
            // Swap the random prisoner the site part generated for our captive; the map's gen step spawns it.
            // Vanilla may reuse an existing world pawn here, with relations to colonists, so pass it back to the
            // world rather than destroying it: a destroyed pawn left colonists' relations pointing at nothing.
            part.things ??= new ThingOwner<Pawn>(part, oneStackOnly: true);
            part.things.ClearAndDestroyContentsOrPassToWorld();
            if (Find.WorldPawns.Contains(captive))
            {
                Find.WorldPawns.RemovePawn(captive);
            }

            part.things.TryAdd(captive);
        }
        else
        {
            quest.AddPart(new QuestPart_ChroniclesSpawnPawn
            {
                inSignal = Signal("site.MapGenerated"), pawn = captive, mapParent = site, willJoinIfRescued = true,
            });
        }

        Tag(captive, "captive");
        var success = Signal("rescueSucceeded");
        quest.SignalPass(null, Signal("captive.Recruited"), success);
        quest.SignalPass(null, Signal("captive.ChangedFactionToPlayer"), success);
        Outcomes(quest, slate, site, success, Signal("captive.Killed"), Signal("captive.Destroyed"));
    }
}

/// <summary>Deliver: a settlement asks for items, brought by caravan within the deadline (vanilla's trade request).
/// Fulfilling it succeeds; running out of time fails.</summary>
public sealed class QuestNode_Root_ChroniclesDeliver : QuestNode_Root_ChroniclesSite
{
    public const string SettlementVar = "rcSettlement";
    public const string ThingVar = "rcThing";
    public const string CountVar = "rcCount";

    protected override void Build(Quest quest, Slate slate)
    {
        var settlement = slate.Get<Settlement>(SettlementVar) ?? throw new InvalidOperationException("Deliver needs a settlement.");
        var thing = slate.Get<ThingDef>(ThingVar) ?? throw new InvalidOperationException("Deliver needs an item.");
        var count = Math.Max(1, slate.Get(CountVar, 1));
        QuestUtility.AddQuestTag(ref settlement.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID("settlement"));
        quest.AddPart(new QuestPart_InitiateTradeRequest
        {
            inSignal = quest.InitiateSignal,
            settlement = settlement,
            requestedThingDef = thing,
            requestedCount = count,
            requestDuration = QuestTemplates.DeliverDays * GenDate.TicksPerDay,
        });

        var success = Signal("settlement.TradeRequestFulfilled");
        var reward = slate.Get(RewardVar, 0);
        if (reward > 0 && slate.Get<Map>("map") is { } home)
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = reward;
            quest.DropPods(home.Parent, [silver], inSignal: success);
        }

        quest.End(QuestEndOutcome.Success, 0, null, success);
        quest.Delay(QuestTemplates.DeliverDays * GenDate.TicksPerDay, () => QuestGen_End.End(quest, QuestEndOutcome.Fail));
    }
}

/// <summary>Escort: members of a friendly faction join the colony for the journey, and a caravan must bring them to a
/// destination settlement within the deadline. Arriving there succeeds, and they go home. One of them dying, or the
/// deadline, fails; either way they leave the colony when the quest ends.</summary>
public sealed class QuestNode_Root_ChroniclesEscort : QuestNode_Root_ChroniclesSite
{
    public const string FactionVar = "rcEscortFaction";
    public const string DestinationVar = "rcDestination";
    public const string CountVar = "rcCount";

    protected override void Build(Quest quest, Slate slate)
    {
        var faction = slate.Get<Faction>(FactionVar) ?? throw new InvalidOperationException("Escort needs a faction.");
        var destination = slate.Get<Settlement>(DestinationVar) ?? throw new InvalidOperationException("Escort needs a destination.");
        var home = slate.Get<Map>("map") ?? throw new InvalidOperationException("Escort needs a home map.");
        var kind = faction.def.basicMemberKind ?? faction.RandomPawnKind();
        var pawns = Enumerable.Range(0, Math.Clamp(slate.Get(CountVar, 1), 1, 3))
            .Select(_ => quest.GeneratePawn_NewTemp(kind, faction, home.Tile, allowAddictions: false, forceGenerateNewPawn: true))
            .ToList();
        foreach (var pawn in pawns)
        {
            Tag(pawn, "escortee");
        }

        QuestUtility.AddQuestTag(ref destination.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID("destination"));
        quest.ExtraFaction(faction, pawns, ExtraFactionType.HomeFaction);
        quest.PawnsArrive(pawns, null, home.Parent, joinPlayer: true, sendStandardLetter: false);

        var success = Signal("escortArrived");
        quest.AddPart(new QuestPart_ChroniclesEscortArrival
        {
            inSignalEnable = quest.InitiateSignal, pawns = pawns, destination = destination, outSignalsCompleted = [success],
        });
        quest.Leave(pawns, success, sendStandardLetter: false, leaveOnCleanup: true);

        var reward = slate.Get(RewardVar, 0);
        if (reward > 0)
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = reward;
            quest.DropPods(home.Parent, [silver], inSignal: success);
        }

        quest.End(QuestEndOutcome.Success, 0, null, success);
        quest.End(QuestEndOutcome.Fail, 0, null, Signal("escortee.Killed"));
        quest.End(QuestEndOutcome.Fail, 0, null, Signal("destination.Destroyed"));
        quest.Delay(QuestTemplates.EscortDays * GenDate.TicksPerDay, () => QuestGen_End.End(quest, QuestEndOutcome.Fail));
    }
}

/// <summary>Defend ally: an attacker's siege camp appears beside an allied settlement. Clearing it succeeds; the camp
/// lasting until it vanishes fails.</summary>
public sealed class QuestNode_Root_ChroniclesDefendAlly : QuestNode_Root_ChroniclesSite
{
    public const string AllyVar = "rcAlly";
    public const string AttackerVar = "rcAttacker";

    protected override void Build(Quest quest, Slate slate)
    {
        var ally = slate.Get<Settlement>(AllyVar) ?? throw new InvalidOperationException("Defend ally needs a settlement.");
        var attacker = slate.Get<Faction>(AttackerVar) ?? throw new InvalidOperationException("Defend ally needs an attacker.");
        var site = MakeSite(quest, SitePartDefOf.BanditCamp, attacker, slate.Get("points", 300f), ally.Tile);
        QuestUtility.AddQuestTag(ref ally.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID("ally"));
        var success = Signal("site.AllEnemiesDefeated");
        Outcomes(quest, slate, site, success, Signal("ally.Destroyed"));
    }
}

/// <summary>Sabotage: an objective building stands inside an enemy settlement. Destroying it (or the whole settlement)
/// within the deadline succeeds. The building is placed whenever the settlement's map is generated, so leaving and
/// coming back finds a new one.</summary>
public sealed class QuestNode_Root_ChroniclesSabotage : QuestNode_Root_ChroniclesSite
{
    public const string SettlementVar = "rcTargetSettlement";
    public const string ObjectiveVar = "rcObjective";

    /// <summary>True for the Imperial capital: no objective building, only destroying the settlement succeeds.</summary>
    public const string NoObjectiveVar = "rcNoObjective";

    /// <summary>The deadline in days; <see cref="QuestTemplates.SabotageDays"/> when unset.</summary>
    public const string DaysVar = "rcDays";

    protected override void Build(Quest quest, Slate slate)
    {
        var settlement = slate.Get<Settlement>(SettlementVar) ?? throw new InvalidOperationException("Sabotage needs a settlement.");
        QuestUtility.AddQuestTag(ref settlement.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID("target"));
        if (!slate.Get(NoObjectiveVar, false))
        {
            quest.AddPart(new QuestPart_ChroniclesSpawnObjective
            {
                inSignal = Signal("target.MapGenerated"), mapParent = settlement, thingDef = slate.Get<ThingDef>(ObjectiveVar) ?? ThingDefOf.CommsConsole,
                tag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("objective"),
            });
        }

        var success = Signal("sabotageDone");
        quest.SignalPass(null, Signal("objective.Destroyed"), success);
        quest.SignalPass(null, Signal("target.Destroyed"), success);
        var reward = slate.Get(RewardVar, 0);
        if (reward > 0 && slate.Get<Map>("map") is { } home)
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = reward;
            quest.DropPods(home.Parent, [silver], inSignal: success);
        }

        quest.End(QuestEndOutcome.Success, 0, null, success);
        quest.Delay(Math.Max(1, slate.Get(DaysVar, QuestTemplates.SabotageDays)) * GenDate.TicksPerDay, () => QuestGen_End.End(quest, QuestEndOutcome.Fail));
    }
}
