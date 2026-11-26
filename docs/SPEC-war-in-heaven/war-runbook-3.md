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
