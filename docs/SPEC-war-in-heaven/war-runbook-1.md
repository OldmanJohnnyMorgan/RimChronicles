# War in Heaven checkpoint 1 runbook: sides, the Hollow Choir, waking (W2–W5)

One in-game session on the reference PC covers W2 to W5. Royalty and Biotech must be active. Write what you find in
`war-results-1.md` next to this file, or tell Claude Code and it will. Each milestone adds its steps here as it is
built; run the whole runbook once W5 is merged.

## Setup

The War in Heaven lives on its own branch. In PowerShell:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git fetch
git checkout war-in-heaven
git pull
dotnet build RimChronicles.slnx
```

Start RimWorld with **Development mode** on. Use a **new test colony** for most steps, and one **save from `main`**
with the Empire plot under way for step W2.1. Every Dev Mode tool below is in the Debug actions menu under
**Rim Chronicles**. When you're done, go back to `main` the same way (`git checkout main`, `git pull`, build).

## W2: allegiance

| # | Do | Pass when |
| --- | --- | --- |
| W2.1 | Load a save from `main` where the Empire plot has a stance (answer the envoy first if needed). Let a day pass, then **War in Heaven: show allegiance** | No errors on load. The allegiance shows the Empire stance the plot had, with cause `plot` |
| W2.2 | New colony: **Empire: trigger awakening**, answer the envoy "Stand with the Empire", let a day pass, **show allegiance** | `empire: ally`, side `empire` |
| W2.3 | **War in Heaven: set side** → Independent; **show allegiance**; open the Factions tab | `empire: oppose`, side `independent`, cause `dev`. Once the Empire plot is past the envoy, the Empire is hostile |
| W2.4 | **Empire: set stance** → ally; let a day pass; **show allegiance** | The allegiance follows the plot back to `empire: ally` |
| W2.5 | Save, reload, **show allegiance** | The same stances and log as before saving |
