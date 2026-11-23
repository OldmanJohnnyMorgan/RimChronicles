# C9: location generator

Crisis milestone C9 (project plan §2). Built 2026-11-23; not yet checked in game (checkpoint 3).

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Sites/SiteArchetype.cs` | An archetype as plain data (size, objective tag, required and optional pieces, gates, budget range, capability), its `Site:*` id, and the weighted pick by stage |
| `src/RimChronicles.Core/Crisis/Sites/CellGrid.cs` | The plan's grid: what fills each cell, which piece owns it, conduits; flood fill and shortest path |
| `src/RimChronicles.Core/Crisis/Sites/SitePlanner.cs` | `SitePlanner` (partition, connect, fill, wrap, conduits, defender spots, the fixed fallback), `SitePlan` (with a text picture), `SiteValidator` |
| `src/RimChronicles.Core/Crisis/Operations/ForceMix.cs` | `ForSite`: each archetype's garrison |
| `src/RimChronicles.Game/Crisis/Sites/` | `SiteArchetypeDef`; `GenStep_RcSite` and `SiteStamper` (clears the ground, stamps the plan, spawns the garrison); `MapComponent_RcSite` (the objectives, and the capability when they fall); the quest root and objective tagging; `SiteOffers`; Dev Mode |
| `src/RimChronicles.Game/Empire/EmpireBeats.cs` | `SabotageImperial` offers an Imperial site while the crisis runs (tech spec R7), else the old settlement sabotage |
| `src/RimChronicles.Game/Crisis/Units/UnitKit.cs` | Psykers arriving while a relay site is down get **relay severed** |
| `Defs/SiteArchetypeDefs/`, `Defs/SitePartDefs/` | Forward Outpost, Psychic Relay, Orbital Uplink, and their site parts and gen steps (Royalty only) |
| `Defs/PrefabDefs/RimChronicles_Pieces.xml` | Five new pieces: command room, relay chamber, uplink station, power yard, armory |
| `Defs/HediffDefs/RimChronicles_Sites.xml` | `RimChronicles_RelaySevered` |
| `Defs/QuestScriptDefs/RimChronicles_Quests.xml` | `RimChronicles_ImperialSite` |
| `tests/…/SiteTests.cs`, `TestSupport/SiteXml.cs` | 50 seeds per archetype, the fallback, determinism, each step, the validators on hand-made plans, stages and garrisons |

## The archetypes

| Archetype | From stage | Grid | Objective | Required | Optional (weight, cap) | Gates | Budget | Destroying it |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Forward Outpost | 1 | 40×40 | comms console in a command room | barracks, power yard | mortar pit (1, 1), armory (1, 1) | 1 | 600–2,500 | a letter (no capability) |
| Psychic Relay | 2 | 52×52 | relay core in a walled chamber | barracks, power yard | turret nest (2, 3), mortar pit (1, 1), armory (1, 1), second barracks (1, 1) | 2 | 1,500–5,000 | Imperial psykers weakened for 15 days |
| Orbital Uplink | 2 | 52×52 | uplink console in a walled station | barracks, power yard | turret nest (3, 4), mortar pit (1, 2), armory (1, 1) | 2 | 1,500–5,000 | no orbital strikes for 15 days |

Pieces may take up to 40% of the budget; the garrison gets the rest. Garrisons: Praetorians with a Breacher and, from
stage 3, a Herald (outpost); with Control, Ward and Mind Adepts (relay); with two Heralds and a Ward Adept (uplink).
Their stage is the crisis stage, never below the archetype's own.

## How a site is planned

On a grid with two cells of open ground, a wall ring, and a two-cell lane inside it:

1. **Partition** the slot area by BSP. The smallest slot fits the largest objective or required piece with a lane round
   it; below three slots across, a split may stop early, but the whole area always splits once.
2. **Connect** touching slots with a random spanning tree grown from the slots on the entry side, plus up to two
   loops. Each slot gets a depth; the objective goes in the deepest.
3. **Fill:** the objective, facing its parent slot (toward the entry), then each required piece in the deepest free
   slot it fits, then optional pieces by weight in the remaining slots (70% chance each) within the budget share.
   Emplacements turn their open back away from the entry. Pieces keep one free cell between them.
4. **Wrap:** the wall ring with gates spread along the entry side; cover (sandbag lines) inside each gate, beside the
   lane in and never in it; conduits from the power yard toward every piece that needs power; defender spots (each
   slot's centre, in front of each gate, inside the barracks).
5. **Validate:** the objective is reachable from the map edge; no piece has a cell shut off (unless tagged `Sealed`);
   every powered piece shares a conduit run with the generator; required pieces are there; the budget is in range.
   A failed plan tries the next seed; after five, the fixed fallback (south entry, objective as deep as it fits).

The seed is the site's world-object id mixed with the archetype's name, so a site always looks the same. Across 50
seeds, every archetype passes on the first seed (the tests require 45).

## In the game

- **Generation:** the site's gen step plans the site and stamps it at the map's centre: it clears rock, buildings,
  items and plants, lays gravel where the ground can't hold heavy buildings, and clears a road from each gate straight
  to the map edge. Then walls and gates (stone blocks or steel), pieces, and conduits, all the Empire's. It registers
  the area in vanilla's `UsedRects` so later gen steps keep out. After stamping it re-checks on the real map that the
  objective's room reaches the map edge, and logs if not.
- **The garrison** spawns at the defender spots under vanilla's `LordJob_DefendBase`.
- **The objective:** `MapComponent_RcSite` keeps the objective buildings. When all are destroyed, it holds the
  archetype's capability down for its days (`CrisisState.Hold`) and sends a letter. Orbital strikes already check
  `strikes` (C5); psykers that arrive while `psykers` is down get **relay severed** (psychic sensitivity −35%, heat
  recovery −25%).
- **The quest:** while the crisis runs, the Empire plot's `Empire:SabotageImperial` job offers "Destroy the Imperial
  …", a site 2–10 tiles away with the archetype picked by weight among those the stage allows. The quest counts as
  the Sabotage template, so the plot follows it as before; destroying the objective succeeds, with the usual reward.
  Without the crisis, or without site defs, the job is the settlement sabotage it was.

## Decisions

- **Conduits only come within reach.** Vanilla wires a building to any conduit within 6 cells of its position, through
  walls (`PowerConnectionMaker`). Conduits never cross walls or doors; they stop within 4 cells of a powered thing's
  cells, which keeps things up to 5 cells across inside vanilla's 6. The power validator uses the same rule.
- **A generator piece doesn't need power.** `PieceCheck` exempted pieces tagged `Power` from "uses power, so needsPower
  must be true". The command room's console does need power, so outposts require a power yard.
- **The sabotage quest keeps its template id.** No new transition id reaches the director (R17); the site is how the
  existing `SabotageImperial` job is carried out. The director picks the archetype from C11.
- **"Capture the officer"** (crisis spec §4.1, Forward Outpost) waits for captives (C10).
- **No floors or roofs yet.** Rooms stand on the cleared ground. Roofs over the rooms can come with C12's Citadel work
  if checkpoint 3 shows they matter.

## Dev Mode

- **Crisis: generate site** → an archetype → seed 1–5 or the site's own: makes a site near the colony, generates its
  map and goes there. The log has the seed, attempt, pieces and budget split.
- **Crisis: batch sites:** plans 50 seeds of each archetype (no maps) and reports first-seed passes, fallbacks, the
  problems found and how often each piece was used.

## In-game checks

For checkpoint 3 (`crisis-runbook-3.md`, written with C10).
