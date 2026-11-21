# Stage 3 results: minor plots

Stage 3 closed on 2026-11-21. The colony's own history now grows stories: a raider who killed or maimed a colonist and
escaped becomes a nemesis, and killing the reporter starts Silent Press. All three of the spec's exit criteria passed in
the proof run on the reference PC (`docs/stage3-runbook.md`, M6): Nemesis end to end with one inheritance, every beat
and handoff forced from Dev Mode, and the arcs running on templates with the model off.

| Milestone | What it added | PRs |
| --- | --- | --- |
| M1 | Plot engine: four-beat plots, the spec's limits, the protagonist slot and inheritance order, the pacing gate | #38 |
| M2 | Nemesis seed: grudges from kills and maimings, escape tracking, the strongest grudge seeds a plot | #39, #40 |
| M3 | Nemesis beats: sighting, a raid led by the nemesis, a bounty-or-wait choice, endings with payoffs and news; plot letters written by the model | #41–#43 |
| M4 | Inheritance: a dead, taken or departed protagonist passes the plot on; handoff scenes; revenge when the nemesis kills them | #44 |
| M5 | Silent Press: the silence, two clues, the envoy's reparations (pay, haggle, refuse), a cold new reporter | #45 |
| M6 | Proof run, and a Silent Press re-offer timing fix | #46 |

## The plot-letter prompt

`plot-letter` v2 rewrites each Nemesis beat's template letter in the game's voice. From the API-credit cut-off on, trials
ran on the local models without the Claude judge; Claude Code reviewed the raw letters instead.

- **v1 (run `20261121-222750`, 36 letters):** every letter passed the checks on the first try. The review found guessed
  genders, a template that put the bounty hunters in the nemesis's own camp (so E4B had the raider's faction hunting its
  own man), and roles 12B invented ("commands", "the authorities").
- **v2 (run `20261121-224041`):** the game passes each person's pronoun as a fact, the hunters have no location, and
  added ranks and outside powers are forbidden. All three models used the given pronouns, including a female raider they
  had called "he" under v1. The remaining slips were one-offs.

The handoff scene uses the Stage 0 prompt (inheritance-handoff v3), unchanged. Silent Press uses template letters only.

## What Stage 3 learned about 1.7

These are recorded in `docs/ENGINE_NOTES.md`, or noted in the code where they're used.

- `Pawn_HealthTracker.AddHediff` has two overloads (`Hediff` and `HediffDef`). A patch naming only the method failed
  at load, and `PatchAll` stopped there. Patches on overloaded methods now name their argument types, and the mod
  patches each class on its own, so one failure doesn't take down the rest.
- A severed part arrives as a `Hediff_MissingPart`, together with the `DamageInfo` that caused it. "As big as a hand" is
  the game's own bar (`FindBiggestMissingBodyPart`).
- `TradeUtility`'s silver helpers only see silver near a powered trade beacon, so payments use the colony's own stock.
- A kidnapped colonist keeps the player's faction and sits on the kidnapper's `kidnapped` list. A colonist away on a
  caravan is a world pawn too, so "off the map" doesn't mean "taken".
- Custom `ThoughtDef`s need `thoughtClass` set: it has no default. Choice letters are a `ChoiceLetter` subclass with
  its own `LetterDef`.

## Carried into Stage 4

- Arcs choose their next beat by fixed rules. The plot director (Stage 4) will choose among allowed transitions, with
  the model and a decision log.
- The Nemesis choice letter can become a Hunt target quest once quest templates exist.
- The other arc types in the spec (Lost Family, Old Debt, Grief, Rivalry, The Past Catches Up) are still to come.
- Silent Press letters are templates, and its envoy is a letter, not a visiting pawn.
