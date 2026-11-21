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

## M3: the news pipeline

The reporter is a placeholder (Ilse Varrow of the Rim Dispatch) until M4. Keep **Development mode** on: bulletins then
show where they came from (`Model` or `Template`) in grey under the text, and the debug log notes when each is written.
**Show news desk** and **Force bulletin** are under **Debug actions → Rim Chronicles**.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Load a colony (Recommended tier, cadence 3). Let a day pass, then **Show news desk**. | It shows the next due day, about 3 days out. |
| 2 | Play on to 2 days before that day. | The debug log says `Bulletin written (Model), due day N`, and the game doesn't stutter while it's written. **Show news desk** shows the waiting text. |
| 3 | Play to the due day. | A letter **The Rim Dispatch, day N** arrives with that exact text, and `(Model, news-bulletin/v8)` underneath. The next due day is 3 days later. |
| 4 | Execute **Cold snap**, then **Force bulletin**. | The bulletin leads with the cold snap. |
| 5 | **Force bulletin** again. | The cold snap isn't presented as new. This one is a quiet week, or mentions the snap only as old news. |
| 6 | Wait until a bulletin has been written but not delivered (**Show news desk** shows its text). Note the text, save, quit to the menu, and load. | **Show news desk** shows the same text, and the letter on the due day matches it word for word. |
| 7 | Pick **Off** in the mod settings, then **Force bulletin**. | A bulletin still arrives, marked `(Template)`. No red errors. |
| 8 | Pick **Recommended** again, quit Ollama, and **Force bulletin**. | A `(Template)` bulletin after the connection times out (up to a minute), and a yellow `fell back to a template` line in the log. Start Ollama again. |
| 9 | Open the letters archive (the **History** tab, **Messages**). | Every bulletin is there with its text unchanged. |

Send me pass or fail per row, and paste 2–3 of the model's bulletins (from step 3 onwards), so I can check how the
prompt reads real game news.

## M4: the reporter and the Dispatch tab

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Start a **new colony**, and play until the first in-game day changes. | A letter **The Rim Dispatch: new correspondent** names a reporter from a non-hostile faction (civil outlanders if there are any), and the bulletin cadence. |
| 2 | Load an **existing save** (one with bulletins from Ilse Varrow). | The same letter arrives just after loading. Later bulletins are signed by the new reporter. |
| 3 | Look at the bottom bar. | A **Dispatch** button. Tell me where it sits and whether it looks out of place. |
| 4 | Open **Dispatch** before any bulletin has arrived, then again after a few (use **Force bulletin**). | The outlet's name, the correspondent and their faction, the next due day, and then every bulletin, newest first, each under its day. Long lists scroll. |
| 5 | **Show news desk**. | The reporter's name matches the letter. |
| 6 | Save, quit to the menu, and load. | No second "new correspondent" letter. **Dispatch** shows the same reporter and bulletins. |
| 7 | Open the **World** tab, then **Dispatch**. | It opens on the world map too, or tell me if it doesn't. |

Send me pass or fail per row, and a screenshot of the Dispatch tab with a few bulletins in it.

## M5: the reporter's visits and death

Use a colony that already has a reporter from a faction (the "new correspondent" letter). **Show news desk** now also
shows the reporter's status, where their pawn is, and the next visit day. Save before rows 4 and 5, so you can reload
between them.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | **Reporter: visit now**. | A letter **correspondent visiting** arrives, and the reporter walks in from the map edge as a visitor of their faction. They hang around for about a day, then leave. Afterwards, **Show news desk** says `in the world`, with a next visit 15–30 days out. |
| 2 | Play on a few days after they've left, then **Show news desk**. | Still `in the world`. The game's world-pawn cleanup didn't delete them. |
| 3 | During a visit, arrest the reporter. Then **Force bulletin**. | No bulletin arrives. **Show news desk** says `Held`. Release them: once they've left, the status goes back to `Active` and bulletins resume. |
| 4 | **Reporter: kill (raiders)**. | No letter right away. **Show news desk** says `Replacing`, with a replacement day 5–10 days out. To skip the wait, use **Reporter: replacement now**. On that day, a "new correspondent" letter names someone new, and their first bulletin mentions the old reporter's death. |
| 5 | Reload the save from before row 4, then **Reporter: kill (by colony)**. | No letter, no notice, and no bulletins from then on. The Dispatch tab still shows the old bulletins, but no next due day. **Show news desk** says `Silenced`. |
| 6 (optional) | During a visit, have a colonist shoot the reporter so they go down and bleed out, rather than dying on the spot. | Same as row 5: `Silenced`. |

Send me pass or fail per row, and the text of the replacement's first bulletin from row 4.

## M6: prompt check and the 60-day proof run

### 1. Harness trial of the bulletin prompt

Bulletin prompt v9 (then v10) stops quiet weeks from being padded with faction settlement counts. v10 also drops the
counts from the reporter's briefing. The harness now has 23
snapshots: the 20 hand-written ones plus 3 exported from your games (a quiet week, two conditions ending, and the
reporter's death). This trial runs only the bulletin task, once per snapshot, on the three Gemma models, with the judge.
That's 69 judged bulletins, about $1.

```powershell
cd C:\RimworldMods\RimChronicles\RimChronicles
git pull
dotnet run --project harness/RimChronicles.Eval -- run --repeats 1 --tasks news-bulletin --models gemma4-e2b,gemma4-e4b,gemma4-12b --judge
dotnet run --project harness/RimChronicles.Eval -- report latest --compare 20261121-204624
```

The second command adds the change against the v9 trial (run `20261121-204624`) to `summary.html`. Push the run to the
results branch as before, with the run ID from the `Report:` line:

```powershell
$run = "<new runId>"
cd ..\rc-results
git pull
Copy-Item -Recurse ..\RimChronicles\results\$run results\
git add -f results\$run
git commit -m "Stage 2 M6 trial: bulletin v10 (run $run)"
git push
cd ..\RimChronicles
```

### 2. The 60-day proof run (the Stage 2 exit criteria)

Use a new colony, Recommended tier, and a 3-day cadence. Dev mode can stay on.

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Play 60 in-game days at speed 3 (or use **Increment time** in 1-day steps, unpausing between them). | About 20 bulletins arrive, each on its due day. The game never stutters when one is written or delivered. |
| 2 | Around day 30, note the newest bulletin in the Dispatch tab, then save, quit to the menu, and load. | The Dispatch tab shows every bulletin unchanged. |
| 3 | Around day 40, pick **Off** in the mod settings, and play to the next bulletin. Then pick **Recommended** again. | The bulletin arrives as a template (`(Template)` in Dev mode), with no red errors. The next one is from the model again. |
| 4 | At the end, open the debug log. | No red errors from Rim Chronicles. |

Send me pass or fail per row, the debug log's Rim Chronicles lines if any are red, and 3–4 bulletins from the run.

### 3. Follow-ups from the proof run

The proof run's bulletins reused whole sentences on quiet weeks, and its reporter was 13. A repetition check now sends a
bulletin back once if it repeats 8 or more words in a row from either of the last two. Reporters are now 20 or older.

1. **Harness (free, no judge, Ollama running):** the v10 trial's first answers are cached, so this re-checks them
   with the new rule. Only answers the check sends back are asked again, from the local models. It shows how often the
   first answer is now sent back:

   ```powershell
   dotnet run --project harness/RimChronicles.Eval -- run --repeats 1 --tasks news-bulletin --models gemma4-e2b,gemma4-e4b,gemma4-12b
   ```

   Send me the summary table it prints.

2. **In game:** on a new colony, the "new correspondent" is 20 or older (check their **Bio** tab when they visit, or use
   **Reporter: visit now**). With the cadence at 1 day and **Increment time**, about 6 quiet bulletins in a row never
   repeat a whole sentence from the one before.
