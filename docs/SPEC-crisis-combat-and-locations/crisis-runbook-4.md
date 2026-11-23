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

(Added with C13.)

## C11: director wiring

(Added with C11, after the director model ships.)
