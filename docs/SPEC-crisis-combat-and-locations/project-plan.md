# Crisis combat and locations: project plan

**For:** `SPEC-crisis-combat-and-locations.md` (rev. 5) and `tech-spec.md`, both in this folder. **Date:** 2026-11-22.

## 1. Branches and merging

- **`crisis-combat-locations`** is the integration branch. It merges into `main` only when the director-model track
  is done (D11 passed, or John calls it done) and John says to merge it.
- Each milestone is its own branch off `crisis-combat-locations` (`cc-c2-pieces`, …) and a PR **into
  `crisis-combat-locations`**, with tests passing, like the stages. PRs are opened freely; nothing is merged until
  John says so.
- After each director-model milestone lands on `main`, `main` is merged into `crisis-combat-locations`, so the final
  merge stays small.
- Every change keeps `dotnet build RimChronicles.slnx` and `dotnet test RimChronicles.slnx` green, and the Game project
  building in the codespace.

## 2. Milestones

The crisis spec's build order (§10), with three changes: the threat budget and crisis state move to the front (units
and operations scale with them, and they are pure Core); the API survey needs one step on the PC (the Royalty XML,
tech spec R11); and director wiring (C11) waits for the director model. Size: S (a day or two of work), M, L.

| # | Milestone | Where | Size | Delivers | Done when |
| --- | --- | --- | --- | --- | --- |
| **C0** | API survey | PC (copy XML), then codespace | M | `Data/Royalty/Defs` and `Data/Core/Defs` copied to `/workspaces/reference/data/` (not committed); a "Crisis survey" section in `docs/ENGINE_NOTES.md`: psycast defNames and levels, `aiCanUse` per psycast, Empire pawn kinds, casting from code and its heat/psyfocus checks, lord toils for assault, siege and breaching, prefab export tool, attack map size, prisoner tab mode filtering, building damage path, telegraph flecks; crisis spec §5.3.1 table fixed to real defNames | Every "C0 confirms" in the tech spec has an answer; Q1–Q3 decided (tech spec §10) |
| **C1** | Crisis core | codespace | M | Core `Crisis/`: `CrisisState` (saved), `CrisisStages`, `ThreatBudget`, `ColonyStrength`, `SeededRng`, `CrisisArc` (operations by stage, cooldowns, pacing gap, `Wait`); the `ImperialCrisis` plot (not counted toward the cap); Game `CrisisDriver` reading colony strength and sending stage-change news; crisis settings | Unit tests for stages, budget edges, cooldowns, persistence (old saves load); a Dev Mode action prints today's stage, budget and allowed operations |
| **C2** | Piece library | codespace | M | `PieceExtension`, `PieceSpec`, rotation, instant and blueprint stamping through `PrefabUtility`, 5 starter pieces; an XML footprint test for every piece | Tests pass; "RC: Stamp piece" stamps any piece at any rotation |
| **C3** | Units | codespace | M | Pawn kinds for Praetorian, Herald, Breacher, High Stellarch and the four adept specialties; `UnitExtension`; gear by stage; kit grant, random-psycast strip, ban list, re-check on load | "RC: Spawn adept" spawns each specialty at each stage with exactly its kit; ban list unit test |
| **C4** | Psyker AI | codespace | L | `BattleView`, `IPsycastScorer` and the §5.3.1 scorers, `ScorerDef`, `IPsycastProvider` + `VanillaProvider`, `ThinkNode_RcPsykerCast`, `RimChronicles_PsykerSupport` duty, the `AICastableAbilities` patch, "RC: Psyker debug", "RC: Check psycasts" | Scorer unit tests on fixtures; every scorer's psycasts resolve at startup |
| | **Checkpoint 1** (in game) | PC | | `crisis-runbook-1.md` (in this folder): spawn each specialty against a test colony; each casts at least once; no vanilla-chosen casts; overheated psykers stop | John runs it; results in `crisis-results-1.md` |
| **C5** | Orbital strikes | codespace | M | `StrikeRules`, strike marker and telegraph, letter, delay, cancel on Herald loss, Bombardment and PowerBeam spawn | Unit tests for source, cooldown, cap, targeting; Dev Mode action calls a strike from a spawned Herald |
| **C6** | Operations I | codespace | L | `OperationDef` / `OperationSpec`, shared toils (focus fire, escort, retreat and regroup, carry downed), **ProbingRaid** (writes entrances) and **Decapitation** (drop pods, roof rule); "RC: Fire operation" | Retreat-path test on every operation; roof-rule unit test |
| **C7** | Operations II | codespace | M | **Breach** and **PsychicAssault** with specialty mixes by stage | Force-mix unit tests; both fire from Dev Mode |
| **C8** | Siege planner | codespace | L | `SiegeSpotSampler`, `SpotScorer`, `BuildStages`, staged blueprints, siege goals (`Goal:*` by weighted pick until C11), assault or retreat when builders fall; **SiegeWithBombardment**; "RC: Show siege scoring" | Unit tests for scoring and stages |
| | **Checkpoint 2** (in game) | PC | | `crisis-runbook-2.md`: each operation on a well-defended test colony at stage 2 and 3; strikes telegraphed; a siege built stage by stage and broken early; retreat with downed carried off | Results in `crisis-results-2.md`; tuning notes for C14 |
| **C9** | Location generator | codespace | L | `SitePlanner`, `CellGrid`, validators, fallback layouts, `SiteArchetypeDef`, `GenStep_RcSite`; **Forward Outpost**, then **Psychic Relay** and **Orbital Uplink**, with their capability effects; the rest of the room pieces they use; `SabotageImperial` offers a site archetype; "RC: Generate site", "RC: Batch sites" | Planner and validator unit tests; 50 seeds per archetype pass in Dev Mode |
| **C10** | Captives | codespace | L | Imperial resistance by rank, `RimChronicles_Interrogate`, `IntelDef` and effects, intel log, records room piece, defectors, release and harsh-treatment effects, **Retrieval**; "RC: Spawn captive", "RC: Grant intel" | Intel-effect unit tests; Retrieval offered only while a captive or defector is held |
| | **Checkpoint 3** (in game) | PC | | `crisis-runbook-3.md`: travel to each site archetype, destroy its objective, see the capability go down; interrogate a captive to intel; recruit a defector; survive a Retrieval | Results in `crisis-results-3.md` |
| **C11** | Director wiring | codespace, then PC | M | **Starts after the director model ships.** Descriptions for `Operation:*`, `Goal:*`, `Site:*`, captive ids; the snapshot `crisis` block, schema and summary; Golden files updated; `Site:ReclaimedHolding` offered; the `crisis` arc in `director-gen`, new test version; a free harness run (no `--judge`, no `claude-ref`) | `validate` passes; harness run at least as valid as before on the old snapshots; crisis cases generate reproducibly |
| **C12** | Citadel | codespace, then PC | L | `MapGeneratorDef` postfix for the capital, three rings, keep shield and psyfocus hediff, ring reaction, relay and uplink sites weakening the courtyard, side entrance from intel, per-ring and edge-to-throne validators; map size setting | Generation under 30 s and steady speed-1 ticks on the PC (or lazy ring spawning on); validators pass on 20 seeds |
| **C13** | Finale | codespace | M | High Stellarch kit and Farskip escape, **FinalAssault**, the Citadel's honor guard, Stellarch capture or death feeding the Resistance ending | FinalAssault fires at stage 4 only; Stellarch escapes at most once |
| **C14** | Balance and proof run (**passed 2026-11-24**) | PC | M | Tuning of budget, cooldowns, stage thresholds and force mixes against the playtest bar (crisis spec §9); `crisis-runbook-4.md` covers C11–C13 plus a late-game run from stage 1 to the Citadel | Results in `crisis-results-4.md`; John agrees the bar is met |

C11 is the only milestone that touches the director's vocabulary. If the director model is late, C12–C13 can go first:
they don't depend on C11, since the fallback picks until then.

## 3. In-game testing

Per the stage pattern, John tests in game once per checkpoint, not per milestone. A milestone that can't be checked in
the codespace beyond its unit tests and the Game build is folded into the next checkpoint's runbook. Each runbook gives
exact steps and PowerShell commands, and its results file records what passed, what didn't, and the follow-ups.

## 4. What C0 needed from John (done 2026-11-22)

1. On the PC, copy the game's `Data\Royalty\Defs` and `Data\Core\Defs` folders to the codespace, into
   `/workspaces/reference/data/Royalty/Defs` and `/workspaces/reference/data/Core/Defs` (outside the repo; they are
   game files and never committed). Ideology, Biotech, Anomaly and Odyssey `Defs` help too, for the layout and prefab
   defs they use.

Q1–Q3 were answered on 2026-11-22 (tech spec §10).

## 5. Relation to the director-model track

| Director milestone | Effect here |
| --- | --- |
| D10–D11 still running | C0–C10 and C12–C13 go ahead; the crisis picks by weighted random |
| Director model shipped (preset committed) | C11 starts; merge `main` in first |
| Crisis arc added to `director-gen` (C11) | Retraining follows the director plan: new labels for crisis cases (local teacher, zero API spend by default), a new report, a new blind review |
