# Crisis checkpoint 3 runbook: sites and captives (C9–C10)

One in-game session on the reference PC covers C9 and C10 (about two hours). Royalty must be active. Write what you
find in `crisis-results-3.md` next to this file, or tell Claude Code and it will.

## Setup

As for checkpoint 2: in PowerShell,

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git fetch
git checkout crisis-combat-locations
git pull
dotnet build RimChronicles.slnx
```

Start RimWorld with **Development mode** on. Use the checkpoint 2 test colony ("crisis-test-2") or a new one with six
to eight armed colonists, a prison room with a bed, and a warden with good Social. To reach the crisis quickly:
**Empire: trigger awakening**, **Empire: end stirring now**, choose **Stand against it** when the envoy comes, then
**Empire: expansion tick** twice and wait a day (**Crisis: show state** should say stage reclamation or war). Save it as
"crisis-test-3". When you're done, go back with `git checkout main`, `git pull`, `dotnet build RimChronicles.slnx`.

## C9: sites

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: batch sites**. | Each of the three archetypes: "50/50 pass on the first seed", 0 fallbacks; the pieces used include CommandRoom, RelayChamber, UplinkStation, PowerYard and Barracks. |
| 2 | **Crisis: generate site** → ForwardOutpost → seed 1. | You're taken to a new map: a stone-walled outpost with one gate, sandbags just inside it, a command room with a comms console deepest inside, a barracks and a power yard. Imperial troops stand guard. The log line says "attempt 1". |
| 3 | Look over the outpost. | A cleared road runs from the gate to the map edge. No room is shut off. The comms console and any turrets show as powered (conduits run toward them). |
| 4 | Destroy the comms console (Dev Mode: **Destroy** tool is fine). | Within a few seconds, a letter: "Outpost comms destroyed". |
| 5 | **Crisis: generate site** → PsychicRelay → seed 2, then OrbitalUplink → seed 3. | Bigger bases with two gates, turret nests (powered), maybe a mortar pit, armory or records room; the relay or uplink sits in a walled room far from the gates. Relay garrisons have adepts; uplink garrisons have heralds. |
| 6 | On the relay map, destroy the relay core. **Crisis: show state** back home. | "Psychic relay destroyed" letter. Then **Crisis: fire operation** → PsychicAssault → stage 3: inspect an arriving adept's health tab: **relay severed**. |
| 7 | On the uplink map, destroy the uplink. Back home, spawn a Herald and two Praetorians near your colonists. | "Orbital uplink destroyed" letter. No strike is called while the hold lasts (**Crisis: strike state**: "the colony knocked out the Empire's orbital uplink"). |
| 8 | **Crisis: generate site** → the same archetype and seed twice. | Both maps look the same. |
| 9 | From the save, wait for (or force with Dev Mode's plot tools) the Empire plot's **Empire:SabotageImperial** job. | A quest "Destroy the Imperial …" with a site 2–10 tiles away. Accept it, travel there, destroy the objective: the quest succeeds and pays its reward. |
| 10 | Save on a site map and reload. | The site is as it was; destroying its objective still sends the letter. |

## C10: captives

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Crisis: spawn captive** → imperial herald, click in the prison room. | A herald prisoner; a message says they're an Imperial herald and how to question them. Their resistance is about 45–60. |
| 2 | Prisoner tab: tick **interrogate (Imperial intel)**. Let the warden work (speed 3). | The warden goes to interrogate them; the social log shows "questioned … about …". Each session lowers resistance a little. |
| 3 | Keep going until a success. | A letter "Intel: uplink codes" or "Intel: next operation", saying what was learned. **Crisis: show state** lists it under Intel. |
| 4 | After two successes from the herald. | The warden no longer interrogates them; the work tab says they've "told all they know". |
| 5 | Spawn a regular prisoner (not Imperial) and tick interrogate. | The warden skips them: "knows nothing about the Empire". |
| 6 | **Crisis: grant intel** → next operation. Then **Crisis: show state**. | A forecast (operation and earliest day). When the crisis next fires on its own, it's that operation, not before that day. |
| 7 | **Crisis: grant intel** → uplink codes. Spawn a Herald near colonists. | The first strike called fails: "the colony's stolen uplink codes scrambled it". The next one is real. |
| 8 | **Crisis: grant intel** → relay location. | A Psychic Relay site appears on the world map (letter jumps to it). The next **PsychicAssault** (fired by the crisis, not Dev Mode) has about half its usual adepts. |
| 9 | With a captive held, **Crisis: fire operation** → Retrieval → stage 2. | "Imperial retrieval" letter; pods land on open ground near the prison. Soldiers dig or bash their way in; when one stands beside the captive, "has freed …", and they all leave together. |
| 10 | Interrogate a captive at least once, then recruit them (switch to recruit until they join). | "An Imperial defects" letter and a news item. **Crisis: show state** lists them as a defector. |
| 11 | **Crisis: fire operation** → Retrieval with only the defector held. | The squad goes for the defector. Down them in the fight's way: a soldier carries them off the map. |
| 12 | Release an Imperial captive. | A goodwill message; **Crisis: show state** shows "next budget x0.85". After the next operation fires, it's back to x1.00. |
| 13 | Execute one Imperial captive (or harvest an organ, or enslave one with Ideology). | **Crisis: show state**: the matching treatment count goes up. |
| 14 | On a relay or uplink site with a records room, carry the Imperial dispatch home and have a colonist read it. | A letter "Intel: …" from reading it; the dispatch is used up. |
| 15 | Save with captives, a defector and intel, and reload. | **Crisis: show state** lists the same captives, defector, forecast and intel. |

## Results

In `crisis-results-3.md`: each part's numbers with pass or fail, what failed and how, anything in the log (red errors
especially), and whether interrogation feels too fast or too slow.
