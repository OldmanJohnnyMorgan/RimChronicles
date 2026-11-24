# Crisis checkpoint 4 runbook: Citadel, finale, director wiring (C11–C13)

Written in parts as the milestones land. The project plan folds C11–C13 and a late-game run into this checkpoint,
with C14's balance pass. Write what you find in `crisis-results-4.md` next to this file, or tell Claude Code.

## Setup

As for the earlier checkpoints: `git checkout crisis-combat-locations`, `git pull`, `dotnet build RimChronicles.slnx`,
RimWorld with Development mode on, Royalty active. Use the checkpoint 3 colony ("crisis-test-3"), at stage 3 or 4
(**Crisis: show state**).

## C12: Citadel

C12's done-when is measured here: generation under 30 s and steady speed-1 ticks on the PC, or lazy rings on.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: batch Citadels**. | "20/20 pass every validator", planned in under a second each. |
| 2 | **Crisis: generate Citadel** → standard. Note the time in the message and the log line. | Generated in **under 30 s**. A 275×275 map: an outer wall with three gates, trenches, turret nests and mortar pits; a walled courtyard with barracks, an armory, two relay chambers and an uplink station; a walled keep with the throne room deepest inside. |
| 3 | Unpause at speed 1 with the whole map in view for a minute; then speed 3. | Steady: no visible stutter at speed 1 (Dev Mode's TPS readout, if on, near 60). Note the speed-3 TPS. |
| 4 | If 2 or 3 fails: settings → **spawn inner defenders only as you reach them**, and repeat. | Under 30 s and steady. Note which way passes; the default follows. |
| 5 | Drop a few colonists at the map edge (Dev Mode spawn, or a caravan to a test Citadel). | "The Imperial Citadel" letter: rings, 2 relays, the shield. |
| 6 | Walk a colonist into the outer works. | "The Citadel's courtyard garrison is moving out to meet you"; courtyard troops come out. The keep's guard stays (**Crisis: Citadel state**). |
| 7 | Shoot a keep wall (Dev Mode god mode helps). | It takes a tenth of the damage. Destroy both courtyard relays: "The keep's shield is down"; the wall now takes full damage. |
| 8 | Inspect an Imperial adept inside the keep while the shield is up. | Health tab: **psychic relay link**; its psyfocus stays high. |
| 9 | **Crisis: generate Citadel** → side entrance known. | The keep has a second gate on the far side from its first; the arrival letter mentions it. |
| 10 | **Crisis: generate Citadel** → no relays left. | No relay chambers in the courtyard; the arrival letter says the keep is unshielded; keep walls take full damage. |
| 11 | In a normal game at stage 3, destroy a relay site (C9), then attack the Empire's capital. | The capital's map is the Citadel with one courtyard relay. |
| 12 | Save on the Citadel map and reload. | Shield state, rings reached and held garrisons are kept (**Crisis: Citadel state**). |

## C13: finale

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: fire operation** → FinalAssault. | Only "stage 4 finale" is offered. "The Imperial final assault" letter, led by the High Stellarch: Praetorians, breachers, a herald and one adept of each kind. |
| 2 | Hurt the attackers badly (more than half). | They keep coming: no retreat. |
| 3 | Bring the Stellarch below about 40% health (or down it) without killing it. | "The High Stellarch skips away": skip effects, and it leaves the map. **Crisis: Stellarch state**: escape used. |
| 4 | **Crisis: generate Citadel** → standard; look in the throne room. | The Stellarch (the same one) stands there with the keep's guard. |
| 5 | Hurt it badly again in the Citadel. | No second escape. Kill or down and capture it. |
| 6 | Kill it (or capture it). | "The High Stellarch is dead" (or "captured") letter and a news item. **Crisis: Stellarch state**: fate dead (or captured). Firing FinalAssault again: "without a Stellarch to lead them". |
| 7 | From a save, start the Empire plot's **Act3:StormTheCapital** job, attack the capital (the Citadel), and kill the Stellarch. | The capital quest succeeds when it falls; the Empire plot ends with the Resistance ending. |
| 8 | From a save, with the escape unused, hurt the Stellarch in the Citadel. | It skips deep into the keep, away from your colonists, and fights on. |
| 9 | Save with the Stellarch off the map after its escape, reload, and fire FinalAssault. | The same Stellarch leads it, and can't escape again. |

## C11: director wiring

What changed: `milestones/C11-director-wiring.md`. Settings → Rim Chronicles. The shipped fine-tunes (E2B, E4B) were
trained before the crisis, so the crisis keeps picking by weight with them; the main model (no separate director)
decides it. Turn on **Dev: record director decisions for training** for steps 2–5.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Director model: **rimchron-director-e4b**. **Crisis: show state**; then **Crisis: decide now**. | "Decided by: weighted pick". A message says the crisis picks by weight; **Plot: director log** shows the crisis decision with source `Random`. |
| 2 | Director model: **None** (main model `gemma4:e4b`). **Crisis: show state**; **Crisis: decide now**. | "Decided by: the director model". The log's crisis decision has source `Model` and a reason about the colony. |
| 3 | **Crisis: ask director for a siege goal**. | A box with a `Goal:*` pick, a reason and source `Model`; nothing spawns. |
| 4 | At stage 3 (War), **Crisis: decide now** until the director picks `Operation:SiegeWithBombardment` (or fire it from a save where it's the only operation off cooldown). | The log has two lines that day: the operation, then a `Goal:*` follow-up (source `Model`). The siege letter's aim matches the goal. |
| 5 | **Crisis: ask director for a site**; then, from a save where the Empire plot offers `Empire:SabotageImperial`, **Plot: advance beat** until it's chosen. | The box shows a `Site:*` pick. When the job is offered, the log has the `Site:*` follow-up and the quest is that site. |
| 6 | Open the recorded cases folder (`…\RimChronicles\director-cases`). | The crisis decisions and their `-sub` follow-ups are there; their JSON has a `crisis` block. With the E4B director (step 1) there's no crisis case. |
| 7 | Save and reload mid-crisis; **Crisis: decide now**. | Works as before; no errors. |
| 8 | At stage 2 or later, opposed: **Crisis: Empire takes the nearest settlement**, then **Crisis: ask director for a site**. | The options include `Site:ReclaimedHolding`. |
| 9 | **Crisis: offer Reclaimed Holding**; accept the "Liberate {town}" quest. | The site appears next to the town (Imperial colours); its map is a walled garrison with the comms console deepest inside. |
| 10 | Destroy the comms console. | The quest succeeds; a "{town} liberated" letter (in the Chronicle tab under the Empire story); news; the town is in its old faction's colours and owner again; +15 goodwill with them. No "Outpost comms destroyed" letter. |

Then the free harness run (no `--judge`, no `claude-ref`) in PowerShell:

```powershell
dotnet run --project harness/RimChronicles.Eval -- validate
dotnet run --project harness/RimChronicles.Eval -- run --tasks director
```

Pass: `validate` OK; on snap-01 to snap-23 the director is as valid as before (those prompts didn't change, so
they're cache hits); snap-24 (the crisis at War) answers valid JSON with one of its operations or Wait.
