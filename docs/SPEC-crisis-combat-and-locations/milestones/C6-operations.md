# C6: operations I (probing raid, decapitation)

Crisis milestone C6 (project plan §2). Built 2026-11-22; not yet checked in game (checkpoint 2).

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Operations/OperationProfile.cs` | Each operation's phases and what moves it between them (losses, time, target down, crisis over), with the outcome each ending counts as; the shared "standard" shape (assault, regroup once, second assault) and the final assault's |
| `src/RimChronicles.Core/Crisis/Operations/ForceMix.cs` | Each operation's unit mix, and the force for a budget |
| `src/RimChronicles.Core/Crisis/Operations/Tactics.cs` | Focus target (medics, then the easiest to bring down), the drop-pod rule, when it's safe to carry a downed ally |
| `src/RimChronicles.Game/Crisis/Operations/OperationLord.cs` | `LordJob_RcOperation` (its state graph built from the profile), `LordToil_RcAssault`, `LordToil_RcRetreat`, `JobGiver_RcFocusFire` |
| `src/RimChronicles.Game/Crisis/Operations/OperationSpawner.cs` | The probing raid and the decapitation, registered with the crisis; Dev Mode **Crisis: fire operation** |
| `Defs/DutyDefs/RimChronicles_Duties.xml` | `RimChronicles_Assault`: vanilla's assault duty with focus fire |
| `tests/…/OperationTests.cs` | Retreat paths for every operation, outcomes, force mixes, drop spots, focus fire, carrying, the probe's report |

The crisis now offers **Operation:ProbingRaid** from stage 1 and **Operation:Decapitation** from stage 3, picked by weight
(the director gets them in C11).

## The two operations

**Probing raid.** Half the budget walks in from the map edge: Praetorians at stage 1, with a Breacher and a Ward
Adept from stage 2 and a Herald from stage 3. It fights for about two in-game hours (5,000 ticks), or until it has lost
a quarter, then leaves. As it leaves it reports the colony's doors, nearest first from where it came in
(`CrisisState.Entrances`, up to six; a later probe replaces them). The siege planner uses them in C8. Lasting the two
hours counts as success, since it got its look.

**Decapitation.** 80% of the budget drops in pods 3–12 cells from the colony's leader, on open sky only: the
ideoligion's leader, then a plot's protagonist, then the colonist with the highest Imperial title, then the most
valuable colonist. Praetorians and Skip, Ward and Control Adepts, about one adept per Praetorian. The squad focuses
the target. Once the target is down, dead or off the map, it leaves (success); after half its force is lost, or about
four hours, it leaves too.

## Shared tactics

- **Focus fire:** the whole squad shoots the lord's focus when it can: the hunted pawn, else a colonist tending the
  wounded, else the one easiest to bring down (lowest health, most pain) nearest the squad. Otherwise vanilla's
  targeting.
- **Protect key units:** psykers and Heralds take vanilla's escort duty on a Praetorian (5 cells), so they move and
  fight behind the line; they pick a new guard if theirs goes down.
- **Retreat and regroup:** every operation except the final assault leaves after losses. The standard shape used by
  later operations regroups once at its staging point after 40% losses, and comes back with a 25% reserve that drops
  in by pod.
- **Carry off the downed:** while retreating, a standing soldier within 30 cells carries a downed crisis unit off the
  map, if no colonist is within 10 cells of it (vanilla's `CarryDownedPawnToExit`, which doesn't mark them kidnapped).
- **Pulling out:** every raid leaves if the crisis ends or the colony makes peace (fairness rule 6 and R5), except one
  fired from Dev Mode.

## Decisions

- **The lord's state graph is built from Core's profile.** The retreat rules (fairness rule 6) are then tested on the
  same data the game runs: `RetreatsOnLosses` walks every phase and finds a loss path to a retreat, for every
  operation except the final assault.
- **Drop pods never land under any roof**, not only under mountain: stricter than fairness rule 3, and simpler for the
  player to read. The pods use `canRoofPunch: false`.
- **Psykers escort Praetorians, not the other way round.** It keeps them behind the line without a custom formation.
  This is the "PsykerSupport" positioning that C4 left for this milestone.
- **The probe's report is the colony's doors.** Gaps in walls aren't counted yet; C8 can add them if sieges need them.

## Dev Mode

**Crisis: fire operation** → ProbingRaid or Decapitation → a stage: fires it on the current map at that stage's budget,
recorded in the crisis history. **Crisis: show state** then shows it running, and how it ended.

## In-game checks (for checkpoint 2)

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: fire operation** → ProbingRaid → stage 1. | "Imperial probe" letter; Praetorians walk in and attack. |
| 2 | Let it fight two in-game hours, or kill a quarter of it. | It leaves; **Crisis: show state** shows ProbingRaid succeeded (time) or repelled (losses), and the doors it found are kept. |
| 3 | Down a probe soldier far from your colonists during the retreat. | Another soldier carries them off the map. Down one next to your colonists: it's left behind. |
| 4 | **Crisis: fire operation** → Decapitation → stage 3, with the leader outdoors. | "Imperial drop pods" letter naming them; pods land on open ground 3–12 cells away; the squad goes for them. |
| 5 | Put the leader indoors under a mountain roof, then fire it. | Pods land outside, never inside or under the mountain. |
| 6 | Watch a stage 3 fight with psykers. | Adepts stay near Praetorians; the squad's shots go to one colonist at a time (medics first). |
| 7 | Down the leader, or make peace with the Empire mid-raid. | The squad pulls out. |
| 8 | Save mid-operation and reload. | It carries on; no errors. |
