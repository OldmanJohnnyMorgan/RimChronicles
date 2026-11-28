using RimChronicles.Core.Powers;
using RimChronicles.Game.Quests;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RimChronicles.Game.Powers.Summons;

/// <summary>
/// The summons quest (War in Heaven W6), built like Royalty's <c>PawnLend</c> (ENGINE_NOTES, "War in Heaven survey"):
/// once accepted, the power's shuttle lands for the named colonist; when it leaves with them, <see cref="QuestPart_RcSummons"/>
/// holds them <see cref="SummonsBook.DaysAway"/> days and returns them converted. The shuttle leaving without them is a
/// refusal (<see cref="QuestPart_RcSummonsRefused"/>); so is declining the offer, which the driver reads from the quest.
/// </summary>
public sealed class QuestNode_Root_ChroniclesSummons : QuestNode_Root_Chronicles
{
    public const string PawnVar = "rcSummonsPawn";
    public const string FactionVar = "rcSummonsFaction";
    public const string PowerVar = "rcSummonsPower";

    protected override void Build(Quest quest, Slate slate)
    {
        var pawn = slate.Get<Pawn>(PawnVar) ?? throw new InvalidOperationException("A summons needs a pawn.");
        var faction = slate.Get<Faction>(FactionVar) ?? throw new InvalidOperationException("A summons needs a faction.");
        var power = slate.Get<string>(PowerVar) ?? "";
        var map = slate.Get<Map>("map") ?? throw new InvalidOperationException("A summons needs a home map.");

        var shuttle = QuestGen_Shuttle.GenerateShuttle(faction, [pawn], acceptColonists: true, onlyAcceptColonists: true,
            dropEverythingIfUnsatisfied: true, leaveImmediatelyWhenSatisfied: true);
        QuestUtility.AddQuestTag(ref shuttle.questTags, QuestGenUtility.HardcodedTargetQuestTagWithQuestID("rcShuttle"));
        var ship = quest.GenerateTransportShip(TransportShipDefOf.Ship_Shuttle, null, shuttle).transportShip;
        quest.SendTransportShipAwayOnCleanup(ship, unloadContents: true, TransportShipDropMode.All);
        quest.AddShipJob_Arrive(ship, map.Parent, null, null, ShipJobStartMode.Instant, faction);
        quest.AddShipJob_WaitTime(ship, GenDate.TicksPerDay, leaveImmediatelyWhenSatisfied: true);
        quest.AddShipJob_FlyAway(ship);

        var sent = QuestGenUtility.HardcodedSignalWithQuestID("rcShuttle.SentSatisfied");
        var returned = QuestGenUtility.HardcodedSignalWithQuestID("rcSummonsReturned");
        var died = QuestGenUtility.HardcodedSignalWithQuestID("rcSummonsDied");
        var part = new QuestPart_RcSummons
        {
            inSignalEnable = sent,
            shuttle = shuttle,
            lendColonistsToFaction = faction,
            returnLentColonistsInTicks = SummonsBook.DaysAway * GenDate.TicksPerDay,
            returnMap = map.Parent,
            outSignalColonistsDied = died,
            power = power,
        };
        part.outSignalsCompleted.Add(returned);
        quest.AddPart(part);

        var refused = QuestGenUtility.HardcodedSignalWithQuestID("rcShuttle.SentUnsatisfied");
        quest.AddPart(new QuestPart_RcSummonsRefused { inSignal = refused });
        quest.End(QuestEndOutcome.Success, 0, null, returned);
        quest.End(QuestEndOutcome.Fail, 0, null, refused);
        quest.End(QuestEndOutcome.Fail, 0, null, died);
        quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("rcShuttle.Destroyed"));
        quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("rcShuttle.LeftBehind"));
    }
}

/// <summary>Holds the summoned colonist and returns them converted (<see cref="SummonsConversion"/>), with a letter.</summary>
public sealed class QuestPart_RcSummons : QuestPart_LendColonistsToFaction
{
    /// <summary>The summoning power's id.</summary>
    public string power = "";

    protected override void Complete(SignalArgs signalArgs)
    {
        if (Power.All.FirstOrDefault(p => p.Id == power) is { } summoner)
        {
            foreach (var pawn in LentColonistsListForReading.OfType<Pawn>().Where(p => !p.Dead))
            {
                var became = SummonsConversion.Convert(pawn, summoner);
                SummonsDriver.Notify_Converted(pawn, summoner, became);
            }
        }

        base.Complete(signalArgs);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref power, "rcPower", "");
        power ??= "";
    }
}

/// <summary>The shuttle left without the summoned colonist: tells the driver, which sets the colony independent.</summary>
public sealed class QuestPart_RcSummonsRefused : QuestPart
{
    public string inSignal = "";

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            SummonsDriver.Notify_Refused(quest.id);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal", "");
        inSignal ??= "";
    }
}
