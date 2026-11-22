# C5: orbital strikes

Crisis milestone C5 (project plan §2). Built 2026-11-22; not yet checked in game. Its in-game checks join
checkpoint 2, with the operations that bring Heralds to the colony.

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Strikes/StrikeRules.cs` | `StrikeRules` (source, cooldown, cap, targeting, kind), `StrikeState` (saved), `StrikeCandidate`, `SiegeGoal` |
| `src/RimChronicles.Game/Crisis/Strikes/StrikeCaller.cs` | `MapComponent_RcStrikes`: watches each map, calls, telegraphs, calls off and lands strikes |
| `src/RimChronicles.Game/Crisis/Strikes/StrikeDevActions.cs` | Dev Mode tools |
| Settings | **Orbital strikes land N seconds after the warning** (6–10, default 8) |
| `tests/…/StrikeTests.cs` | The rules, and saving |

## How a strike happens

1. **A raid is on the map:** any standing crisis unit of the Empire. When none is left, the raid's strike count resets.
2. **A source:** a Herald who is alive, awake, standing and not in a mental state, or a standing uplink. Uplinks are
   ThingDefs in `MapComponent_RcStrikes.Uplinks`; the siege uplink (C8) and Orbital Uplink sites (C9) register theirs.
   While a destroyed uplink site holds the `strikes` capability down, there are no strikes at all.
3. **Not blocked:** the cooldown (1,800 ticks, about 30 seconds at normal speed) has passed and the raid's cap isn't
   spent. The cap is set from the crisis budget when the raid's first source appears: one strike per 1,200 points,
   1 to 6.
4. **A target:** colony turrets, power plants and batteries, and colonists with another colonist near them. Never a
   cell the source can't see (Herald: within 60 cells and in line of sight), under overhead mountain (as vanilla's
   permit), with an Imperial unit in the blast, or within 4 cells of the last strike. Turrets score 3, a cluster 2 per
   colonist, power 2; a siege goal (C8) doubles or triples what it's after.
5. **The telegraph:** the cell is marked (a psycast flash every half second), the orbital strike sound plays, and a
   letter names the Herald and says how long there is. **If the Herald goes down, dies, leaves or loses sight before
   the strike lands, it's called off** (fairness rule 1), with a message.
6. **The strike:** Royalty's own things with the permits' numbers. A **shell** (one blast, 7.9 radius) for turrets
   and power, a **salvo** (six blasts over 7.9 cells) for groups of colonists and walls, and a **power beam** for
   fields and stores (a Starve siege, C8) when no Imperial unit is within its 15-cell fires.

A called strike counts toward the cap and the cooldown even if it's called off, so downing a Herald costs the raid
that strike.

## Decisions

- **The telegraph is our marker, not vanilla's warmup.** Vanilla's `Bombardment.warmupTicks` delays the blasts, but
  a strike that exists can't be called off, so the strike is only spawned when the telegraph ends.
- **No targeting laser line yet.** Drawing a line from the Herald needs Unity types outside Dev Mode (rule 3). The
  mark on the target, the sound and the letter (which jumps to the spot) are the telegraph; a laser fleck can be added
  in XML later if checkpoint 2 finds the warning too easy to miss.
- **A map component, not a Herald job.** Strikes don't depend on the Herald's lord or duty, so they work for any
  operation, and the Herald keeps fighting while it calls them.

## Dev Mode

- **Crisis: call orbital strike:** pick shell, salvo or beam, then click. The nearest Herald that can see the spot
  calls it; with none, it has no source and can't be called off.
- **Crisis: strike state:** Heralds up, strikes this raid and the cap, why strikes are blocked, pending strikes, what
  it would pick, and the ten best candidates.

## In-game checks (for checkpoint 2)

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Spawn a Herald and two Praetorians (**Crisis: spawn unit**) where they can see a turret or a few colonists. | Within a minute, a strike letter; the target flashes; it lands about 8 seconds later. |
| 2 | Call one (**Crisis: call orbital strike**) and down the Herald during the countdown. | "The orbital strike was called off"; nothing lands. |
| 3 | Stand colonists under a mountain roof. | No strike aims there (**Crisis: strike state** marks them "under mountain"). |
| 4 | Let a raid strike until the cap. | **Crisis: strike state** says "this raid's N strikes are spent"; no more strikes. |
| 5 | Save during a countdown and reload. | The strike still lands (or is called off) after the reload. |
