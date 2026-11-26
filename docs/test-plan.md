# Test plan: everything left to run on the PC

Updated 2026-11-26. Every outstanding PC and in-game test, in the order to run them. Tick the boxes as you go. Each
part ends with what to send Claude Code.

**Where things stand:**

| Track | State |
| --- | --- |
| Director model (D0–D11) | Done 2026-11-24. E2B v3 and E4B v1 shipped as presets; record below |
| Imperial crisis (C0–C14) | Done 2026-11-24, merged into `main` (#93); record below |
| War in Heaven director retrain (W13) | E4B passed (`director-e4b-war-v2`, review 96%, #157) and is the E4B preset (#158). E2B failed (`director-e2b-war-v2`); its retrain is on hold, with hand labels ready (#159) |
| War in Heaven game (W2–W6) | Built on `war-in-heaven`, not yet checked in game: Parts 2 and 3 |
| War in Heaven game (W7) | The Choir's crisis is built (2026-11-26): its operations, commander AI, cluster drops, burrowing and the Hum. Checked in game at checkpoint 2 (`war-runbook-2.md`, W7.1–W7.17) |
| War in Heaven game (W8–W11) | Not built yet: the cluster siege, the Choir's sites, the Cathedral map, the War in Heaven plot and the endings' screens. Checkpoint 2's rest and checkpoint 3 come when they land |

**Order:**

1. **Part 1: the E4B director in a War in Heaven game.** What you're playtesting now.
2. **Part 2: checkpoint 1 (W2–W5)**: sides, the Hollow Choir, waking. Much of it happens along the way in Part 1;
   tick what you saw there.
3. **Part 3: checkpoint 2, first part (W6)**: the summons.
4. **Part 4: fixes since C14**: the crisis panic-flee fix, in any crisis fight.
5. **Later:** the E2B war retrain (v3), checkpoint 2's rest (W7–W9), checkpoint 3 (W10–W11), and the W14 proof run.

PowerShell for `dotnet`, `ollama` and `git`; WSL2 bash for Python. The repo is at
`C:\RimworldMods\RimChronicles\RimChronicles` (WSL2: `/mnt/c/RimworldMods/RimChronicles/RimChronicles`).

---

## Setup, before each session (`war-in-heaven`)

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git fetch
git checkout war-in-heaven
git pull
dotnet build RimChronicles.slnx
```

- [ ] The build ends with `Build succeeded` and copies the DLL into `1.7\Assemblies`.
- [ ] RimWorld: **Royalty and Biotech** active, **Development mode** on (Options → General).
- [ ] Ollama is running, and `ollama list` shows `rimchron-director-e4b`. It must be the War in Heaven model, copied
      over the shipped name once (do it again if in doubt; it's quick):
      ```powershell
      ollama cp rimchron-director-e4b-war rimchron-director-e4b
      ```
- [ ] Use **new colonies**. Saves from this branch may not load cleanly on `main`.

Every Dev Mode tool below is in the Debug actions menu (the first button on the dev bar), under **Rim Chronicles**.
To go back to `main` afterwards: `git checkout main`, `git pull`, `dotnet build RimChronicles.slnx`.

**Reading the director log.** **Plot: director log** opens a letter with the last 25 decisions, each as
`Day N, plot-id: Choice (Source)`, then the reason and the options it had. The source is `Model` (the director model
chose), `ModelRetry` (it chose on a second try), `Random` (a weighted pick: no model, or the model isn't trained on
this plot), or `Only` (a single option, no choice to make).

---

## Part 1: the E4B director in a War in Heaven game

What changed: the director's E4B preset is now the model retrained on the Imperial crisis, the Hollow Choir and the
War in Heaven. With it, the game lets the model make the crisis's decisions and the Choir plot's, which were weighted
picks before. E2B still uses the old model and the old rules, which makes it the comparison. About 2 hours for 1a–1d,
then 1e runs as long as you like.

### 1a. Settings and connection (5 minutes)

- [ ] **Options → Mod settings → Rim Chronicles**:
  - [ ] **Story**: the major plot on, **Imperial crisis** on, **The Hollow Choir** on, all story arcs on.
  - [ ] **Advanced: separate models** → **Show a separate prose or director model** → **Director model**: pick
        `rimchron-director-e4b`. No orange warning shows.
  - [ ] **Dev: record director decisions for training** on (below the advanced part; Dev Mode only).
- [ ] **Test connection**: the main model answers, then the director.

### 1b. The Hollow Choir's plot is the model's now (about 40 minutes)

New colony (any storyteller, 5–8 colonists).

- [ ] 1. **Hollow Choir: wake now**: "The Hollow Choir awakens" letter; the faction and its settlements appear near
      the colony (Factions tab, world map).
- [ ] 2. Let a few days pass (Dev Mode time skip): a "Chorus envoy" letter asks the colony's stance; the Chronicle tab
      shows "The Hollow Choir". Answer **Stand with the Choir**.
- [ ] 3. **Plot: director log**: the Choir plot's (`hollowchoir-…`) decisions show source **`Model`** (or
      `ModelRetry`), never `Random`, each with a reason. Note one or two reasons.
- [ ] 4. Let 10–20 days pass. The Choir's jobs (escort its envoys, sabotage a settlement it means to take) come as
      quests; the log shows the director choosing them, or waiting, with reasons that fit the colony (a raid or a
      death recently: it waits, or picks something gentle).
- [ ] 5. **Hollow Choir: grant rank favor** until a colonist is Choirmaster ("rise in the Chorus" letters), then
      **Plot: advance beat** on the Choir plot until it reaches Act 3. Once it's offered and the colony is steady, the
      director should soon choose **the First Voice's offer** for that colonist (a letter: "Join the Chorus" or "Not
      yet"). Answer **Not yet** to keep the game going.
- [ ] 6. **Compare with E2B:** set the director to `rimchron-director-e2b`, let a Choir decision happen (or **Plot:
      advance beat**), then **Plot: director log**: that decision shows `Random`. Set it back to E4B.
- [ ] 7. Save, reload, and let another decision happen: no errors; the log keeps its earlier entries.

### 1c. A colony against the Choir, and the Choir breaking (about 30 minutes)

New colony, 5 or more colonists.

- [ ] 1. **Hollow Choir: wake now**, wait for the envoy, answer **Stand against it**: defend-ally and sabotage jobs
      against the Choir; the Choir is hostile.
- [ ] 2. **Plot: advance beat** on the Choir plot to Act 3. If the letter says the Choir is **faltering** ("fell
      back after heavy losses"), the plot is in its Broken beat. If it says ascendant, try again in another colony, or
      play on and finish jobs against the Choir first (its losses decide it).
- [ ] 3. With the Choir breaking and the colony steady (no raid or death in the last 3 days, mood not desperate), the
      director should offer **the assault on the Choir's Cathedral** (a quest) within a few decisions. With a colony
      that's reeling or small (4 or fewer), it should offer **the treaty** instead. It shouldn't keep waiting: this is
      what the retrain fixed (E2B waits here two times in three).
- [ ] 4. Don't accept the Cathedral quest yet: the Cathedral's own map comes with W9.

### 1d. The Imperial crisis is the model's now (about 40 minutes)

New colony with a stone wall, a few turrets and 6–8 armed colonists (as checkpoint 2 of the crisis).

- [ ] 1. **Empire: trigger awakening**, **Empire: end stirring now**, wait for the envoy, answer **Stand against
      it**. Wait for "The Empire marches".
- [ ] 2. **Crisis: show state**: the line "Decided by" says **"Decided by: the director model"**. (With E2B as director it
      says "weighted pick": check that once, then set E4B back.)
- [ ] 3. **Crisis: decide now**: an operation is chosen with source `Model` and a reason that fits the crisis stage
      (probing raids while the Empire tests the colony; no operation the colony just faced).
- [ ] 4. **Crisis: ask director for a siege goal**: a `Goal:*` pick with a reason. Small colony (5 or fewer): the
      leader; strained: starve it out; larger and steady: the walls; colony reeling: the walls, sparing people and food.
- [ ] 5. **Crisis: ask director for a site**: a `Site:*` pick. Liberating a settlement the Empire holds when that's on
      offer; the forward outpost when the colony is reeling; otherwise what is hurting it (the relay after psychic
      assaults, the uplink in open war or after a siege).
- [ ] 6. **Empire: expansion tick** twice, then let the crisis run 10–20 days at speed 3: each operation it fires is
      in the director log as `Model`, and a siege it starts logs a `Goal:*` follow-up that matches its letter's aim.
- [ ] 7. Save, reload, **Crisis: decide now** again: no errors.

### 1e. Free play: a long game with the E4B director

One or two long games, one per side if you have time. Play normally, or at speed 3–4 with Dev Mode to hurry the
slow parts. Save every 20 days.

- [ ] Every 20–30 days, open **Plot: director log** and read the newest decisions. Pass when they read like a sensible
      story outline:
  - [ ] after a raid on the colony or a death in the last 3 days, or with mood desperate: Wait, or relief (a treaty,
        the First Voice's offer), never a raid, a demand or an assault;
  - [ ] a beat that has waited 25 days or more moves on (unless the colony is reeling);
  - [ ] no beat repeated back to back;
  - [ ] reasons that name only people, places and events from this game, and state no facts that didn't happen.
- [ ] The minor plots (Nemesis, Lost Family, Silent Press) and the Empire's plot still behave as before.
- [ ] Note anything that feels wrong as a story: too quiet, too relentless, a letter that doesn't fit.
- [ ] Every 50 days, **Plot: check beat spacing**: no two beats less than two days apart.

**What to send Claude Code:** the director log as text (copy it from the letter) at a few points, every reason that
looks wrong (with the day), and anything else odd. At the end, import the recorded cases (they stay on the PC):

```powershell
dotnet run --project harness/RimChronicles.Eval -- director-import --from "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimChronicles\director-cases"
```

---

## Part 2: checkpoint 1, sides, the Hollow Choir and waking (W2–W5)

The full table, with every step's pass condition, is `docs/SPEC-war-in-heaven/war-runbook-1.md`; the boxes below
follow it. About 2 hours. Write what you find in `docs/SPEC-war-in-heaven/war-results-1.md`, or tell Claude Code, with
each step's number, pass or fail, what happened, and any red errors in the log.

### W2: allegiance

- [ ] W2.1 A save from `main` with the Empire plot under way: it loads on this branch with no errors; **War in Heaven:
      show allegiance** shows the plot's Empire stance, cause `plot`. (Back up the save first.)
- [ ] W2.2 New colony: **Empire: trigger awakening**, answer the envoy "Stand with the Empire", let a day pass:
      `empire: ally`, side `empire`.
- [ ] W2.3 **War in Heaven: set side** → Independent: `empire: oppose`, side `independent`, cause `dev`; the Empire
      turns hostile once its plot is past the envoy.
- [ ] W2.4 **Empire: set stance** → ally, let a day pass: the allegiance follows the plot back to `empire: ally`.
- [ ] W2.5 Save, reload: the same stances and log.

### W3: the Hollow Choir's faction

- [ ] W3.1 New colony: no Hollow Choir in the Factions tab or on the world map.
- [ ] W3.2 **Hollow Choir: create faction**: listed, leader titled First Voice, hostile to the Empire; about four
      settlements 8–30 tiles away, plus one elsewhere.
- [ ] W3.3 **Hollow Choir: raid (1,500 points)** while it isn't hostile: mechanitors and mechs arrive and hold their
      ground; the message counts both.
- [ ] W3.4 Goodwill to -100 (Factions tab, Dev Mode buttons), raid again and fight a day: they attack together; no
      errors, no mech stops for lack of power or goes feral.
- [ ] W3.5 Save, reload, create faction again: the same faction; no second Choir.

### W4a: the Choir's saga

- [ ] W4a.1 Mod settings → Story: **The Hollow Choir** sits under the major plot, on by default.
- [ ] W4a.2 New colony, **Hollow Choir: show**: Running yes, State Dormant, no faction.
- [ ] W4a.3 **Hollow Choir: trigger stirring**, skip 10–20 days: news of a hum at night, then rumours of the hum and of
      mechlink strangers. No faction yet.
- [ ] W4a.4 **Hollow Choir: wake now**: "The Hollow Choir awakens", the faction and settlements; **show**: Awakened,
      next expansion about a week on.
- [ ] W4a.5 Another colony: **War in Heaven: set side** → empire before waking the Choir: it starts hostile.
- [ ] W4a.6 **Hollow Choir: expand now** a few times: settlements change hands or burn, with news lines.
- [ ] W4a.7 New colony with a mechanitor colonist (install a mechlink), past day 30: the Choir stirs on its own;
      **show** says a mechanitor drew it.
- [ ] W4a.8 The Hollow Choir setting off, or no Biotech: **show** says it isn't running; nothing happens.
- [ ] W4a.9 Save and reload in each state: state, log and faction survive.

### W4b: the Choir's plot and ranks

- [ ] W4b.1 **Hollow Choir: wake now**, a few days: "The Hollow Choir" in the Chronicle tab; a "Chorus envoy" letter.
- [ ] W4b.2 "Stand with the Choir": the Choir marches; escort or conquest quests; **show allegiance**: side `choir`,
      Empire `oppose`.
- [ ] W4b.3 Finish a Choir job (or **Hollow Choir: grant rank favor** three times): a "rise in the Chorus" letter;
      at 12 favor (Choirmaster) the First Voice's offer can come in Act 3.
- [ ] W4b.4 Another colony, "Stand against it": defend-ally and sabotage jobs against the Choir; the Choir is hostile.
- [ ] W4b.5 Another colony, "Keep out of it": a tribute letter with "Join the Chorus" as the third choice.
- [ ] W4b.6 **Plot: director log**: with the E2B preset, Choir decisions are `Random`; with E4B, `Model` (Part 1b
      covers this: tick it if you did that).
- [ ] W4b.7 Save, reload mid-plot: the plot, its facts and the ranks survive.

### W5: cross-waking, the ship launch and the Royal Ascent

- [ ] W5.1 New colony: **Empire: trigger awakening**, **Empire: end stirring now**, **Empire: expansion tick**, a
      day: the Choir wakes at once; **Hollow Choir: show** says the Empire's attack woke it.
- [ ] W5.2 New colony: **Hollow Choir: wake now**, **Hollow Choir: expand now**, a day: the Empire wakes at once;
      **Empire: show state** says the Choir woke it.
- [ ] W5.3 New colony, both asleep: build and start a ship reactor (Dev Mode: spawn the ship parts): the Empire wakes,
      goes to war and raids; the next day the Choir wakes too.
- [ ] W5.4 New colony with a Royal title: accept the Royal Ascent (or **Hollow Choir: react to the Royal Ascent**):
      the Choir wakes, goes to war and raids, again every 4 days while the ascent runs; the Empire wakes after the
      first raid.
- [ ] W5.5 As W5.4 but side with the Choir first: the Choir wakes and doesn't raid.

---

## Part 3: checkpoint 2, first part: the summons (W6)

`docs/SPEC-war-in-heaven/war-runbook-2.md`. About 1 hour. Write results in `war-results-2.md` or tell Claude Code.
Colony with a mechanitor colonist (install a mechlink) and two mechs it controls.

- [ ] W6.1 **Empire: trigger awakening**, **Empire: end stirring now**, side with the Empire (the envoy, or **War in
      Heaven: set side** → empire), a day (or **War in Heaven: send a summons now**): "The Empire's summons: {name}",
      naming the colonist and what refusing means.
- [ ] W6.2 Accept: an Imperial shuttle lands; the colonist boards; it leaves.
- [ ] W6.3 Wait 5 days (or **War in Heaven: return the summoned colonist now**, then an hour): the colonist comes
      back as a psycaster (level 1 with a basic mechlink, higher with more bandwidth), with no mechlink, and a letter.
- [ ] W6.4 The two mechs are deactivated where they stood ("Deactivated."), never feral, even days later.
- [ ] W6.5 Any colonist, right-click a deactivated mech: "Disassemble …" gives steel and components.
- [ ] W6.6 Another colony siding with the Choir (wake it first), a colonist with psylink 3: "The Choir's summons";
      back after 5 days by drop pod as a mechanitor with 14 bandwidth (mechlink 6 + Chorus tuning 8), no psylink or
      psycasts.
- [ ] W6.7 Decline a summons (or let it lapse): "The colony stands alone"; **show allegiance** says independent, cause
      `summons`; both powers hostile.
- [ ] W6.8 Accept but forbid the colonist from boarding until the shuttle leaves: the same as W6.7.
- [ ] W6.9 Save and reload with a summons on offer, and with the colonist away: all survives, and they return.
- [ ] W6.10 **War in Heaven: convert the selected colonist** both ways on a test pawn: psylink level = 1 + (bandwidth −
      6) / 4, up to 6; back the other way, 6 + 4 × (level − 1) bandwidth.

---

## Part 4: fixes since C14, at any crisis fight

**Crisis lords no longer panic-flee (#153).** Vanilla had added its own panic flee (at a random 40–70% of the force
lost) to our operation and garrison lords, which cut short the profiles' regroup and retreat. Part 1d's colony works.

- [ ] 1. **Crisis: fire operation** → a standard raid; down about 40%: it pulls back to regroup (no "fleeing"
      message), then comes back with the reserve.
- [ ] 2. Down 60% of it: it retreats and carries off its downed where no colonist is close.
- [ ] 3. FinalAssault: down more than 70%: it still doesn't retreat.
- [ ] 4. A Citadel's inner rings (**Crisis: generate Citadel**): down most of a garrison: the rest hold their ring.

---

## Later

- **E2B war retrain (v3).** On hold until after the playtest. The hand labels (`hand-choir-v1`, PR #159) and the exact
  commands are in `training/README.md`, "Hand labels for the Hollow Choir". If it passes: its review, then
  `ollama cp rimchron-director-e2b-war rimchron-director-e2b` and its preset change (as #158 for E4B).
- **Checkpoint 2, the rest (W7–W9)**: the Choir's crisis (W7, built: `war-runbook-2.md` steps W7.1–W7.17), each Choir
  site and the cluster siege (W8), the Cathedral (W9). Run it once W9 is built; the steps are added as the milestones land.
- **Checkpoint 3 (W10–W11)**: the War in Heaven plot from day 600 and the endings; `war-runbook-3.md`.
- **W14**: the balance pass and a full proof run per side; then `war-in-heaven` merges into `main`.
- **Stage 5 proof run** (`docs/stage5-runbook.md`, M7) has no results file. Run it if it hasn't been.

---

## Done: the record

### Director model (D0–D11): passed 2026-11-24

| Step | Result |
| --- | --- |
| E2B training (v1–v3) | v3, with 250 hand labels for the Empire (`hand-empire-v1`), passed all 13 ship bars: top-1 85.4% (`harness/reports/director-e2b-v3.md`) |
| Prompted baseline | `base-v1` |
| E2B blind review (D9) | Picked or tied 70% (bar 40%) |
| E4B training and review | `director-e4b-v1` passed all 13 bars (top-1 86.0%); review 93% |
| In game (A6, D7) | Both presets connect, decide with source `Model`, and read sensibly |
| 200-day check (D11) | Passed |

### Imperial crisis (C0–C14): passed 2026-11-24

Checkpoints 1–4 (`docs/SPEC-crisis-combat-and-locations/crisis-runbook-1.md` to `-4.md`) and C14, the balance pass and
proof run (stage 3 costs real losses but is winnable without save-scumming; stage 4 and the Citadel need preparation).
Merged into `main` on 2026-11-24 (#93). Its director wiring (C11) gave crisis decisions to a director trained on the
crisis; that is now the E4B preset (Part 1d).

### War in Heaven director retrain (W13): E4B passed 2026-11-26

One retrain covered the crisis and the War in Heaven, on test version 3 (1040 cases: `test-v1`, `test-crisis-v1`,
`test-war-v1`; top-1 bar 80% from #156).

| Model | Result |
| --- | --- |
| `director-e4b-war-v2` | All 13 bars: top-1 83%, regret 0.29, orders 92%; blind review picked or tied 96% (#157). The E4B preset (#158) |
| `director-e2b-war-v2` | Failed: top-1 76%, regret 0.44, orders and ids. Weak on HollowChoir/Broken (25%) and the Empire's Broken beat. Retrain v3 later |
