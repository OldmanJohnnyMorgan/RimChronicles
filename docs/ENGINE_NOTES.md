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

## Stage 4 notes

- Vanilla builds 70 quests in C# (`QuestNode_Root_*`); a quest def then only needs `<root Class="…"/>`. A def started
  from code with `defaultChallengeRating` must set `isRootSpecial`, which also lists it in vanilla's Dev Mode quest menus.
- A quest's site is placed on the world map only when the quest is accepted. An expired offer never calls `Quest.End`,
  so plots poll quest states daily.
- `PrisonerWillingToJoinQuestUtility.GeneratePrisoner` may reuse a world pawn with relations (`forceGenerateNewPawn:
  false`). Swap it out with `ClearAndDestroyContentsOrPassToWorld`, never destroy it.
- A raid with a forced faction skips the storyteller's points check: raise points to
  `faction.def.MinPointsToGeneratePawnGroup(Combat)` or no raid strategy fits.

## Stage 5 notes

- `WorldObject.SetFaction` only sets the field. `Settlement.cachedMat` and `WorldObject.expandingMaterial` keep the
  old owner's colour, so a conquered settlement clears both and calls `WorldRenderer.SetDirty<WorldDrawLayer_WorldObjects>`.
- `Faction.SetRelationDirect` refuses two factions that both use goodwill (most NPC pairs), so the Empire's wars with
  neighbours are news, not relation changes.
- Royal favor from `GainFavor` doesn't award titles by itself; call `TryUpdateTitle` after it. A renounced title is
  `SetTitle(faction, null, …)` with `sendLetter: true`; the game's own cleanup passes false.
- A ship reactor's start is `CompHibernatable.Startup`; the reactor is `ThingDefOf.Ship_Reactor`.
- `Site.CheckAllEnemiesDefeated` sends the `AllEnemiesDefeated` quest signal; `Thing.Destroy` sends `Destroyed`;
  `MapParent` sends `MapGenerated` for any tagged settlement, not only sites.

## Crisis survey (crisis C0)

Checked on 2026-11-22 for `docs/SPEC-crisis-combat-and-locations/`, against the same 1.7 build and the game's XML
(`Data/{Core,Royalty,Ideology,Biotech,Odyssey}/Defs`, copied from the PC to `/workspaces/reference/data/`, outside the
repo and never committed; the PC has no Anomaly). Nothing here is checked in game yet; checkpoint 1 does that.

### Psycasts

Every Royalty psycast, from `Royalty/Defs/AbilityDefs/Abilities.xml`. Heat is `Ability_EntropyGain`, focus is
`Ability_PsyfocusCost`.

| Level | defName (heat, focus, range, radius) |
| --- | --- |
| 1 | `Painblock` (8, 2%, 24.9), `Stun` (12, 1%, 24.9), `Burden` (8, 1%, 29.9), `Chunkskip` (14, 4%, 24.9), `SolarPinhole` (–, 8%), `WordOfTrust` |
| 2 | `BlindingPulse` (20, 1%, 24.9, r3.9), `EntropyDump` (0, –, 24.9), `Waterskip` (25, 1.5%), `WordOfJoy` |
| 3 | `Beckon` (20, 1%, 19.9), `VertigoPulse` (30, 2%, 24.9, r3.9), `ChaosSkip` (18, 2%, 24.9), `WordOfLove` |
| 4 | `Skip` (25, 2%, 27.9), `Wallraise` (35, 2%, 24.9), `Smokepop` (30, 2%, 24.9), `Focus` (15, 3%), `WordOfSerenity` |
| 5 | `Berserk` (40, 4%, 19.9), `Invisibility` (45, 3%, 19.9), `Flashstorm` (65, 4%, r13.9), `Farskip` (–, 70%), `WordOfInspiration` |
| 6 | `BerserkPulse` (65, 6%, 14.9, r2.9), `ManhunterPulse` (50, 4%, r27.9), `MassChaosSkip` (40, 3%, r7.9), `BulletShield` (65, 4%, r4.9), `Neuroquake` (–, 50%) |

- There is **no Mass Burden**. `EntropyDump` dumps the caster's heat into a conscious target with
  `onlyTargetColonists`, which then sleeps for about a day. `Farskip` targets a world tile (`targetWorldCell`) and its
  comp is written around player caravans and home maps.
- **Vanilla AI casting:** `JobGiver_AIFightEnemy` (offensive) and `JobGiver_NPCCastNonCombatAbilities` (non-offensive)
  cast what `Pawn_AbilityTracker.AICastableAbilities(target, offensive)` returns, which needs `AbilityDef.aiCanUse`.
  `aiCanUse` defaults to false and **no Royalty psycast sets it**, so vanilla NPCs never cast psycasts. Some Biotech and
  Odyssey abilities do set it (gene abilities, weapon-trait abilities). The crisis still patches `AICastableAbilities`
  for its units, so a unit's persona weapon or another mod's XML patch can't cast behind the scorers.
- **Random psycasts:** `Hediff_Level.SetLevelTo` calls `ChangeLevel`, and `Hediff_Psylink.ChangeLevel` calls
  `TryGiveAbilityOfLevel`, which adds a random psycast of each new level. Royal NPCs get their psylink this way in
  `PawnGenerator` (title `maxPsylinkLevel`: Yeoman 1, Acolyte 2, Knight 3, Praetor 4, Baron 5, Count and every NPC
  title from Duke up 6). Kits are applied after this: remove every psycast, grant the kit.
- **Granting and casting:** `pawn.abilities.GainAbility(def)` / `RemoveAbility(def)`; cast with
  `Ability.QueueCastingJob(target, dest)` (or `GetJob` and start it). `Psycast.Activate` pays the psyfocus and adds the
  heat.
- **Cast checks:** `Psycast.CanCast` is **incomplete**: when a psycast makes heat it checks only the psylink level and
  heat overflow, not psyfocus; and it never checks the psyfocus band. `Psycast.GizmoDisabled(out reason)` checks
  everything: psychic sensitivity, psylink level, psyfocus (with queued casts), the band, heat (with queued casts),
  then the base checks (cooldown and so on). **`VanillaProvider` uses `GizmoDisabled`**, not `CanCast`.
- **Psyfocus bands:** `Pawn_PsychicEntropyTracker.PsyfocusBandPercentages` 0/25/50%, and `MaxAbilityLevelPerPsyfocusBand`
  2/4/6: level 3–4 casts need 25% psyfocus, level 5–6 need 50%. Psyfocus starts at **-1** (band 0, level 2 at most) until
  `SetInitialPsyfocusLevel()` runs (non-colonists: 50–70%). Only drop pods call it on arrival, so crisis units call it
  at spawn, or set it by stage with `OffsetPsyfocusDirectly`.

### Units

Royalty's Empire pawn kinds (`Royalty/Defs/PawnKinds/PawnKinds_Empire.xml`): fighters `Empire_Fighter_Trooper`,
`_Grenadier`, `_Janissary`, `_Champion`, `_Cataphract`, `_StellicGuardRanged`, `_StellicGuardMelee` (abstract bases
`ImperialFighterBase`, `CataphractBase`, `StellicGuardBase`); royals `Empire_Royal_Yeoman`, `_Acolyte`, `_Knight`,
`_Praetor`, `_Baron`, `_Count`, `_Duke`, `_Consul`, `_Stellarch` (bases `RoyalBase`, `RoyalLowerBase`, `RoyalUpperBase`).
Praetorians inherit `CataphractBase` or `StellicGuardBase`; adepts inherit the royal kind whose title gives their
psylink level; the High Stellarch inherits `Empire_Royal_Stellarch`.

### Lords and sieges

- `LordJob_AssaultColony(faction, canKidnap, canTimeoutOrFlee, sappers, useAvoidGridSmart, canSteal, breachers, …)`,
  `LordJob_Siege(faction, siegeSpot, blueprintPoints)`, `LordToil_AssaultColony`, `LordToil_AssaultColonyBreaching` and
  `LordToil_Siege` are public, unsealed classes. `LordJob_Siege` chains travel → siege → assault (at 40% losses) or exit.
- `LordToil_Siege` places its camp with `SiegeBlueprintPlacer.PlaceBlueprints(center, map, faction, points)` (camp
  radius 14–25, 25–40% builders). The crisis siege is its own toil, since its camp is built in stages.
- Siege builders get `DutyDefOf.Build`, whose think tree builds (`JobGiver_Work` with only Construction on), fights
  fires and mans turrets (`JobGiver_ManTurretsNearPoint`): builders are also the mortar crews. `DutyDefOf.Defend` doesn't
  man turrets. `GenConstruct.CanConstruct` checks the builder's Construction skill, not research, so any faction can
  build a blueprint; vanilla raises the skill with `EnsureMinLevelWithMargin`. `Frame.CompleteConstruction` adds the
  building to the builder's lord (`lord.AddBuilding`); `LordToil_Siege.Cleanup` sets their faction to null.
- A ThingDef only gets a blueprint and frame when `designationCategory` is set (`ThingDefGenerator_Buildings`,
  `BuildableByPlayer`). `canGenerateDefaultDesignator: false` keeps it out of the build menu (C8's camp buildings).
  The mech shields (`ShieldGeneratorMortar`, `ShieldGeneratorBullets`) have no category, so no blueprint.
- `Turret_Mortar`: `minRange` 29.9, `range` 500. Vanilla's siege resupplies shells (under 4: drop 6), survival meals
  (under 5: drop 12) and, without classic mortars, reinforced barrels, every 500 ticks within 20 cells.

### Pieces and layouts

- `PrefabDef` (`size`, `rotations`, things, terrain, sub-prefabs) and
  `PrefabUtility.SpawnPrefab(prefab, map, pos, rot, faction, spawned, overrideSpawnData, onSpawned, blueprint)`;
  `CanSpawnPrefab` checks first. `blueprint: true` places blueprints.
- Dev Mode → Generation → **Create prefab** (`DebugActionsPrefabs.CreatePrefab`) takes a rectangle on the map and copies
  its `PrefabDef` XML to the clipboard. Pieces are built in game and pasted into `Defs/`; no character-grid format is
  needed. The same menu spawns a prefab, or its blueprint, at the mouse.
- `LayoutDef`, `StructureLayoutDef`, `LayoutRoomDef` and `RoomLayoutGenerator.GenerateRandomLayout(container, …)`
  exist (ancient ruins, mechhive). Reference only (tech spec R3).

### Maps and sites

- Attacking a settlement (`SettlementUtility.Attack`, `TransportersArrivalAction_AttackSettlement`) calls
  `GetOrGenerateMapUtility.GetOrGenerateMap(tile, null)`, which uses the world's `initialMapSize`. Every map goes
  through `MapGenerator.GenerateMap(mapSize, parent, mapGenerator, …)`: **a prefix there sets the Citadel's size and
  generator** when `parent` is the Empire capital. `Settlement.MapGeneratorDef` (a virtual getter, `Base_Faction` for
  NPC settlements) is the other hook.
- `SitePartDef.minMapSize` sets a minimum map size for a site part.
- A site part's map content comes from `GenStepDef`s with `<linkWithSite>` naming the part (`BanditCamp.xml`: the
  outpost generator and power). C9's site parts inherit vanilla's `Outpost` part (threat points, faction, arrival
  letter) and link our own gen step. Vanilla's outpost adds its rect to the map-gen var `UsedRects` so later steps
  keep out; ours does too.
- Prisoner modes: `PrisonerInteractionModeDef.isNonExclusiveInteraction` puts a mode in the prisoner tab's checkbox
  section (`ITab_Pawn_Visitor`), toggled with `Pawn_GuestTracker.ToggleNonExclusiveInteraction`; the tab's filter only
  hides modes by the def's flags, so a new mode shows for every prisoner. Warden visits share one schedule
  (`ScheduledForInteraction`: at most two a day, 10,000 ticks apart; `Toils_Interpersonal.SetLastInteractTime` uses it).
  Anomaly's identity interrogation (`WorkGiver_Warden_InterrogateIdentity`, `JobDriver_InterrogatePrisoner`) is the
  model; its chance is `SocialImpact × 0.15`.
- Resistance and will come from the pawn kind's `initialResistanceRange` / `initialWillRange` at capture
  (`Pawn_GuestTracker.CapturedBy`), plus the royal title's `recruitmentResistanceOffset`.
- Power: a building wires itself to the nearest power transmitter within 6 cells of its position, walls or not
  (`PowerConnectionMaker.BestTransmitterForConnector`). Plain walls don't transmit power. A conduit isn't an edifice:
  spawned after a building, it sits under it without wiping it (`GenSpawn.SpawningWipes` only wipes an old conduit when
  the new thing transmits power).

### Strikes and drop pods

- `GenSpawn.Spawn(ThingDefOf.Bombardment, cell, map)` then set `impactAreaRadius`, `explosionRadiusRange`,
  `bombIntervalTicks`, `explosionCount`, `warmupTicks` (default 60), `randomFireRadius` and `instigator`, as
  `RoyalTitlePermitWorker_OrbitalStrike.CallBombardment` does. `PowerBeam` the same way. The permit refuses cells
  under thick roof (`RoofDef.isThickRoof`).
- `warmupTicks` is a built-in delay before the first impact, but cancelling needs the strike not to exist yet, so the
  telegraph is a marker of ours and the strike spawns when it ends. Sounds to reuse: `SoundDefOf.OrbitalStrike_Ordered`
  (the permit's) and the `Bombardment_PreImpact` SoundDef. No vanilla targeting-laser visual was found; C5 defines a
  fleck in XML.
- `DropCellFinder.CanPhysicallyDropInto(c, map, canRoofPunch, allowedIndoors)`,
  `TryFindDropSpotNear(center, map, out cell, allowFogged, canRoofPunch, maxRadius, allowIndoors, …)`, and
  `DropPodUtility.DropThingsNear(center, map, things, …, canRoofPunch, …)`. Fairness rule 3 passes
  `canRoofPunch: false` and rejects thick-roofed cells.

### Damage

`Thing.TakeDamage(DamageInfo)` is non-virtual and calls the virtual `PreApplyDamage`. `Building`, `Building_Turret` and
others override `PreApplyDamage`, so **the keep shield patches `Thing.TakeDamage`** (scale `dinfo` for keep walls and
doors) to catch every building type.

### Prisoners

- `PrisonerInteractionModeDef` has display flags only (`hideIfNotRecruitable`, `allowOnWildMan`, …), no worker class.
  Work comes from a `WorkGiver_Warden_*` that checks the mode, as Anomaly's `WorkGiver_Warden_InterrogateIdentity`,
  `JobDriver_InterrogatePrisoner` and `InteractionWorker_InterrogateIdentity` do.
- The prisoner tab's filter is a local function inside `ITab_Pawn_Visitor` (compiled as a generated method,
  `CanUsePrisonerInteractionMode`). Hiding our mode for non-Empire prisoners means patching that generated method
  found by name, which can break on any update. **Decision:** show "Interrogate" for every prisoner, and have the work
  giver skip and explain (inspect string) for prisoners with nothing to tell; patch the filter only if that reads badly
  in game.
- `Pawn_GuestTracker.resistance` and `will` are public fields (-1 until set).

### Signals and naming

- `Thing.Destroy` sends the `Destroyed` quest signal for tagged things (`questTags`); `Spawned` and `Despawned` too.
- Crisis defNames use the existing `RimChronicles_` prefix (as `RimChronicles_Dispatch`), not `RC_`.
- Odyssey already has `AncientOrbitalUplink` and its layouts. Our site defs stay prefixed, so the names never clash.

## Differences that matter later

- `Mod.DoSettingsWindowContents(Rect)` still takes `UnityEngine.Rect`, which is now a type in `UnityStubs`.
  **Decided 2026-11-21:** UI code (the settings page, and windows such as the Rim Dispatch tab) may use the Unity stub
  types RimWorld's UI needs (`Rect`, `Listing_Standard`). The Game project references `UnityStubs` with
  `Private=false` for this, and nothing outside UI code uses Unity types.
- **Decided 2026-11-22 (crisis Q2):** Dev Mode drawing (debug text over pawns, overlays) may use the stub types it
  needs too, such as `Pawn.DrawPos` (a `Vector3`) for `MoteMaker.ThrowText`. Only behind a Dev Mode switch.
- A fight logs one `Wounded` tale per hit, and animals and unnamed raiders have no `name` in their tale data (use
  `kind.label`). Archived letters can carry a whole paragraph as their label (the scenario intro).
- `WorldGrid.LongLatOf` returns a Unity `Vector2`. The snapshot reader uses `GenLocalDate` instead, to keep Unity types
  out of new code.

## Adapter layer

The Stage 1 interfaces are in `src/RimChronicles.Core/Engine/`: `IWorldReader`, `IGameClock`, `ILetterSink` and
`IMainThread` (backed by `MainThreadQueue`). The save bridge is `IArchive` / `IArchivable` in
`src/RimChronicles.Core/Persistence/`. Their RimWorld implementations are in `src/RimChronicles.Game/Engine/`.
`IPacingGate`, `IQuestSpawner` and `IEffectExecutor` come with the stages that need them (2–4).
