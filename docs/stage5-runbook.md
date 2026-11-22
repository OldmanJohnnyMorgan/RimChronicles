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
