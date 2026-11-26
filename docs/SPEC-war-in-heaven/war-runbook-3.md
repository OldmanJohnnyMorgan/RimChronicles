# War in Heaven checkpoint 3 runbook: the War in Heaven and its endings (W10–W11)

One in-game session on the reference PC covers W10 and W11. Royalty and Biotech must be active. Setup as in
`war-runbook-1.md` (the `war-in-heaven` branch, Development mode, new test colonies). Write what you find in
`war-results-3.md`, or tell Claude Code. Each milestone adds its steps here as it is built.

## W10: the War in Heaven

Setup: a colony with both powers awake (**Empire: trigger awakening**, **Empire: end stirring now**, **Hollow Choir:
wake now**), the colony on a side (**War in Heaven: set side**). **War in Heaven: show** gives the state, the odds,
the front and the log.

| # | Do | Pass when |
| --- | --- | --- |
| W10.1 | Mod settings → Story, under the Hollow Choir | **The War in Heaven** checkbox (on), the certain day (600), the odds (1.0×), the front factor (80%) |
| W10.2 | Both awake, the colony at peace with both (**set side** → neither, if offered, or a fresh colony before choosing): **War in Heaven: show** | "Can start: False": the colony isn't at war |
| W10.3 | Side with the Empire; let 30+ days pass at speed 3; **show** | The odds rise from 0% after 30 days of war; the war may start on its own |
| W10.4 | **War in Heaven: start now** | A "The War in Heaven" letter (which side the colony is on), a news line; **show**: Started; a "war in heaven" story in the Chronicle tab |
| W10.5 | **Empire: expansion tick** and **Hollow Choir: expand now** several times | Each now goes for the other's settlements about three times as often as for others; neither ever takes the other's capital |
| W10.6 | **Crisis: show state** and **Choir crisis: show state** | The budget today is 80% of what it was before the war (the front factor) |
| W10.7 | **Plot: advance beat** on the war plot a few times (on the Empire's side) | Jobs come as quests: "Hold {settlement}" (break a Choir camp), "Strike {settlement}" (a Choir settlement's comms console), "A convoy to {settlement}", "Destroy the Choir {site}" (or a strike) |
| W10.8 | Win a strike (or **Quest → complete** in Dev Mode) | The Empire takes the settlement; the front moves +1 (**show**); a news line. Lose a "Hold": the Choir takes it, front −1 |
| W10.9 | **War in Heaven: move the front** → to 4, then advance the beat | "The final push on {the Choir's capital}" is offered; at war stage the map is the Cathedral (W9) |
| W10.10 | Take the capital (or **War in Heaven: destroy a power** → the Hollow Choir) | "{The Hollow Choir} is destroyed" letter and news; its plot and crisis end; it expands no more; **show**: destroyed |
| W10.11 | Advance the war plot | "Victory in the War in Heaven": the Empire's offer. **Not yet** → it comes again later; **Accept** → "The War in Heaven is over", news; the story's ending is recorded (the victory screen comes with W11) |
| W10.12 | New colony, on the Choir's side: as W10.7–W10.11 | The same, mirrored: the final push on the Imperial capital (the Citadel), then the Choir's offer (Choral) |
| W10.13 | New colony, independent (**set side** → independent), war started | Jobs against both: "Strike" and "Destroy the … site" for the Empire and the Choir |
| W10.14 | Destroy one power (storm its capital through its own plot, or Dev Mode) | "{power} is destroyed" (not the Resistance ending, not "the Choir silenced"); the war plot offers "A treaty of independence" from the survivor |
| W10.15 | Refuse the treaty, then destroy the survivor too | "Both powers are destroyed": the war is over (the total independence ending's screen comes with W11) |
| W10.16 | Storm a capital before the war begins (both awake) | The power is destroyed and the war begins at once ("the fall of …") |
| W10.17 | A colony with only the Empire awake (the Choir dormant): storm the Imperial capital | The old Resistance ending, as before W10 |
| W10.18 | Save and reload mid-war (a job under way) | The war, the front, the destroyed powers and the job survive |
| W10.19 | **Plot: director log** with the E4B war preset | The war plot's decisions are `War:*` with source `Model` |

## W11: the endings

Each ending ends the game's story with the victory screen: the credits roll with the epilogue, written by the model
(or the template), and the epilogue stays as a letter and in the Chronicle tab. The game goes on afterwards.
**War in Heaven: reach an ending** reaches any of the four at once.

| # | Do | Pass when |
| --- | --- | --- |
| W11.1 | On the Empire's side, win the war (W10.11 → **Accept**), or **reach an ending** → Imperial victory | "Rim Chronicles: Imperial victory" on the credits: the Empire won the War in Heaven with the colony at its side, the Choir destroyed; the colony's people named (the title holder first) |
| W11.2 | On the Choir's side (W10.12 → **Accept**), or **reach an ending** → Choral victory | "Choral victory": the Choir won, the Empire destroyed, the colony in the Chorus; the Choirmaster named first |
| W11.3 | Independent: destroy one power, then sign the treaty (W10.14), or **reach an ending** → Independence | "Independence": the survivor's treaty after the other's fall |
| W11.4 | Independent: destroy both (W10.15), or **reach an ending** → Total independence | "Total independence": both powers brought down |
| W11.5 | Outside the war: the Choir plot's First Voice's offer accepted (W4b.3, Act 3) | "Choral victory" (without the war): the colony joined the Chorus |
| W11.6 | Only the Choir awake (the Empire asleep): storm the Cathedral | "The Choir silenced" |
| W11.7 | Only the Empire awake: storm the Imperial capital | "Resistance victory", as before |
| W11.8 | After any ending | Every power's story and crisis has ended (Chronicle tab), the news reports it, and no more crisis operations come |
| W11.9 | With Ollama stopped, reach an ending | The template epilogue shows; it names the same people |

## Paths to each victory (Dev Mode)

Three new colonies, one per side, each with Royalty and Biotech, the crisis, the Hollow Choir and the War in Heaven on
(mod settings → Story), and Ollama running. Every action is under the Dev Mode menu's *Rim Chronicles* section.
**Plot: advance beat** opens a menu of the running plots: pick by template (`AwakenedEmpire`, `HollowChoir`,
`WarInHeaven`). Each path storms one fortress for real; **War in Heaven: reach an ending** is the shortcut that skips
the fight (use it for the second look at each screen, and for W11.9 with Ollama stopped).

### Common start (every path)

1. **Empire: trigger awakening**, then **Empire: end stirring now**: the Empire is Awakened (**Empire: show state**).
2. **Hollow Choir: wake now**: the faction and its settlements appear on the world map (**Hollow Choir: show**).
3. **War in Heaven: set side**: the path's side. **War in Heaven: show allegiance** shows it, and the other power (or
   both) hostile in the factions tab.
4. **Make the enemy's capital a fortress.** A capital becomes the Citadel (Empire) or the Cathedral (Choir) only once
   that power's crisis is at the War stage; a capital visited before then stays an ordinary settlement for good, so
   don't send anyone there yet. For each power you're fighting:
   - **Plot: advance beat** → its plot, until **Show plots** says it's in Act 2 (beat `Complication`).
   - Its expansion four times: **Empire: expansion tick** or **Hollow Choir: expand now**.
   - **Crisis: show state** (Empire) or **Choir crisis: show state** (Choir) says **Stage: War**.
5. **War in Heaven: start now**: "The War in Heaven" letter, a news line, a war story in the Chronicle tab.
   **War in Heaven: show**: Started.

### Storming a capital (Imperial and Choral paths)

6. **War in Heaven: move the front** → to 4, then **Plot: advance beat** → `WarInHeaven`: the quest "The final push on
   {capital}". Accept it in the Quests tab.
7. Form a caravan of your best fighters for the capital; on the world map select it and press **DEV: Teleport to
   destination**, then attack.
8. On the map (takes a while to generate):
   - **Crisis: Citadel state**: Shield up; relays (resonators, at the Cathedral) standing 2 of 2; rings reached; the
     garrisons. It works on the Cathedral too.
   - Destroy the courtyard's relays (resonators): **Citadel state** says Shield down.
   - Walk in ring by ring: **Citadel state** ticks each ring reached, and its garrison wakes as you reach it.
   - The keep: the High Stellarch in the throne room (**Crisis: Stellarch state**), or the First Voice in the
     Cathedral's sanctum (**Choir crisis: First Voice state**).
   - Win by clearing the map. To speed up, Dev Mode's **Pawns → Destroy non-colonists** clears what's left, but fight
     at least the first ring and the leader for real once.
9. The settlement is destroyed: "{power} is destroyed" letter and news; **War in Heaven: show**: destroyed; its crisis
   sends nothing more.
10. **Plot: advance beat** → `WarInHeaven`: "Victory in the War in Heaven". Press **Accept**.
11. The credits roll with the epilogue; the epilogue letter stays, and the Chronicle tab has it. Check the people named.

### Imperial victory

Side with the Empire. Step 4 for the **Hollow Choir**; steps 6–11 storm the **Cathedral**. Pass: "Imperial victory",
the Choir destroyed, the colony's title holder named first (W11.1).

### Choral victory

Side with the Hollow Choir. Step 4 for the **Empire**; steps 6–11 storm the **Citadel**. Pass: "Choral victory", the
Empire destroyed, the Choirmaster named first (W11.2). Grant a colonist rank first (**Hollow Choir: grant rank favor**,
a few times) so there is a Choirmaster to name.

### Independence and total independence

Independent (step 3 → **Independent (against every power)**). Step 4 for **both** powers. No final push comes for an
independent colony, so:

1. Destroy one power: storm its capital through its own plot (**Plot: advance beat** on `AwakenedEmpire` until it
   offers storming the capital, then steps 7–8 at the Citadel), or **War in Heaven: destroy a power** → the Hollow Choir.
2. **Plot: advance beat** → `WarInHeaven`: "A treaty of independence" from the survivor.
   - **Sign the treaty** → "Independence" (W11.3). Save before this, so you can reload and try the other answer.
   - **Fight on** → destroy the survivor (storm its capital, or **destroy a power**) → "Total independence" (W11.4).

### After every ending

**Crisis: show state** and **Choir crisis: show state** report no stage; the Chronicle tab shows every power's story
ended; no more operations arrive over a few days at speed 3 (W11.8).
