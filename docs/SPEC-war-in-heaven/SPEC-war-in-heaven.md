# Rim Chronicles — The War in Heaven Spec

**For:** Claude Code · **Owner:** John · **Date:** 2026-11-24 (draft 1: John's outline and four decisions; open questions in §11)
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

- A second full crisis-combat system. The mechanites reuse the crisis framework (operations, sites, threat budget)
  with their own units; how far that goes is open question Q5.
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

- **Faction:** a new `FactionDef` (name to be chosen, Q1), tech level Ultra, hidden and dormant until it wakes, then
  revealed. Leaders and officers are mechanitors (mechlink, bandwidth implants); the army is mechs (vanilla Biotech and
  Core mech kinds), with mechanitors commanding from the back.
- **Titles:** a mechanite rank ladder mirrors Royal titles, so allying with it can lead to a mechanite victory ending
  (§7). Ranks give bandwidth, the way titles give psylink levels.
- **Lore:** the mechanites are the Empire's original enemy. Imperial envoys, bulletins and letters treat them as the
  old foe; the mechanites see the Empire as the usurper.

### 3.1 Mechanite states (mirror of the Empire)

| State | What the mechanites do | Moves on when |
| --- | --- | --- |
| Dormant | Hidden; no settlements on the map; vanilla mech raids only | A waking trigger (§5) |
| Stirring | Rumours, signals, strange mech clusters near the colony | 20–40 days pass |
| Awakened | Reveals settlements, expands, raids | Act 2 milestones |
| Ascendant or Broken | Controls most of the region, or is collapsing | The player completes or fails an ending |

---

## 4. Waking

Each empire still wakes on its own triggers. Today the Empire's are wealth, a destroyed Imperial site, an Imperial
colonist and a refused title, with day 300 at the latest. The mechanites get a mirror set (Q2).

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
  level: the mechlink is removed and a psylink of the matching level is added (mapping: Q3).
- **Mechanite side:** the mirror. A mechanite shuttle takes each **psycaster** for 5 days and brings them back as a
  mechanitor of the equivalent level.
- **Refusal** starts the **Independence track**: war with both.
- **Independents** are the only colonies that can keep both kinds.

The demand repeats for each new mechanitor (or psycaster) the colony gets while on that side. A pawn away on the
shuttle is safe and can't be lost in the meantime (Q4 covers their mechs).

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
| Independence | **Independence**: outlast both, or force both to sign treaties |

Each ending has its own victory screen and an LLM-written epilogue, as today. Today's Resistance victory (destroy the
Imperial capital with a coalition) becomes a mechanite-side or independent route in the War in Heaven (Q6).

---

## 8. Director and training

The War in Heaven changes the director's option vocabulary: a mechanite arc, the War in Heaven, the shuttle demands
and new jobs. **The fine-tuned directors need retraining after this is built.**

- **One retrain, not two.** The crisis retrain (`training/README.md`, "Crisis retrain") waits until the War in Heaven
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
| D5 | One director retrain covers the crisis and the War in Heaven | 2026-11-24 (proposed) |

---

## 11. Open questions

- **Q1 Name.** What is the mechanite empire called in game, and what do its people call themselves?
- **Q2 Mechanite waking triggers.** Proposed mirror of the Empire's: colony wealth, a destroyed mechanite site, a
  mechanitor colonist, the Royal Ascent (§5), day 300 at the latest. Or should the mechanites have no deadline of their
  own and only wake from triggers or when the Empire attacks?
- **Q3 Level mapping.** Psylinks have levels 1–6; mechanitors have bandwidth. Proposed: psylink level = 1 + (bandwidth
  above the mechlink's 6, in steps of 4), up to 6, and the reverse for psycasters. Or tie it to the pawn's title or rank.
- **Q4 Mechs on the shuttle.** When a mechanitor is converted, what happens to their mechs: given to the Empire,
  turned feral, or shut down where they stand?
- **Q5 Mechanite combat.** Should the mechanites get the full crisis treatment (their own operations, sieges, sites
  and a citadel), or raids and territory only in this iteration?
- **Q6 Resistance victory.** Does destroying the Imperial capital stay as an ending (for the mechanite side or
  independents), or is it folded into the War in Heaven endings?
- **Q7 Order of work.** Proposed: C14 and the crisis merge to main first, then this spec's tech spec and build, then the
  one retrain and a proof run.
