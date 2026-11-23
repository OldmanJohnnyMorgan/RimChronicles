# C12: Citadel

Crisis milestone C12 (project plan §2). Built 2026-11-23 in the codespace; the PC part (generation time and tick
speed) is in `crisis-runbook-4.md`, part C12.

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Sites/Citadel.cs` | `CitadelOptions`, `CitadelRings` (sizes, the three rings as site archetypes, budget shares, garrison caps), `CitadelPlan` (rings, which ring a cell is in, the whole map as one grid), `CitadelPlanner` (plan and validators) |
| `src/RimChronicles.Core/Crisis/Sites/` | The site planner can leave a **hole** for an inner ring, open a **side gate**, and searches faster on big maps |
| `src/RimChronicles.Core/Crisis/` | Destroyed relay and uplink sites are counted; the Citadel's settlement id is kept; `ForceMix.Citadel` and a unit cap |
| `src/RimChronicles.Game/Crisis/Sites/Citadel.cs` | `CitadelMaps` (which settlement is the Citadel, its options), `GenStep_RcCitadel`, `LordJob_RcGarrison`, `MapComponent_RcCitadel` (shield, relays, rings reached, lazy garrisons, the arrival letter), Dev Mode |
| `src/RimChronicles.Game/Patches/CitadelPatches.cs` | `MapGenerator.GenerateMap` prefix (generator and size); `Thing.TakeDamage` prefix (the keep shield) |
| `Defs/MapGeneration/RimChronicles_Citadel.xml` | The map generator and gen step |
| `Defs/PrefabDefs/RimChronicles_Pieces.xml` | Throne room (Royalty) and sealed vault |
| Settings | **Imperial Citadel map** 225–350 (default 275), **spawn inner defenders only as you reach them** (off) |
| `tests/…/CitadelTests.cs` | 20 seeds, map sizes, nesting, contents, relays and uplinks by destroyed sites, the side entrance, the throne check, determinism, garrisons |

## The Citadel

When the colony attacks the Empire's capital while the crisis is at war or in its finale (the Empire hostile), the
capital's map is the Citadel instead of a vanilla settlement. Once generated, that settlement stays the Citadel.

| Ring | Grid | Holds | Gates | Budget share | Garrison (cap) |
| --- | --- | --- | --- | --- | --- |
| Outer works | the map less 6 cells each side (263 at 275) | trench lines (sandbag lines), turret nests, mortar pits, guardhouses, a power yard | 3 | 40% (half to pieces) | Praetorians, a Breacher, a Herald (24) |
| Courtyard | 112 | two barracks, armory, power yard, **psychic relays** (2, less one per relay site destroyed), **uplink** (1, less one per uplink site destroyed), turret nests, mortar pits, records room | 2 | 35% | Praetorians, Control and Ward Adepts, a Herald (22) |
| Keep | 46 | **throne room** (deepest), records room, sealed vault, barracks or armory | 1, plus the **side entrance** with the Citadel plans | 25% | the honor guard: Praetorians, Mind, Skip and Ward Adepts (16) |

The budget is twice the finale's operation budget (about 12,000 at normal strength), so about 50 defenders in all.
Each ring's gates face a random side, so the way in winds round. The courtyard's uplink counts as a strike source
(C5), so orbital strikes come while it stands.

**The keep's shield.** While any courtyard relay stands, the keep's walls, doors and other Imperial buildings take a
tenth of their damage, and Imperial psykers inside gain psyfocus and the relay link. When the last relay falls, a
letter says the shield is down. With no relay left (two relay sites destroyed), the keep is never shielded.

**Rings reacting.** When a colonist first enters the outer works, the courtyard's garrison moves out to meet them
(the crisis assault, with focus fire and escorts). The keep's honor guard never leaves. With **lazy rings** on, the
courtyard's garrison appears when the colonists reach the outer works and the keep's when they reach the courtyard.

**Arrival.** A letter explains the rings, how many relays stand, and the side entrance if it's known.

## Validators

Each ring runs the site validators (reachability inside the ring, sealed rooms, power, required pieces, budget). Then
over the whole map: the throne room and every courtyard relay are reachable from the map edge (fairness rules 4 and
8), the rings sit in each other's holes, and the courtyard has the relays it should. All 20 test seeds pass with every
mix of relays, uplinks and side entrance, at 225, 275 and 350; planning takes about 0.3 s.

## Decisions

- **One prefix, not two.** The tech spec named a `Settlement.MapGeneratorDef` postfix as well; the
  `MapGenerator.GenerateMap` prefix sets both the generator and the size, so it's the only hook.
- **The Citadel is the capital at war.** Before stage 3, attacking the capital is a vanilla settlement fight.
- **The keep's whole garrison is the honor guard** for now: it holds the keep. The High Stellarch, the Farskip escape
  and the honor guard's own kit are C13.
- **No shuttle pad or archotech vault piece.** Vanilla has no shuttle pad building, and the vault holds an AI persona
  core rather than archotech (Royalty has no archotech item to place); both can come with C14's tuning.
- **Rings are planned separately and nested.** Each inner ring sits in a hole the outer one leaves free (reserved
  cells take no piece, wall, conduit or defender), so each ring keeps the site planner's guarantees.

## Dev Mode

- **Crisis: generate Citadel** → standard, side entrance known, after a relay and an uplink site, or no relays left:
  makes an Imperial settlement nearby that generates as the Citadel at the settings' size, goes there, and says how
  long generation took (red if 30 s or more).
- **Crisis: batch Citadels:** plans 20 Citadels (no maps) and reports the validators and planning time.
- **Crisis: Citadel state:** shield, relays standing, rings reached, garrisons and what each is doing, held garrisons.
