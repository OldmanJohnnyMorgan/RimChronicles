# Crisis checkpoint 1 runbook: crisis core, pieces, units, psykers (C1–C4)

One in-game session on the reference PC covers C1 to C4. Royalty must be active (Ideology, Biotech and Odyssey can be
too). Write what you find in `crisis-results-1.md` next to this file, or tell Claude Code and it will.

## Setup

The crisis lives on its own branch. In PowerShell:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git fetch
git checkout crisis-combat-locations
git pull
dotnet build RimChronicles.slnx
```

Start RimWorld with **Development mode** on (Options → General). Use a **new test colony** (crisis saves go back to
`main` badly: this branch adds things `main` doesn't know). Every Dev Mode tool below is in the Debug actions menu
(the first button on the dev bar), under **Rim Chronicles**. When you're done, go back to the normal branch:

```powershell
git checkout main
git pull
dotnet build RimChronicles.slnx
```

## C1: crisis core

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Options → Mod settings → Rim Chronicles. | Under the major plot: **Imperial crisis** (on), **Crisis difficulty** 1.0×, **At least 8 days between Imperial operations**. |
| 2 | **Crisis: show state** in the new colony. | "Stage: none (the Empire isn't awake)", a colony strength line and budget 0. |
| 3 | **Empire: trigger awakening**, then **Empire: end stirring now**. Wait for the envoy letter (a few days; **Increment time** helps) and choose **Stand against it**. | The envoy letter comes; the stance is taken. |
| 4 | Wait for "The Empire marches" (6–12 days on), then **Crisis: show state**. | Stage **probing** ("Act 2: the Empire is testing the colony"); a letter **Imperial scouts**; a news item about scouts; operations allowed: "Operation:ProbingRaid (not built yet)". |
| 5 | **Empire: expansion tick** twice, wait a day, **Crisis: show state**. | Stage **reclamation**; an **The Empire escalates** letter. |
| 6 | Check the budget line against a colony with more fighters or turrets (spawn some turrets with Dev Mode, wait a day). | The budget goes up with colony strength, within the clamp (factor 0.3 to 1.8). |
| 7 | Save, quit to the menu, reload; **Crisis: show state**. | Same stage and announcement day; no errors. |

## C2: pieces

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: check pieces**. | "16 pieces loaded" (the five starters, C8's three siege pieces, C9's five site rooms, C10's records room and C12's throne room and vault), none left out, and a picture of each. |
| 2 | **Crisis: stamp piece** → each piece, click on open ground. | Each spawns; the message says "the footprint matches the planner's". Walls, doors and beds are made of stone blocks or steel; sandbags of cloth. |
| 3 | **Crisis: turn pieces**, then stamp the mortar pit and the barracks again; repeat for all four facings. | Each facing matches the planner's footprint. The mortar's crew spot (one cell in front of it) is open at every facing. |
| 4 | Stamp a piece on top of a wall or rock. | Refused: "it doesn't fit there"; nothing spawns. |
| 5 | **Crisis: stamp piece as Imperial blueprints** → the mortar pit. | Blueprints appear; their inspect pane names the Empire; your colonists don't build them. |
| 6 | Stamp the turret nest. | It spawns; its turret is unpowered (expected: it needs power). |

## C3: units

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: check units**. | "Every unit at every stage has exactly its kit." |
| 2 | **Crisis: spawn unit** → each unit → a stage, and click. Use stage 1 for the soldiers and adepts, stage 3 for the Mind Adept, stage 4 for the High Stellarch. | Each spawns hostile, in fitting gear: Praetorians in cataphract armor, the Breacher in power armor with EMP grenades, adepts in a psyfocus robe and an eltex skullcap, the Stellarch in prestige cataphract armor with a persona weapon. The message lists only that unit's kit, with no problems. |
| 3 | Inspect an adept (health and character tabs). | A psylink at its stage's level; **psychically hypersensitive**; stage 3 and 4 units have bionics. |
| 4 | Save with hostile adepts on the map, reload. | No errors; they still cast only their kit (C4 below shows it). |

## C4: psyker AI

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: check psycasts**. | 14 scorers enabled; "Kit psycasts with no scorer (never cast): Farskip". |
| 2 | **Crisis: psyker debug on/off** (on). **Crisis: spawn psyker squad** → stage 2, about 40 cells from your colonists, and let them fight a few armed colonists in cover. | The squad attacks; text floats over each adept as it decides ("Stun: stun Ana, the most dangerous shooter in range") with its heat and psyfocus. |
| 3 | Watch the fight, then **Crisis: psyker report**. | Each adept (Skip, Control, Ward) casts at least once; **Casts the scorers didn't choose: 0**. |
| 4 | Keep the fight going until adepts run hot. | Adepts near their heat limit hold ("hold: …") and fight with guns instead; none goes past its heat limit. |
| 5 | **Crisis: spawn psyker squad** → stage 3 near a group of colonists. | The Mind Adept berserks a colonist standing with others, or pulses a group. |
| 6 | Cluster three or more colonists and wait. | Vertigo or Blinding Pulse lands on the group, not on a group with Imperial troops in it. |
| 7 | Put an Imperial soldier in front of one of your turrets (spawn a Praetorian near a turret) with a Ward Adept nearby at stage 2. | A Wallraise goes up between the turret and the soldier, or the soldier gets a Bullet Shield or smoke. |
| 8 | Draft a colonist and stand next to a Praetorian with a Skip Adept around. | The Skip Adept skips a colonist in cover out next to its Praetorians, or skips its own ally out of a melee. |
| 9 | Spawn a stage 4 squad and let it fight 2–3 in-game hours. | No errors in the log; the game keeps its speed. **Crisis: psyker report** still shows 0 unchosen casts. |
