# The War in Heaven: project plan

**For:** `SPEC-war-in-heaven.md` and `tech-spec.md` (this folder). **Date:** 2026-11-24.
**Branch:** `war-in-heaven` (tech spec T1). Each milestone is a PR into it; the branch merges into `main` after W14.

**Before W0:** the crisis's C14 (balance and proof run) carries on, on the PC, in parallel. Fixes from C14 go to `main`
first, then `main` is merged into `war-in-heaven`.

## Milestones

| # | Milestone | Where | Done when |
| --- | --- | --- | --- |
| W0 | API survey (**done 2026-11-24**) | Codespace (in-game checks at checkpoint 1) | `ENGINE_NOTES.md` gets a "War in Heaven survey" section: creating a faction mid-game, mechlink and psylink add/remove, bandwidth without band nodes, deactivating mechs vs feral, the dismantle job, the shuttle pickup and return parts, `Quest.Accept` for the Royal Ascent, mixed mech and mechanitor groups under vanilla lords. Tech spec §1 corrected from it |
| W1 | Generalise to `Power` (no behaviour change; **done 2026-11-24**) | Codespace | Tech spec §3's proof: tests and Golden files unchanged, case manifests byte-identical, save round-trip |
| W2 | `Allegiance` and sides (**built 2026-11-24**; in game at checkpoint 1) | Codespace | Stance read from the allegiance; old saves derive it; siding with one power sets the other hostile; Dev Mode "set side" |
| W3 | Choir faction and units (**built 2026-11-24**; in game at checkpoint 1) | Codespace | Faction def, mechanitor kinds, group makers by stage; created on waking with its settlements; Dev Mode spawns a Choir group |
| W4a | Choir saga (**built 2026-11-24**; in game at checkpoint 1) | Codespace | `ChoirDriver`: triggers (wealth, a mechanitor colonist, day 300), stirring rumours and the hum, waking creates the faction and applies the stored allegiance, expansion; the Hollow Choir setting; Dev Mode show, trigger stirring, wake, expand |
| W4b | Choir plot and ranks (**built 2026-11-24**; in game at checkpoint 1) | Codespace | `HollowChoir` plot template and beats (envoy, Act 2 jobs, tribute, Act 3), the Choir rank ladder from service (spec D13) opening the First Voice's offer; Empire and Choir drivers share one loop over `PowerFactions.Running` |
| W5 | Cross-waking, ship launch and Royal Ascent (**built 2026-11-24**; in game at checkpoint 1) | Codespace | Waking rules of spec §4–§5 unit-tested; `Quest.Accept` patch; Dev Mode for each |
| **Checkpoint 1** | `war-runbook-1.md` | PC | Choir wakes (each way), faction and settlements appear, sides set relations, the hum shows in bulletins |
| W6 | Summons, conversion, shut-down and dismantle (**built 2026-11-24**; in game at checkpoint 2) | Codespace | Both quest scripts; conversion at every level (unit); shut-down mechs never go feral; dismantle yields parts; refusal sets independence |
| W7 | Choir crisis: commander AI, operations, cluster drop, burrowing strike, the Hum (**director vocabulary built 2026-11-24**; **game built 2026-11-26**; in game at checkpoint 2) | Codespace | Tech spec §4.3 rows 1–3 and 5 (the cluster siege, row 4, came with W8's pieces); fairness tests as for the crisis |
| W8 | Choir sites and cluster siege (**site defs built 2026-11-24**; **pieces, sites and the cluster siege built 2026-11-26**; in game at checkpoint 2) | Codespace | Cluster outpost, Resonator, Signal spire, reclaimed holding; siege planner with cluster pieces |
| W9 | The Cathedral and the First Voice (**built 2026-11-26**; in game at checkpoint 2) | Codespace | Three-ring generator with resonators as the shield's relays; bossgroup guard |
| **Checkpoint 2** | `war-runbook-2.md` | PC | Summons both ways; a Choir raid of each operation; each site; the Cathedral |
| W10 | The War in Heaven (**Core built 2026-11-24**: odds, day 600, `WarArc`; **game built 2026-11-26**; in game at checkpoint 3) | Codespace | Odds and day 600 (unit); expansion against the rival; `WarInHeaven` plot and `War:*` jobs; front factor |
| W11 | Endings (**Core built 2026-11-24**; **victory screens and epilogue v3 built 2026-11-26**; in game at checkpoint 3) | Codespace | Choral victory, Independence (treaty and total), Resistance folded in; epilogue prompt updated, Stage 0 run at zero API spend |
| W12 | Director wiring (**built 2026-11-24**) | Codespace | Snapshot blocks, prompts, Golden files, vocabulary levels, `director-gen` arcs `choir`, `choir-crisis`, `war`; old cases byte-identical |
| W13 | Test labels and the one retrain (**test labels done 2026-11-24**: test-war-v1, teacher v5; the retrain is next, on the PC) | Codespace (labels), then PC | Claude Code labels `test-war-v1` in session (teacher v5); the PC labels training cases with the 12B, retrains E2B and E4B under `-war` names, reports and reviews pass, presets declare `WarInHeaven` |
| W14 | Balance and proof run | PC | `war-runbook-3.md` (a full game per side, with Dev Mode to speed up) and the balance pass; then `war-in-heaven` merges into `main` |

## Order and parallel work

- W1 must land before anything else touches the Empire code.
- W3 and W6's quest scripts can go in parallel with W4–W5.
- W12 waits for W11, so the option vocabulary is final before the retrain (spec D5).
- In-game checks are batched into three checkpoints, run once each, as for the crisis
  (`docs/test-plan.md` gets a section per checkpoint).
