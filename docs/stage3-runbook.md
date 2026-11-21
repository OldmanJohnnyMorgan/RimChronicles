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

## M3a: the Nemesis beats (template letters)

The beats run with template letters (M3b adds letters written by the model). **Plot: advance beat** fires the oldest
plot's next beat at once, ignoring the 5–15 day wait and the pacing gate. Save before row 1, so you can reload to try
both choices.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Nemesis: force seed**, then **Plot: advance beat**. | A letter **Nemesis: <name> sighted** names the raider, what they did, and a settlement of their faction. **Show news log** has `The raider <name> of <faction> was seen near <place>`. |
| 2 | **Plot: advance beat** again. | A raid by the nemesis's faction, plus a letter **Nemesis: <name> leads the raid**. The nemesis is on the map with the raiders. |
| 3 | Let the nemesis escape (keep them alive), wait until the raid has left, then **Plot: advance beat**. | A choice letter **Nemesis: what to do about <name>**, with **Post a bounty (N silver)**, greyed out if there isn't enough silver on the map, and **Wait for them to return**. |
| 4 | Choose **Post a bounty**, then **Plot: advance beat**. | The silver is gone from the map. A letter **the bounty is collected**, the protagonist gains **score settled** (+6 mood), and **Show plots** lists the plot under ended (`KilledByBounty`). |
| 5 | Reload the save from before row 1, and play up to the choice letter again. Choose **Wait**, then **Plot: advance beat**. | A second raid with the nemesis: **Nemesis: <name> returns**. Kill the nemesis: a letter **<name> is dead**, the protagonist gains **avenged** (+10 mood), and the plot ends `KilledByColony`. |
| 6 (optional) | Instead of killing them in row 5, capture the nemesis (down them, then capture). | The next day: **<name> captured**, and the plot ends `Captured`. |
| 7 | Leave a choice letter unanswered for 5 days. | It disappears, and the next beat is the second raid (the colony waited). |

Send me pass or fail per row, and anything in the letters that reads oddly.

## M3b: letters written by the model

### 1. Harness trial of the plot-letter prompt (rule 8)

Four hand-written snapshots now carry a Nemesis letter case: a sighting (`snap-07`), a raid (`snap-16`), a choice
(`snap-12`) and an ending (`snap-09`). This runs only the new task, 3 times each, on the three Gemma models, with the
judge: 36 judged letters, well under $1. Start Ollama first.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet run --project harness/RimChronicles.Eval -- run --tasks plot-letter --models gemma4-e2b,gemma4-e4b,gemma4-12b --judge
```

Push the run to the results branch as before, with the run ID from the `Report:` line:

```powershell
$run = "<new runId>"
cd ..\rc-results
git pull
Copy-Item -Recurse ..\RimChronicles\results\$run results\
git add -f results\$run
git commit -m "Stage 3 M3b trial: plot-letter v1 (run $run)"
git push
cd ..\RimChronicles
```

### 2. In game

With Dev mode on, each plot letter ends with a grey note of who wrote it: `(Model, plot-letter/v1)` or `(Template)`.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Nemesis: force seed**, then **Plot: advance beat** four times, letting the raid leave before the choice. | The sighting, raid and choice letters arrive a few seconds after each beat, marked `(Model, …)`. They name only the raider, the faction, the colonists and places the template named. The choice letter gives the price, and its buttons work as in M3a. |
| 2 | Pick **Off** in the mod settings, and run the arc again. | The same letters arrive at once, marked `(Template)`, with no red errors. |
| 3 | Pick **Recommended** again, then quit Ollama and fire a beat. | After the connection times out (up to a minute), the letter arrives as `(Template)`, with a yellow `fell back to its template` line in the debug log. |

Paste me two or three of the model's letters.
