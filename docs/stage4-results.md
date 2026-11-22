# Stage 4 results: plot director and quest templates

Stage 4 closed on 2026-11-22. Plots now choose their next beat through the director, talk to the player through quests,
and run side by side. All three of the spec's exit criteria passed in the 200-day proof run on the reference PC
(`docs/stage4-runbook.md`, M6): Nemesis, Lost Family and Silent Press ran together with no overlapping beats, every quest
template spawned from Dev Mode, and the director log read as a sensible outline.

| Milestone | What it added | PRs |
| --- | --- | --- |
| M1 | Director: allowed transitions per beat, the model picks one with a reason (weighted random fallback), a saved decision log | #48 |
| M2 | Quest framework: quest roots in C#, started only by plots, outcomes (success, failure, ignored, other) fed back daily | #49 |
| M3 | Templates: Hunt target, Rescue, Deliver, Choice letter, each spawnable from Dev Mode | #50 |
| M4 | Nemesis gains a Hunt option; plot-letter prompt v3 ("name every option") | #51 |
| M5 | Lost Family, the third arc: letters, the kin taken, a Rescue quest, reunion or loss | #52 |
| M6 | Proof run and the beat-spacing check | #53 |
| Fixes | Quest def config errors, site quests pointing at the camp before acceptance, the rescue prisoner swap, Nemesis raid points | #54–#56 |

## Bugs the proof run found

- **Config errors on load:** our quest defs set a challenge rating without `isRootSpecial`. Fixed in #54.
- **Quests with no location:** a site exists only once its quest is accepted, so "jump to" fell back to the reward drop
  at the base. Offers now point at the camp's tile. Fixed in #55.
- **`otherPawn ... not deep-saved` on every save:** the Rescue quest destroyed the prisoner vanilla's site part made,
  which can be an existing world pawn related to a colonist. It is now passed back to the world. Fixed in #56.
- **`No raid strategy found`:** the Nemesis raid forces its faction, and a poor colony's points were below that
  faction's minimum group. Points are now lifted to it. Fixed in #56.

## Still open

- The free plot-letter harness run for prompt v3 and the two Lost Family cases hasn't been pushed yet. The game falls
  back to template letters if the model's letter fails the checks, so it doesn't block Stage 5.

## Carried into Stage 5

- Escort, Defend ally and Sabotage templates, built for the Awakened Empire.
- The remaining arc types (Old Debt, Grief, Rivalry, The Past Catches Up) are optional; the spec only needs a minor plot
  that can be promoted into the major plot.
