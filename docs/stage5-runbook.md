# Stage 5 runbook: the Awakened Empire in game

In-game testing happens once, at the end of the stage. Each milestone adds its checks here, and the final proof run
gathers them into one session. Royalty must be active. Before testing:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet build RimChronicles.slnx
```

Then restart RimWorld, with **Development mode** on.

## M1: Empire states

The Empire starts **Dormant**. From day 30 it can wake (move to **Stirring**) on any of these signs; with several, it
picks one by weight, and the choice goes into **Plot: director log** as `empire`:
- the colony destroys an Imperial settlement (weight 4);
- a colonist renounces an Imperial title (3);
- a colonist has an Imperial backstory (2);
- colony wealth reaches 150,000 (1);
- with no sign at all, day 300.

While stirring (20–40 days), a rumour reaches the news every 5–8 days: envoys, demands, recalled officers. Then the
Empire is **Awakened**, with a "The Empire awakens" letter. Waking and awakening count as beats: they wait for the
pacing gate and keep 2 days from any minor-plot beat. Expansion (M2) isn't in yet, so nothing happens after that.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Options → Mod settings → Rim Chronicles. | A **Major plot: the Awakened Empire** checkbox, on by default. |
| 2 | **Empire: show state** in a new or Stage 4 save. | `Dormant`, the Empire's name, and today's signs (wealth, any Imperial colonist). |
| 3 | **Empire: trigger awakening**, then **Empire: show state** and **Plot: director log**. | `Stirring`, with a change day 20–40 days on; the log has `Awaken:Dev`. |
| 4 | Let 10–15 days pass (**Increment time**), then open the Rim Dispatch's news log (or wait for bulletins). | Empire rumours appear in the news, and bulletins may mention them. |
| 5 | **Empire: end stirring now**. | "The Empire awakens" letter; the state is `Awakened`; the next bulletin may report the Empire's declaration. |
| 6 | Save, quit to the menu, and reload. | State, trigger and log are unchanged. No errors. |
| 7 | In a fresh save past day 30, renounce a colonist's Imperial title (character card), and wait a day. | **Empire: show state** shows the renounced title, and the Empire is `Stirring` by `TitleRefused`. |
| 8 | Turn the setting off, then **Empire: trigger awakening**. | Refused with a message; the daily tick does nothing. |

## M2: territory expansion

Once **Awakened**, the Empire attacks a settlement 7 days later, then every 15–30 days. Pure C#, no model. It picks
from other factions' settlements within 60 tiles of an Imperial one (the nearest, if none), favouring near and weak
targets. It never attacks your settlements, one with a map open, one a quest points at, or a faction's last settlement.
The result:
- **Conquered:** the settlement turns Imperial, in the Empire's colour on the world map.
- **Razed:** it becomes ruins.
- **Repelled:** nothing changes. Owners with more settlements hold more often (20% to 45%).

Each result goes to the news.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | With the Empire awakened (M1 checks 3 and 5), **Empire: expansion tick** a few times. | A dev log line per attack; **Empire: show state** counts them and gives the next attack day. |
| 2 | Open the world map after a conquest. | That settlement has the Empire's icon and colour, and its inspect pane names the Empire. |
| 3 | After a razing, look at the world map. | Ruins in its place; no letter and no raid against you. |
| 4 | Open the Rim Dispatch news log, then wait for a bulletin. | "… took X from Y", "… razed X", "… drove off an Imperial attack on X"; a bulletin reports at least one. |
| 5 | Visit a conquered settlement by caravan to trade. | It trades as an Imperial settlement, with no errors. |
| 6 | Save, reload, and **Empire: show state**. | The expansions and the next attack day are unchanged. |
| 7 | Let 30+ days pass without dev actions. | An attack happens on its own. |

## M3: Escort, Defend ally, Sabotage

The three templates left over from Stage 4. Each Dev Mode test action offers its quest for the oldest active plot
(seed one first, e.g. **Nemesis: force seed**). **Quest: show links** lists them, and each outcome reaches the plot the
next day, as in Stage 4.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Quest: Escort (test)**, accept it. | A traveller of the destination's faction walks in and joins the colony for now. |
| 2 | Form a caravan with them and travel to the named settlement. | On arrival the quest succeeds, silver drops at home, and the traveller leaves the caravan for their faction. |
| 3 | Another Escort; let the traveller die, or let 20 days pass. | The quest fails, and a living traveller leaves the colony. |
| 4 | **Quest: Defend ally (test)**, accept it. | A hostile camp appears 1–4 tiles from the friendly settlement named in the quest; "jump to" points there. |
| 5 | Send a caravan, attack the camp and kill or drive off every enemy. | The quest succeeds and silver drops at home. Ignoring it until the camp vanishes (15 days) fails. |
| 6 | **Quest: Sabotage (test)**, accept it, and attack the named hostile settlement. | A comms console of that faction stands near the middle of its map. |
| 7 | Destroy the console. | The quest succeeds and silver drops at home. Leaving and coming back before destroying it finds a new console. |
| 8 | Save and reload with each quest under way. | No errors, and each still completes. |
