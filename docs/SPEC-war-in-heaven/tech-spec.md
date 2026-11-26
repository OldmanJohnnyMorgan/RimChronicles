# The War in Heaven: technical spec

**For:** `SPEC-war-in-heaven.md` (draft 3, in this folder). **Date:** 2026-11-24. **Status:** T1–T5 confirmed 2026-11-24; §1 corrected by the W0 survey (`ENGINE_NOTES.md`, "War in Heaven survey").
**Branch:** `war-in-heaven`, a long-lived integration branch off `main` (as the crisis had). Milestone PRs target it;
it merges into `main` after W14 passes.

This turns the War in Heaven spec into a design that fits the repo as it is after the crisis track (C0–C13 on `main`,
C14 in progress). §1 says where it departs from the spec and why. The parent design spec wins over both.

## 1. Review: what changes and why

Checked against `main` at `3c65abe` and the decompiled 1.7 source (`/workspaces/reference/src/`) and Defs
(`/workspaces/reference/data/`). Rows marked W0 were corrected by the survey.

| # | Spec says | Repo or 1.7 reality | Decision |
| --- | --- | --- | --- |
| R1 | The mechanites mirror the Empire plot and the full crisis | The Empire plot (`EmpireSaga`, `EmpireArc`, `EmpireExpansion`) and the crisis (`CrisisState`, `CrisisStages`, `CrisisArc`, operations, sites, citadel, finale) are written for one empire. "Empire" appears ~500 times across about 15 files; Game finds the faction through `Faction.OfEmpire` | **Generalise, don't copy.** Core gets a `Power` record (id, plot template ids, option prefix, triggers, unit roster ids, texts); saga, arc, expansion and crisis take a `Power`. `Power.Empire` reproduces today's behaviour exactly; `Power.Choir` is the Hollow Choir. Game gets `PowerFactions.Of(power)` in place of `Faction.OfEmpire`. W1 does this with no behaviour change (§3) |
| R2 | The Choir is hidden and dormant, with no settlements, until it wakes | A hidden faction has no settlements or world-map presence. `Faction.hidden` is a saved per-faction override, so revealing one is possible (W0), but a faction made at world generation doesn't exist in old saves. Factions can be created mid-game: `FactionGenerator.NewGeneratedFaction(layer, parms)` and `Find.FactionManager.Add` | **The Choir faction doesn't exist until it wakes.** Waking creates it, places its settlements (a ring of 4–6 at a distance from the colony, a setting) and sends the reveal letter. That also makes adding the mod to an old save work (rule 5). While dormant, the saga is plain Core data, and Stirring rumours and the hum need no faction |
| R3 | A mechanitor faction with a mech army | Biotech has mechanitor pawn kinds (`Mechanitor`, `Mechanitor_Basic`, with `Mechlink`). Mechs can belong to any faction (player mechs do); the mechanoid hive's group makers list the mech kinds. Mechs in a non-player faction have no overseer and never go feral (`CompOverseerSubject.CanGoFeral` needs a colony mech) | Non-player mechs have no energy need (`playerMechsOnly`, W0). No vanilla humanlike faction fields mechs, so mixed groups are checked in game at checkpoint 1 | **Choir units are XML pawn kinds:** mechanitor ranks (Cantor, Choirmaster, the First Voice) parented on Biotech's `Mechanitor`, and mechs from vanilla Core and Biotech kinds, in `PawnGroupMaker`s by stage. A `UnitExtension` role per kind, as for the Empire (§4.2) |
| R4 | A converted mechanitor's mechs shut down where they stand | Removing a mechlink drops every overseer relation (`Notify_MechlinkRemoved`); uncontrolled colony mechs then go feral, but only while awake. **`CompMechanoid.Deactivate()`** is vanilla: a saved flag that makes `CanBeAwake` false, so the mech never goes feral (W0). Consciousness 0 would kill a mech (`lethalMechanoids`). `JobDriver_DisassembleMech` has no overseer check; only its float menu does (W0) | **Each mech is deactivated (`CompMechanoid.Deactivate()`)** before the mechlink comes off. **Dismantle** is a float menu option of ours that queues vanilla `JobDefOf.DisassembleMech` for any colonist on a deactivated mech; no new job. The same call serves Choir counterplay: kill a Choir mechanitor in battle and its mechs are deactivated (§4.3) |
| R5 | Psylink level from bandwidth, and the reverse | `pawn.GetPsylinkLevel()`, `ChangePsylinkLevel(offset)` adds a `PsychicAmplifier` psylink if there is none; `TotalBandwidth` is the `MechBandwidth` stat; band nodes add it through a `Hediff_BandNode` (+1 per tuned node). The mechlink hediff is `MechlinkImplant` and needs a psychically sensitive pawn. Removing a psylink leaves its psycasts (W0) | **Psylink level = 1 + ⌊(bandwidth − 6) / 4⌋, clamped 1–6, measured before the pawn leaves**, from `TotalBandwidth` minus the `Hediff_BandNode` offset. Reverse: add `MechlinkImplant`, plus a `RimChronicles_ChoirBandwidth` hediff with a `MechBandwidth` offset of 4 × (level − 1). The psylink hediff and every `Psycast` in `pawn.abilities` are removed. The conversion is a Core function on plain numbers, unit-tested at every level |
| R6 | A shuttle takes the pawn for 5 days and returns it | Royalty's `PawnLend` quest does this: `QuestGen_Shuttle.GenerateShuttle(requiredPawns)` collects colonists, and `QuestPart_LendColonistsToFaction` keeps them in a saved list and returns them after `returnLentColonistsInTicks` (by Imperial shuttle for `Faction.OfEmpire`, drop pods otherwise). `Complete` is overridable (W0) | **A quest script per side** (`RimChronicles_ImperialSummons`, `RimChronicles_ChoirSummons`) modelled on `PawnLend`, with `requiredPawns` set to the summoned pawn and 5 days. **`QuestPart_RcSummons`** subclasses `QuestPart_LendColonistsToFaction` and converts the pawns in `Complete` before `base.Complete` sends them home. Refusing is declining the quest, or the shuttle leaving without the pawn (`ShuttleLeaveDelay`) |
| R7 | The Royal Ascent wakes the Choir | The Royal Ascent is `QuestScriptDef EndGame_RoyalAscent` (Royalty), offered by the Empire to a titled colonist, with no `QuestScriptDefOf` field; `Quest.Accept(Pawn)` acts only when the quest is `NotYetAccepted` (W0). The ship launch is already hooked (`ShipReactorStartupPatch`) | **A prefix and postfix on `Quest.Accept`** that fire when a quest whose `root.defName` is `EndGame_RoyalAscent` changes state, and call `ChoirDriver.Notify_RoyalAscent()`, which mirrors `EmpireDriver.Notify_ShipStartup()`. Because only Empire-friendly colonies get the Royal Ascent, this is usually how an Imperial colony wakes the Choir |
| R8 | Sides: Empire, mechanites, Independence | Stance is a fact on the Empire plot (`stance`: ally, oppose, neutral), set by Act 1's envoy and by letter answers | **`Allegiance` (Core, saved on the game component):** side (undeclared, Empire, Choir, independent), the day and the cause. Each power's plot reads its stance from the allegiance (`StanceToward(power)`), not from its own fact. A plot that turns the colony toward or against a power changes the allegiance, which updates both plots in one place. Saves from before the War in Heaven derive the allegiance from the old `stance` fact on load |
| R9 | The War in Heaven: odds rise with the length of the player's war; certain on game day 600 | The crisis stages already count how long the colony has been opposed (`stanceCause`, job counts); every daily driver runs from the game component | **`WarInHeaven` (Core, saved):** state (not yet, started, ended), start day, and a daily roll. `Odds(warDays)` is a curve setting (default: 0 for the first 15 days of war, rising to 5% a day at 165 days; spec D14); on game day 600 it starts for certain. Tested on plain numbers |
| R10 | The War in Heaven is fought on the world map | `EmpireExpansion` picks targets among settlements in range, by distance and strength, and conquers, razes or is repelled | **Expansion takes an enemy set.** Once the War in Heaven starts, each power's targets include the other power's settlements, weighted up. A conquest changes the settlement's faction, as today. The news reports every result |
| R11 | Resistance victory is folded in | `EmpireStep.StormCapital` ends in `EmpireEnding.Resistance` | **`StormCapital` stays as a job** (the Citadel finale), but its outcome becomes "the Empire is destroyed", not an ending. Each power gets a `Destroyed` state after its citadel falls. The endings are decided by the War in Heaven arc (§6) |
| R12 | The fine-tuned directors are retrained once | `TierPresets.DirectorPresets` carries `KnowsCrisis`; an unlisted `rimchron-director-*` counts as not knowing the crisis | **Replace the flag with a vocabulary level:** `Vocabulary.Base` (today's arcs), `Crisis`, `WarInHeaven`. A preset declares what it was trained on; the game shows a model only the plots at or below its level. Prompted models get every level. Only the Empire arc and the crisis exist at level `Crisis`; the Choir, its crisis and the War in Heaven are level `WarInHeaven` |
| R13 | Requires Biotech for the mechanite side | Crisis defs use `MayRequire="Ludeon.RimWorld.Royalty"`; Royalty is optional (crisis R13) | **Choir defs load with `MayRequire` Biotech** (and Royalty). Without Biotech, `Power.Choir` is off, the War in Heaven is off, and the Empire plot runs exactly as today, including Resistance victory (R11 applies only when the Choir can exist) |

## 2. Architecture

```
Core (no RimWorld, unit-tested)                   Game (RimWorld, Harmony)
-------------------------------------------       ----------------------------------------------------
Powers/                                           Powers/
  Power              id, templates, prefix,         PowerFactions     Of(power): Faction.OfEmpire, or the
                     triggers, rosters, texts                         Choir faction once it exists
  Allegiance         side, cause, day; stance       PowerDriver       daily: wake, expand, crisis, per power
                     toward each power              ChoirFaction      create on waking, place settlements
  PowerSaga          was EmpireSaga                 Summons/          quest scripts, QuestPart_RcSummons,
  PowerArc           was EmpireArc (jobs, acts)                       conversion, deactivate, dismantle menu
  PowerExpansion     was EmpireExpansion          Crisis/            (as today, per power: units, commander
  Conversion         bandwidth <-> psylink level                     AI, operations, sites, citadel, finale)
War/                                              War/
  WarInHeaven        odds, start, state             WarDriver         daily roll, front, letters
  WarArc             jobs and endings               Endings           victory screens, epilogues
Crisis/  (as today, keyed by Power)
```

`Empire*` names stay as thin aliases where renaming would touch saves or the director's vocabulary: scribe labels
(`empire`, `crisis`), plot template ids (`AwakenedEmpire`, `ImperialCrisis`) and option ids (`Empire:*`,
`Operation:*`) don't change, so old saves load and seed-9001 cases stay byte-identical.

**The Choir's ids:** templates `HollowChoir` and `ChoirCrisis`, options `Choir:*` for its jobs (the mirror of
`Empire:*`), and `Operation:*`, `Goal:*` and `Site:*` ids of its own where the Choir does something different (§4).
The War in Heaven arc is `WarInHeaven`, with options `War:*`.

## 3. W1: the generalisation (no behaviour change)

The riskiest step, so it comes first and alone.

- `Power` record; `Power.Empire` holds every Empire constant that is today in `EmpireSaga`, `EmpireArc`,
  `EmpireExpansion`, `CrisisStages` and the crisis operation tables.
- Saga, arc, expansion and crisis take a `Power`; Game's drivers run once per power (only the Empire in W1).
- **As built (W1):** `EmpireSaga`, `EmpireArc`, `EmpireStep`, `EmpireStance`, `EmpireState`, `EmpireSigns`,
  `EmpireEvent` and `EmpireExpansion` became `Power*` types in `Core/Powers/`. `Power` holds the ids and a `PowerText`;
  the saga holds its `Power` (not saved; the Empire by default), and the arc reads option ids and descriptions from it.
  The Empire's texts stay in `Core/Empire/` (`EmpireText`, `EmpireLetters`, `EmpireIds`, `EmpirePowerText`). The
  crisis already reads the saga, so it needed only the renames; its template id is on `Power.CrisisTemplateId`. Game's
  faction lookup is `PowerFactions.Of(power)`. **The drivers' loop over powers moves to W4**, where a second power
  exists to test it. Proof: `PowerNoChangeTests` (case hashes, saved labels) was committed before the rework and passes
  unchanged after it; `PowerTests` shows a second power getting its own ids and texts.
- **Proof of no change:** all existing tests pass unchanged; the Golden files don't move; `director-gen --seed 9001`,
  `--seed 1` and `--arc crisis` are byte-identical (a test compares hashes against the committed manifests); a save
  from `main` loads, plays a day and saves the same `empire` and `crisis` nodes.

## 4. The Hollow Choir

### 4.1 Faction and units

- `RimChronicles_HollowChoir` `FactionDef`: humanlike, Ultra, `permanentEnemy` false, a name maker and colours of its
  own; not in the world at start (R2). The leader is **the First Voice**.
- **Units** (§4.2 of the crisis tech spec, mirrored): Cantor (mechanitor, the Herald's mirror), Choirmaster (senior
  mechanitor), Chorister (mechanitor adept), and mechs by stage (Militor, Scyther, Lancer, Pikeman, Centipede, Tesseron,
  Legionary; bossgroup mechs like the War Queen and Apocriton only at the finale). Mech kinds are vanilla; only the
  mechanitor kinds and the group makers are ours.
- **Gear and implants by stage** set bandwidth, which sets how many mechs a mechanitor brings.

- **As built (W3):** `Defs/FactionDefs/RimChronicles_HollowChoir.xml` (fixed name "The Hollow Choir", leader
  title First Voice, not required or configurable at world creation) and `Defs/PawnKindDefs/RimChronicles_ChoirUnits.xml`
  (Chorister, Choirmaster, First Voice on Biotech's `MechanitorBase`), all `MayRequire` Biotech. Group makers mix them
  with vanilla mech kinds. `Power.Choir` and `ChoirText` (Core/Choir) are ready but stay out of `Power.All` until W4.
  `ChoirFaction.Create(home)` (Game) makes the faction, places 4 settlements 8–30 tiles from the colony and sets it
  hostile to the Empire; `PowerFactions.Of(Power.Choir)` finds it. Dev Mode: **Hollow Choir: create faction**,
  **Hollow Choir: raid (1,500 points)** (the checkpoint 1 check of mixed groups under vanilla lords). The Cantor and the
  stage rosters come with W7; the settlement count setting with W4.

### 4.2 Saga and plot

`Power.Choir`'s saga has the Empire's states and timings (spec §3.1, D6): triggers are colony wealth, a destroyed
Choir site, a mechanitor colonist, the Royal Ascent, and day 120 at the latest (spec D14, D15: the Empire stirs on day 20). The **hum** is the Stirring rumour:
bulletins and letters mention it, and Dev Mode can play it.

**Cross-waking (spec §4):** when a power is Awakened and **its first operation or expansion against anyone** happens,
the other power, if dormant or stirring, goes straight to Awakened (trigger `Rival`). Waking the Choir creates its
faction (R2).

**The plot** `HollowChoir` mirrors `AwakenedEmpire`'s acts and jobs, with `Choir:*` ids: envoy, expansion, escort,
conquest job, defend ally, sabotage, tribute, reckoning, and the Choir's Act 3 offers (§6).

- **As built (W4a):** the Choir's saga is saved as `choir` (`component.Choir`, `SagaOf(power)`); its `Power` is set
  after load. `Power.All` is `[Empire, Choir]`; the game runs `PowerFactions.Running` (the Empire with the major plot
  and Royalty; the Choir also with the **Hollow Choir** setting and Biotech). Each power has its own colonist trigger
  (`Power.ColonistTrigger`: `ImperialColonist`, `MechanitorColonist`). `ChoirDriver` (Game) runs the saga: triggers,
  rumours, waking (creates the faction, applies the colony's stored stance) and expansion, with the Empire's
  `Targets`, `Repaint` and `Raze`. Before the War in Heaven the Choir may already take Imperial settlements; W10 weights
  them up. The Empire's driver is unchanged; W4b folds both into one loop once the Choir has its plot.

- **As built (W4b):** the `HollowChoir` plot template; `ChoirBeats` (Game) mirrors `EmpireBeats` with `ChoirLetters`
  (Core/Choir): envoy, Act 2 jobs (escort, conquest job, defend ally, sabotage of a Choir settlement until W8 adds sites),
  tribute ("Join the Chorus"), reckoning, the First Voice's offer, the final demand, the treaty and the assault on the
  Cathedral (the Choir's oldest settlement, as a capital quest until W9). `Power.ServiceEnding`/`DefeatEnding` give the
  Choir its own endings (`Choral`, `Silenced`); `PowerArc.FinishJob(plot, power)` uses them. **Ranks (D13):**
  `ChoirRanks` (Core, saved as `choirRanks`): Listener, Chorister (4 favor), Choirmaster (12), Cantor (24), Voice of the
  Choir (40); 4 favor per finished escort or conquest job to the colonist with the most; Choirmaster or above opens the
  offer (the saga's `HighTitleHolder`, read daily). The plot is registered with `useDirector: false` and left out of the
  snapshot's `activePlots` until W12. Its endings send news and a letter and end the plot; victory screens and epilogues
  come with W11. `PowerDrivers.Daily` runs each running power's driver, then the allegiance sync; the Chronicle tab
  titles the story "The Hollow Choir".

- **As built (W5):** triggers `Rival`, `ShipLaunch`, `RoyalAscent`; `PowerSaga.WakeAtOnce` goes straight to Awakened
  (keeping a stirring's first trigger). `CrossWaking.RivalShouldWake`: a power awake and having attacked (an expansion,
  a crisis operation, or a raid against a ship launch or the Royal Ascent) wakes a rival that is still dormant or
  stirring; `PowerDrivers` checks it daily. The ship launch now wakes a sleeping Empire before its reaction; the Royal
  Ascent (`RoyalAscentPatch` on `Quest.Accept`) wakes the Choir and, unless the colony is its ally, raids at once and
  every 4 days while the ascent quest is ongoing (the saga's `NextShipRaidDay` serves both reactions). Dev Mode:
  **Hollow Choir: react to the Royal Ascent**.

### 4.3 The Choir's crisis

The Empire's crisis code, run with `Power.Choir`. What differs:

| Empire | Choir | Why |
| --- | --- | --- |
| Psyker AI (`IPsycastScorer`) | **Commander AI**: mechanitors hold back behind their mechs, keep line of sight, retreat when threatened; killing one shuts its mechs down (R4) | Mechanitors have no combat psycasts; their mechs are the threat, and the counterplay is reaching the mechanitor |
| Orbital strike from a Herald | **Cluster drop**: a Cantor calls in a small mech cluster (vanilla `MechClusterGenerator` parts), telegraphed by a marker and a delay, called off if the Cantor dies or loses sight | Same fairness rule 1: telegraphed, needs a living source |
| Drop-pod decapitation | **Burrowing strike**: mechs dig in near the leader (vanilla tunneler-style arrival), never under thick roof | Same fairness rule 3 |
| Siege with bombardment | **Cluster siege**: the siege planner places mech cluster buildings (turrets, mortars, a shield) in stages | `BuildStages` and `SpotScorer` reused |
| Psychic attack | **The Hum**: a map condition with a mood penalty and a work-speed penalty near Choir resonators, ended by destroying them | Mirrors the psychic relay's counterplay |
| Sites: forward outpost, psychic relay, orbital uplink, reclaimed holding | Cluster outpost, **Resonator** (powers the Hum), **Signal spire** (enables cluster drops), reclaimed holding | One for one, so the site planner's archetype rules carry over |
| The Citadel and the Stellarch | **The Cathedral** and **the First Voice** with a bossgroup guard | Same three-ring generator and keep shield (resonators as the shield's relays) |

Captives, interrogation, intel and defectors work as today, with Choir intel items.

**As built (W7–W8, the director's vocabulary first, so the retrain can go ahead):** `CrisisOperations.Choir` lists
`ProbingSwarm`, `TunnelBreach`, `TheHum`, `Retrieval`, `ClusterSiege`, `Burrowing` and `FinalChorus` (stages,
weights and cooldowns as the Empire's), `For(power)` picks the list, `IsSiege(step)` marks the sieges that take a
`Goal:*` (the goals are shared), and `CrisisContext.Power` (the Empire by default) selects them in `CrisisArc`. Site
defs carry a `power` (`empire` by default); `RimChronicles_ChoirSites.xml` adds `ClusterOutpost`, `Resonator`
(capability: psykers, the hum), `SignalSpire` (capability: strikes, the cluster drops) and `ChoirHolding`, on the
Imperial site parts until W8 builds the Choir's pieces. The game offers only the Empire's sites; `director-gen`'s site
loader takes a power. The Empire's operations, sites and cases are unchanged. The game side (units, the commander AI,
spawning, sites in game, the Cathedral) is still to do.

**As built (W7, the game side):**

| Where | What |
| --- | --- |
| Core `ChoirRoster`, `ChoirMixes` | Units: the mechanitor roles `Chorister`, `Cantor`, `Choirmaster`, `FirstVoice`, and `Mech` with a `MechClass` (Light, Line, Heavy, Tunneler, Boss) that maps to vanilla Core and Biotech mech kinds by stage, falling back to a lighter class when a kind is missing. Each operation has a mix; `Assign` gives each mech a commander by bandwidth (Chorister 5, Cantor 4, Choirmaster 8, First Voice 12) |
| Core `OperationProfiles.For` | The Choir's operations run their Imperial mirror's shape: ProbingSwarm the probe, Burrowing the hunt, FinalChorus the final assault, TunnelBreach and TheHum the standard; the retreat rules are tested as for the Empire |
| Core `CommanderTactics`, `ClusterDropRules`, `Burrowing`, `HumRules` | Row 1: a mechanitor stands 7 cells behind its mechs' centre, away from the nearest enemy, in sight of them, and falls back 12 cells when an enemy is within 7 (never more than 20 from its mechs). Row 2: a cluster drop needs a living Cantor who sees the target within 55 cells (or a signal spire, W8), is telegraphed 6–10 s, lands 6–14 cells from the target on open sky only, with a 1-minute cooldown and a 1–4 cap per raid from the budget; targets are chosen as for strikes. Row 3: tunnels come up 3–12 cells from the target, never under overhead mountain (a built roof doesn't stop them), with vanilla's 26–30 s rumble as the warning. Row 5: a resonator hums for at most two days over colonists within 70 cells, severity their psychic sensitivity (none for the deaf) |
| Game `ChoirCrisisDriver` | The mirror of `CrisisDriver`, with its own saved `CrisisState` (`choirCrisis` on the game component; old saves load without it), its stage texts (`CrisisText.ChoirStage*`), the `ChoirCrisis` plot, budget and Dev Mode. The two crises take turns (`CrisisArc.OtherBusy`): neither starts while the other's operation runs or within half the gap after it |
| Game `ChoirOperations`, `ChoirForce` | ProbingSwarm (walks in), TunnelBreach (tunnelers burrow up by the turrets or walls, the rest walks in), TheHum (1–2 resonators drop 18–34 cells from the colony, then an assault), Retrieval (burrows up by a Choir prisoner), Burrowing (by the leader), FinalChorus (the First Voice leads). `LordJob_RcOperation` takes a `power` (saved; `empire` by default) and the chain of command; a commander killed, downed or captured deactivates its mechs (`CompMechanoid.Deactivate`), which then count as lost. Reserves drop in under the surviving commanders |
| Game `ChoirCommand`, `RimChronicles_Command` | Mechanitors hold and shoot from the commander AI's spot (vanilla `JobGiver_AIDefendPoint` around a focus refreshed every 250 ticks); mechs take the crisis assault duty (or the retrieval's) |
| Game `MapComponent_RcClusterDrops` | Cantors' drops, telegraphed and called off as strikes are; the mechs join the Choir's operation under the cantor |
| Game `ChoirHum`, `RimChronicles_ChoirResonator`, `RimChronicles_Hum` | The resonator (Royalty's defoliator art for now) refreshes the `RimChronicles_Hum` hediff (−15% work speed, −30% and −5% consciousness when loud) and its mood thought (−6, −10) |
| Not yet | The Choir's captives' interrogation and intel, and the Cathedral (W9) |

**As built (W8):**

| Where | What |
| --- | --- |
| `PieceMeta.Power`, `PieceExtension.power`, `PieceSources.For`, `PieceLibrary.For` | A piece is the Empire's, the Choir's, or shared (no `power`). Each power's sites, camps and the Citadel plan only with their own and the shared: sandbags, barracks, the command room, the power yard and the armory are shared; the Imperial mortar pit, turret nest, siege buildings, relay chamber, uplink station, records room, throne room and vault are the Empire's |
| `Defs/PrefabDefs/RimChronicles_ChoirPieces.xml`, `Defs/ThingDefs/RimChronicles_Choir.xml` | The Choir's pieces: a mech charge-turret nest (Turret tag), an auto-mortar (Artillery, stage 2), a signal spire (Uplink slot, stage 3), a shield and resonator (Relay slot, stage 4), a drop beacon (stage 5), and the objective rooms `ResonatorChamber` and `SpireStation`. Its buildings: `RimChronicles_SignalSpire` (Biotech's mechband antenna art; lets cluster drops come with no cantor), `RimChronicles_ChoirShield`, `RimChronicles_ChoirBeacon`, and the W7 resonator. Vanilla's mech-cluster turrets start awake and need no power or crew |
| `Defs/SitePartDefs/RimChronicles_ChoirSites.xml`, the Choir archetypes | Each Choir archetype has its own site part and gen step (W7's pointed at the Imperial ones). The Resonator's objective is a resonator, the SignalSpire's a spire; the records room is left out |
| `GenStep_RcSite`, `SiteStamper`, `MapComponent_RcSite` | A site is generated for its archetype's power: its faction (`PowerFactions.Of`), palette (plasteel and steel for the Choir), pieces and garrison (`ChoirMixes.ForSite`, under `LordJob_DefendBase`). The site's record keeps the garrison's chain of command and silences a fallen mechanitor's mechs. Destroying a Choir objective holds the Choir's capability (the resonator: the hum, 15 days; the spire: cluster drops and the cluster siege, 15 days) in `ChoirCrisis` |
| `SiteOffers`, `Liberation`, `ChoirBeats` | Offers, the holding and liberation take the power from the faction (the holding from that power's saga). The Choir plot's `SabotageChoir` job offers the Choir's sites while its crisis runs (the director picks the `Site:*` with a War in Heaven model), and a Choir-held town is freed as the Empire's are |
| Core `ClusterSiegeRules`, `ChoirMixes.ClusterSiege`; Game `ClusterCamp`, `LordToil_RcClusterSiege`, `ChoirOperations.ClusterSiege` | Row 4: the cluster siege walks to the Imperial siege's best camp spot and lays out `CampLayout` with the Choir's pieces. Its stages aren't built: the first lands on arrival, each next one drops in by pod (`PieceStamper.Pods`) 18,000 ticks after the last while a mechanitor stands within 20 cells. Its mechanitors are its builders (`SiegeRules.AfterBuildersFall`); its beacon brings waves from the reserve; its goal (`Goal:*`, the director's second call) aims the cluster drops; when it ends, what it dropped is left ownerless |

**As built (W9):**

| Where | What |
| --- | --- |
| Core `CitadelOptions.Power`, `CitadelRings.*TagFor`, `ChoirMixes.Cathedral` | The Cathedral is the Citadel's three-ring plan with the Choir's rooms: resonator chambers hold up the shield (the relays' place), signal spire stations stand in the cloister (the uplinks'), and the sanctum (`RimChronicles_Piece_Sanctum`) is the keep's heart (the throne room's). Ring names: outer works, cloister, sanctum. Garrisons: the outer works' mech line with a cantor, the cloister's heavier line under a choirmaster, the sanctum's bossgroup (boss mechs and heavies) commanded by the First Voice |
| Game `CitadelMaps.IsCathedral`, `GenStep_RcCitadel`, `MapComponent_RcCitadel` | The Choir's capital generates as the Cathedral once its crisis is at war and the Choir is hostile (the same map generator and patch). Generation, the shield (resonators in the cloister; the sanctum takes a tenth of the damage while one stands), lazy rings and the reinforce memo are the Citadel's; the welcome and shield letters are the Choir's; the cloister's resonators also hum. The Cathedral keeps its chain of command and silences a fallen mechanitor's mechs. Destroyed Resonator and Signal Spire sites take resonators and spires from the cloister |
| Game `FirstVoice` | One First Voice, made once and kept between appearances (the Choir crisis's leader fields), leads the final chorus and stands in the sanctum. Its death or capture is announced and carries the colony's assault on the Cathedral (`StormTheCathedral`). It doesn't skip away like the Stellarch: bringing it down silences the bossgroup it commands |

## 5. Allegiance and the summons

- **Allegiance** (R8). Siding with a power makes the colony hostile to the other (faction relation set, the other
  plot's stance to oppose). Independence opposes both. Before the Choir exists, an Imperial side is stored and applied
  when the Choir wakes.
- **As built (W2):** `Allegiance` (Core, saved as `allegiance`) keeps a stance per power and a log; siding with one
  power sets the others to oppose, and `SideAmong(powers)` gives the side. Each plot still reads its own `stance` fact,
  so `PowerArc` and the director's cases are unchanged; `AllegianceDriver` (Game) syncs them daily: a stance the plot
  set comes into the allegiance, and the rivals' stances it changes go out to their plots and goodwill
  (`PowerFactions.ApplyStance`, shared with the Empire plot). An old save has no allegiance, so its first sync reads the
  Empire plot's stance. A power without a plot yet takes its stance from the allegiance when its plot starts (W4).
  Dev Mode: **War in Heaven: set side**, **War in Heaven: show allegiance**.
- **The summons** (spec §6.1). A daily rule, not a director choice: while the side is Empire (or Choir) and the colony
  has a pawn of the other kind not already summoned, a summons quest starts for that pawn. A refusal sets the side to
  independent (cause `Summons`). Each pawn is summoned once per side change.
- **Conversion** (R5) happens when the pawn returns: the mechs are deactivated first, then the mechlink comes off and
  the psylink goes on (or the psylink and its psycasts come off and the mechlink goes on). The pawn's backstory and skills don't change. A letter names what they became.
- The LLM writes the summons letter and the return letter, with template fallbacks (rule 7).

- **As built (W6):** `SummonsBook` (Core, saved as `summons`) picks the next colonist of the kind the side can't keep
  (one pending per power, each colonist once per side) and records how each summons ended. `SummonsDriver` (Game,
  daily after the allegiance sync) offers `RimChronicles_Summons` (`QuestNode_Root_ChroniclesSummons`: the side's
  shuttle with the colonist required, a day to board) through the side's plot. `QuestPart_RcSummons` subclasses
  `QuestPart_LendColonistsToFaction` and converts in `Complete` (`SummonsConversion`): mechs deactivated, mechlink off,
  psylink set to the formula's level; or psylink and psycasts off, `MechlinkImplant` on, `RimChronicles_ChoirBandwidth`
  (six stages, +4 each above the first) at the level. A shuttle leaving empty (`QuestPart_RcSummonsRefused`) or an
  offer declined or lapsed sets the colony independent (cause `summons`). `FloatMenuOptionProvider_RcDismantle` lets
  any colonist queue vanilla `DisassembleMech` on a deactivated mech. Template letters only. Dev Mode: **War in
  Heaven: send a summons now**, **return the summoned colonist now**, **convert the selected colonist**.

## 6. The War in Heaven and the endings

- **Start** (R9). The daily roll begins once both powers are Awakened (or beyond) and the colony is at war with at
  least one. A news bulletin and a letter announce it.
- **Front** (R10). Each power's expansions target the other's settlements first. Crisis operations against the colony
  continue, with the stage budget scaled by a "pulled to the front" factor (a setting, default ×0.8).
- **The `WarInHeaven` plot** offers `War:*` jobs by side: defend your side's settlement, strike an enemy settlement,
  escort a convoy, sabotage the enemy's resonator or uplink. Losses and wins move a front score.
- **Endings** (spec §7.3):

| Side | Ending | When |
| --- | --- | --- |
| Empire | Imperial victory | The Choir's Cathedral falls (to the player or to the Empire's push), then the governorship offer is accepted |
| Choir | **Choral victory** (new) | The Empire's Citadel falls, then the First Voice's offer is accepted |
| Independent | Independence | One power is destroyed and the other's treaty is accepted |
| Independent | **Independence, total** (new) | Both powers are destroyed |

The epilogue task gets the two new endings (prompt change, rule 8). A refused treaty sends a letter and the war goes
on.

**As built (W10–W11 Core):** `WarInHeaven` (Core/War, saved): state, start day and cause, the front score, destroyed
powers and a log. `Odds(warDays)`: 0 for 15 days of war, then rising to 5% a day at 165 days, capped at 10% (spec D14);
`StartsToday` is certain from day 600; `CanStart` needs both powers awake (story not over) and the colony at war
(`Allegiance.WarSince()` gives the war's first day). `WarArc` (template `WarInHeaven`, options `War:*`): on a power's
side `DefendFront`, `StrikeRival`, `EscortConvoy`, `SabotageRival`, and `FinalPush` from front 3 (spec D14) (success destroys the
rival); once the rival is destroyed, `VictoryOffer` (the side's `ServiceEnding`: Imperial or Choral). Independent:
`StrikeEmpire`/`StrikeChoir`, `SabotageEmpire`/`SabotageChoir`; after one power falls, `TreatyOffer` (refusable,
Independence); after both, `Ending:IndependenceTotal`. Jobs move the front by ±1. `Power.DisplayName` names the
powers in descriptions. The game side (the daily roll, the War plot's quests, the rival weighting in expansion,
destruction instead of the Resistance ending, victory screens and the epilogue's new endings) is still to do.

**As built (W10, the game side):** `WarDriver` (Game/War) rolls the start daily while both are awake and the colony at
war (the settings: on/off, the certain day, the odds' steepness), announces it (news, a letter) and starts the
`WarInHeaven` plot, which the director decides with a War in Heaven model. Its jobs are the template quests: DefendFront
a `DefendAlly` on the side's nearest settlement against the rival; Strike a `Sabotage` of a settlement's comms console
(never a capital); EscortConvoy an `Escort` to the side's settlement; Sabotage one of the target's crisis sites while its
crisis runs (W8), else a strike; FinalPush the rival's capital (`Capital`, which generates as the Citadel or the
Cathedral); VictoryOffer and TreatyOffer letters. Aftermath: a won strike hands the settlement to the side, a lost
defence to the rival; a won final push destroys the rival. **Destruction (R11):** once the war is on, or both powers are
awake, taking a power's capital through its own plot destroys it (`WarDriver.Destroy`: the war's record, the saga's
ending `Destroyed`, no more expansions, its plot and crisis ended, news and a letter) instead of the Resistance or
Silenced ending; with only the Empire awake, Resistance stands. A destruction before the war begins it. **The front:**
expansions weight the rival's settlements ×3 during the war (`PowerExpansion.FrontWeight`) and never take a power's
capital; both crises' budgets are scaled by the front factor (0.8). The ending is recorded (the war's log, both sagas,
news, a letter); the victory screens and epilogues are W11. Dev Mode: **War in Heaven: show**, **start now**, **destroy
a power**, **move the front**.

**As built (W11):** the epilogue covers every ending: Imperial, Resistance, Independence, and new Choral victory, the
Choir silenced and total independence. `EpilogueCase.Rival` marks a War in Heaven ending, and `EpilogueTask.Meaning`
says how the rival fell (Imperial and Choral in the war, Independence by the survivor's treaty, total independence).
Each has a headline and a template epilogue. **Prompt `epilogue/v3`** (rule 8) speaks of the powers, not only the
Empire, and heads the power's name "The power"; the stance line for a neutral colony reads "the powers' wars". The
harness gains `snap-26-ending-choral` and `snap-27-ending-independence` (schema: the new endings, `rival`); a Stage 0
run on the PC checks v3 at zero API spend. In game, `EmpireEndings.Epilogue` is the shared victory screen (the credits
with the epilogue, a letter, the plot's story), and `CaseFor` takes the power (the Choir's high-rank holder named
first), the rival and the war's events. The Choir plot's Choral and Silenced endings and every war ending
(`WarDriver.Finish`, which also ends every power's plot and crisis) use it. Dev Mode: **War in Heaven: reach an
ending**.

## 7. Director, snapshot and prompts

- Snapshot gains `allegiance`, a `choir` block (mirror of `empire`), a `choirCrisis` block (mirror of `crisis`) and a
  `warInHeaven` block. Schema, `WorldSummarizer`, Golden files (`UPDATE_GOLDEN=1`, then review) and a Stage 0 harness
  run at zero API spend (rule 8).
- `director-gen` gains arcs `choir`, `choir-crisis` and `war`. The existing arcs' cases stay byte-identical (W1 proof);
  the test set becomes **version 3**: test-v1, test-crisis-v1 and a new `test-war-v1`, which Claude Code labels in
  session (teacher prompt v5: the v4 rubric plus Choir and War in Heaven rules).
- Vocabulary levels replace `KnowsCrisis` (R12). The retrained presets declare `WarInHeaven`.
- **One retrain** (spec D5): the 12B labels the new training cases (crisis and war together), E2B and E4B retrain under
  `-war` names, reports on test version 3, then `ollama cp` over the shipped names. `training/README.md`'s "Crisis
  retrain" section becomes "Retrain for the War in Heaven".

**As built (W12):**

| Where | What |
| --- | --- |
| `WorldSnapshot.War` (`WarInfo`), `ChoirCrisis` | The war block (started, side, both stances, front, destroyed powers) and the Choir's crisis block; schema and `WarBrief` |
| `WorldSummarizer` | "The War in Heaven:" and "Hollow Choir crisis, stage …" blocks, director only, only when present (existing prompts byte-identical); the Choir's faction reads "Choir state", and bulletins use Choir rumours for it |
| `Power.OptionNames` | The Choir's own option names: `Choir:SabotageChoir`, `Act3:FirstVoiceOffer`, `Act3:StormTheCathedral` (the Empire's unchanged) |
| `TierPresets.DirectorVocabulary` | `Base`, `Crisis`, `WarInHeaven` per preset (both shipped presets `Base`); `KnowsCrisis` and `KnowsWarInHeaven` read it; prompted models know everything |
| `ChoirDriver.DirectorDecides`, `WorldReader` | The Choir plot (and later its crisis and the War plot) and the war block reach the snapshot, and the director decides the Choir plot, only for a model that knows the War in Heaven |
| `director-gen --arc war` | `CaseGenerator.WarScenarios`: 9 Choir plot, 7 Choir crisis and 9 War plot scenarios (300 per cycle); ids `dir-s{seed}-w{n}`; `seed-<n>-war` (default 1,500); generator version 1 of its own. The Empire, crisis and minor-arc cases are byte-identical |
| `director-dataset` | Test version 3 when War in Heaven test cases are in the test split; `DirectorLabels.Group` names Choir follow-ups `ChoirCrisis/SiegeGoal` and `ChoirCrisis/Site` |
| `harness/snapshots/snap-25-war-in-heaven.json` | A War plot snapshot for the Stage 0 harness; golden `director.snap-07-war.*` |

## 8. Settings

The **Major plot** section gains: Hollow Choir on/off (needs Biotech), War in Heaven on/off, its certain-start day
(default 600), the odds curve's steepness, the front factor, and Choir settlement count. The crisis difficulty and
gap settings apply to both powers.

## 9. Testing

- **Unit tests:** W1's no-change proof; `Allegiance` transitions; cross-waking; conversion at every level; the War in
  Heaven odds and day-600 rule; expansion with an enemy set; the endings table; Choir unit rosters by stage; commander
  AI on `BattleView` fixtures; persistence of every new saved type, and old saves without them (rule 5).
- **Dev Mode actions** (rule 7): wake the Choir, wake both, set the side, send a summons now, return the pawn now,
  shut down a mech, start the War in Heaven, jump to game day 600, destroy a power, each ending.
- **In-game tests:** `war-runbook-1.md` … per checkpoint (project plan), added to `docs/test-plan.md`; John runs them
  once per checkpoint, folded into the final proof run where possible.

## 10. Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| W1 changes behaviour by accident | Old saves or the director break | W1 alone, with byte-identical case hashes, unchanged Golden files and a save round-trip |
| A mid-game faction misbehaves (relations, ideology, world map) | Choir broken after waking | W0 checks `NewGeneratedFaction` mid-game in a dev save; fallback: create it at game start as hidden and reveal it on waking (`Faction.hidden` is a saved override, W0) |
| Mechs in a humanlike faction's lords act oddly | Choir raids look wrong | W0 spawns a mixed Choir group with vanilla `LordJob_AssaultColony` before any custom AI |
| Summons quest loses the pawn | A colonist vanishes | The pawn is kept by the quest; a test-only Dev Mode loop runs 50 summons and checks every pawn returns |
| Two crises at once are too hard for an independent colony | Unfair | The front factor, the minimum gap between operations (shared by both powers) and the C14 balance results; the balance pass (W14) tests an independent colony |
| Retraining twice | GPU time | One retrain, after W12 fixes the vocabulary (spec D5) |

## 11. Decisions to confirm

- **T1** Long-lived branch `war-in-heaven` (as the crisis had), merged into `main` after W14.
- **T2** The Choir faction is created when it wakes (R2), not at game start.
- **T3** Vocabulary levels replace `KnowsCrisis` (R12).
- **T4** The Choir's crisis differences in §4.3 (commander AI, cluster drop, burrowing strike, cluster siege, the Hum,
  Resonator, Signal spire, the Cathedral, the First Voice).
- **T5** Choral victory and Independence, total as the two new endings (§6).
