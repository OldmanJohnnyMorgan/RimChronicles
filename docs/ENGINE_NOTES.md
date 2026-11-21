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
| A patch on an overloaded method needs its argument types: `[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(AddHediff), [typeof(Hediff), …])]`. 1.7 has `AddHediff(Hediff, …)` and `AddHediff(HediffDef, …)`, and without the types the patch throws an ambiguous-match error at load. Check the decompiled source for overloads before patching. The mod patches each class on its own (`RimChroniclesMod.PatchEach`), so one failure doesn't stop the rest. | The M2 load error | Yes |
| The mod references `0Harmony.dll` with `Private=false`, so the DLL is never copied into the mod (rule 2). | `RimChronicles.Game.csproj` | — |

## Game APIs used by the spike (all unchanged from 1.6)

| API | Use |
| --- | --- |
| `Verse.Mod(ModContentPack)`, `GetSettings<T>()`, `ModSettings.ExposeData` | Entry point and settings |
| `GameComponent(Game)` via `Activator.CreateInstance(type, game)`, `GameComponentUpdate`, `ExposeData` | Per-game state. Update runs every frame, even when paused. |
| `TickManager.DoSingleTick`, `TicksGame`, `GenDate.TicksPerDay` (60,000), `GenDate.DaysPassed` | Daily patch. It compares day numbers instead of waiting for `TicksGame % 60000 == 0`, because `TickManager.DebugSetTicksGame` (dev time tools) sets the counter directly and can skip that tick. That's how the first M3 bulletin got stuck undelivered. |
| `Scribe.mode`, `Scribe.EnterNode` / `ExitNode`, `Scribe_Values.Look`, `Scribe_Collections.Look(..., LookMode.Value)` | `ScribeArchive`. `EnterNode` returns false on load when the node is missing, which is how old saves load. |
| `Find.LetterStack.ReceiveLetter(string, string, LetterDef)`, `LetterDefOf.NeutralEvent` / `PositiveEvent` / `NegativeEvent` | Letters |
| `[DebugAction(category, name, allowedGameStates = ...)]` on a static method in any loaded assembly | Dev Mode actions (found through `GenTypes.AllTypes`) |
| `PawnsFinder.AllMaps_FreeColonists`, `Pawn.Name.ToStringShort`, `ageTracker.AgeBiologicalYears`, `story.traits.allTraits`, `story.Adulthood.TitleCapFor`, `relations.DirectRelations`, `needs.mood.CurLevel` | Snapshot pawns and colony mood |
| `other.GetRelations(pawn)` (`PawnRelationUtility`), `PawnRelationDef.GetGenderSpecificLabel(pawn)` | Snapshot relations. The result is what `pawn` is to `other`, as on the Social tab. `DirectRelations` runs the other way: an entry on a child points at its parent. |
| `Find.TaleManager.AllTalesListForReading`, `Tale_SinglePawn.pawnData`, `Tale_DoublePawn.firstPawnData` / `secondPawnData`, `TickManager.gameStartAbsTick` | Snapshot tales |
| `Find.FactionManager.AllFactionsVisible`, `Faction.PlayerGoodwill`, `Find.WorldObjects.Settlements` | Snapshot factions |
| `Find.Archive.ArchivablesListForReading`, `GenLocalDate.Twelfth(map).GetQuadrum()`, `GenLocalDate.Year(map)`, `WealthUtility.PlayerWealth` | Snapshot events, date and wealth |

## World news hooks (Stage 2 M2)

All are postfixes in `src/RimChronicles.Game/Patches/NewsPatches.cs`. They read game state only, and record only while
`Current.ProgramState == Playing`, so loading a save records nothing.

| Hook | News |
| --- | --- |
| `GameConditionManager.RegisterCondition(GameCondition cond)` | A condition starting. `ownerMap` is null for world-wide conditions. |
| `GameCondition.End()` | A condition ending. Only ones that ran 3+ days (`TicksPassed`) are logged. |
| `Faction.Notify_RelationKindChanged(Faction other, FactionRelationKind previousKind, …)` | War and peace. The game calls it on **both** factions, so we skip the colony's side, and keep one side for two other factions. |
| `Faction.Notify_LeaderDied()` | A faction leader dying. A prefix reads `leader` before `TryGenerateNewLeader` replaces it. |
| `SettlementDefeatUtility.CheckDefeated(Settlement factionBase)` | A settlement destroyed, and its faction wiped out if it was the last. Vanilla only destroys settlements through the colony. |

Vanilla RimWorld makes little world news: no wars between other factions, no expansion. Most bulletins will be quiet
weeks until the plots of Stages 3–5 add news.

Core lists are saved as `<label><count/><i0>…</i0>…`, using only `Scribe.EnterNode` and `Scribe_Values`
(`ScribeArchive.DeepList`). This keeps Core types from having to be `IExposable`, and keeps Scribe from looking a
type up by name on load, which might not resolve nested generic types.

## Stage 3 notes

- `TradeUtility.ColonyHasEnoughSilver` and `LaunchSilver` only see silver near a powered orbital trade beacon. Payments
  use silver anywhere on the home maps (`Plots/Silver`).
- A kidnapped colonist keeps the player's faction. It's on the kidnapping faction's `kidnapped.KidnappedPawnsListForReading`,
  and is a world pawn, as is a colonist on a caravan.
- A severed part is added as a `Hediff_MissingPart` through `Pawn_HealthTracker.AddHediff(Hediff, …)`, with the damage.
- `ThoughtDef.thoughtClass` has no default: XML memories set `Thought_Memory`.

## Differences that matter later

- `Mod.DoSettingsWindowContents(Rect)` still takes `UnityEngine.Rect`, which is now a type in `UnityStubs`.
  **Decided 2026-11-21:** UI code (the settings page, and windows such as the Rim Dispatch tab) may use the Unity stub
  types RimWorld's UI needs (`Rect`, `Listing_Standard`). The Game project references `UnityStubs` with
  `Private=false` for this alone, and nothing outside UI code uses Unity types.
- A fight logs one `Wounded` tale per hit, and animals and unnamed raiders have no `name` in their tale data (use
  `kind.label`). Archived letters can carry a whole paragraph as their label (the scenario intro).
- `WorldGrid.LongLatOf` returns a Unity `Vector2`. The snapshot reader uses `GenLocalDate` instead, to keep Unity types
  out of new code.

## Adapter layer

The Stage 1 interfaces are in `src/RimChronicles.Core/Engine/`: `IWorldReader`, `IGameClock`, `ILetterSink` and
`IMainThread` (backed by `MainThreadQueue`). The save bridge is `IArchive` / `IArchivable` in
`src/RimChronicles.Core/Persistence/`. Their RimWorld implementations are in `src/RimChronicles.Game/Engine/`.
`IPacingGate`, `IQuestSpawner` and `IEffectExecutor` come with the stages that need them (2–4).
