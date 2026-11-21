# Stage 2 results: the news agent

Stage 2 closed on 2026-11-21. A reporter from a neutral faction sends a bulletin every 3 days (adjustable from 1 to 7),
written by the local model from world news collected during play. Every bulletin is archived in the Dispatch tab.
All six milestones passed their in-game checks on the reference PC (`docs/stage2-runbook.md`), including the spec's
exit test: a 60-day game with bulletins on time and no stutter, a reload that changed no delivered bulletin, and the
model switched off mid-game falling back to templates without errors.

| Milestone | What it added | PRs |
| --- | --- | --- |
| M1 | Settings page: tier presets, server, Test connection, news cadence | #24 |
| M2 | World news collector: conditions, war and peace, leader deaths, settlements destroyed | #25, #26 |
| M3 | News pipeline: written 2 days early, delivered on the due day, never rewritten; templates as fallback | #27–#29 |
| M4 | A reporter pawn from a non-hostile faction; the Dispatch tab | #30, #31 |
| M5 | Reporter visits; held, killed by the colony (news stops), or lost (replacement in 5–10 days) | #32, #33 |
| M6 | Bulletin prompt v10, real snapshots in the harness, a repetition check, adult reporters | #34–#36 |

## Bulletin prompt v10

Real games are mostly quiet weeks, and v8 let models pad them with faction settlement counts. v10 writes one short item
on a quiet week, mentions a faction only when there is news of it, and its digest no longer gives settlement counts.
The harness gained three snapshots exported from real games (`snap-21` to `snap-23`).

Trial run `20261121-205723` (bulletins only, 1 repeat, judge Sonnet 5.5), on the 20 hand-written snapshots, against the
Stage 0 full run (v8, 3 repeats):

| Model | Major invented facts per 10 (v8 → v10) | Overall (v8 → v10) | Real exports, overall |
| --- | --- | --- | --- |
| Gemma 4 E4B (default) | 0.7 → 0.0 | 2.75 → 2.50 | 3.00 |
| Gemma 4 12B | 0.7 → 1.5 | 3.17 → 2.85 | 3.00 |
| Gemma 4 E2B | 2.7 → 4.0 | 2.20 → 2.10 | 2.33 |

Bulletins citing settlement counts fell from 14 of 69 (v9) to 2 of 69. E2B, the Minimum tier, invents more without
counts to recite (wars between real factions, old news told as new). The grounding check can't catch an invented
relation between two real names, so Minimum-tier players will see some of this. A 1-repeat trial is noisy; the 12B
change is within it.

## What Stage 2 learned about 1.7

These are in `docs/ENGINE_NOTES.md`. The ones that cost a round of testing:

- Dev time tools set the tick counter directly, so daily work must compare day numbers, not wait for a day's first tick.
- Most NPC factions leave `FactionDef.basicMemberKind` unset; pawn kinds come from `pawnGroupMakers`, as the game's own
  `Faction.RandomPawnKind` does.
- Spawning a world pawn removes it from the keep-forever set, and leaving the map passes it back with the default
  discard rule, so a pawn we track must be re-pinned (`WorldPawns.PassToWorld` patch).
- The adult life stage starts at 13, so "adult" alone can give a teenage pawn.
- `GameCondition.Label` can add detail ("psychic soothe: Female"); news uses `def.label`.
- The game's relation-change notice fires on both factions.

## Carried into Stage 3

- The Silent Press arc starts from `ReporterState.SilencedDay`; Stage 2 only stops the news.
- Vanilla makes little world news; plots add their own public news to the news log.
- The reporter can't yet be visited at their faction's settlement, only met when they visit the colony.
