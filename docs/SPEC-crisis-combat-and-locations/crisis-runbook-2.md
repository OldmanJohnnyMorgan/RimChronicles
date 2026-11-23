# Crisis checkpoint 2 runbook: strikes, operations, sieges (C5–C8)

One in-game session on the reference PC covers C5 to C8 (about two to three hours). Royalty must be active. Write
what you find in `crisis-results-2.md` next to this file, or tell Claude Code and it will. Note anything that feels
too easy or too hard: those are the tuning notes for C14.

## Setup

The crisis lives on its own branch. In PowerShell:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git fetch
git checkout crisis-combat-locations
git pull
dotnet build RimChronicles.slnx
```

Start RimWorld with **Development mode** on. Every tool below is in the Debug actions menu under **Rim Chronicles**.

**The test colony.** A new colony on a medium map, open ground around it (not a mountain base), built up with Dev
Mode's god mode:

- a stone wall around the base with two or three doors;
- four or five mini-turrets just inside the wall, and one mortar with shells;
- six to eight armed colonists (rifles, some armor), one of them the ideoligion's leader, and one with Medicine
  for doctoring;
- a growing zone outside the wall and a freezer with food inside.

Save it now (**Save as "crisis-test-2"**): most parts start from this save, so a lost fight costs nothing. Speed 3 and
**Increment time** are fine between fights.

Most operations need a stage: **Crisis: fire operation** → the operation → stage 2 (reclamation) or 3 (war). Run each
at stage 2 first, then stage 3 from the save.

When you're done, go back to the normal branch:

```powershell
git checkout main
git pull
dotnet build RimChronicles.slnx
```

## C5: orbital strikes

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Spawn a Herald and two Praetorians (**Crisis: spawn unit**) where they can see a turret or a few colonists. | Within a minute, an **Orbital strike called** letter; the target flashes; it lands about 8 seconds later. |
| 2 | Call one (**Crisis: call orbital strike**) and down the Herald during the countdown. | "The orbital strike was called off"; nothing lands. |
| 3 | Stand colonists under a mountain roof (or check a mountain room if the map has one). | No strike aims there (**Crisis: strike state** marks them "under mountain"). |
| 4 | Let a raid strike until the cap. | **Crisis: strike state** says "this raid's N strikes are spent"; no more strikes. |
| 5 | Save during a countdown and reload. | The strike still lands (or is called off) after the reload. |

## C6: probing raid and decapitation

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: fire operation** → ProbingRaid → stage 1. | **Imperial probe** letter; Praetorians walk in and attack. |
| 2 | Let it fight two in-game hours, or kill a quarter of it. | It leaves; **Crisis: show state** shows ProbingRaid succeeded (time) or repelled (losses), and the doors it found are kept. |
| 3 | Down a probe soldier far from your colonists during the retreat. | Another soldier carries them off the map. Down one next to your colonists: it's left behind. |
| 4 | **Crisis: fire operation** → Decapitation → stage 3, with the leader outdoors. | **Imperial drop pods** letter naming them; pods land on open ground 3–12 cells away; the squad goes for them. |
| 5 | Put the leader indoors under a roof (a mountain roof if there is one), then fire it. | Pods land outside, never inside or under a roof. |
| 6 | Watch a stage 3 fight with psykers. | Adepts stay near Praetorians; the squad's shots go to one colonist at a time (medics first). |
| 7 | Down the leader, or make peace with the Empire mid-raid (Dev Mode: set goodwill). | The squad pulls out. |
| 8 | Save mid-operation and reload. | It carries on; no errors. |

## C7: breach and psychic assault

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: fire operation** → Breach → stage 2. | **Imperial breach** letter; grenadiers blast through a wall and the rest follow; EMP grenades land on turrets, which stop. |
| 2 | Hurt it badly (about 40%). | It falls back to where it came in; a few minutes later reinforcements drop in and it attacks again. |
| 3 | **Crisis: fire operation** → PsychicAssault → stage 2, then stage 3 from the save. | **Imperial psychic assault** letter; about as many adepts as Praetorians; stage 2 has no Mind Adepts, stage 3 does. |
| 4 | Watch with **Crisis: psyker debug on/off**. | Adepts cast throughout, including during the breach. |

## C8: sieges

Fire every siege from the save, at stage 3. Speed 3 and **Increment time** help while the camp is built.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: show siege scoring**. | Numbers around the colony, brighter further from the turrets and behind cover; `x` inside turret range and on rock or water; one **BEST**. The window lists the five best with their parts (close, cover, ground, flanks…) and why the others were ruled out. |
| 2 | Fire a **ProbingRaid** and let it leave, then **Crisis: show siege scoring** again. | The doors it found are marked "door"; spots facing them score an "entrance" part. |
| 3 | **Crisis: fire siege with goal** → Goal:BreakWalls → stage 3. | **Imperial siege** letter that says the strikes will go for walls and turrets; it jumps to the camp spot (the BEST one). The force walks in from the nearest edge. |
| 4 | Watch the camp go up without attacking it. | Sandbag lines facing the colony and a barracks behind come first, with materials by drop pod; "sandbag lines up". Only then the mortar pits (blueprints appear only after stage 1 is all built), then the uplink, then the shield and relay, then the beacon, each with its message. No stage's blueprints appear while the one before still has any. |
| 5 | Once the mortars are up. | The builders man them and shells start falling on the colony; shells and meals are dropped in when they run low. |
| 6 | Once the uplink is up, kill or down the Herald (or check one isn't needed). | Strikes still come, marked and announced ("An Imperial uplink has called…"); with BreakWalls they go for walls and turrets (**Crisis: strike state** shows "Siege goal: Goal:BreakWalls" and "Uplink standing: yes"). |
| 7 | Once the shield is up, fire your mortar at the camp. | Your shells are stopped at the shield's bubble. An EMP shell knocks it out for a while. |
| 8 | Inspect an Imperial adept near the relay. | Health tab: **psychic relay link**. Walk them out of range (or destroy the relay): it fades within about ten seconds. |
| 9 | Once the beacon is lit, wait a quarter day. | "Imperial reinforcements are dropping in at the siege beacon", up to three times. |
| 10 | From the save: fire a siege, then **break it early**: attack the camp while it's at stage 1 or 2 and kill or down more than half of the builders, keeping your own losses low. | "The Imperial siege has lost most of its builders… it's coming in": the rest attack. |
| 11 | From the save: fire a siege and cut it down hard (kill half the force) before its builders fall. | It retreats instead of attacking, carrying off downed soldiers where no colonist is close. The blueprints and frames vanish; the built pieces stay as neutral ruins, and the uplink no longer counts (**Crisis: strike state**: "Uplink standing: no"). |
| 12 | Fire a siege and leave it alone for two in-game days. | "The Imperial siege is done digging in: it's coming in." |
| 13 | Fire a siege with **Goal:Starve**, and another with **Goal:KillLeader** (leader outdoors). | Starve: strikes land on fields, the freezer and power. KillLeader: strikes aim at the leader whenever they're outside and the uplink or a Herald can see them. |
| 14 | Save while the camp is at stage 2 or 3, reload. | The camp carries on from the same stage; no errors. |
| 15 | **Crisis: show state** after a siege ends. | SiegeWithBombardment with its outcome (repelled, or running while it attacks). |
| 16 | Try a map where no spot qualifies (turrets covering everything, or a small island). | **Crisis: fire siege with goal** says there's no camp spot; nothing spawns. |

## Results

In `crisis-results-2.md`: each part's numbers with pass or fail, what failed and how, anything in the log (red
errors especially), and how hard each operation felt at stage 2 and 3.
