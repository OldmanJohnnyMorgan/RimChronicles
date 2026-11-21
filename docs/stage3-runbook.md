# Stage 3 runbook: minor plots in game

In-game checks for each Stage 3 milestone, run on the reference PC. Before each section:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet build RimChronicles.slnx
```

Then restart RimWorld. Keep **Development mode** on.

## M1: the plot engine

M1 adds no arc yet, only the engine, so this checks that nothing breaks.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Load an existing save (one from Stage 2), and run **Debug actions → Rim Chronicles → Show plots**. | A letter shows `Active: 0` and a pacing-gate reading. No red errors on load. |
| 2 | Execute a raid (**Debug actions → Incidents → Raid**), then **Show plots** while raiders are on the map. | `Pacing gate: the colony is under attack`. After the raid ends, it reads `open`. |
| 3 | Save, quit to the menu, and load. | Loads with no red errors. **Show plots** still works. |

## M2: the Nemesis seed

M2 starts Nemesis plots but doesn't run their beats yet (M3), so a plot stays at `Hook`. The actions are under
**Debug actions → Rim Chronicles**: **Nemesis: show grudges**, **Nemesis: force seed** and **Show plots**. With Dev mode
on, the debug log prints a `Grudge:` line when one is recorded, and a line when a plot starts.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Nemesis: force seed**. Then **Show plots**. | A message names the new plot (`nemesis-1`). **Show plots** lists it at `Hook`, with a protagonist and facts: `nemesis`, `nemesisName`, `victimName`, `grievance=maimed`. |
| 2 | **Nemesis: show grudges**. | One grudge: the raider, `maimed`, the colonist, `arm (Dev Mode test)`, `escaped, in the world`, `in nemesis-1`. |
| 3 | Save, quit to the menu, and load. Then **Show plots** and **show grudges** again. | The same plot and grudge, with the raider still `in the world`. |
| 4 | Play a few days, then **show grudges**. | The raider is still `in the world`, not `lost track`: the world-pawn cleanup kept them. |
| 5 | A real grudge: execute a **Raid**, and let a raider kill a colonist, or take off an arm, leg, hand or eye (**Debug actions → Damage** helps). Let the raid retreat with that raider alive. | A `Grudge:` line in the debug log at the moment of the injury. The next day, **show grudges** says `escaped`. |
| 6 | After row 5, if no other Nemesis plot started in the last 15 days, play a day. | A second plot starts. For a killing, the protagonist is the victim's kin or partner if they have one in the colony, else the colonist who liked them most. The debug log names the rule used. |
| 7 | Capture or kill a raider with an open grudge before they escape, then play a day. | **show grudges** no longer lists that grudge. |

Send me pass or fail per row, and the **Show plots** text from row 6.
