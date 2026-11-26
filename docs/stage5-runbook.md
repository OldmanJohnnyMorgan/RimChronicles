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
- colony wealth reaches 80,000 (1);
- with no sign at all, day 120.

While stirring (10–20 days at normal story intensity), a rumour reaches the news every 5–8 days: envoys, demands, recalled officers. Then the
Empire is **Awakened**, with a "The Empire awakens" letter. Waking and awakening count as beats: they wait for the
pacing gate and keep 2 days from any minor-plot beat. Expansion (M2) isn't in yet, so nothing happens after that.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Options → Mod settings → Rim Chronicles. | A **Major plot: the Awakened Empire** checkbox, on by default. |
| 2 | **Empire: show state** in a new or Stage 4 save. | `Dormant`, the Empire's name, and today's signs (wealth, any Imperial colonist). |
| 3 | **Empire: trigger awakening**, then **Empire: show state** and **Plot: director log**. | `Stirring`, with a change day 10–20 days on; the log has `Awaken:Dev`. |
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

## M4: acts and stance

The Empire is now a plot in the plot book (`awakenedempire-N`, not counted toward the 3-plot cap), so the director
picks its jobs with reasons and its beats keep 2 days from other plots.

- **Act 1 (stirring):** a few days after the Empire wakes, an **Imperial envoy** letter asks the colony's stance: stand
  with the Empire, against it, or keep out. No answer in 5 days means keeping out.
- **Act 2 (awakened):** "The Empire marches". Allying raises Imperial goodwill to at least +25; opposing means war
  (goodwill -100). Then, every 6–12 days, the director picks a job that fits the stance:
  - **Ally:** escort Imperial envoys to an Imperial settlement (goodwill +12, or -8 on failure), or sabotage a settlement the Empire means to take (it then falls to the Empire).
  - **Oppose:** break an Imperial siege of a neighbour (success counts as an Imperial defeat; failure, the neighbour falls), or sabotage an Imperial settlement (an Imperial defeat).
  - **Keep out:** a tribute demand (pay, refuse, which means war, or swear fealty, which means alliance). Ignoring it costs 10 goodwill.
- **Act 3:** after 5 expansions or 3 finished jobs, the reckoning. The Empire is **Ascendant** (its conquests and the
  colony's help outweigh its defeats and the colony's resistance) or **Broken**. The endings come in M5.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Empire: trigger awakening**, then let 2–5 days pass. | The Imperial envoy letter, with three choices. **Plot: director log** has `Act1:ImperialEnvoy`. |
| 2 | Choose **Stand against it**, then **Empire: end stirring now** and let a few days pass. | "The Empire marches"; the Empire turns hostile. |
| 3 | Let 6–12 days pass, or **Plot: advance beat** on the Empire plot. | A Break the siege or Sabotage quest, chosen by the director with a reason. |
| 4 | Finish or ignore that quest. | The next day, the aftermath: the news reports it, and a lost siege turns the neighbour Imperial on the world map. |
| 5 | **Empire: set stance** → ally; repeat 3–4. | Escort or "Weaken X" quests; finishing "Weaken X" makes X Imperial. |
| 6 | **Empire: set stance** → neutral; advance a beat. | A tribute demand; paying takes the silver; refusing sends "At war with the Empire". |
| 7 | **Empire: expansion tick** until 5 expansions, then let the plot's next beat come. | "The Empire ascendant" or "The Empire breaking"; **Empire: show state** shows `Ascendant` or `Broken`. |
| 8 | Save and reload in the middle of a job. | No errors; the job's outcome still reaches the plot. |

## M5: endings

Act 3 opens the ending paths, which depend on the stance and on whether the Empire is Ascendant or Broken:

| Stance | Ascendant | Broken |
| --- | --- | --- |
| Ally | Keep serving (escorts, "Weaken X"); each success gives 4 royal favor to the colonist with the most. Once a colonist is a **Knight or above**, a **governorship** letter: accept → **Imperial victory**. | Same. |
| Oppose | **Storm the capital** (the Empire's oldest settlement, 60 days): destroy it → **Resistance victory**. | Storm the capital, or a **treaty** letter: sign → **Independence**. |
| Keep out | The **final demand**: submit (ally) or refuse (war; ignoring it is refusing). | A **treaty** letter: sign → **Independence**; fight on → opposed. |

An ending shows the epilogue on the victory screen (the game carries on afterwards, as after vanilla's royal ascent),
and keeps it as a letter. The model writes it with the new **epilogue** prompt, naming up to four colonists who
mattered: a title holder, protagonists of the colony's stories, then its most skilled hands. With the model off or
failing its checks, the template epilogue is used.

**Ship launch:** while the Empire is Awakened or Ascendant and the colony isn't its ally, starting a ship reactor means
war and an Imperial raid at once, then another every 4 days until the reactor is running.

Run the free epilogue harness first (three cases, one per ending), and push it to `rc-results`:

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --tasks epilogue --models gemma4-e2b,gemma4-e4b,gemma4-12b
```

| # | Do | Pass when |
| --- | --- | --- |
| 1 | With the Empire in Act 3 (M4 check 7), **Empire: reach an ending** → each of the three, from a save. | The victory screen shows "Rim Chronicles: …" and an epilogue naming colonists; the letter stays in the archive; the game continues. |
| 2 | Opposed, Act 3: let the director offer **Storm X**, accept, and destroy X with a caravan. | Resistance victory. **Empire: show state** says `Broken`, ending `Resistance`. |
| 3 | Allied, Act 3: Dev Mode → give a colonist the Knight title (vanilla Royalty dev action), wait a day, advance a beat. | The governorship letter; accepting gives the Imperial victory. |
| 4 | Neutral or opposed against a Broken Empire: advance a beat. | The treaty letter; signing gives Independence, and the Empire is no longer hostile. |
| 5 | Neutral against an Ascendant Empire: advance a beat, then ignore the letter for 5 days. | The final demand; ignored, it means war. |
| 6 | Opposed or neutral, Empire awake: build and start a ship reactor (Dev Mode god mode). | "The Empire will not let you leave", a raid at once, another 4 days later while it starts up. |
| 7 | With the model off (tier Off), reach an ending. | The template epilogue appears. |

## M6: promotion

Minor plots can join the major one (the spec's "Links to minor plots"):
- **A nemesis becomes an Imperial officer.** While the Empire is awake and at war with the colony, a Nemesis plot
  between its sighting and its choice letter gets a director option, `Promote:<Name>JoinsTheEmpire`. If chosen, the
  nemesis joins the Empire: a letter and news, and the raids they lead from then on are Imperial.
- **A relative caught in a conquest.** When the Empire takes a settlement of the faction a Lost Family relative lives
  with (after the first letter, before they're taken), the relative is caught in the fall: the Empire becomes the
  captor and the Rescue quest is against it.

Both are noted in the Empire plot's log and **Empire: show state**, and the epilogue can mention them.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Empire awake and at war (stance Oppose); **Nemesis: force seed**, let the sighting come. **Empire: promote a minor plot** → the nemesis. | "<Name> in Imperial colours"; the nemesis's faction is the Empire; news reports it. |
| 2 | Let the nemesis's raid come. | Imperial troops, led by the nemesis. |
| 3 | Without the dev action: let a few Nemesis beats pass with the Empire at war. | **Plot: director log** offers `Promote:…JoinsTheEmpire` among the options (and sometimes picks it). |
| 4 | **Lost Family: force seed**, let the first letter come. **Empire: promote a minor plot** → the Empire takes a settlement of the relative's faction. | The settlement turns Imperial; "<Kin> caught in the war"; the Rescue quest that follows names the Empire as captor. |
| 5 | Reach an ending (**Empire: reach an ending**). | The epilogue's facts include the promotions (raw output, or the template's events in Dev Mode). |

## M7: the proof run

One session, and the only in-game round for Stage 5. Royalty on, Development mode on.

**Before you start** (free local runs, no judge; push both to `rc-results`):

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet build RimChronicles.slnx
dotnet run --project harness/RimChronicles.Eval -- run --tasks epilogue --models gemma4-e2b,gemma4-e4b,gemma4-12b
dotnet run --project harness/RimChronicles.Eval -- run --tasks plot-letter --models gemma4-e2b,gemma4-e4b,gemma4-12b
```

The first checks epilogue v2; the second is the plot-letter v3 run still open from Stage 4. Then restart RimWorld fully
(new defs: the Empire plot and three quest scripts).

### Part A: the milestone checks

Work through the M1–M6 tables above in one colony. Save before each milestone (`s5-m1`, `s5-m2`, …), so Part B can
start from them. Note pass or fail per row.

### Part B: the spec's exit test

1. **Each of the three endings, reached in play** (Dev Mode may speed time and seed plots, but not **Empire: reach an
   ending**). From a save in Act 2 (M4), make three copies:
   - **Resistance:** stance Oppose; play to Act 3; accept **Storm <capital>**; destroy it with a caravan.
   - **Imperial:** stance Ally; finish jobs (and, to save time, give a colonist the Knight title with vanilla's
     Royalty dev action); accept the governorship.
   - **Independence:** stance Oppose or neutral; push the Empire to **Broken** (win sieges and sabotage; **Empire:
     expansion tick** helps reach the reckoning); sign the treaty.

   Each passes when the victory screen shows an epilogue naming colonists, and the game carries on.
2. **Territory on the map and in bulletins:** across the runs, at least three conquests or razings show correctly on
   the world map (Imperial colours, or ruins), and at least two reach a bulletin.
3. **A promotion:** at least one minor plot promoted (M6), by the director or the dev action.
4. **Saves:** save and reload once in each run, mid-act. No red errors.

**Send me:**
- pass or fail per row (Part A) and per step (Part B);
- the three epilogue texts (they're kept as letters);
- **Plot: director log** from one run;
- any red `[Rim Chronicles]` errors, straight away if something breaks early.

## Settings from the spec (after M7 started)

Three settings the design spec lists, added while the proof run was under way. Each defaults to how the mod behaved
before, so they don't affect the proof run. Check them afterwards:

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Open Options → Mod settings → Rim Chronicles. | The page scrolls; **Story intensity**, three **Story arcs** checkboxes, and **Separate prose model** are at the bottom. |
| 2 | Turn **Nemesis** off; give a raider a grudge (kill a colonist and let them escape). | No Nemesis plot starts on its own; **Nemesis: force seed** still works. |
| 3 | Set intensity to 2×; seed two plots a few days apart with the dev actions, then watch the daily log. | The "next may start on day N" gap is 8 days, not 15. |
| 4 | Set a prose model (e.g. the 12B) with the main tier at E4B; force a bulletin. | The bulletin's grey Dev Mode footer shows it came from the model; the Ollama log shows the prose model was called. Director decisions still use the main model. |
| 5 | Save the settings, restart RimWorld. | All three settings kept. |
