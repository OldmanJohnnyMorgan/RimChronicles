# C3: units

Crisis milestone C3 (project plan §2). Built 2026-11-22; not yet checked in game (checkpoint 1).

## What's there

| Where | What |
| --- | --- |
| `Defs/PawnKindDefs/RimChronicles_Units.xml` | Eight pawn kinds, all `MayRequire` Royalty: Praetorian, Herald, Breacher, the Skip, Control, Mind and Ward Adepts, and the High Stellarch |
| `Defs/AbilityBanListDefs/RimChronicles_Bans.xml` | The enemy ban list: Neuroquake, Manhunter Pulse, Flashstorm, Entropy Dump |
| `src/RimChronicles.Core/Crisis/Units/Units.cs` | `PsycastKits`: the fixed kits (crisis spec §5.3.1 as fixed in C0), the required bans, first stage per unit, and the stage tiers (psylink range, gear quality, bionics, starting psyfocus) |
| `src/RimChronicles.Game/Crisis/Units/` | `UnitExtension` and `AbilityBanListDef`; `UnitKit` (generate, apply the tier and kit, enforce, check); Dev Mode tools |
| `src/RimChronicles.Game/Patches/CrisisPatches.cs` | Re-checks a unit's kit when a save loads |
| `tests/…/UnitTests.cs` | Kits against the real defNames and levels, bans, tiers, the defs files |

## How a unit is made

`UnitKit.Generate(kind, faction, stage)` generates the pawn as usual, then:

1. **Psylink:** psykers get the stage's level (stage 1: 3; 2: 3–4; 3: 4–5; 4: 5–6; the Stellarch 6), never below
   their kit's first psycast, so a Mind Adept is at least 5. It's set without letters; vanilla adds a random psycast
   for each level gained.
2. **Kit:** every psycast is removed, then the kit up to that level is granted, minus the ban list and any psycast the
   game doesn't have. Non-psykers keep no psycasts (a royal title can bring a psylink).
3. **Psychically hypersensitive** (psykers), replacing any other psychic sensitivity trait.
4. **Psyfocus** set to the stage's start (60–90%; the Stellarch 100%). Vanilla leaves NPC psyfocus at -1, which caps
   casts at level 2 (C0, R20).
5. **Gear** raised to the stage's quality (Good, Excellent, Masterwork; Legendary for the finale's adepts and the
   Stellarch), and **bionics** added (arm, leg, then eye) by stage.

The kit is enforced again when a save loads (`Pawn.ExposeData` postfix), except on player pawns: a recruited
defector levels its psylink like any colonist, and the ban list is for enemies only.

## Decisions

- **Only crisis code generates these kinds.** They're in no faction's pawn groups, so nothing patches vanilla
  generation; operations call `UnitKit.Generate`.
- **Bases:** Praetorian on `CataphractBase` (cataphract armor, heavy or spacer guns), Herald on `ImperialTrooperBase`,
  Breacher on `ImperialFighterBase` with EMP grenades (as vanilla's grenadier), Adepts on `RoyalBase` with a psyfocus
  robe and an eltex skullcap, the High Stellarch on `RoyalBase` with prestige cataphract armor and a persona weapon.
  Royal titles are off for the soldiers (`royalTitleChance` 0).
- **The Herald carries a rifle, not vanilla's orbital targeter.** The AI could fire a targeter without a telegraph,
  which breaks fairness rule 1. Its strikes come in C5.
- **The ban list has two layers:** the XML def (for balance and modders) adds to `PsycastKits.RequiredBans` in code,
  which can't be removed.

## Dev Mode

- **Crisis: spawn unit:** pick a unit, then a stage, then click; it spawns hostile (Empire) and reports its psylink,
  psycasts and any problem.
- **Crisis: check units:** generates every unit three times at every stage it appears at, checks each has exactly its
  kit and psylink range and nothing banned, logs the result, and discards them.
