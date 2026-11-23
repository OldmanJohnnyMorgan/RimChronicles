# C13: finale

Crisis milestone C13 (project plan §2). Built 2026-11-23; checked in game in `crisis-runbook-4.md`, part C13.

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Finale.cs` | `StellarchFate`, `StellarchRules` (escape once, falls once, leads while it hasn't fallen) |
| `src/RimChronicles.Core/Crisis/CrisisState.cs` | The Stellarch's pawn id, fate, escape used, day it fell (saved; an old save has none) |
| `src/RimChronicles.Core/Crisis/Operations/ForceMix.cs` | The final assault's mix |
| `src/RimChronicles.Game/Crisis/Finale/Stellarch.cs` | The Stellarch (made once, kept between appearances), its escape, its fall carrying the capital assault, the final assault, `MapComponent_RcStellarch`, Dev Mode |
| `src/RimChronicles.Game/Patches/` | `Pawn.Kill` (the Stellarch dies); the capture hook also catches it |
| `src/RimChronicles.Game/Crisis/` | The final assault registered; the Stellarch kept behind a Praetorian like other key units; the Citadel's keep led by it from the throne room; Dev Mode offers each operation only at the stages it's allowed |
| `tests/…/FinaleTests.cs` | The final assault only in the finale, fighting to the end; the escape at most once; one fall; saving |

## The High Stellarch

- **One pawn.** It's made the first time it's needed (the final assault or the Citadel's keep) with the finale kit
  (C3: psylink 6, the Skip and Ward kits, Farskip, legendary gear, bionics), and kept between appearances.
- **Escape once.** Checked every 30 ticks on any map: when it's below 40% health or downed (but alive), it skips
  away, once in the whole game, at a quarter of its psyfocus. In the Citadel it reappears deep in the keep, as far
  from the colonists as it can get; anywhere else it leaves the map and waits in the Citadel. A letter says so, and
  that it can't do it again. Killed outright, it doesn't escape.
- **Its fall.** Killed or captured, once: a letter, a news item, and it never leads again. If the colony is storming
  the capital (the Empire plot's `StormCapital` job with its quest still running), that quest succeeds: the Resistance
  ending, through the plot as before.

## The final assault

Offered only in the finale (stage 4), by weight until C11. Every arm of the Empire walks in from one edge:
Praetorians, two Breachers, a Herald, and one Adept of each specialty, with the High Stellarch at its head while it
hasn't fallen. It never retreats after losses (fairness rule 6's one exception); it pulls out only if the crisis ends.

## The Citadel's keep

The keep's garrison is the honor guard, and the Stellarch, if it hasn't fallen and isn't on another map, stands in
the throne room at its head. The honor guard never leaves the keep (C12). With lazy rings, it all appears when the
colonists reach the courtyard.

## Decisions

- **A scripted escape, not the psycast.** Farskip is a world-map cast for caravans (C0); the escape uses its
  psyfocus cost and skip effects and moves the pawn itself (as vanilla's Skip does: `Position` and
  `Notify_Teleported`, so it keeps its lord).
- **Checked on a timer, not inside the hit.** The escape runs from a map component every 30 ticks, so a pawn never
  vanishes in the middle of a damage call.
- **The fall ends the capital quest, not the plot.** Ending the quest in success lets the Empire plot reach the
  Resistance ending the way it always has; nothing in Core's Empire arc changes.
- **A Stellarch killed in the final assault** doesn't end the Empire: the capital still has to be stormed, but its
  keep has no Stellarch.

## Dev Mode

- **Crisis: fire operation** → FinalAssault → stage 4 (the only stage offered).
- **Crisis: spawn High Stellarch:** the kept one, or a new one, at the click.
- **Crisis: Stellarch state:** who it is, its fate, whether the escape is used, where it is.
