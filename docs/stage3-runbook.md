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
