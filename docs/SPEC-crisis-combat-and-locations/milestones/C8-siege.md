# C8: siege planner

Crisis milestone C8 (project plan §2). Built 2026-11-23; not yet checked in game (checkpoint 2).

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Siege/SpotScorer.cs` | `SpotFeatures` (what the game measures about a candidate camp spot), `SpotWeights` (first guesses for C14), `SpotScorer` (rule-outs, score parts, ranking, which way the camp faces) |
| `src/RimChronicles.Core/Crisis/Siege/BuildStages.cs` | The five stages and what each unlocks; when to move on; `CampLayout` (eleven slots in the camp's own frame, turned to face the colony) |
| `src/RimChronicles.Core/Crisis/Siege/SiegeRules.cs` | Siege goals (`Goal:BreakWalls`, `Goal:Starve`, `Goal:KillLeader`) and the weighted pick; how many build; what happens when the builders fall; the beacon's waves |
| `src/RimChronicles.Core/Crisis/Operations/` | The siege's profile (travel → siege → assault, with a retreat from each) and its force mix |
| `src/RimChronicles.Game/Crisis/Siege/SiegeSpotSampler.cs` | Samples and measures candidate spots on the map; Dev Mode **Crisis: show siege scoring** |
| `src/RimChronicles.Game/Crisis/Siege/SiegeCamp.cs` | The camp: staged blueprints, supplies, the relay's link, the beacon's waves, teardown. Saved with the lord |
| `src/RimChronicles.Game/Crisis/Siege/LordToil_RcSiege.cs` | The dig-in phase: places stage 1, picks builders, defends the camp, moves stages on |
| `src/RimChronicles.Game/Crisis/Operations/` | `LordJob_RcOperation` runs the siege (travel and siege toils, the builders trigger); `OperationSpawner.Siege`; Dev Mode **Crisis: fire siege with goal** |
| `src/RimChronicles.Game/Crisis/Strikes/` | Strikes aim by the siege goal; the camp's uplink is a strike source |
| `Defs/ThingDefs/RimChronicles_Siege.xml` | The camp's own buildings: Imperial uplink, shield emitter, psychic relay, drop-pod beacon (Royalty only) |
| `Defs/HediffDefs/RimChronicles_Siege.xml` | `RimChronicles_RelayLink`: the relay's buff for nearby Imperial psykers |
| `Defs/PrefabDefs/RimChronicles_Pieces.xml` | Three new pieces: siege uplink (stage 3), shield and relay (stage 4), drop beacon (stage 5) |
| `tests/…/SiegeTests.cs` | Scoring, stages, layout at every facing, goals, builders, waves, the profile |

The crisis now offers **Operation:SiegeWithBombardment** from stage 3 (War), picked by weight until C11.

## How a siege goes

1. **The spot.** Candidates sit on rings 36 to 80 cells from the colony's centre, every 10°. A spot is ruled out if
   the colony can't be walked to from it, it's in the home area or roofed, the camp's own mortars couldn't reach the
   colony (vanilla's mortar won't fire under 29.9 cells), any colony turret reaches the camp's edge (3 cells to spare),
   or less than 60% of its ground takes heavy buildings. The rest score on being close, cover on the line in, ground,
   facing a door the last probe found, open flanks (a penalty) and colony mortars that reach it (a penalty). The best
   wins; with none, the siege can't start and the crisis picks something else another day.
2. **The march.** The force walks in from the map edge nearest the spot. If not everyone has arrived after about two
   hours (5,000 ticks), the camp starts anyway and the stragglers join it.
3. **The camp, in stages.** Each stage is placed as the Empire's blueprints only when nothing of the stage before is
   left to build (built, or wrecked by the colony). Materials, mortar shells and survival meals arrive by drop pod.

   | Stage | Pieces | Unlocks |
   | --- | --- | --- |
   | 1 | Five sandbag lines (front and flanks), a barracks | The camp |
   | 2 | Two mortar pits behind the line, open to the rear | Shelling: the builders man the mortars |
   | 3 | Uplink behind a sandbag row | Orbital strikes with no Herald in sight |
   | 4 | Shield emitter and psychic relay | Shells can't fall in the camp; Imperial psykers within 30 cells shed heat faster and cast harder |
   | 5 | Drop-pod beacon | Up to three waves of reinforcements, a quarter day apart, from the 30% reserve |

   A message says when each stage stands, and only if what it unlocks was actually built.
4. **The end.** The siege attacks when more than half of its builders are down while it has lost less than half its
   force, or after two days. It leaves after losing half its force (65% once it's attacking), or when the crisis ends.
   On leaving, unbuilt blueprints and frames are cancelled and the camp is abandoned (no faction), as vanilla's siege
   leaves its camp: an abandoned uplink calls no strikes.

**Goal.** Picked by weight (BreakWalls 2, Starve 1, KillLeader 1) among those the colony gives a target for. It adds
targets to the strike candidates and weights them (C5's `StrikeRules.Score`): the colony walls nearest the camp; each
field and each room of food; the leader. The letter says what the strikes will go for.

## Decisions

- **One lord job for every operation.** The tech spec named a `LordJob_RcSiege`; the siege is instead a profile run by
  C6's `LordJob_RcOperation` with a `SiegeCamp` on it. Focus fire, pulling out, the retreat with carried downed and the
  outcome bookkeeping are the same code, and the retreat rule (fairness 6) is tested on the profile like the others.
- **Builders are picked once and not replaced.** Vanilla's siege refills its builders as they die, so "kill the
  builders" never ends it. Here, losing more than half of the first builders is what breaks the siege; reinforcements
  join as defenders. Praetorians and Breachers build; psykers and Heralds stay free to fight.
- **Losses are checked before the builders.** A siege that has lost half its force leaves rather than attacking, even
  when its builders fall in the same moment (the profile's link order; tested).
- **The camp's buildings are ours, with vanilla graphics.** No vanilla building is a buildable uplink, shield or beacon
  (the mech shields have no blueprint). Ours set a build-menu category so vanilla makes their blueprints and frames,
  and `canGenerateDefaultDesignator: false` keeps them out of the player's menu. Graphics: the comms console, the mech
  high-shield, the psychic droner (drawn 2×2) and the mech drop beacon.
- **The shield stops shells, not bullets.** A bullet shield over the camp would make it nearly untouchable; a mortar
  shield answers the colony's mortars, which is the usual way to break a vanilla siege, and leaves the assault on the
  camp as the counter. EMP knocks it out for 18 seconds.
- **The camp's mortars need range, not distance.** Vanilla mortars reach 500 cells, so "inside the Empire's mortar
  range" is really "beyond the mortars' 29.9-cell minimum": the camp keeps 36 cells from the colony's centre.
- **Colony mortars are a penalty, not a rule-out.** They usually reach the whole map; a spot inside their minimum range
  is the only place they can't hit, and that's always inside turret range anyway.

## Dev Mode

- **Crisis: show siege scoring:** draws every candidate on the current map (brighter is better, `x` ruled out, `BEST`),
  the colony's centre and the doors a probe found, and lists the five best with their score parts and why the rest
  were ruled out.
- **Crisis: fire operation** → SiegeWithBombardment → a stage: a siege with a goal picked by weight.
- **Crisis: fire siege with goal** → a goal → a stage.
- **Crisis: strike state** now shows whether an uplink stands and the siege goal, and picks as the goal would.

## In-game checks (for checkpoint 2)

See `crisis-runbook-2.md`, part C8.
