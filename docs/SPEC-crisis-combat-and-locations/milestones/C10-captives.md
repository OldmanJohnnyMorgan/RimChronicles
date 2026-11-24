# C10: captives

Crisis milestone C10 (project plan §2). Built 2026-11-23; not yet checked in game (checkpoint 3).

## What's there

| Where | What |
| --- | --- |
| `src/RimChronicles.Core/Crisis/Captives/Captives.cs` | Ranks, captive and defector records, the intel log and forecast; `CaptiveRules` (resistance and will by rank, how much each rank knows, the session); `IntelEffects` |
| `src/RimChronicles.Core/Crisis/CrisisState.cs` | Captives, defectors, intel, the forecast, strike jams, adept shortfall, keep entrance, the release discount, treatment counts (all saved; an old save loads them empty) |
| `src/RimChronicles.Core/Crisis/CrisisArc.cs` | A valid forecast holds the crisis to the warned-of operation |
| `src/RimChronicles.Core/Crisis/Operations/` | Retrieval's profile and mix; `ForceMix.ShortOfAdepts` |
| `src/RimChronicles.Game/Crisis/Captives/` | `IntelDef`, `CaptiveOps` (captures, defectors, release, treatment, applying intel, revealing sites and garrisons, Dev Mode), the interrogation (work giver, job, interaction), `CompUseEffect_ReadRecords`, Retrieval's toil and job giver |
| `src/RimChronicles.Game/Patches/CaptivePatches.cs` | Capture, recruitment, release, execution, enslavement, organ harvest |
| `src/RimChronicles.Game/Crisis/` | Retrieval in `OperationSpawner` and `LordJob_RcOperation` (freeing, focus, carrying a defector off); the crisis knows when a captive is held; the release discount; stolen codes jam a strike; relay intel thins the next psychic assault; **Crisis: show state** lists captives, intel and the rest |
| `Defs/Captives/RimChronicles_Captives.xml` | The prisoner mode, work giver, job, interaction, the Retrieve duty, the Imperial dispatch item and five intel items |
| `Defs/PawnKindDefs/RimChronicles_Units.xml` | Resistance and will by rank |
| `Defs/PrefabDefs/`, `Defs/SiteArchetypeDefs/` | The records room piece, optional in all three sites |
| `tests/…/CaptiveTests.cs` | Every intel effect, sessions, ranks against the pawn kinds, defectors, release, Retrieval, saving |

## Captives

| Rank | Units | Resistance | Will | Can tell | Gives up |
| --- | --- | --- | --- | --- | --- |
| Soldier | Praetorians | 35–50 | 7–9 | 1 item | garrison, next operation |
| Breacher | Breachers | 30–45 | 6–8 | 1 | garrison, next operation |
| Herald | Heralds | 45–60 | 8–10 | 2 | uplink codes, next operation |
| Psyker | Adepts | 60–80 | 11–14 | 2 | relay location |
| Officer | High Stellarch | 90–120 | 18–22 | 3 | Citadel plans |

Vanilla raiders sit around 15–25 resistance. The ranges are on the pawn kinds, so vanilla applies them at capture (a
royal title adds its own offset); a test holds them to Core's table.

**Interrogate (Imperial intel)** is a checkbox in the prisoner tab, beside the usual modes, shown for every prisoner.
A warden questions the prisoner when they're due a visit (the same schedule as recruitment chats). A session's chance
is 35% per point of the warden's social impact, less for heralds (×0.8), adepts (×0.7) and officers (×0.5), between 5%
and 80%. A success gives an item the captive hasn't given yet, takes 4 resistance and half a point of will; a failure
takes 1 and half a point. A prisoner who isn't Imperial, or has told all they know, is skipped with the reason in the
work tab (tech spec R21).

## Intel

| Item | Effect | Stale after |
| --- | --- | --- |
| Garrison | The nearest known Imperial site's garrison and gates, planned as its map will be | 15 days |
| Next operation | Picks the next operation now, by weight, and names it and its earliest day; the crisis then runs that one (and nothing before that day) until it fires or the warning goes stale 5 days later | 15 days |
| Uplink codes | The next orbital strike called fails (up to two held) | 20 days |
| Relay location | A Psychic Relay site appears on the world map for 15 days; the next psychic assault has half as many adepts | — |
| Citadel plans | The keep's side entrance is known (C12 uses it) | — |

Each item is logged (**Crisis: show state** lists the last ten) and announced in a letter. **Imperial dispatches** lie
in the records room that sites may have (optional in all three archetypes): a colonist reading one gains one item of
any kind (Citadel plans at a quarter of the usual chance).

## Defectors, release and treatment

- A captive recruited **after at least one interrogation** becomes a defector: a letter, a news item, and the crisis
  may now send a Retrieval. Recruited without questioning, they're just a recruit.
- Releasing an Imperial captive: +5 goodwill with the Empire and the next operation's budget ×0.85, once.
- Executions, organ harvests and enslavements of Imperial captives are counted (`CrisisState.Executed`, …) for the
  director's harsher options in C11. Vanilla's own tales and goodwill effects still apply.

## Retrieval

Offered from stage 2 while the colony holds an Imperial captive, or has a defector (the done-when). Drop pods land
on open sky near the held pawn (captives first, then by rank). Praetorians, a Breacher, Skip and Ward Adepts, and a
Herald from stage 3.

- **For a captive**, the soldiers make for them: they walk in if there's a way without breaking anything, else they
  dig (vanilla's sapper). A soldier beside the captive frees them: they rejoin the Empire and leave with the squad,
  carried if they can't walk.
- **For a defector**, it's an assault with every gun on them. Once they're down, a soldier carries them off (vanilla's
  kidnap).
- It leaves after losing half its force, or after about four hours (counted as repelled).

## Decisions

- **Interrogate is non-exclusive.** It's a checkbox, so a prisoner can be questioned and worked on for recruitment at
  once; each warden visit does one or the other. "Interrogating first lowers loyalty faster" is the resistance a
  session takes.
- **The forecast is a commitment, not a guess.** The item picks the next operation then and there, and the arc holds
  to it, so the warning is always true while it holds.
- **The "captured psykers keep their psylink"** part needs nothing: vanilla keeps it.
- **Revealed sites aren't quests.** A relay revealed by intel is a site with a timeout; destroying it knocks the
  capability out as any relay site does, without a reward.
- **Snapshot and director wiring wait for C11**: captives by rank, defectors, intel and treatment in the summary,
  `Hook:DefectorPlot`, `Complication:DefectorHunted` and `Complication:ImperialVengeance`.
- **The outpost's "capture the officer"** objective isn't added: outposts have no officer unit yet (the High Stellarch
  is the only officer). Capturing any Imperial there and questioning them is the way to their intel.

## Dev Mode

- **Crisis: spawn captive** → a unit: spawns it at the click as the colony's prisoner (stage 3 kit).
- **Crisis: grant intel** → an item: applies it as if a captive had given it up.
- **Crisis: fire operation** → Retrieval → a stage (needs a held captive or defector on the map).
- **Crisis: show state** now lists captives, who's held, defectors, the forecast, jams and the intel log.
