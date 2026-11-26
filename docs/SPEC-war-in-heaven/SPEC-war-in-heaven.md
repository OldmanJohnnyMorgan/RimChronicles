# Rim Chronicles — The War in Heaven Spec

**For:** Claude Code · **Owner:** John · **Date:** 2026-11-24 (draft 3: all open questions decided; tech spec and project plan in this folder)
**Parent spec:** Rim Chronicles — Design Spec (Stage 5). Siblings: `SPEC-crisis-combat-and-locations/`,
`SPEC-director-model.md`. Where they disagree, the parent spec wins.

---

## 1. Goal

The Awakened Empire isn't the whole story. It leads up to **the War in Heaven**: the Empire against its original
enemy, a broken **mechanite empire** that has been asleep as long as the Empire has. The mechanite plot mirrors the
Empire plot, either empire can wake first, and if their war with the player drags on, they turn on each other.

The player's choice of side decides who they fight:

| Player's side | At war with |
| --- | --- |
| The Empire | The mechanites |
| The mechanites | The Empire |
| Independence | Both |

### Core rule (same as the other specs)

**The LLM picks the strategy, code does the execution.** The director chooses from allowed options and gives a reason;
code owns waking, timing, odds, units and territory.

### Non-goals (this spec)

- A second crisis-combat system written from scratch. The mechanites get the full crisis (decided, §3.2), built on
  the Imperial crisis's framework (operations, sites, siege planner, threat budget, citadel) with their own units.
- Archonexus and Anomaly endings. Only the ship launch and the Royal Ascent wake an empire (§5).
- Changing the vanilla mechanoid hive. It stays as wild mechs (decided, §10).

---

## 2. Requirements

| DLC | Needed for |
| --- | --- |
| Royalty | The Empire plot (as today), psylinks, the Royal Ascent |
| Biotech | The mechanite faction, mechlinks and mechanitors |

Without Biotech the Empire plot runs as it does now: no mechanite empire, no War in Heaven, no pawn transfers.
Without Royalty there is no major plot at all, as today.

---

## 3. The mechanite empire (decided: a new mechanitor faction)

A new humanlike faction led by **mechanitors**, with mech armies. Unlike the hidden mechanoid hive, it has
settlements on the world map, relations, envoys and territory, so it can do everything the Empire does.

- **Faction:** a new `FactionDef`, **the Hollow Choir** (decided, D12), tech level Ultra, hidden and dormant until it wakes, then
  revealed. Leaders and officers are mechanitors (mechlink, bandwidth implants); the army is mechs (vanilla Biotech and
  Core mech kinds), with mechanitors commanding from the back.
- **Titles:** a mechanite rank ladder mirrors Royal titles, so allying with it can lead to a mechanite victory ending
  (§7). Ranks give bandwidth, the way titles give psylink levels. Rank comes from service (D13): each job done for the
  Choir raises a colonist's standing, and a colonist at a high rank opens the First Voice's offer.
- **Lore:** the mechanites are the Empire's original enemy. Imperial envoys, bulletins and letters treat them as the
  old foe; the mechanites see the Empire as the usurper. The model for their character is an ancient power that hides
  in the dark, works through agents and believes that conflict makes peoples stronger; the name and voice are our own.
- **Name and voice:** outsiders and the Empire call them **the Hollow Choir**; they call themselves **the Chorus**.
  Their mechanites hum in one voice, and colonists hear the hum before raids and while the Choir is Stirring, which
  gives the narrator a sign to use ("the Choir is singing again").

### 3.1 Mechanite states (mirror of the Empire)

| State | What the mechanites do | Moves on when |
| --- | --- | --- |
| Dormant | Hidden; no settlements on the map; vanilla mech raids only | A waking trigger (§5) |
| Stirring | Rumours, signals, strange mech clusters near the colony | 10–20 days pass |
| Awakened | Reveals settlements, expands, raids | Act 2 milestones |
| Ascendant or Broken | Controls most of the region, or is collapsing | The player completes or fails an ending |

### 3.2 Mechanite crisis (decided: the full crisis)

The mechanites get everything the Imperial crisis has, mirrored: their own operations, siege goals, sites, a citadel,
captives and defectors, strikes and a threat budget, with mechs and mechanitors in place of Imperial troops and
psykers. The crisis code is generalised so both empires run on it; the mechanite tech spec says which parts are shared
and which are new (mech commanders instead of the psyker AI, for example).

---

## 4. Waking

Each empire still wakes on its own triggers. Today the Empire's are wealth, a destroyed Imperial site, an Imperial
colonist and a refused title, and it stirs on day 20 whatever the signs: the opening hook (D15). The mechanites mirror them (decided): colony wealth, a
destroyed mechanite site, a mechanitor colonist, the Royal Ascent (§5), from day 30 and day 120 at the latest; beside the Empire it usually wakes on the Empire's first attack. Without Royalty, the Choir takes the day-20 hook.

**The other empire follows.** When one empire wakes and **starts attacking**, the other wakes too. (John:
"if the mechanites wake first and start attacking, then the empire wakes … and vice versa.") Waking second skips
straight to Awakened, so there's no second Stirring phase.

---

## 5. Endings that wake an empire

| The player starts | Wakes | If neither empire is awake yet |
| --- | --- | --- |
| The ship launch | The Empire | It wakes and attacks at once |
| The Royal Ascent | The mechanites | They wake and attack at once |

If the empire the ending wakes is already awake, it reacts the way the Empire reacts to a ship launch today: it tries
to stop it. Waking an empire this way also wakes the other one (§4).

---

## 6. Sides and stances

The existing Empire stance (neutral, ally, oppose) becomes a colony-wide **side**:

| Side | Empire stance | Mechanite stance |
| --- | --- | --- |
| Undeclared | Neutral | Neutral |
| Empire | Ally | Oppose |
| Mechanites | Oppose | Ally |
| Independence | Oppose | Oppose |

Joining one empire declares war on the other. The Independence track (today's treaty ending) means war with both.

### 6.1 Pawn purity: the shuttle

A side allows only its own kind:

- **Empire side:** the colony can't keep **mechanitors** (pawns with a mechlink). An Imperial shuttle lands and asks
  for each one. If the player agrees, the pawn leaves for **5 days** and comes back as a **psycaster** of the equivalent
  level: the mechlink is removed and a psylink of the matching level is added.
- **Level mapping (decided):** psylink level = 1 + (bandwidth above the mechlink's base 6, in steps of 4), at most 6.
  Bandwidth 6–9 gives level 1, 10–13 level 2, and so on. The reverse gives a mechlink with 6 + 4 × (level − 1)
  bandwidth (the extra from a mod hediff, since bandwidth from band nodes stays at the colony). The tech spec checks
  how 1.7 counts bandwidth.
- **The pawn's mechs (decided):** they shut down where they stand, so the player can dismantle them for parts.
- **Mechanite side:** the mirror. A mechanite shuttle takes each **psycaster** for 5 days and brings them back as a
  mechanitor of the equivalent level.
- **Refusal** starts the **Independence track**: war with both.
- **Independents** are the only colonies that can keep both kinds.

The demand repeats for each new mechanitor (or psycaster) the colony gets while on that side. A pawn away on the
shuttle is safe and can't be lost in the meantime.

---

## 7. The War in Heaven

Once both empires are awake, they are at war with each other in name. **The War in Heaven** is when they fight it in
full: both turn their armies on each other across the world map, and the player's side decides the outcome.

### 7.1 When it starts

- It can start once both empires are awake and the player is at war with at least one of them.
- **Each day there's a chance it starts, and the chance rises the longer the player's war goes on** (with either
  side). The curve is a balance setting: near zero for the first weeks, steep after a season or two.
- **On game day 600 it starts for certain** if the player hasn't finished an ending yet (decided: game day, not days
  of war).

### 7.2 What changes

- Territory: the two empires take each other's settlements, not only neutral ones. Every result goes in the news.
- Raids on the colony keep coming from the player's enemies, but some of their force is pulled away to the front.
- The director gets War in Heaven jobs: defend your side's settlements, strike the enemy's, escort, sabotage.

### 7.3 How it ends (decided: your side's victory)

It's the final act.

| Player's side | Victory |
| --- | --- |
| Empire | **Imperial victory**: the Empire wins the war with the player's help (today's governorship ending) |
| Mechanites | **Mechanite victory**: a new ending, the mirror of Imperial victory |
| Independence | **Independence**: defeat one empire, then take the other's treaty or destroy it too |

**Independence fights both crises at once** (decided). When the player destroys one empire (its citadel or capital),
the other offers a treaty that recognises the colony as a free state. Accepting it ends the game in Independence;
refusing keeps the war going until the player destroys the second empire as well, a harder Independence victory with
its own epilogue.

Each ending has its own victory screen and an LLM-written epilogue, as today. **Today's Resistance victory is folded
in** (decided): destroying the Imperial capital is how the mechanite side and independents defeat the Empire, and it
is no longer a separate ending.

---

## 8. Director and training

The War in Heaven changes the director's option vocabulary: a mechanite arc, the War in Heaven, the shuttle demands
and new jobs. **The fine-tuned directors need retraining after this is built.**

- **One retrain, not two.** The crisis retrain (`training/README.md`, now "Retrain for the crisis and the War in Heaven") waits until the War in Heaven
  options are settled, then one round of 12B labels, one E2B and E4B retrain, and one report covers both.
- **Test set:** test version 3, with War in Heaven cases added beside seed 9001 and seed-9001-crisis. Claude Code
  labels them in session, as for test-crisis-v1.
- **Until then:** the fine-tuned presets see neither the crisis nor the War in Heaven (`KnowsCrisis` stays false, and a
  matching flag covers the War in Heaven). Prompted models get both.

---

## 9. Testing and dev tools

Every new step gets a Dev Mode action: wake the mechanites, wake both, set the side, send the shuttle, start the War in
Heaven, jump to game day 600, and each ending. Every LLM feature has a template fallback (CLAUDE.md rule 7). In-game
tests go into `docs/test-plan.md` and a runbook, for John to run once at the end of the stage.

---

## 10. Decisions

| # | Decision | Date |
| --- | --- | --- |
| D1 | The mechanite empire is a new mechanitor-led faction with settlements; the vanilla hive stays as wild mechs | 2026-11-24 |
| D2 | A "mechanite pawn" is a mechanitor (mechlink); the reverse transfer takes psycasters | 2026-11-24 |
| D3 | The War in Heaven starts for certain on game day 600 | 2026-11-24 |
| D4 | The War in Heaven ends with the player's side winning: Imperial victory, mechanite victory or Independence | 2026-11-24 |
| D5 | One director retrain covers the crisis and the War in Heaven | 2026-11-24 |
| D6 | The mechanites wake on a mirror of the Empire's triggers, day 300 at the latest | 2026-11-24 |
| D7 | Psylink level = 1 + (bandwidth above 6) / 4, at most 6; the reverse for psycasters | 2026-11-24 |
| D8 | A converted mechanitor's mechs shut down where they stand, for the player to dismantle | 2026-11-24 |
| D9 | The mechanites get the full crisis; Independence fights both; destroying one empire brings the other's treaty offer, which the player may refuse | 2026-11-24 |
| D10 | Resistance victory is folded into the War in Heaven endings | 2026-11-24 |
| D11 | Order: C14 and the crisis merge to main, then this tech spec and build, then the one retrain and a proof run | 2026-11-24 |
| D12 | The mechanite empire is the Hollow Choir; its people call themselves the Chorus | 2026-11-24 |
| D13 | Choir rank comes from service: a rank ladder of its own, raised per colonist by jobs done for the Choir (like Imperial favor); a colonist at a high rank opens the First Voice's offer | 2026-11-24 |
| D14 | Faster pacing after the playtest: the powers wake by day 120 at the latest (D6's day 300) or at 80,000 wealth (was 150,000), stir 10–20 days (was 20–40); a job's result applies the day after it ends; the War in Heaven's odds start after 15 days of war and reach 5% a day at 165 (was 30 and 3% at 180); the final push opens at front +3 (was +4); story intensity scales every beat gap, the stirring and the war's odds | 2026-11-26 |
| D15 | The awakening is the opening hook, not an endgame: the Empire stirs on day 20 (its signs only say why); the Choir wakes from day 30 on its signs, on the Empire's first attack, or on day 120, and takes the day-20 hook itself without Royalty; the crisis budget's weak-colony floor drops from 0.6× to 0.3× (Probing at least 240 points) | 2026-11-26 |
| D16 | Expansion happens where the colony can see it: each power attacks out of its settlements within 40 tiles of the colony (else its three nearest), reaches 30 tiles (was 60), weights distance as `1/(1 + d/5)³` (was `1/(1 + d/10)`), divides by the square root of the owner's settlements (was the count), and weights targets near its latest conquest up to 3×, so it advances as a front | 2026-11-26 |

---

## 11. Open questions

None open. The tech spec comes after C14 and the crisis merge to main (D11).
