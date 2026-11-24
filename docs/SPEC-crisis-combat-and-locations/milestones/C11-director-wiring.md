# C11: director wiring

Crisis milestone C11 (project plan §2, tech spec R6–R8, R17 and §5). Built 2026-11-24; not yet checked in game
(checkpoint 4, part two). `Site:ReclaimedHolding` came in a follow-up PR (below).

## Who decides the crisis

| Director model | Crisis decisions | Snapshot |
| --- | --- | --- |
| None (the main model decides) | The model | Crisis plot and `crisis` block shown |
| A prompted model (e.g. `gemma4:12b` as a separate director) | The model | Shown |
| A fine-tune whose preset says `KnowsCrisis` | The model | Shown |
| `rimchron-director-e2b` / `-e4b` (trained before C11), or an unlisted `rimchron-director-*` | Weighted pick, as before | Left out, so the prompt is what it was trained on |
| No model (tier Off) | Weighted pick | – |

`TierPresets.KnowsCrisis` and `ModelSlots.DirectorKnowsCrisis` decide it; `CrisisDriver.DirectorDecides` adds "the
crisis is on". When the retrained directors ship, their presets set `KnowsCrisis` and nothing else changes.

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Llm/TierPresets.cs`, `ModelSlots.cs` | `KnowsCrisis` on each director preset; the gate |
| `src/RimChronicles.Core/Snapshots/WorldSnapshot.cs` | `CrisisInfo`: stage, latest operations, knocked-out capabilities, prisoners, defectors, live intel |
| `src/RimChronicles.Core/Crisis/CrisisBrief.cs` | Builds it from `CrisisState` (only prisoners and defectors still present) |
| `src/RimChronicles.Core/Summaries/WorldSummarizer.cs` | The director's world summary gains an "Imperial crisis, stage …" block when there is one. Bulletins never see it. Without a crisis the prompt is byte-identical |
| `src/RimChronicles.Core/Plots/Director.cs` | `PlotTransition.Sub`: the follow-up the director picked |
| `src/RimChronicles.Game/Plots/PlotDriver.cs` | Arcs may list follow-up options per option; once one is chosen, a **second director call** (same world, the follow-ups as options, no Wait) runs in the same background task, and the pick reaches `Fire` as `Sub`. It's logged as its own decision. No model or no valid answer: weighted, as before. `useDirector` is now a check made each time |
| `src/RimChronicles.Game/Crisis/CrisisDriver.cs` | The crisis arc uses the director when `DirectorDecides`; siege goals (`Goal:*`) are its follow-ups; operations get the pick |
| `src/RimChronicles.Game/Empire/EmpireBeats.cs`, `Crisis/Sites/SiteOffers.cs` | `Empire:SabotageImperial`'s follow-ups are the allowed `Site:*` archetypes; the director's pick is offered if still allowed |
| `src/RimChronicles.Game/Engine/WorldReader.cs` | The crisis plot and block in the snapshot when `DirectorDecides` |
| `harness/RimChronicles.Eval/DirectorCases/CaseGenerator.cs` | `GenerateCrisis`: 8 scenarios (Probing, Reclamation ±captive, War ±captive, Finale, SiegeGoal, Site); ids `dir-s{seed}-c{n}`; past operations started through `CrisisArc.OperationStarted`, so summaries read as in game |
| `harness/RimChronicles.Eval/DirectorCases/SiteArchetypeXml.cs` | Site names, descriptions, stages and weights from `Defs/SiteArchetypeDefs` (one source with the game) |
| `director-gen --arc crisis` | Writes them to `seed-<n>-crisis` (default 1,200); generator version 1 of its own |
| `director-report`, `director-review` | `--cases a,b --labels a,b`: test-v1 and the crisis test labels scored together; follow-ups group as `ImperialCrisis/SiegeGoal` and `ImperialCrisis/Site` |
| `director-dataset` | The manifest's test version becomes 2 when crisis test cases (seed 9001) are in the test split |
| `harness/snapshots/snap-24-crisis-war.json`, schema | A crisis snapshot for the Stage 0 harness; `crisis` and the `ImperialCrisis` template in the schema |
| Dev Mode | **Crisis: decide now**, **Crisis: ask director for a siege goal**, **Crisis: ask director for a site**; **Crisis: show state** says who decides |

## Reclaimed Holding

Tech spec R7 calls it "a new opposed Act 2 option". It's built as a fourth `Site:*` archetype under
`Empire:SabotageImperial` instead: a new Empire option would put an unknown id in front of the shipped directors and
could change seed 9001's Empire cases. As a site, it follows the other sites: the director picks it in the follow-up
call when it may decide the crisis, else by weight.

| Where | What |
| --- | --- |
| `Defs/SiteArchetypeDefs`, `Defs/SitePartDefs` | `ReclaimedHolding`: 52×52, two barracks and power, the comms console in the comms room as objective, from Reclamation, weight 3, `needsHolding` |
| `SiteArchetype.NeedsHolding`, `SiteArchetypes.Allowed(…, holdingNear)` | Offered only while the Empire holds a settlement it conquered near the colony |
| `SiteOffers.Holding` | The nearest settlement the saga says the Empire conquered and still holds, within 30 tiles, its former owner still standing, no map or quest on it |
| `EmpireBeats` (sabotage job), `QuestNode_Root_ChroniclesImperialSite` | "Liberate {town}": the site appears beside the held settlement |
| `Crisis/Sites/Liberation.cs` | On the objective's destruction (`QuestPart_ChroniclesLiberate`): the settlement goes back to its former owner (+15 goodwill with the colony), news, a letter in the Empire plot's story, a saga note. The conquest stays in the saga, so the crisis stage doesn't move back |
| Dev Mode | **Crisis: Empire takes the nearest settlement** (sets one up), **Crisis: offer Reclaimed Holding** |
| `director-gen --arc crisis` | About half the Site cases have a holding (the Empire summary says so) and offer `Site:ReclaimedHolding`; crisis generator version 2 |

## Unchanged on purpose

- seed 9001 (500 cases, sha256 `b6816947…`) and seed 1 (5,000, `4ac3740b…`) regenerate byte-identical, so test-v1
  and its labels stand.
- The director prompt files (`prompts/director/v2`) don't change; only the summary they're filled with does, and only
  when there's a crisis.

## Next

1. Claude Code labels the seed-9001 crisis test cases in session (`test-crisis-v1`; no API spend).
2. The local teacher labels the seed-1 crisis cases (`director-label --cases …/seed-1-crisis`).
3. Retrain E2B and E4B on the combined dataset; report with `--cases seed-9001,seed-9001-crisis --labels
   test-v1,test-crisis-v1`; blind review; then the presets get `KnowsCrisis`.
