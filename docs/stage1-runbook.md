# Stage 1 runbook: the spike in game

The five spike tests from the design spec, run on the reference PC. Each one says what to do and what you should see.
Note anything different, and copy any red errors from the debug log.

## Set up (once)

1. Get the code and build it. `Directory.Build.props.user` must point at the 1.7 install (see
   `Directory.Build.props.user.example`).

   ```powershell
   cd C:\RimworldMods\RimChronicles\RimChronicles
   git pull
   dotnet build RimChronicles.slnx
   Get-ChildItem 1.7\Assemblies
   ```

   The build should not print `building without game sources`. `1.7\Assemblies` should list
   `RimChronicles.Core.dll`, `RimChronicles.Game.dll` and `RimChronicles.Llm.dll`, and no `0Harmony.dll`.

2. Link the repo into RimWorld's `Mods` folder, so every build is picked up without copying:

   ```powershell
   New-Item -ItemType Junction -Path "D:\SteamLibrary\steamapps\common\RimWorld\Mods\RimChronicles" -Target "C:\RimworldMods\RimChronicles\RimChronicles"
   ```

3. Start Ollama, with `gemma4:e4b` pulled (the default model).

4. Start RimWorld. In **Mods**, turn on Harmony and Rim Chronicles, with Harmony above Rim Chronicles, then restart when
   asked. In **Options**, turn on **Development mode**.

## The tests

| # | Do | Pass when |
| --- | --- | --- |
| 1 | Open the debug log (the first icon in the dev toolbar at the top of the screen). | It shows `[Rim Chronicles] Loaded on 10.0...`, with no red errors from Rim Chronicles. |
| 2 | Start a new colony (or load one), then run the game at top speed through at least 2 in-game days. | The log shows `[Rim Chronicles] Day N: daily patch ran` once per day, never twice for the same day. |
| 3 | Open **Debug actions** (the second icon in the dev toolbar), find **Rim Chronicles**, run **Test LLM letter**. Keep the game running while you wait. | Within a minute, a letter called **Rim Chronicles: test broadcast** arrives with two sentences from the model, and the game doesn't stutter. The letter's footer shows a different thread number for the call than for the main thread. |
| 3b | Stop Ollama (quit it from the tray icon) and run **Test LLM letter** again. | A **radio silence** letter arrives with the connection error. This is the template fallback. |
| 4 | Run **Show spike state** and note the numbers. Save the game, quit to the main menu, load the save, and run **Show spike state** again. | Days seen, the last reply and the log are the same as before saving. Also load a save made *before* the mod was added: it loads with no errors, and the state starts at 0. |
| 5 | Run **Dump world snapshot**. | A message gives the file's path (under the RimWorld save-data folder, in `RimChronicles\snapshots`). Copy that file into the repo's `harness\snapshots\` folder, and `dotnet run --project harness/RimChronicles.Eval -- validate` reads it. |

For test 5, `validate` may reject the snapshot for fields that only hand-written snapshots fill, such as allowed
transitions or the reporter. Send me its output either way: it shows what the exporter must add for Stage 2.
Remove the file from `harness\snapshots\` afterwards, so it isn't committed by accident.

## When you're done

Send me:

- pass or fail for each test, and anything odd;
- any Rim Chronicles lines from the debug log, especially red ones;
- the snapshot JSON file from test 5 (paste it, or upload it to the codespace's `reference` folder).

The full log is `Player.log`, in the same save-data folder as the snapshot.
