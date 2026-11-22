# C4: psyker AI

Crisis milestone C4 (project plan §2). Built 2026-11-22; not yet checked in game. Checkpoint 1 comes next.

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Battle/BattleView.cs` | The fight as plain data: each unit's side, position, role, health, pain, weapon damage and range, cover, under fire, seen, melee, value, psychic sensitivity and effects; plus "is this cell open" and "is there a line of sight" |
| `src/RimChronicles.Core/Crisis/Psykers/Scorers.cs` | `IPsycastScorer` and the 14 scorers from crisis spec §5.3.1 |
| `src/RimChronicles.Core/Crisis/Psykers/PsykerBrain.cs` | Scores every castable psycast, lowers a cast's worth as heat nears the limit, and holds when nothing is worth it |
| `src/RimChronicles.Game/Crisis/Psykers/` | `BattleViewBuilder` (one view per map and side, every 250 ticks), `IPsycastProvider` + `VanillaProvider`, `ScorerDef` + `ScorerRegistry`, `JobGiver_RcPsykerCast`, `PsykerLog`, Dev Mode tools |
| `Defs/ScorerDefs/RimChronicles_Scorers.xml` | Which scorer handles which psycasts |
| `Defs/ThinkTreeDefs/RimChronicles_Psykers.xml` | Inserts the think node into every humanlike think tree (`Humanlike_PostMentalState`) |
| `src/RimChronicles.Game/Patches/CrisisPatches.cs` | No vanilla AI casting for crisis units; every cast is checked against what the scorers chose |
| `tests/…/PsykerTests.cs` | Each scorer on fixtures, the brain (choice, hold, heat, psyfocus, unscored psycasts), the scorer defs |

## Scorers

| Scorer | Psycasts | Picks |
| --- | --- | --- |
| SkipOutOfCover | Skip | A colonist in cover (or with a strong weapon), out next to 2+ Praetorians |
| SkipAllyOutOfMelee | Skip | An ally with 2+ enemies on it (or 1 while badly hurt), out to a safe cell near allies |
| ChaosSkipCluster | Chaos Skip, Mass Chaos Skip | A colonist with 2+ others within 4 cells; for Mass Chaos Skip, 3+ colonists in the area and at most 1 ally |
| BeckonShooter | Beckon | The strongest shooter in cover |
| StunShooter | Stun | The colonist with the most dangerous weapon in range, not already stunned |
| BurdenCloser | Burden | The colonist nearest a psyker or Herald, within 8 cells |
| PulseCluster | Vertigo, Blinding, Berserk Pulse | 3+ colonists in the radius not already affected, at most 1 ally |
| BerserkInGroup | Berserk | A colonist with 2+ others within 5 cells |
| PainblockAlly | Painblock | The most valuable ally with 30%+ pain, not already painblocked |
| ChunkCover | Chunkskip | Chunks next to an ally pinned in the open, on the enemy's side |
| WallraiseLane | Wallraise | A wall across a colony turret's line onto an ally |
| SmokeAdvance | Smokepop | 2+ exposed allies under fire |
| ShieldGroup | Bullet Shield | 3+ allies under fire in its radius |
| InvisibleFlank | Invisibility | A Herald (or the adept itself) no colonist has seen yet |

Farskip has no scorer until C13, so it's never cast. Psychic casts skip targets with no psychic sensitivity and are
weighted by it.

## How a cast happens

1. The think node runs for a hostile crisis psyker at most every 250 ticks, while the map has a threat to its faction.
2. `VanillaProvider` lists the psycasts the pawn can cast now, through `Psycast.GizmoDisabled`: psychic sensitivity,
   psylink, psyfocus and its band, heat, cooldown (R20).
3. `PsykerBrain` scores each one. A cast's worth drops as the heat after it goes from 60% to 100% of the limit. Below
   a worth of 1.5 the psyker **holds** and fights on.
4. The best cast the game accepts (target still there, in range and in sight, a skip destination within the skip's
   range of the target and in its sight) becomes the job: `Ability.GetJob(target, dest)`.

Vanilla AI casting is off for crisis units (`AICastableAbilities` returns nothing for them). Every psycast a crisis
unit activates is checked against its last chosen cast; any other is logged as unchosen.

## Decisions

- **The think node goes in through vanilla's insert hook, not a duty.** Psykers cast under any lord, vanilla or ours,
  so C4 can be tested before the operations exist. The `RimChronicles_PsykerSupport` duty in the plan is about where
  psykers stand (near Praetorians, behind the line); that comes with the lords in C6.
- **Dev Mode drawing may use Unity stub types** (crisis Q2). Rule 3 in `CLAUDE.md` and `docs/ENGINE_NOTES.md` now say
  so. The debug text uses `Pawn.DrawPos`.

## Dev Mode

- **Crisis: spawn psyker squad:** pick a stage, then click. It spawns one adept of each specialty that stage allows,
  a Herald and three Praetorians, attacking the colony.
- **Crisis: psyker debug on/off:** each decision floats over the psyker with its heat and psyfocus.
- **Crisis: psyker report:** every psyker's last decision, its top three candidates, and the chosen and unchosen
  cast counts.
- **Crisis: check psycasts:** each scorer enabled or disabled (and why), and any kit psycast with no scorer.
