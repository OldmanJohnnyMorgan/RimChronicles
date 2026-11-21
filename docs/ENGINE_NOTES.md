# Engine notes: RimWorld 1.7 (Waylith)

What the Stage 1 spike confirmed about 1.7, and how. **Source** means it was read in the decompiled game. **In game** means
it was seen working in a running game on the reference PC (see `docs/stage1-runbook.md`). All five spike tests passed in
game on 2026-11-21.

Checked against RimWorld **1.7.9819.35854** (`Assembly-CSharp` version) and Harmony **3.0.0-rc.7**, on 2026-11-21.
Recheck after any beta update.

## Where the decompiled source lives

`ilspycmd` 11.2 decompiled the game into `/workspaces/reference/src/` in the codespace, outside the repo. The input
DLLs are in `/workspaces/reference/dlls/`. The game's code is copyrighted and the repo is public, so neither folder is
ever committed. To refresh after a game update, upload the new DLLs and run `ilspycmd -p -o src/<name> -r dlls
dlls/<name>.dll` for `Assembly-CSharp`, `RimWorld`, `Waylith`, `UnityStubs`, `Waylith.UnityCompat` and `0Harmony`.

## Runtime and layout

| Fact | How we know | In game |
| --- | --- | --- |
| The game DLLs sit in the RimWorld folder itself (`D:\SteamLibrary\steamapps\common\RimWorld`), not under `RimWorldWin64_Data\Managed` as in 1.6. | The PC install | — |
| The runtime is .NET 10 on WaylithCLR (`10.0.15-waylithclr.dev`). `Assembly-CSharp` targets `net10.0`. | `RimWorld.runtimeconfig.json`, the csproj | Yes (test 1) |
| `System.Net.Http`, `System.Text.Json`, `System.Collections.Concurrent` and `Task` ship with the game. | The DLL folder | Yes (test 3) |
| `UnityEngine*` and `Unity.*` assembly names redirect to `UnityStubs`. | `WaylithClr.AssemblyNameRedirects` | — |
| Only `Assembly-CSharp` is marked JIT-patchable (`WaylithClr.JitPatchableAssemblies`). Harmony patches should target methods in `Assembly-CSharp`. Patching `RimWorld.dll` or `Waylith*` code may not work. | `runtimeconfig.json` | — |
| Mods still load DLLs from `<mod>/1.7/Assemblies/`, and `About/About.xml` still uses `packageId`, `supportedVersions`, `modDependencies` and `loadAfter`. | `ModContentPack`, `ModMetaData` | Yes (test 1) |
| The log is `Player.log` in the save data folder, written by `Waylith.Logging.FileLog`. | `FileLog` | — |

## Harmony

| Fact | How we know | In game |
| --- | --- | --- |
| Harmony 3 is Workshop mod 2009463077, in its `1.7/Assemblies` folder (`packageId` `brrainz.harmony`). | The PC install, `AssemblyInfo` | — |
| `new Harmony(id).PatchAll(assembly)`, `[HarmonyPatch(typeof(T), nameof(T.M))]` and `__instance` postfixes work as in Harmony 2. | `HarmonyLib` source | Yes (test 2) |
| The mod references `0Harmony.dll` with `Private=false`, so the DLL is never copied into the mod (rule 2). | `RimChronicles.Game.csproj` | — |

## Game APIs used by the spike (all unchanged from 1.6)

| API | Use |
| --- | --- |
| `Verse.Mod(ModContentPack)`, `GetSettings<T>()`, `ModSettings.ExposeData` | Entry point and settings |
| `GameComponent(Game)` via `Activator.CreateInstance(type, game)`, `GameComponentUpdate`, `ExposeData` | Per-game state. Update runs every frame, even when paused. |
| `TickManager.DoSingleTick`, `TicksGame`, `GenDate.TicksPerDay` (60,000), `GenDate.DaysPassed` | Daily patch |
| `Scribe.mode`, `Scribe.EnterNode` / `ExitNode`, `Scribe_Values.Look`, `Scribe_Collections.Look(..., LookMode.Value)` | `ScribeArchive`. `EnterNode` returns false on load when the node is missing, which is how old saves load. |
| `Find.LetterStack.ReceiveLetter(string, string, LetterDef)`, `LetterDefOf.NeutralEvent` / `PositiveEvent` / `NegativeEvent` | Letters |
| `[DebugAction(category, name, allowedGameStates = ...)]` on a static method in any loaded assembly | Dev Mode actions (found through `GenTypes.AllTypes`) |
| `PawnsFinder.AllMaps_FreeColonists`, `Pawn.Name.ToStringShort`, `ageTracker.AgeBiologicalYears`, `story.traits.allTraits`, `story.Adulthood.TitleCapFor`, `relations.DirectRelations`, `needs.mood.CurLevel` | Snapshot pawns and colony mood |
| `other.GetRelations(pawn)` (`PawnRelationUtility`), `PawnRelationDef.GetGenderSpecificLabel(pawn)` | Snapshot relations. The result is what `pawn` is to `other`, as on the Social tab. `DirectRelations` runs the other way: an entry on a child points at its parent. |
| `Find.TaleManager.AllTalesListForReading`, `Tale_SinglePawn.pawnData`, `Tale_DoublePawn.firstPawnData` / `secondPawnData`, `TickManager.gameStartAbsTick` | Snapshot tales |
| `Find.FactionManager.AllFactionsVisible`, `Faction.PlayerGoodwill`, `Find.WorldObjects.Settlements` | Snapshot factions |
| `Find.Archive.ArchivablesListForReading`, `GenLocalDate.Twelfth(map).GetQuadrum()`, `GenLocalDate.Year(map)`, `WealthUtility.PlayerWealth` | Snapshot events, date and wealth |

## Differences that matter later

- `Mod.DoSettingsWindowContents(Rect)` still takes `UnityEngine.Rect`, which is now a type in `UnityStubs`. A settings
  window means referencing `UnityStubs` for `Rect` and `Listing_Standard`, which bends rule 3. Decide this in Stage 2.
  Until then, settings live in the mod's config file.
- A fight logs one `Wounded` tale per hit, and animals and unnamed raiders have no `name` in their tale data (use
  `kind.label`). Archived letters can carry a whole paragraph as their label (the scenario intro).
- `WorldGrid.LongLatOf` returns a Unity `Vector2`. The snapshot reader uses `GenLocalDate` instead, to keep Unity types
  out of new code.

## Adapter layer

The Stage 1 interfaces are in `src/RimChronicles.Core/Engine/`: `IWorldReader`, `IGameClock`, `ILetterSink` and
`IMainThread` (backed by `MainThreadQueue`). The save bridge is `IArchive` / `IArchivable` in
`src/RimChronicles.Core/Persistence/`. Their RimWorld implementations are in `src/RimChronicles.Game/Engine/`.
`IPacingGate`, `IQuestSpawner` and `IEffectExecutor` come with the stages that need them (2–4).
