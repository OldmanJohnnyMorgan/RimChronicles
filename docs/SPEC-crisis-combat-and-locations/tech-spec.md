# Crisis combat and locations: technical spec

**For:** `SPEC-crisis-combat-and-locations.md` (rev. 5, in this folder). **Date:** 2026-11-22. **Status:** reviewed; Q1–Q3 decided and the C0 API survey done, 2026-11-22.
**Branch:** `crisis-combat-locations`. It merges into `main` only after the director-model track is done (see `project-plan.md`).

This turns the crisis spec into a design that fits the repo as it is after Stage 5 and the director-model milestones
D0–D9. Where it departs from the crisis spec, §1 says why. The parent design spec wins over both, as the crisis spec
itself says.

## 1. Review: what changes and why

Checked against the repo at `2e57f9d` and the decompiled 1.7 source (`/workspaces/reference/src/`). "Source" below means
a quick read during this review, not yet the full API survey (milestone C0).

| # | Crisis spec says | Repo or 1.7 reality | Decision |
| --- | --- | --- | --- |
| R1 | Code in `Source/RimChronicles/{Pieces,Locations,Commander,Captives,Threat}` | The repo splits story logic (`src/RimChronicles.Core`, no RimWorld) from engine calls (`src/RimChronicles.Game`). Rule 6: story logic never calls RimWorld. Rule 9: Core has unit tests that run without the game | **Split every module in two.** Rules, scoring, planning and validation go in Core under `Crisis/`, on plain data, unit-tested. Engine work (lord jobs, think nodes, stamping, GenSteps, Harmony) goes in Game under `Crisis/`. §2 has the layout |
| R2 | A custom `PieceDef` with a character-grid layout | 1.7 has **`PrefabDef`** (things, terrain and sub-prefabs at positions, `rotations`) and `PrefabUtility.SpawnPrefab(prefab, map, pos, rot, faction, …, blueprint: bool)`. It can already stamp as **blueprints**, and Dev Mode → Generation → Create prefab copies a map rectangle's `PrefabDef` XML to the clipboard | **A piece is a vanilla `PrefabDef` plus a `DefModExtension`** (`RimChronicles.PieceExtension`: tags, door sides, needs power, defense value, threat cost, build order). Vanilla does the stamping and the rotation. Pieces are built in game and exported with Create prefab; no character-grid format (C0) |
| R3 | Partition with BSP, connect, fill | 1.7 has `LayoutDef` / `StructureLayoutDef`, `LayoutRoomDef` and `RoomLayoutGenerator.GenerateRandomLayout` (rooms, doors, corridors), used for ancient ruins and the mechhive | **Core owns the plan** (partition, graph, fill, wrap, budget, validators) on an abstract cell grid, so the site rules are seeded, testable without the game, and identical every time for one site. Game turns the plan into prefab stamps, walls and doors. Vanilla's generator is the reference for the shapes, not a dependency: its randomness and output can't be unit-tested or kept stable across game updates |
| R4 | Crisis stages named Stirring, Reclamation, War, Finale | `EmpireState` already has `Stirring` (the Empire **before** it wakes), then `Awakened`, `Ascendant`, `Broken`. The Empire plot has acts (Hook, Complication, Choice) and a **stance**: ally, oppose or neutral | **Rename crisis stage 1 to "Probing"** so it can't be confused with `EmpireState.Stirring`. Stages are derived from the saga and the plot (§4.1), not stored as a second copy of the plot state |
| R5 | The Empire fights the player | The colony may be the Empire's **ally**, or neutral and paying tribute. The Empire is only hostile after the colony opposes it, refuses tribute or refuses the final demand | **The crisis runs only while the Empire is hostile to the colony** (`EmpireStance.Oppose`, or the faction relation is hostile). Allies never see crisis operations; a neutral colony that refuses tribute sees probing raids only, until it opposes the Empire (**decided 2026-11-22, Q3**). A colony that swears fealty mid-crisis ends it: raids in progress retreat |
| R6 | The director picks an operation from the allowed list | The director decides when a **plot's beat** is due, once a day, through the plot book (beat spacing, waits, pacing gate). The Empire plot runs one job at a time (a quest or a letter) and counts wins and losses towards Act 3 | **Operations get their own plot: `ImperialCrisis`**, a plot-book entry that doesn't count toward the plot cap (`PlotTemplate.CountsTowardCap = false`), with its own arc in Core (`CrisisArc.Transitions`) offering `Operation:*` and `Wait`. It reuses the director, the fallback, the decision log and the pacing gate unchanged, and it doesn't block the Empire plot's quests. The Empire plot's arc is unchanged |
| R7 | `Site:*` ids appear "when a plot creates a site" | The opposed Empire plot already offers `Empire:SabotageImperial` (a sabotage quest on an Imperial settlement) and `Act3:StormTheCapital` (the capital as a sabotage quest) | **Sites hang off these jobs.** `SabotageImperial` becomes a site job whose archetype the director picks in a second call (`Site:ForwardOutpost`, `Site:PsychicRelay`, `Site:OrbitalUplink`). `StormCapital` generates the **Citadel** on the capital's map. `Site:ReclaimedHolding` is a new opposed Act 2 option, offered when the Empire has conquered a settlement near the colony. **As built (C11):** a fourth `Site:*` archetype under `SabotageImperial`, allowed only while the Empire holds a conquered settlement near the colony, so the Empire arc's option list (and seed 9001) stays unchanged |
| R8 | Siege goal picked by the director | The director answers one choice per call | **A second director call** on the same day, only after `Operation:SiegeWithBombardment` is chosen, with `Goal:*` as its options. Same task, same fallback. The site's flavor call (§4.4 of the crisis spec) works the same way |
| R9 | Vanilla AI casting "if any" must be turned off | `JobGiver_AIFightEnemy` and `JobGiver_NPCCastNonCombatAbilities` cast what `Pawn_AbilityTracker.AICastableAbilities` returns, which needs `aiCanUse`. **No Royalty psycast sets it** (C0), so vanilla NPCs never cast psycasts; some Biotech and Odyssey abilities do | **A Harmony prefix on `AICastableAbilities`** still returns an empty list for Rim Chronicles units, as a guard against weapon-trait abilities and other mods' XML patches. Both callers are in `Assembly-CSharp`, which is JIT-patchable |
| R10 | Strip random psycasts given with the psylink level | `Hediff_Psylink.TryGiveAbilityOfLevel` gives a random psycast of each new level (`DefDatabase<AbilityDef>…RandomElement()`) | After generation, a postfix on our pawn kinds **removes every psycast and grants the kit** (`Pawn_AbilityTracker.RemoveAbility` / `GainAbility`), filtered by psylink level and the ban list. The same check runs on load, so a save never keeps a random or banned psycast |
| R11 | Confirm psycast defNames in the survey | The codespace has the game DLLs only, no `Data/` XML. `AbilityDefOf` lists only a few Biotech and Anomaly abilities | **C0 needs the Royalty defs from the PC:** copy `Data/Royalty/Defs/` (and `Data/Core/Defs/` for pawn kinds) into `/workspaces/reference/data/`, outside the repo, never committed. The §5.3.1 table is fixed from those files |
| R12 | Record findings in `docs/1.7-api-notes.md` | The repo already keeps 1.7 findings in `docs/ENGINE_NOTES.md`, with a section per stage | **Add a "Crisis survey" section to `ENGINE_NOTES.md`** instead of a second notes file |
| R13 | Declare Royalty as a dependency in `About.xml` | Royalty is optional today: the news agent and minor plots work without it, and `About.xml` says the Empire arc "needs Royalty" | **Keep Royalty optional:** crisis defs load with `MayRequire="Ludeon.RimWorld.Royalty"` and the crisis code checks `ModsConfig.RoyaltyActive`; without Royalty the crisis is off. Making it a hard dependency would cut off players without Royalty from the whole mod. **Decided 2026-11-22 (Q1)** |
| R14 | Interrogate as a new prisoner mode | 1.7 already has Anomaly's `InterrogateIdentity` (`WorkGiver_Warden_InterrogateIdentity`, `JobDriver_InterrogatePrisoner`, `InteractionWorker_InterrogateIdentity`) | **Model ours on it, under its own name** (`RimChronicles_Interrogate`, label "Interrogate"); see R21 for where it shows |
| R15 | "Entropy dump / wait" as a shared scorer | `EntropyDump` (level 2) exists, but it targets only colonists (`onlyTargetColonists`) and puts the target in a coma for about a day (C0) | **It joins the ban list.** The shared fallback is "hold": no cast, keep fighting, let heat fall |
| R16 | Debug overlays, targeting laser | Rule 3: no Unity APIs in new code, except UI code. Overlays and lasers usually draw with Unity types (`Color`, `Material`) | **Telegraphs use XML-defined flecks and things** (no Unity calls). Dev Mode overlays use `GenDraw` helpers that take cells. Dev Mode overlays may use the Unity stub types they need (such as `Color`), under rule 3's UI exception. **Decided 2026-11-22 (Q2)** |
| R17 | "When these are added, the director model needs retraining" | The director-model track (D0–D11) is training on today's option vocabulary, and the test set (seed 9001) is frozen | **No `Operation:*`, `Goal:*` or `Site:*` id reaches the director until the director model ships** (milestone C11). Until then the crisis picks by weighted random, which rule 7 needs anyway as the fallback. C11 adds the crisis arc to the case generator as a new arc, keeps the seed-9001 cases for the existing arcs, and bumps the test version |
| R18 | Everything seeded from the site's world-object ID | `System.Random(seed)` is used in the harness; its seeded algorithm isn't guaranteed stable across .NET versions | **Core gets a small `SeededRng` (SplitMix64)** for site generation, so a site's layout never changes with the runtime |
| R19 | Adept psylink 2–3 at stage 1; Mass Burden in the Control kit | Skip Adepts' first cast is level 3; vanilla has no Mass Burden; level 3–4 casts need 25% psyfocus and level 5–6 need 50% (C0) | Crisis spec §5.3.1 and §6 fixed in C0: stage 1 adepts are psylink 3, Mind Adepts start at stage 3, Chunkskip joins the Ward kit, Mass Burden is dropped |
| R20 | Psykers obey psyfocus | NPC psyfocus starts at -1 (band 0: level 2 casts at most) until `SetInitialPsyfocusLevel` runs, and only drop pods call it. `Psycast.CanCast` skips the psyfocus check for heat-making casts (C0) | Units set psyfocus at spawn, by stage. `VanillaProvider` checks `Psycast.GizmoDisabled`, which checks everything, not `CanCast` |
| R21 | Interrogate shown only for Empire prisoners | The prisoner tab's mode filter is a compiler-generated local function in `ITab_Pawn_Visitor` (C0) | Show the mode for every prisoner; the work giver skips, with a reason, prisoners with nothing to tell. Patch the filter only if that reads badly in game |
| R22 | Lord jobs extend vanilla's | `LordJob_AssaultColony`, `LordJob_Siege` and their toils are public and unsealed; `LordToil_Siege` places its camp with `SiegeBlueprintPlacer` in one go (C0) | Assault and breach operations reuse vanilla toils plus ours; the crisis siege has its own toil, since its camp is built in stages |

## 2. Architecture

```
Core (no RimWorld, unit-tested)                 Game (RimWorld, Harmony)
-------------------------------------------     --------------------------------------------------
Crisis/                                         Crisis/
  CrisisState        stage, cooldowns,            CrisisDriver       daily: stage, budget, fire ops
                     capabilities, history        ColonyStrengthReader -> ColonyStrength
  CrisisStages       stage from saga + plot       Units/             PawnKindDef + UnitExtension,
  ThreatBudget       budget math                                     kit grant, ban list, strip patch
  CrisisArc          Operation:* transitions      Psykers/           IPsycastProvider, VanillaProvider,
  Operations/        OperationSpec, force mix,                       ThinkNode_RcPsykerCast,
                     retreat rules                                   AICastableAbilities patch
  Battle/            BattleView (plain units,     Operations/        LordJob_Rc*, toils, duties,
                     cells, cover, heat)                             retreat + carry downed
  Psykers/           IPsycastScorer, scorers      Strikes/           StrikeMarker thing, telegraph,
  Strikes/           StrikeRules (source,                            Bombardment / PowerBeam spawn
                     cooldown, cap, targets)      Siege/             SiegeSpotSampler (features),
  Siege/             SpotScorer, BuildStages                         staged blueprints, LordJob_RcSiege
  Pieces/            PieceSpec, rotation          Pieces/            PieceExtension, PrefabDef -> PieceSpec,
  Locations/         SitePlanner, CellGrid,                          stamping (instant / blueprint)
                     validators, SeededRng        Locations/         SiteArchetypeDef, GenStep_RcSite,
  Captives/          IntelLog, intel effects,                        Citadel map gen, keep shield
                     captive and defector facts   Captives/          prisoner mode, work giver, job,
                                                                     intel letters
                                                  CrisisDevActions   every Dev Mode action in §9
Defs/ (XML, MayRequire Royalty)
  PrefabDefs/RimChronicles_Pieces.xml  PawnKindDefs/RimChronicles_Units.xml  DutyDefs/  ThinkTreeDefs/
  RimChronicles.SiteArchetypeDef/  RimChronicles.OperationDef/  RimChronicles.IntelDef/  RimChronicles.ScorerDef/
  RimChronicles.CrisisSettingsDef (ban list, cooldowns, stage table)
```

Def files follow the repo's existing pattern (`Defs/<Kind>Defs/RimChronicles_<Name>.xml`, defNames prefixed
`RimChronicles_`, decided in C0).

**The data flow for one decision.** Once a day, `CrisisDriver` reads the colony into `ColonyStrength` and the saga
into a `CrisisStage`, and `CrisisArc.Transitions` lists the operations that the stage, budget, cooldowns and pacing gap
allow. The plot book makes the `ImperialCrisis` beat due; the director (or the weighted fallback) picks. Game builds
the force from the `OperationDef` and the budget, spawns it with its lord job, and records the operation in
`CrisisState`. When the lord ends, the outcome goes back into `CrisisState` and the news.

**The data flow for one fight tick.** Every 250 ticks per lord, Game builds one `BattleView` (positions, sides, health,
weapon damage, cover, psylink, heat, psyfocus, the casts each psyker has ready) and hands it to the Core scorers. Each
psyker gets its best `(psycast, target)` or nothing; Game checks that the cast is still legal and queues the vanilla
casting job. Scorers never touch the game; the game never decides a cast itself.

**Threading.** No LLM call happens in a fight. Director calls go through the existing task runner and main-thread
queue (rule 4). Map generation and every tick-level step run on the main thread.

## 3. Pieces and the location generator

### 3.1 Pieces

- **`PieceExtension`** on a `PrefabDef`: `tags`, `doorSides` (N/E/S/W, rotated with the piece), `needsPower`,
  `defenseValue`, `threatCost`, `buildOrder`, and `stuffRole` per thing (wall, floor, furniture) so materials are chosen
  at stamp time.
- **`PieceSpec`** (Core) is what the planner sees: size, a cell mask (wall, door, floor, blocked, open), door cells per
  side, tags and costs. Game builds it from the prefab at startup; tests build it from fixtures and from the repo's
  `Defs/` XML read with `System.Xml` (no RimWorld), so a piece whose things fall outside its declared size, or whose
  door side has no door, fails `dotnet test`.
- **Stamping** is `PrefabUtility.SpawnPrefab` with `overrideSpawnData` for stuff, `blueprint: true` for sieges.
  `CanSpawnPrefab` is checked first; a piece that can't spawn is reported to the caller, never forced.
- **Starter set** in C2 is five pieces (sandbag line, sandbag corner, mortar pit, turret nest, barracks). The rest come
  with the milestones that use them.

### 3.2 Site planner (Core)

`SitePlanner.Plan(archetype, pieces, buildArea, seed) → SitePlan` runs the crisis spec's steps 1–4 on a `CellGrid`:
partition (BSP with a minimum slot size from the largest required piece), connect (a spanning tree plus a few loops;
the objective slot is the one deepest from the entry side), fill (required pieces first, then weighted optional ones
by tag and size), wrap (walls, gates, outer defenses by the archetype's density and budget). The plan also lists
defender spawn spots and the defender roster's points.

**Validators** run on the plan before anything spawns: reachability (flood fill from every edge cell to the
objective), power (each `needsPower` piece is joined to a generator piece by a conduit path in the plan), no sealed
rooms except `Sealed` pieces, and budget. After the plan spawns, Game runs reachability again on the real map (rock
and rivers can block what the plan left open); a failure there regenerates with the next seed. After 5 failures, the
archetype's fixed fallback layout spawns and the failure is logged.

The seed is the site's world-object ID mixed with the archetype's defName, through `SeededRng`. The same site always
plans the same layout.

### 3.3 Sites in the game

- A `SiteArchetypeDef` names a `SitePartDef` and a `GenStep_RcSite`, required and optional pieces, defender roster,
  density and budget range. Sites are made through the existing quest code (`QuestNode_Root_ChroniclesTemplates.MakeSite`).
- **Objective tracking** uses the signals the Stage 5 notes found: `Thing.Destroy` sends `Destroyed` for a relay core
  or uplink; `Site.CheckAllEnemiesDefeated` sends `AllEnemiesDefeated`. Destroying a relay or uplink sets
  `CrisisState.Capabilities` (psykers weakened or strikes off until day N) and marks a Citadel element removed.
- **The Citadel** replaces the capital's map. `Settlement.MapGeneratorDef` is a virtual getter; a postfix returns
  `RimChronicles_Citadel` for the settlement `EmpireBeats.Capital` returns, while the crisis is active. The map size
  (default 275, a setting) comes from a prefix on `MapGenerator.GenerateMap` when the parent is the capital (C0). The rings are three
  `SitePlanner` runs, each inside the previous ring's walls, plus a whole-map reachability check to the throne room.
- **Keep shield:** a map component knows the keep's cells and whether any courtyard relay stands. A prefix on
  `Thing.TakeDamage` (non-virtual, so it catches every building type; C0) scales damage to keep walls and doors. Empire psykers inside the keep get a hediff with a psyfocus bonus. Destroying
  the last relay lifts both and sends a letter.
- **Ring reaction:** a map component tracks which ring each colonist is in; entering a ring sends the next ring's
  defenders (except the honor guard) a reinforce duty. If the performance check fails, the same component spawns a
  ring's defenders when the first colonist reaches the ring before it.

## 4. Raid commander

### 4.1 Crisis state, stages and budget (Core)

**`CrisisState`** (saved, `IArchivable`): last operation day, per-operation cooldowns, operations this stage, the
probe's findings (colony entrances as cells, from the last probing raid), capabilities disabled until a day (relay,
uplink), the intel log, captive and defector pawn ids, and the outcome of the last operations. Missing data loads as
"no crisis yet" (rule 5).

**Stage** is computed, never stored on its own:

| Stage | When (Empire hostile to the colony) |
| --- | --- |
| 1 Probing | Empire plot in Act 2 (Complication) and the saga `Awakened`, fewer than 2 expansions |
| 2 Reclamation | Act 2 with 2+ expansions, or 2+ crisis operations survived |
| 3 War | Act 3 (`Reckoning` done) with the Empire `Ascendant`; or Act 2 with `ExpansionsForReckoning - 1` expansions |
| 4 Finale | `StormCapital` accepted, the colony started a ship launch, or the final demand refused |

A `Broken` Empire is capped at stage 2 and its budget is scaled down (a setting, default ×0.6). These thresholds are
constants in `CrisisStages`, tested, and tuned in the balance pass.

**`ThreatBudget.Compute(stage, difficulty, ColonyStrength)`** is the crisis spec's formula. `ColonyStrength` is plain
data from Game: combat power of colonists (health, skills, armor and weapon quality), defenses (turrets, walls by
material), colonists' psylink levels, and wealth at a reduced weight. Each input is clamped, so the formula can be
tested at the edges. The storyteller's raid points are not used.

**Pacing:** a minimum gap between operations (default 8 days, a setting), on top of the plot book's own spacing.

### 4.2 Units

Pawn kinds in XML, each with a `UnitExtension`: role (`Praetorian`, `Adept`, `Herald`, `Breacher`, `Stellarch`),
specialty for adepts, the psycast kit by psylink level, and gear and bionic tiers by stage. Royalty's Empire kinds are
the parents where they fit (listed in `ENGINE_NOTES.md`, "Crisis survey"). A postfix after pawn generation applies the stage's gear tier, the kit and
the ban list (R10); `Pawn.ExposeData` on load re-checks the kit.

The **ban list** is one XML def. Kit lists are checked against it at startup: a banned psycast in a kit is removed and
logged. Fairness rule 9 has a test that generates every unit at every stage.

### 4.3 Psykers

- **`IPsycastScorer`** (Core): `Score(BattleView view, Unit caster) → ScoredCast?` (psycast id, target cell or unit,
  utility). One class per scorer in §5.3.1 of the crisis spec, tested on fixed `BattleView` fixtures.
- **`ScorerDef`** (XML) maps a scorer class to the psycast defNames it needs. At startup, a scorer whose psycasts are
  missing is disabled and logged ("RC: Check psycasts" shows the list). Psycasts with no scorer are never cast.
- **`IPsycastProvider`** (Game): lists a pawn's castable psycasts with their range, cost and heat, and casts one.
  `VanillaProvider` reads `pawn.abilities`, checks `Psycast.GizmoDisabled` (psychic sensitivity, level, psyfocus and
  its band, heat, cooldown; `CanCast` alone misses psyfocus, R20) and calls `QueueCastingJob`. A later `VpeProvider` implements the same interface.
- **`ThinkNode_RcPsykerCast`** sits first in the `RimChronicles_PsykerSupport` duty's think tree. Every 250 ticks it
  asks the lord's shared `BattleView` (built once per lord per 250 ticks) for the psyker's best cast, then falls through
  to normal combat. A cast is never forced past heat or psyfocus (fairness rule 2).
- **Vanilla's own casting is off** for every Rim Chronicles unit (R9). A test-only counter in Dev Mode records every
  cast an RC pawn starts and who chose it; the psyker test checks it was always a scorer.

### 4.4 Operations

An `OperationDef` names its lord job, force mix (roles and specialty ratios per stage), arrival mode, retreat
threshold, reinforcement rule, and its `desc` for the director. Core's `OperationSpec` mirrors it so the force mix
and retreat rules are tested without the game.

- **Lord jobs** extend vanilla's (`LordJob_AssaultColony`, `LordJob_Siege`, the breaching toils) with our toils:
  focus fire (a target picker on the `BattleView`), escort duty for Praetorians around Heralds and psykers, retreat
  and regroup at the loss threshold, and carry-off of downed allies when no colonist is within a safe radius.
- **Decapitation** uses drop pods (`DropPodUtility.DropThingsNear`) near the leader or protagonist, never inside a
  room under thick roof (fairness rule 3, checked by `RoofDef.isThickRoof` as the orbital strike permit does).
- **Probing raid** retreats early on purpose and writes the entrances it found to `CrisisState`.
- **Retrieval** is allowed only while `CrisisState` holds a captive or defector; its lord goes for that pawn.
- Every lord job but `FinalAssault` has a reachable retreat toil (fairness rule 6, tested on the state graph).

### 4.5 Orbital strikes

`StrikeRules` (Core) decides: is there a source (a living, conscious Herald with line of sight, or a standing uplink),
is the cooldown over, is the per-raid cap left, and which target (turrets, clustered colonists, power, never the same
cell twice in a row; the siege goal shifts the weights). Game then spawns a `RimChronicles_StrikeMarker` (a thing with
an XML fleck as the targeting laser), sends the letter, and after the delay (6–10 s, a setting) spawns
`ThingDefOf.Bombardment` or `PowerBeam` with `instigator` set to the Herald, as `RoyalTitlePermitWorker_OrbitalStrike`
does. If the Herald dies or loses sight during the delay, the strike is called off (fairness rule 1).

### 4.6 Siege planner

`SiegeSpotSampler` (Game) samples candidate cells around the colony and measures each one's features: distance to the
nearest colony turret and mortar range, the Empire's mortar range, cover cells on the line to the colony, the line to
known entrances (from the probe), and open flanks. `SpotScorer` (Core) weighs the features and ranks the spots;
"RC: Show siege scoring" draws the scores. Vanilla `SiegeBlueprintPlacer` is not used for Empire sieges.

`BuildStages` (Core) holds the five stages; Game stamps each stage's pieces as blueprints and starts the next only when
every blueprint of the current one is built. The siege lord moves to assault or retreat when the builders fall below a
share of the start, depending on the remaining budget.

### 4.7 Captives, intel and defectors

- Empire pawns get high resistance and will by rank at generation. **`RimChronicles_Interrogate`** works like vanilla's
  identity interrogation: a warden job on a schedule, success chance from Social, one intel item per success, each
  success lowering resistance a little.
- **`IntelDef`** (XML): rank that yields it, effect id, expiry. Effects are Core functions on `CrisisState` (delay a
  Herald, reveal a site's defender count, warn of the next operation, reveal a site, reveal the keep's side entrance).
  The records-room piece holds one intel item, taken when a colonist opens it.
- **Defectors:** a captive recruited after interrogation gets a `defector` mark in `CrisisState`. The director's
  minor-plot rules can take them as a protagonist (`Hook:DefectorPlot`, after C11). Retrieval becomes allowed while one
  is held.
- **How captives are treated:** execution, harvesting and enslavement of Empire captives are already tales; the
  snapshot reader gains their pawn kind so the summary can say "an Imperial captive". Release gives a small goodwill or
  a one-time cheaper next operation (code applies it).

## 5. Director, snapshot and prompts

Until C11, `CrisisArc` lists its operations and the fallback picks by weight. The plot book still records each pick
as a `DirectorDecision` (source `Random`), so the decision log shows the crisis from day one.

At C11 (after the director model ships):

- `CrisisArc.Describe` gives each `Operation:*` its `desc`; `SiteArchetypeDef` and `Goal:*` give theirs.
- The snapshot gains a `crisis` block (stage, recent operations, disabled capabilities, captives by rank, defectors,
  live intel), `harness/schema/snapshot.schema.json` and `WorldSummarizer` render it, and the Golden files are updated
  (`UPDATE_GOLDEN=1 dotnet test`, then review). That is a prompt-text change, so the Stage 0 harness runs again
  (rule 8), at zero API spend (no `--judge`, no `claude-ref`).
- `director-gen` gains a `crisis` arc (operations by stage, sites, siege goals, captive threads); the existing arcs'
  cases are unchanged; the test set gets a new version with crisis cases added. Retraining follows the director-model
  plan.

## 6. Settings

Under the mod's settings, a **Crisis** section: crisis on/off (off: Empire raids are vanilla raids), difficulty
multiplier (default 1.0), minimum gap between operations, Citadel map size (default 275), strike delay, and Dev Mode
switches for the overlays. All saved in `RimChroniclesSettings`.

## 7. Fairness rules to tests

| Rule (crisis spec §8) | Test |
| --- | --- |
| 1 Strikes telegraphed, need a source | `StrikeRules` unit tests; in game: a strike with the Herald killed in the delay never lands |
| 2 Heat and psyfocus obeyed | Scorers never offer a cast the view says is unaffordable (unit); `VanillaProvider` refuses when `CanCast` is false (Dev Mode test) |
| 3 No drop pods under thick roof | Drop-cell picker unit test on a grid with roofs; Dev Mode batch of 100 decapitations logs zero violations |
| 4 Every site reachable | Validator unit tests; "RC: Batch sites" 50 seeds per archetype with zero failures past the fallback |
| 5 Sieges built in stages | `BuildStages` unit test; in game: stage N+1 blueprints never exist before stage N is built |
| 6 Every operation can retreat | A test walks each operation's toil graph (Core mirror) and finds a retreat; FinalAssault is the only exception |
| 7 Stage changes announced | `CrisisDriver` sends news on every stage change (unit on `CrisisStages` transitions; news via the existing `NewsDesk`) |
| 8 Keep shield has a reachable relay | Citadel validator: path from the edge to every courtyard relay |
| 9 Ban list | Every unit at every stage spawns with no banned psycast (Dev Mode test); kit lists vs ban list checked at startup |

## 8. Testing

- **Unit tests (Core and Game-free):** piece specs from the XML, rotation, planner and validators, `SeededRng`
  stability, budget math, stages, operation lists and cooldowns, scorers on `BattleView` fixtures, strike rules, siege
  spot scoring, build stages, intel effects, persistence of `CrisisState` (old saves without it load). `LayeringTests`
  keeps Core free of RimWorld and NuGet references.
- **Dev Mode actions:** all of §9 of the crisis spec, in `CrisisDevActions` (rule 7: each feature has one).
- **In-game tests:** one runbook per checkpoint (`docs/crisis-runbook-1.md` …), run on the reference PC; see
  `project-plan.md` for what each covers.

## 9. Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| Psycast defNames or levels differ from the spec's table | Kits and scorers are wrong | C0 fixes the table from the real XML before any kit is written; the startup check disables what's missing |
| Vanilla lord toils don't extend cleanly (sealed classes, private state) | Custom toils from scratch | C0 found them public and unsealed (R22); C6 starts with ProbingRaid, the simplest |
| A 275×275 Citadel with ~60 defenders misses 30 s generation or the tick budget | Finale lags | Measured in C12 on the PC; fallback is lazy ring spawning (crisis spec §4.5), then a smaller default size |
| Save with crisis lords or things loads after the mod is removed | Errors on load | Same as any content mod; documented. Adding the mod mid-game is supported (rule 5) |
| Crisis too hard, or dodged by a weak colony | Fun | Budget inputs are clamped and tested; the balance pass (C14) uses a fixed test colony and the playtest bar |
| Merge drift from `main` while the director track runs | Painful final merge | `main` is merged into this branch after each director milestone lands (see the plan) |
| Option vocabulary change invalidates a trained director | Retraining | Nothing crisis-related reaches the director before C11 (R17) |

## 10. Decisions (2026-11-22)

- **Q1 (R13):** Royalty stays optional. Without it the crisis switches off; the rest of the mod works as before.
- **Q2 (R16):** Dev Mode overlays may use the Unity stub types they need (such as `Color`), like UI code. Add this to
  rule 3 in `CLAUDE.md` and `docs/ENGINE_NOTES.md` when the first overlay lands (C4).
- **Q3 (R5):** a neutral colony that refuses tribute gets probing raids only, until it opposes the Empire.

## 11. Crisis core (C1)

Built in `src/RimChronicles.Core/Crisis/` and `src/RimChronicles.Game/Crisis/`; 33 unit tests in `CrisisTests`.

- **Stages** (`CrisisStages.Explain`) follow §4.1: a refused tribute caps the crisis at Probing until the colony finishes
  a job against the Empire or Act 3 starts (Q3). To know that, `EmpireArc.FinishJob` now records what turned the colony
  against the Empire (`stanceCause`: tribute, final demand or treaty) and the job count at that moment. These are plot
  facts, which the director's snapshot doesn't show, so the director's input is unchanged.
- **Budget** (`ThreatBudget`, `ColonyStrength`): base points by stage 800 / 1,800 / 3,500 / 6,000 at a reference colony
  (score 1,500: about ten fighters in fair gear and a dozen turrets), strength factor clamped to 0.3–1.8 (was 0.6–1.8; War in Heaven spec D15), difficulty =
  storyteller threat scale × the crisis difficulty setting (0.5–2), ×0.6 for a Broken Empire. 300,000 wealth counts for
  less than four fighters. All first guesses for C14.
- **Operations** (`CrisisOperations`): the §5.2 list with the stage each unlocks at, weights and cooldowns. The arc
  offers only operations the game has **registered** (`CrisisDriver.RegisterOperation`), so in C1 it offers nothing; C6
  registers the first ones. Retrieval needs a held captive (C10 fills that in).
- **The `ImperialCrisis` plot** starts when the stage first leaves None and ends with the Empire's story. Each operation
  is its beat, so the plot book's two-day spacing between any two beats applies to it too.
- **Kept away from the director (R17):** the arc is registered with `useDirector: false`, so `PlotDriver` always picks by
  weight (source `Random`), never calls the model and never records a training case for it; `WorldReader` leaves the
  crisis plot out of `activePlots`. Stage-change **news** does reach the news log, as fairness rule 7 needs.
- **Settings:** Imperial crisis on/off (default on, under the major plot), crisis difficulty, and the minimum days
  between operations (default 8, 3–30).
- **Dev Mode:** "Crisis: show state" shows the stage and why, colony strength, today's budget, the operations allowed
  (marking those not built yet) and the operation history.
- **Saved** as `crisis` in the game component; a save without it loads as no crisis.
- Not checked in game; it joins checkpoint 1.
