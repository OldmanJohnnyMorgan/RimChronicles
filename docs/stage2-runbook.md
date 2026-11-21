# Stage 2 runbook: the news agent in game

In-game checks for each Stage 2 milestone, run on the reference PC. Before each section:

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet build RimChronicles.slnx
```

Then restart RimWorld so it loads the new DLLs. Ollama should be running, with the Gemma models pulled.

## M1: settings window

Open **Options → Mod settings → Rim Chronicles**.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Look at the page. | It shows the four tiers (Recommended selected), the server address, model, API key, a **Test connection** button and a news cadence slider at 3 days. |
| 2 | Click **Test connection**. | Within a minute (longer on the first call, while the model loads): `Connected to gemma4:e4b in N s.` in green. |
| 3 | Pick **Minimum**, then **High**. | The model box changes to `gemma4:e2b`, then `gemma4:12b`. **Test connection** works for each one you have pulled. |
| 4 | Change the model box to `nonexistent:1b` and test. | A red failure, with a hint to `ollama pull` the model. |
| 5 | Quit Ollama and test. | A red failure that suggests checking the server is running. Start Ollama again afterwards. |
| 6 | Change the address to `localhost:11434` (no `http://`). | An orange warning under the address. Testing says to fix the address. |
| 7 | Pick **Off**, then load a game and run **Debug actions → Rim Chronicles → Test LLM letter**. | A **radio silence** letter, saying the tier is Off. |
| 8 | Set the slider to 5 days, pick **Recommended**, close the settings, and restart RimWorld. | The page reopens with Recommended, `gemma4:e4b` and 5 days, and the debug log's `Loaded` line says `tier Recommended`. |
| 9 | Click **Reset to defaults**. | Recommended, `http://localhost:11434/`, `gemma4:e4b`, no key, 3 days. |

Send me pass or fail for each row, and a screenshot of the page if anything looks cramped or cut off.

## M2: world news collector

Load a colony with **Development mode** on. "Debug actions" is the second icon in the dev toolbar. The news log is
**Debug actions → Rim Chronicles → Show news log**. With Dev mode on, the debug log also prints a `[Rim Chronicles] News:`
line each time news is recorded.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Show news log** on a fresh colony. | "No world news yet". |
| 2 | **Debug actions → Incidents**, execute **Cold snap**, then **Toxic fallout**. | The log has `The cold snap has begun around <colony>` and `The toxic fallout has begun …`, once each. |
| 3 | Open the **Factions** tab. Dev mode adds goodwill buttons: lower a neutral faction until it turns hostile, then raise it back until it's neutral. | `<faction> turned hostile to <colony>`, then `<faction> made peace with <colony>`. Each change is logged once. |
| 4 | Let a condition run 3+ days (use dev speed or **Debug actions → Time → Advance 1 day**), or wait for it to end. | `The cold snap around <colony> has ended`, logged only if it lasted 3 days or more. |
| 5 | Save, quit to the menu, and load the save. Then **Show news log**. | The same entries as before. Loading added nothing new. |
| 6 | **Dump world snapshot**. | The file's `publicEvents` lists the news from the last 15 days. |
| 7 (optional) | Kill a faction leader (find one in the **Factions** tab), or destroy a settlement with a caravan. | `<name>, <title> of <faction>, has died…`, or `<colony> destroyed <settlement>…`. |

Send me pass or fail per row, the news log text from row 5, and the snapshot from row 6, uploaded to `reference`.
