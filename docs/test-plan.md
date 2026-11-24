# Test plan: everything left to run on the PC

Every outstanding PC and in-game test for the **director model (D)** and the **Imperial crisis (C)**, in the order to
run them. Tick the boxes as you go. Each part ends with what to send Claude Code.

**Order and why:**

1. **Part A: director model**, on `main`. A training run needs the whole GPU, so nothing else runs alongside it.
2. **C11**, the director wiring. Claude Code builds it after Part A, once a director model ships.
3. **Part B: crisis checkpoints 1–4**, on `crisis-combat-locations`, and runbook 4's C11 part once C11 is built.
4. **C14**, the balance pass and proof run, last of all. After that the crisis merges into `main`.

PowerShell for `dotnet`, `ollama` and `git`; WSL2 bash for Python. The repo is at
`C:\RimworldMods\RimChronicles\RimChronicles` (WSL2: `/mnt/c/RimworldMods/RimChronicles/RimChronicles`).

---

## Part A: the director model (`main`)

### A1. Finish the E2B training run (running now)

- [ ] Let it finish: about 3½ hours from the start; steps at about 7–8 s; a check every 100 steps.
- [ ] Wait for the log's last line: `saved the best LoRA (step N) … val answers valid …, matching the teacher …`.
- [ ] Send Claude Code that line and `~/rimchron-runs/director-e2b/progress.csv` (WSL2:
      `cat ~/rimchron-runs/director-e2b/progress.csv`).
- [ ] Merge PR #109 (the row-length fix) afterwards, so later runs print real token lengths.

**If it stops** with "STOPPING: steps now take …": VRAM spilled into system RAM. Task Manager → Performance → GPU
shows Shared GPU memory in use. Send Claude Code the log.

### A2. Score the prompted baseline (`base-v1`)

Needed once, so the trained models have something to be compared against. About 1¾ hours of GPU.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet run --project harness/RimChronicles.Eval -- director-gen --seed 9001 --count 500
dotnet run --project harness/RimChronicles.Eval -- director-report --models gemma4-e2b,gemma4-e4b,gemma4-12b --name base-v1
```

- [ ] `director-gen --seed 9001` rebuilds the frozen test cases. Its manifest may show a different sha256 because of
      Windows line endings; that's harmless, so discard it with `git checkout training/data`.
- [ ] `harness/reports/base-v1.md` exists. Each model shows top-1, validity and the always-Wait row.
- [ ] Commit `harness/reports/base-v1.md` and `.json`, push, and tell Claude Code.

### A3. Export E2B, load it, score it

```bash
# WSL2
cd /mnt/c/RimworldMods/RimChronicles/RimChronicles/training
uv run python -m rimchron_train export --config config/director-e2b.yaml
```

```powershell
# PowerShell
cd C:\RimworldMods\rc-models\director-e2b
ollama create rimchron-director-e2b -f Modelfile.merged.q4_k_m
cd C:\RimworldMods\RimChronicles\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- director-report --config training/models.director.json --models gemma4-e2b,director-e2b --base gemma4-e2b --name director-e2b-v1
```

- [ ] The export writes Q4_K_M and Q8_0 GGUFs, each with a Modelfile, to `C:\RimworldMods\rc-models\director-e2b`.
- [ ] `ollama list` shows `rimchron-director-e2b`.
- [ ] The report `harness/reports/director-e2b-v1.md` shows the fine-tune against its base and the ship bars: it
      beats always-Wait, and choice validity holds.
- [ ] Commit the report, push, and tell Claude Code which bars passed. **If a bar is missed**, Claude Code plans D10
      (DPO) before you go on.

### A4. Blind review of E2B (D9)

About 30 minutes of reading, with Ollama running. The answers come from the A3 cache.

```powershell
dotnet run --project harness/RimChronicles.Eval -- director-review --config training/models.director.json --models director-e2b
start harness\reports\review-director-e2b\review.html
```

- [ ] For each of the 50 decisions, click **A is better**, **Same** or **B is better**. Judge the decision, not the
      wording.
- [ ] Press **Download answers**, then:
      ```powershell
      dotnet run --project harness/RimChronicles.Eval -- director-review --config training/models.director.json --models director-e2b --answers $env:USERPROFILE\Downloads\review-answers.json
      ```
- [ ] `review.md` says the model was picked or tied in **at least 40%** of cases (the ship bar).
- [ ] Commit `harness/reports/review-director-e2b/` and push.

### A5. Train, export, score and review E4B

Same as A1, A3 and A4 with `e4b`. The E4B conversion needs the WSL memory setting from D0 (24 GB).

- [ ] Train (WSL2): `uv run python -m rimchron_train sft --config config/director-e4b.yaml 2>&1 | tee ~/sft-director-e4b.log`.
      Close RimWorld and `ollama stop` any model first; it needs about 9 GB free.
- [ ] Export: `uv run python -m rimchron_train export --config config/director-e4b.yaml`.
- [ ] Load: `cd C:\RimworldMods\rc-models\director-e4b`, then
      `ollama create rimchron-director-e4b -f Modelfile.merged.q4_k_m`.
- [ ] Report: `director-report --config training/models.director.json --models gemma4-e4b,director-e4b --base gemma4-e4b --name director-e4b-v1`.
- [ ] Blind review: `director-review --config training/models.director.json --models director-e4b`, then
      `--answers …`, as in A4.
- [ ] Commit both, and send Claude Code the progress CSV, the log's end, and which bars passed.

### A6. The director model in the game (D7)

- [ ] `git pull`, `dotnet build RimChronicles.slnx`, then start RimWorld with Development mode on.
- [ ] **Options → Mod settings → Rim Chronicles → Separate director model**:
  - [ ] **Director model**: pick `rimchron-director-e2b` (it sets the Minimum prompt tier itself); address and key blank.
  - [ ] Pass when no orange warning shows.
- [ ] **Test connection**: it tests the main model, then the director, and both answer.
- [ ] In a Dev Mode game, **Rim Chronicles → Plot: advance beat** on a plot, then **Plot: director log**. The
      decision's source is `Model`, and its reason reads sensibly.
- [ ] Pick **Other**, name `rimchron-director-e4b`, tier **Recommended**, and repeat the test connection and one decision.

### A7. The 200-day check (D11), recording real decisions (D3)

One long Dev Mode game, run at speed 3 or 4 in the background. Keep the director that did best in A3–A5.

- [ ] Settings:
  - [ ] the director model from A6;
  - [ ] **Dev: record director decisions for training** on;
  - [ ] all three story arcs on;
  - [ ] the major plot on.
- [ ] Start a new colony (any storyteller) and play or fast-forward 200 in-game days. Save every 20 days.
- [ ] Every 50 days, open **Plot: director log** and read the last decisions. Pass when they read like a sensible
      story outline:
  - [ ] waits after a raid or a death;
  - [ ] no repeated beats back to back;
  - [ ] reasons that match the colony.
- [ ] At the end, import the recorded cases:
      ```powershell
      dotnet run --project harness/RimChronicles.Eval -- director-import --from "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimChronicles\director-cases"
      ```
- [ ] Commit only the import manifest (the cases stay local).
- [ ] Send Claude Code the director log as text (copy from the window) and anything odd. **This closes the director
      track; C11 starts.**

---

## Part B: the Imperial crisis (`crisis-combat-locations`)

### Setup, before each session

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git fetch
git checkout crisis-combat-locations
git pull
dotnet build RimChronicles.slnx
```

Start RimWorld with **Development mode** on and Royalty active. Every tool below is in the Debug actions menu (the
first button on the dev bar), under **Rim Chronicles**. Use **new test colonies**: crisis saves don't load cleanly on
`main`. Afterwards: `git checkout main`, `git pull`, `dotnet build RimChronicles.slnx`.

Write results in `docs/SPEC-crisis-combat-and-locations/crisis-results-N.md`, or tell Claude Code, with each check's
number, pass or fail, what happened, and any red errors in the log.

### B1. Checkpoint 1: crisis core, pieces, units, psykers (C1–C4). About 1½ hours.

**Crisis core**

- [ ] 1. Mod settings: under the major plot there's **Imperial crisis** (on), **Crisis difficulty** 1.0×, **At least
      8 days between Imperial operations**, the strike delay, **Imperial Citadel map** 275×275, and the lazy-rings box.
- [ ] 2. New colony, **Crisis: show state**: "Stage: none (the Empire isn't awake)", budget 0.
- [ ] 3. **Empire: trigger awakening**, **Empire: end stirring now**, and wait for the envoy (**Increment time**
      helps). Choose **Stand against it**.
- [ ] 4. Wait for "The Empire marches", then **Crisis: show state**: stage **probing**, an **Imperial scouts** letter
      and news item, and Operation:ProbingRaid allowed.
- [ ] 5. **Empire: expansion tick** twice, wait a day: stage **reclamation**, "The Empire escalates" letter.
- [ ] 6. Spawn a few turrets and wait a day: the budget goes up with colony strength (factor 0.6–1.8).
- [ ] 7. Save, quit, reload, then **Crisis: show state**: same stage, no errors. Save this colony as
      **"crisis-test-1"**.

**Pieces**

- [ ] 8. **Crisis: check pieces**: "16 pieces loaded", none left out, a picture of each.
- [ ] 9. **Crisis: stamp piece** → each piece, on open ground: each spawns and "the footprint matches the planner's";
      stone or steel walls, cloth sandbags.
- [ ] 10. **Crisis: turn pieces**, then stamp the mortar pit and barracks at all four facings: each matches, and the
      mortar's crew cell is open.
- [ ] 11. Stamp a piece on rock: refused, nothing spawns.
- [ ] 12. **Crisis: stamp piece as Imperial blueprints** → mortar pit: Empire blueprints that your colonists don't
      build.

**Units**

- [ ] 13. **Crisis: check units**: "Every unit at every stage has exactly its kit."
- [ ] 14. **Crisis: spawn unit** → each unit (soldiers and adepts at stage 1, Mind Adept at 3, High Stellarch at 4).
      Each has fitting gear and only its kit.
- [ ] 15. An adept has a psylink at its stage's level and is **psychically hypersensitive**; stage 3–4 units have
      bionics.

**Psykers**

- [ ] 16. **Crisis: check psycasts**: 14 scorers enabled; only Farskip has no scorer.
- [ ] 17. **Crisis: psyker debug on/off** (on), then **Crisis: spawn psyker squad** → stage 2, about 40 cells from
      armed colonists in cover. Text floats over each adept as it decides.
- [ ] 18. **Crisis: psyker report** after the fight: each adept cast at least once; **Casts the scorers didn't
      choose: 0**.
- [ ] 19. Keep fighting until adepts run hot: they hold and shoot instead, and none goes past its heat limit.
- [ ] 20. Stage 3 squad near grouped colonists: Berserk or a pulse lands on a group, never on one with Imperials in it.
- [ ] 21. A Praetorian in front of a turret with a Ward Adept near: Wallraise, Bullet Shield or smoke.
- [ ] 22. A stage 4 squad fights for 2–3 in-game hours: no errors, the speed holds, still 0 unchosen casts.

### B2. Checkpoint 2: strikes, operations, sieges (C5–C8). About 2½ hours.

**Test colony:**
- a stone wall with 2–3 doors;
- 4–5 mini-turrets and a mortar with shells;
- 6–8 armed colonists, one of them the ideoligion's leader, one a doctor;
- a field outside the wall and a freezer inside.

Reach stage 3 as in B1 steps 3–5, plus another expansion tick. Save as **"crisis-test-2"** and fire each operation
from that save.

**Orbital strikes**

- [ ] 1. Spawn a Herald and 2 Praetorians who can see a turret or colonists: within a minute, "Orbital strike called";
      the target flashes; it lands about 8 s later.
- [ ] 2. **Crisis: call orbital strike**, then down the Herald during the countdown: "called off", nothing lands.
- [ ] 3. Colonists under a mountain roof: never targeted (**Crisis: strike state** marks them "under mountain").
- [ ] 4. Let a raid strike until "this raid's N strikes are spent".
- [ ] 5. Save during a countdown and reload: the strike still lands, or is called off.

**Probing raid and decapitation**

- [ ] 6. **Crisis: fire operation** → ProbingRaid → stage 1: an "Imperial probe" letter, and Praetorians attack.
- [ ] 7. After two hours or a quarter lost, it leaves. **Crisis: show state** shows the outcome and the doors it found.
- [ ] 8. A downed probe soldier far from your colonists is carried off; one next to your colonists is left.
- [ ] 9. Decapitation → stage 3 with the leader outdoors: pods land 3–12 cells away and the squad goes for the leader.
- [ ] 10. Leader indoors under a roof: pods land outside only.
- [ ] 11. Adepts stay near Praetorians; the squad focuses one colonist at a time, medics first.
- [ ] 12. Down the leader (or make peace in Dev Mode): the squad pulls out.
- [ ] 13. Save mid-operation and reload: it carries on with no errors.

**Breach and psychic assault**

- [ ] 14. Breach → stage 2: grenadiers blast a wall, EMP grenades stop turrets, and the rest follow.
- [ ] 15. Hurt it about 40%: it falls back, then reinforcements drop in and it attacks again.
- [ ] 16. PsychicAssault → stage 2, then stage 3: about as many adepts as Praetorians; Mind Adepts only at stage 3.
- [ ] 17. With psyker debug on, adepts cast throughout, including during the breach.

**Sieges**

- [ ] 18. **Crisis: show siege scoring**: scores around the colony, `x` in turret range or on rock, one **BEST**; the
      window lists the top five and the reasons spots were ruled out.
- [ ] 19. After a probing raid, scoring shows "door" marks and an "entrance" part.
- [ ] 20. **Crisis: fire siege with goal** → Goal:BreakWalls → stage 3: an "Imperial siege" letter naming the goal;
      the force walks to the BEST spot.
- [ ] 21. Leave it alone and watch the stages go up, each with its message: sandbags and barracks, then mortar pits,
      uplink, shield and relay, beacon. No stage's blueprints appear before the last stage is built.
- [ ] 22. Mortars up: builders man them, shelling starts, and shells and meals are resupplied.
- [ ] 23. Uplink up, Herald down: strikes still come ("An Imperial uplink…"), aimed at walls and turrets;
      **Crisis: strike state** shows the goal and "Uplink standing: yes".
- [ ] 24. Shield up: your mortar shells stop at its bubble; EMP knocks it out.
- [ ] 25. An adept near the relay has **psychic relay link**, which fades about 10 s after leaving.
- [ ] 26. Beacon lit: reinforcements drop in a quarter day later, up to 3 times.
- [ ] 27. From the save, kill more than half the builders early: "lost most of its builders… it's coming in".
- [ ] 28. From the save, kill half the force first: it retreats carrying its downed; blueprints vanish; built pieces
      stay neutral; "Uplink standing: no".
- [ ] 29. Leave a siege alone for 2 days: "done digging in: it's coming in".
- [ ] 30. Goal:Starve hits fields, freezer and power; Goal:KillLeader aims at the leader outdoors.
- [ ] 31. Save at camp stage 2–3 and reload: the camp carries on.
- [ ] 32. On a map with no qualifying spot (turrets everywhere): "no siege camp spot"; nothing spawns.

### B3. Checkpoint 3: sites and captives (C9–C10). About 2 hours.

Use "crisis-test-2", or B2's colony plus a prison room and a warden with good Social. Save as **"crisis-test-3"**.

**Sites**

- [ ] 1. **Crisis: batch sites**: each archetype "50/50 pass on the first seed", 0 fallbacks.
- [ ] 2. **Crisis: generate site** → ForwardOutpost → seed 1:
  - [ ] a walled outpost with one gate and sandbags inside;
  - [ ] a command room with a comms console deepest in, a barracks, a power yard;
  - [ ] guards; the log says "attempt 1".
- [ ] 3. A road runs from the gate to the map edge; no room is shut off; the console and turrets are powered.
- [ ] 4. Destroy the console: "Outpost comms destroyed".
- [ ] 5. PsychicRelay seed 2 and OrbitalUplink seed 3:
  - [ ] two gates, powered turret nests, the objective deepest in;
  - [ ] adepts guarding the relay, heralds guarding the uplink.
- [ ] 6. Destroy the relay core: "Psychic relay destroyed". Back home, fire PsychicAssault at stage 3: an arriving
      adept has **relay severed**.
- [ ] 7. Destroy the uplink: "Orbital uplink destroyed". A Herald at home calls no strike; strike state says the
      colony knocked out the uplink.
- [ ] 8. The same archetype and seed twice: the same layout.
- [ ] 9. The Empire plot's **Empire:SabotageImperial** job offers "Destroy the Imperial …". Accept, travel there,
      destroy the objective: the quest succeeds and pays.
- [ ] 10. Save on a site map and reload: destroying the objective still sends the letter.

**Captives**

- [ ] 11. **Crisis: spawn captive** → imperial herald in the prison: a prisoner, a message on how to question them,
      resistance about 45–60.
- [ ] 12. Tick **interrogate (Imperial intel)** in the prisoner tab: the warden interrogates, the social log shows it,
      and resistance drops.
- [ ] 13. A success sends "Intel: uplink codes" or "next operation", and it's listed in **Crisis: show state**.
- [ ] 14. After 2 successes the warden stops: "told all they know".
- [ ] 15. A non-Imperial prisoner with the box ticked is skipped: "knows nothing about the Empire".
- [ ] 16. **Crisis: grant intel** → next operation: a forecast appears, and the crisis next fires that operation, not
      before its day.
- [ ] 17. Grant uplink codes: the first strike called fails ("stolen uplink codes scrambled it"); the next is real.
- [ ] 18. Grant relay location: a relay site appears on the world map. The next PsychicAssault the crisis fires has
      about half the adepts.
- [ ] 19. With a captive held, fire Retrieval → stage 2: pods land near the prison, soldiers dig in, the captive is
      freed and leaves with them.
- [ ] 20. Interrogate a captive once, then recruit them: "An Imperial defects", a news item, listed as a defector.
- [ ] 21. With only the defector held, fire Retrieval: the squad goes for them and carries them off once downed.
- [ ] 22. Release a captive: a goodwill message and "next budget x0.85", back to x1.00 after the next operation.
- [ ] 23. Execute one (or harvest an organ, or enslave one): the treatment count goes up.
- [ ] 24. Bring a site's Imperial dispatch home and have a colonist read it: "Intel: …".
- [ ] 25. Save with captives, a defector and intel, and reload: all still listed.

### B4. Checkpoint 4, part one: Citadel and finale (C12–C13). About 1½ hours.

Use "crisis-test-3" at stage 3 or 4.

**Citadel** (this also decides C12's done-when: generation under 30 s and steady at speed 1)

- [ ] 1. **Crisis: batch Citadels**: "20/20 pass", under a second each.
- [ ] 2. **Crisis: generate Citadel** → standard: **note the seconds** in the message, which must be **under 30**.
      It's a 275×275 map with three walled rings, the courtyard's two relay chambers and uplink, and the throne room
      deepest in the keep.
- [ ] 3. Speed 1 for a minute with the map in view: no stutter. Note the speed-3 TPS (Dev Mode's TPS readout).
- [ ] 4. If 2 or 3 fails: turn on **Citadel: spawn inner defenders only as you reach them** and repeat. Note which
      setting passes.
- [ ] 5. Colonists arrive at the edge: "The Imperial Citadel" letter.
- [ ] 6. A colonist enters the outer works: "courtyard garrison is moving out to meet you"; the keep's guard stays
      (**Crisis: Citadel state**).
- [ ] 7. A keep wall takes a tenth of the damage. Destroy both relays: "keep's shield is down", and full damage again.
- [ ] 8. An adept in the keep shows **psychic relay link** and high psyfocus while the shield is up.
- [ ] 9. Side entrance known: the keep has a second gate on the far side, and the letter says so.
- [ ] 10. No relays left: no relay chambers, "unshielded", full damage.
- [ ] 11. In a normal game, destroy a relay site, then attack the capital: the Citadel has one courtyard relay.
- [ ] 12. Save on the Citadel and reload: shield, rings reached and held garrisons are kept.

**Finale**

- [ ] 13. **Crisis: fire operation** → FinalAssault: only "stage 4" is offered. The assault is led by the High
      Stellarch, with every unit type.
- [ ] 14. Hurt them past half: they don't retreat.
- [ ] 15. Bring the Stellarch below about 40% (or down it): "skips away", and it leaves; **Crisis: Stellarch state**
      shows the escape used.
- [ ] 16. Generate a Citadel: the same Stellarch is in the throne room.
- [ ] 17. Hurt it again: no second escape. Kill or capture it: a letter and news; fate dead or captured; a new
      FinalAssault has no Stellarch.
- [ ] 18. In a game storming the capital (**Act3:StormTheCapital**), kill the Stellarch: the capital quest succeeds
      and the Empire plot gives the **Resistance ending**.
- [ ] 19. From a save with the escape unused, hurt it in the Citadel: it skips deep into the keep and fights on.
- [ ] 20. Save with the Stellarch off-map after its escape, reload, and fire FinalAssault: the same one leads it and
      can't escape again.

### B5. Checkpoint 4, part two: C11 (after Claude Code builds it)

Claude Code adds these steps to `crisis-runbook-4.md` with C11. Expect:
- the director picking Imperial operations, siege goals and sites (the log's source is `Model`);
- a free harness run;
- the crisis cases in `director-gen`.

---

## After all of it

- **C14, the balance pass and proof run:** Claude Code writes its part of runbook 4 from your B1–B5 notes. It's a
  late-game run from stage 1 to the Citadel, tuned until the bar is met:
  - stage 3 costs real losses but is winnable without save-scumming;
  - stage 4 and the Citadel need preparation (relays, uplinks, intel first).
- **Merge:** after C14, `crisis-combat-locations` merges into `main` (with the README update, PR #110).
- **Not in this plan:** the Stage 5 proof run (`docs/stage5-runbook.md`, M7) has no results file yet. Run it if it
  hasn't been run.
