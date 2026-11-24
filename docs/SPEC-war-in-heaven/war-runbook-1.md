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

## W3: the Hollow Choir's faction

| # | Do | Pass when |
| --- | --- | --- |
| W3.1 | New colony with Royalty and Biotech: open the Factions tab and the world map before anything else | No Hollow Choir anywhere: it is never made at world creation |
| W3.2 | **Hollow Choir: create faction**; open the Factions tab and the world map | **The Hollow Choir** is listed, its leader titled First Voice, hostile to the Empire. About four of its settlements sit 8–30 tiles from the colony, plus one elsewhere |
| W3.3 | **Hollow Choir: raid (1,500 points)** while the Choir isn't hostile to you | A group of mechanitors and mechs arrives and holds its ground; the message counts both |
| W3.4 | Make the Choir hostile (Factions tab, Dev Mode goodwill buttons, down to -100), then raid again and fight it for a day | Mechanitors and mechs attack together under the vanilla assault: no errors, no mech stops for lack of power, no mech goes feral. (Killing a mechanitor doesn't stop its mechs yet; that comes with W7) |
| W3.5 | Save, reload; **Hollow Choir: create faction** again | The faction and its settlements are still there, and no second Choir appears |

## W4a: the Choir's saga

| # | Do | Pass when |
| --- | --- | --- |
| W4a.1 | Mod settings → Story | **The Hollow Choir** checkbox sits under the major plot, on by default |
| W4a.2 | New colony: **Hollow Choir: show** | Running: yes; State: Dormant; no faction |
| W4a.3 | **Hollow Choir: trigger stirring**; let 10–20 days pass (Dev Mode time skip) | A news line about a hum at night; later rumours of the hum and of mechlink strangers in the news. No faction yet |
| W4a.4 | **Hollow Choir: wake now** | A "The Hollow Choir awakens" letter; the faction appears with settlements near the colony (as W3.2); **show**: Awakened, next expansion about a week on |
| W4a.5 | Before W4a.4, in another colony: side with the Empire (**War in Heaven: set side**), then wake the Choir | The Choir starts hostile to the colony |
| W4a.6 | **Hollow Choir: expand now** a few times; open the world map | Settlements change hands or burn, in the Choir's colour, with news lines |
| W4a.7 | New colony with a mechanitor colonist (or install a mechlink), wait for day 30+ | The Choir stirs on its own; **show** says a mechanitor drew it |
| W4a.8 | Turn the Hollow Choir setting off, or play without Biotech | **show** says it isn't running; nothing happens |
| W4a.9 | Save and reload in each state | The state, log and faction survive |

## W4b: the Choir's plot and ranks

| # | Do | Pass when |
| --- | --- | --- |
| W4b.1 | **Hollow Choir: wake now**; let a few days pass | The Chronicle tab shows "The Hollow Choir"; a "Chorus envoy" letter asks the colony's stance |
| W4b.2 | Answer "Stand with the Choir"; let days pass | The Choir marches (letter); escort or conquest jobs for the Choir appear as quests; **War in Heaven: show allegiance** says side `choir`, Empire `oppose` |
| W4b.3 | Finish a Choir job (or **Hollow Choir: grant rank favor** three times) | A "rise in the Chorus" letter names the colonist; after 12 favor (Choirmaster) **Hollow Choir: show**… the offer can come in Act 3 |
| W4b.4 | Other colony: "Stand against it" | Defend-ally and sabotage jobs against the Choir appear; the Choir is hostile |
| W4b.5 | Other colony: "Keep out of it" | A tribute letter with "Join the Chorus" as the third choice |
| W4b.6 | Dev Mode decision log (**Plots: show decisions** or the director log) | Choir plot decisions show source `Random`, never `Model` |
| W4b.7 | Save and reload mid-plot | The plot, its facts and the ranks survive |

## W5: cross-waking, the ship launch and the Royal Ascent

| # | Do | Pass when |
| --- | --- | --- |
| W5.1 | New colony: **Empire: trigger awakening**, then **Empire: wake**, then **Empire: expand now**; let a day pass | The Choir wakes at once (no stirring): its letter, its faction; **Hollow Choir: show** says the Empire's attack woke it |
| W5.2 | New colony: **Hollow Choir: wake now**, **Hollow Choir: expand now**; let a day pass | The Empire wakes at once; **Empire: show** says the Choir woke it |
| W5.3 | New colony, both asleep: build and start a ship reactor (Dev Mode: spawn the ship parts) | The Empire wakes at once, goes to war and raids; the next day the Choir wakes too (the Empire attacked) |
| W5.4 | New colony with a Royal title: accept the Royal Ascent quest (or **Hollow Choir: react to the Royal Ascent**) | The Choir wakes at once, goes to war and raids; while the ascent runs, another raid every 4 days; the Empire wakes after the first raid |
| W5.5 | As W5.4, but side with the Choir first (**War in Heaven: set side**) | The Choir wakes but doesn't raid |
