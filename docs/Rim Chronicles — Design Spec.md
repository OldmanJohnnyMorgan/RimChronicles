# Rim Chronicles — Design Spec

Nov 20, 2026 · @Johnny

## Overview

Rim Chronicles is a RimWorld 1.7 mod that adds a story mode: a major plot (the Awakened Empire) and minor plots grown from each colony's own history. A local LLM narrates them, and the player can win by completing a plot's ending. It is built in six stages, each one testable on its own, starting with a model evaluation that needs no game at all.

**Goals**

- The colony gets a story with acts, stakes and an ending, without losing RimWorld's sandbox feel.
- Minor plots come from what actually happened in this colony: its tales, relationships, backstories and deaths.
- A news agent reports on the world every 3 in-game days, so the player sees the story moving.
- The player interacts with plots through quests and choices, not walls of text.
- The mod works on modest hardware with a local model, and works better with a bigger one.

**Non-goals**

- Replacing the storyteller (Randy, Cassandra, Phoebe keep running underneath).
- Free-form chat with colonists (RimTalk and similar mods already do that).
- Supporting RimWorld 1.6 or earlier.

**Target platform:** RimWorld 1.7 on the Waylith engine only, currently on the unstable Steam beta branch.

**Design principles**

1. **Code owns the story; the LLM narrates.** Plot state, allowed transitions, quests and win conditions live in C# and XML. The LLM picks from allowed options and writes the words. A small model can't break the plot.
2. **Never block the game.** Every LLM call is async and generated ahead of need. The game never waits on a model.
3. **Always have a fallback.** Every generated text has a template version. The mod is fully playable with the LLM switched off.
4. **Generate once, save forever.** Generated text is stored in the save file, so a reload never rewrites history.
5. **Model-agnostic.** The mod talks to any OpenAI-compatible endpoint. Prompts target the smallest supported tier.
6. **Built by AI, verified against source.** All code is checked against the decompiled 1.7 assemblies, not against the AI's memory of older versions.

## Stage 0: Model evaluation harness

Stage 0 picks the default model and the tier presets from our own prompts, not from leaderboards. It's a standalone command-line tool that needs no RimWorld. It later becomes the regression test for every prompt change.

### Deliverable

A C# (.NET 10) console app, built on the same LLM-layer library the mod will use, that loads world snapshots, runs each task against each configured model through an OpenAI-compatible endpoint, scores the results, and writes a comparison report (CSV plus an HTML summary).

### World snapshot format

Each snapshot is a JSON file describing one moment in a colony's game, hand-written at first and later exported from real saves (Stage 1 adds the exporter). Start with 20 snapshots covering early, mid and late game, peaceful and desperate colonies, and at least 3 with an active Empire plot.

```json
{
  "id": "snap-07-midgame-raided",
  "day": 412, "season": "Aprimay", "year": 5502,
  "colony": { "name": "Hearthstead", "wealth": 184000, "population": 9, "mood": "strained" },
  "pawns": [ { "id": "p3", "name": "Mara", "age": 34, "traits": ["Tough", "Bloodlust"],
      "backstory": "Imperial deserter", "relations": ["spouse of p5"], "ideoRole": "Shooting specialist" } ],
  "tales": [ { "day": 398, "type": "KilledColonist", "actor": "Raider 'Vex'", "victim": "p5", "escaped": true } ],
  "factions": [ { "name": "The Shattered Empire", "goodwill": -40, "plotState": "Stirring", "settlements": 4 } ],
  "activePlots": [ { "id": "nemesis-01", "template": "Nemesis", "beat": "Hook", "protagonist": "p3" } ],
  "recentEvents": [ "Raid repelled day 398", "Trade caravan day 405" ],
  "allowedTransitions": [ "Complication:VexSighted", "Complication:ImperialBounty", "Wait" ]
}
```

### The three tasks

| Task | Input | Output | What it tests |
| --- | --- | --- | --- |
| Director decision | Snapshot + allowed transitions | JSON: `{ "choice": "...", "reason": "..." }` | Instruction following, valid JSON, picking only allowed options |
| News bulletin | Snapshot + last 2 bulletins | 40–130 words of in-world news, scaled to the news: 40–80 on a quiet week (one new item or none), 50–100 with two new items, 70–130 with three or more | English prose, grounding (no invented facts), voice consistency |
| Inheritance handoff | Dead protagonist's dossier + heir's dossier + plot summary | 60–120 word scene | Emotional writing, using the supplied relationship correctly |

Each task runs twice per model: once with grammar-constrained JSON where supported, once without, to see how much the grammar is carrying.

### Candidate models

| Tier | Model | Why it's in the test |
| --- | --- | --- |
| Minimum (\~2B) | Gemma 4 E2B | Mainstream baseline at the smallest size |
| Recommended (\~4B) | Gemma 4 E4B | Token-efficient, so faster bulletins |
| High (12B) | Gemma 4 12B | Ceiling for local models on a single consumer GPU |
| Reference | Claude (cloud) | Quality ceiling; a Claude model is also the judge |

Candidates are just entries in a config file, each with its own endpoint URL, so new models can be added without code changes.

Qwen3.5 (2B and 4B) and Raon-1 nano were in the original candidate list and are out of the project. The Qwen models invented far more facts than Gemma in the trial rounds. Raon-1 nano runs only on Krafton's llama.cpp fork, which is too much setup to ask of players.

### Scoring

| Metric | How it's measured | Pass bar |
| --- | --- | --- |
| JSON validity | Parses and matches schema | 100% with grammar, ≥95% without |
| Allowed choice | Choice is in the allowed list | 100% |
| Invented facts | Judge lists any name, event or number not in the snapshot | ≤1 per 10 outputs |
| Prose quality | Judge scores 1–5 on voice, clarity and RimWorld tone, against a written rubric | Average ≥3.5 for the default tier |
| Length compliance | Word count within range | ≥90% |
| Latency | Seconds per output on John's GPU, thinking included | Bulletin under 20 s (background), director under 10 s |
| Tokens used | Output tokens, thinking included | Reported, no bar |

The judge is a cloud model with a fixed rubric prompt. Spot-check 10% of its scores by hand to make sure it agrees with you.

### Exit criteria

- A default model chosen for the Recommended tier, and a fallback for the Minimum tier.
- The final prompt templates for all three tasks, versioned in the repo.

**Status: done (2026-11-21).** Gemma 4 E4B is the Recommended default, Gemma 4 E2B the Minimum fallback, and Gemma 4 12B the High preset. The prose-quality bar was waived on the project owner's reading of the outputs, since even the Claude reference scored under 3.5 on the handoff. Results and reasoning are in `docs/stage0-results.md`.

## Stage 1: RimWorld 1.7 technical spike

Stage 1 proves the plumbing works on Waylith before any story code is written. The runtime, Harmony and threading facts are now known (below); this stage confirms them hands-on before anything depends on them.

### Setup

1. Switch RimWorld to the unstable beta branch (1.7).
2. Decompile the game assemblies with ILSpy and keep the output in a `reference/` folder outside the mod repo. This is the AI's ground truth for every API.
3. Set up the toolchain: Visual Studio 2026 or Rider 2026 (older versions don't support .NET 10), a \`net10.0\` class library, and the Harmony 3 mod from the Workshop as a dependency.

### Confirmed for 1.7

| Area | 1.6 (Unity) | 1.7 (Waylith) | What it means for us |
| --- | --- | --- | --- |
| Runtime | Mono, .NET Framework 4.7.2 | .NET 10 on WaylithCLR, Ludeon's modified runtime | Target `net10.0`; modern C#, async/await and `HttpClient` are available |
| Harmony | 2.x | Harmony 3 (Workshop mod or matching NuGet) | Depend on the Workshop mod; never bundle `0Harmony.dll`; injection parameters (`__instance`, `__result`) are type-checked more strictly |
| Threading | Single-threaded simulation | Unchanged: still single-threaded, no new multithreading in game code | The main-thread queue stays mandatory |
| Unity APIs | Native | Compatibility shims (`UnityStubs.dll`) | Old mods keep working, but new code avoids Unity APIs entirely |
| Game logic | n/a | About 96.5% of decompiled functions identical to 1.6 | Most 1.6 knowledge of RimWorld's game APIs still applies; the engine layer is what changed |

Source: [RimWorld 1.7 Mod Updates](https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates) and community research, gathered 2026-11-20. Recheck the details if the beta changes them before stable release.

### The spike mod

A throwaway "hello world" mod that does five things:

| # | Test | Passes when |
| --- | --- | --- |
| 1 | Loads on 1.7 with modern C# (async/await, records) | Mod loads with no errors in the log |
| 2 | Harmony patch on a game method (e.g. the daily tick) | Patched code runs once per in-game day |
| 3 | Background HTTP call to a local LLM server, result handed to the main thread through a queue | Response shows as an in-game letter, no stutter, no errors |
| 4 | Custom `GameComponent` with `ExposeData` | Data survives save, quit and reload |
| 5 | Snapshot exporter: writes the colony state to the Stage 0 JSON format | The file loads in the eval harness |

Test 5 matters most: from then on, the eval harness runs on real colonies.

### Exit criteria

- All five tests pass on the current 1.7 beta.
- A one-page `ENGINE_NOTES.md` recording what was confirmed (runtime, Harmony, threading) and any API differences from 1.6.
- The adapter-layer interface drafted (see Architecture).

**Status: done (2026-11-21).** All five tests passed in game on RimWorld 1.7.9819 with Harmony 3.0.0-rc.7. A live export from a new colony passes the harness's snapshot check, apart from `allowedTransitions`, which the director fills from Stage 4. Findings are in `docs/ENGINE_NOTES.md`.

## System architecture

The mod has three layers, and only the bottom one, the engine adapter, ever calls RimWorld. If 1.7 changes during beta, only the adapter is rewritten, and the story core and LLM layer stay untouched.

&#91;embedded content: mod architecture · 3 layers and an external model server\]

The plot director (highlighted) is the heart of the mod: it owns every plot's state machine and decides when beats fire. The model server is external and optional (dashed); without it, the validator serves template text.

**Data flow for one beat:** readers → world summarizer → plot director picks the allowed transitions → LLM client asks the model in the background → validator checks the reply (or falls back) → the result is queued for the main thread → the quest spawner or letters fire it → it's saved.

**Persistence:** one `GameComponent` saves all plot state, generated text, the director's decision log and the reporter's archive. Nothing is regenerated on load.

## Stage 2: News agent

The news agent is the first player-facing feature: an in-world reporter who sends a bulletin every 3 in-game days (180,000 ticks). It's the smallest feature that runs the whole pipeline, so it ships first and proves the architecture.

### The agent

- A named NPC reporter from a neutral outlander faction, generated at game start, with a voice set by a short persona (e.g. dry, wry, slightly cynical).
- Delivered as a letter, with all past bulletins archived in a "Rim Dispatch" tab the player can scroll back through.
- The reporter can be visited, kidnapped or killed. Killing them starts the Silent Press minor plot (below).

### When the reporter dies

- **Killed by the colony** (by colonists, a colony turret or trap, or executed as a prisoner): bulletins simply stop. No letter explains why, and the player has to work it out. This starts the Silent Press arc.
- **Any other death** (a raid, an animal, disease): the outlet sends a replacement after 5–10 days, who mentions the loss in their first bulletin.

**Silent Press arc:**

1. **Hook: silence.** The next bulletin never arrives. The Rim Dispatch tab still shows only the old bulletins, with no notice.
2. **Complication: clues.** Over the next 10–20 days, hints reach the player indirectly: a trader mentions the Dispatch "isn't sending anyone your way anymore," or a visitor repeats a rumour that the colony murdered a journalist.
3. **Choice: reparations.** Once the player has had time to notice, an envoy from the outlet arrives demanding reparations (silver scaled to colony wealth). The player can pay, refuse or haggle.
4. **Resolution.** Pay: a new reporter is assigned, and their first bulletin is noticeably cold about the colony. Refuse: the news blackout is permanent, the colony loses its world news, and the outlet's faction loses goodwill with the colony. The player can still settle later through a Choice letter.

It goes in the minor plot set (Stage 3), but its trigger is the reporter's death, not a seed detector.

### Bulletin content

Each bulletin has 1–3 short items, drawn by priority from the list below, and its length scales with the news: 1–2 items and 40–80 words on a quiet week (one new item or none), 2 items and 50–100 words with two new items, 3 items and 70–130 words with three or more. A short bulletin beats one padded with invented detail.

1. Major plot developments (Empire actions, territory changes)
2. Minor plot beats the reporter could plausibly know about (a bounty posted, a known raider sighted)
3. Faction relations and wars between factions
4. Notable world events (a settlement destroyed, a caravan lost, weather and disasters)
5. Rumours: foreshadowing for the next plot beat, chosen by the director

The reporter never knows what's inside the colony unless it became public (a famous victory, a released prisoner talking).

### Pipeline

1. Two days before a bulletin is due, the World Summarizer builds a compact digest of events since the last one.
2. The LLM client sends it with the persona and the last two bulletins, on a background thread.
3. The result is checked: right length, no names that aren't in the digest. If it fails, retry once, then fall back to a template.
4. The finished text is saved to the game and delivered on the due tick.

### Fallbacks

Template bulletins per event type ("\[Faction\] forces seized \[Settlement\] this week"), with a few variants each. With the LLM off, the agent still works.

### Exit criteria

- Bulletins arrive on time across a 60-day test game with no stutter.
- Reload never changes a delivered bulletin.
- Turning the model off mid-game falls back to templates with no errors.

**Status: done (2026-11-21).** All three passed in a 60-day test game on the reference PC. The reporter is recruited from a non-hostile faction, visits every 15–30 days, and can be held, killed or lost: a colony kill stops the news (Silent Press itself comes with Stage 3), any other death brings a replacement. Bulletin prompt v10 writes one short item on a quiet week and never pads with faction sizes. Results and lessons are in `docs/stage2-results.md`.

## Stage 3: Minor plots

Minor plots are short arcs, about one season each, grown from the colony's own history. When a protagonist dies, the plot passes to someone else. The first build ships one arc (Nemesis) end to end; the rest follow once it plays well.

### Seed detectors

Small C# checks that run once per in-game day, reading the Tale system (`TaleManager`), pawn relations, backstories and ideology. Each returns candidate hooks with a strength score from 0 to 1.

| Arc | Detector looks for | Example hook |
| --- | --- | --- |
| Nemesis | A raider who killed or maimed a colonist and escaped | "Vex, who killed Mara's husband, was seen at a pirate outpost" |
| Lost Family | A colonist with living kin in another faction | A letter from a sister in a tribal village |
| Old Debt | A released prisoner, or a recruit from an enemy faction | The freed prisoner's faction comes to collect, or repay |
| Grief | A colonist whose spouse, lover or child died | Visions, a grave pilgrimage, a fixation |
| Rivalry | Repeated social fights between two colonists | A challenge, a sabotage, a forced partnership |
| The Past Catches Up | A backstory with a hook (noble exile, deserter, cult escapee) | Old comrades, a bounty hunter, a summons |

The director picks the strongest hook not already in play, subject to the limits below.

### Arc structure

Every arc has four beats: **Hook → Complication → Choice → Resolution**. Beats are deterministic events (a visitor, a letter, a quest, a raid) defined in XML. The LLM writes the letters and dialogue, and frames the choice. Each beat waits 5–15 days, and only moves on when the colony isn't in crisis.

**Nemesis, the first arc (agreed 2026-11-21).** It runs before the quest templates of Stage 4, so its beats use letters, raids and a choice letter:

| Beat | What happens |
| --- | --- |
| Hook | A letter: the raider who killed or maimed the protagonist's kin was seen (e.g. "Vex, who killed Mara's husband, was seen at a pirate outpost"). The sighting becomes public news. |
| Complication | The nemesis leads a raid on the colony. |
| Choice | A choice letter: post a bounty (costs silver; someone else may kill the nemesis), or wait for them to come back (a second raid with the nemesis guaranteed). |
| Resolution | The nemesis dies, by the colony's hand or the bounty hunters': the protagonist gains a lasting good memory, and it's news. If the nemesis kills the protagonist instead, the grudge passes to the heir. |

Stage 4 can later swap the Choice beat for a Hunt target quest.

### Protagonist slot and inheritance

A plot belongs to a protagonist slot, not a fixed pawn. When the protagonist dies, leaves or is captured, the slot passes to the first match:

1. Blood kin in the colony
2. Spouse or lover
3. Bonded animal (it gets a narrated role; a human co-lead is picked next)
4. The colonist with the highest opinion of the protagonist
5. The colony itself, as a collective grudge

Inheriting changes the arc. Each template defines its mutations: a rescue becomes revenge, a rivalry becomes guilt for the survivor, a debt passes to the heir. The LLM writes the handoff scene, which is the arc's emotional peak.

### Payoffs

Every resolution changes the game: a mood memory, a new trait (e.g. a nemesis killed grants a lasting positive memory; a failed rescue adds a grief thought), an inspiration, a goodwill shift, a named item with a generated history, or a pawn leaving or joining.

### Limits

- At most 3 minor plots active, 1 new one per 15 days.
- No beat fires during a raid, a major-plot beat, or when colony mood is in crisis.
- Players can turn off individual arc types in settings.
- Silent Press doesn't count toward the 3-plot cap, since the player causes it.

### Exit criteria

- Nemesis runs end to end in a test game, including one inheritance.
- Every beat and handoff can be forced with Dev Mode actions.
- With the LLM off, arcs still run on template text.

**Status: done (2026-11-21).** All three passed in the proof run on the reference PC. Nemesis runs end to end, including the case where the nemesis kills the protagonist and the heir takes up revenge. Silent Press runs from the silence to reparations or a refused blackout. Model-written plot letters use the plot-letter prompt (v2). Results and lessons are in `docs/stage3-results.md`.

## Stage 4: Plot director and quest templates

The plot director is the scheduler that decides when any plot moves, and what it moves to. It turns Stage 3's single arc into a system that runs many plots side by side and talks to the player through quests.

### Director loop (once per in-game day)

1. Run seed detectors; start a new minor plot if the limits allow.
2. For each active plot whose beat timer is up, check the pacing gate: no raid, no crisis, no other beat in the last 2 days.
3. Build the plot's allowed transitions from its state machine.
4. Ask the LLM to pick one, with the world summary and a short reason (grammar-constrained JSON). If the call fails or returns an invalid choice, pick by weighted random.
5. Fire the beat: its event, quest or letter. Queue the narration for generation.
6. Save the decision and its reason (the reasons double as a debug log).

### Quest templates

The LLM never writes quest logic. Quests are built from a library of parameterized templates on top of RimWorld's own `QuestScriptDef` / `QuestNode` system. The LLM fills in the parameters from an allowed list and writes the briefing.

| Template | Parameters | Used by |
| --- | --- | --- |
| Hunt target | Target pawn, location site, reward | Nemesis, Empire |
| Rescue | Captive, captor faction, site, deadline | Lost Family, Empire |
| Escort | Pawn(s), destination, threat level | Lost Family, Old Debt |
| Defend ally | Allied settlement, attacking faction, waves | Empire |
| Sabotage | Enemy settlement, objective building | Empire |
| Deliver | Item(s), recipient, deadline | Old Debt, Grief |
| Choice letter | 2–3 options, each mapped to a transition | All arcs |

Rewards and threat points scale with the storyteller difficulty and the plot's act, never with the LLM's suggestion.

**Scope (agreed 2026-11-21):** Stage 4 builds Hunt target, Rescue, Deliver and Choice letter. Escort, Defend ally and Sabotage are mainly for the Awakened Empire, so they move to Stage 5. The third arc type for the 200-day test is **Lost Family**, which uses the Rescue template.

### Outcomes

A quest's success, failure or expiry is fed back to its plot as a transition trigger. Ignoring a quest is a valid choice, and the plot carries on without the player.

### Exit criteria

- Three arc types running together in a 200-day test game without overlapping beats.
- Every quest template can be spawned from Dev Mode with test parameters.
- The director's decision log reads like a sensible story outline.

## Stage 5: Awakened Empire (major plot)

The Awakened Empire is the game-long major plot: the Royalty Empire starts dormant, wakes, and starts taking territory, and the player decides whether to stop it, join it or survive it. Vanilla RimWorld has no dormant/awakened state, so the mod adds one. It is built last because it uses every earlier system.

### Requirements

Requires the Royalty DLC. Without Royalty, the mod runs minor plots and the news agent only.

### Empire states

| State | What the Empire does | Moves on when |
| --- | --- | --- |
| Dormant | Few settlements, no expansion, normal Royalty quests | Awakening trigger (below), or day 300 at the latest |
| Stirring | Rumours in bulletins, envoys, demands on nearby factions | 20–40 days pass |
| Awakened | Expands: takes over or founds a settlement every 15–30 days, wars with neighbours | Act 2 milestones are met |
| Ascendant or Broken | Either controls most of the map and makes final demands, or is collapsing after losses | Player completes or fails an ending |

**Awakening triggers** (the director picks one, weighted by what happened): the player's wealth passes a threshold, the player destroys an Imperial site, a colonist with an Imperial backstory is discovered, or a Royalty title is refused.

### Territory expansion

Pure C#, no LLM. On a timer, the Empire picks a target settlement within range, weighted by distance and the target faction's strength. The result is a conquest (the settlement changes faction), a razing, or a repelled attack. Every result goes into the news digest. The player can help or hinder through Defend Ally and Sabotage quests.

### Acts

1. **Act 1, Awakening:** the Empire wakes; the player gets envoys, a choice of stance, and the first quests.
2. **Act 2, Expansion:** territory falls; the player's stance shapes quests (allied: escort and conquest jobs; opposed: defence and sabotage).
3. **Act 3, Reckoning:** the ending path opens.

### Endings

Each ending ends the game with its own victory screen and an LLM-written epilogue that names the colonists who mattered.

- **Resistance victory:** destroy the Imperial capital site with an allied coalition.
- **Imperial victory:** serve the Empire to a high title and accept a governorship.
- **Independence:** force a treaty that recognises the colony as a free state.

The vanilla endings (ship, Royal ascent, archonexus, Anomaly) still count. If the player starts one during the plot, the Empire reacts: an Empire in the Awakened state tries to stop a ship launch, for example.

### Links to minor plots

The director can promote a minor plot into the major one: a Past-Catches-Up colonist turns out to be the Empire's target, a Nemesis raider comes back as an Imperial officer, or a Lost Family relative lives in a conquered settlement. This is what makes the big plot feel personal.

### Exit criteria

- A full playthrough reaches each of the three endings (using Dev Mode to speed up).
- Territory changes show correctly on the world map and in bulletins.
- At least one minor plot is promoted into the major plot in testing.

## Settings, tiers and working rules

### Mod settings

- **LLM endpoint:** URL, model name, optional API key, and a Test Connection button.
- **Tier preset:** Off (templates only), Minimum, Recommended, High. Sets token limits, prompt detail and how much prose each feature asks for.
- **Separate prose model (optional):** a second endpoint for narration while the main one handles director decisions.
- **News cadence:** 3 days by default, adjustable from 1 to 7.
- **Story intensity:** how often minor plots start, and the Empire's expansion speed.
- **Arc toggles:** turn individual minor arc types on or off.
- **Major plot:** on or off (off automatically without Royalty).

### Model tiers

| Tier | Size | Example models | What it does |
| --- | --- | --- | --- |
| Off | none | none | Template text everywhere; the story still runs |
| Minimum | \~2B | Gemma 4 E2B | Director decisions, short bulletins |
| Recommended | \~4B | Gemma 4 E4B | Full narration; the default |
| High | 12B+ or cloud | Gemma 4 12B, or any larger local model or cloud API | Richer prose and epilogues |

The presets were chosen by Stage 0 (`docs/stage0-results.md`). Each preset fills in the Ollama model tag, thinking off and temperature 0.4; players can still point any tier at another OpenAI-compatible endpoint.

### Dev Mode debug actions

Built from Stage 1 onward, never as an afterthought: force a bulletin, force-seed any arc on chosen pawns, advance a plot one beat, kill the protagonist (tests inheritance), trigger the awakening, run one expansion tick, spawn any quest template, dump the world summary to a JSON snapshot, and show the director's decision log.

### Repository layout

The story core and LLM layer are separate libraries with no RimWorld dependency, so they can be unit-tested and shared with the eval harness.

| Folder | Holds | Needs RimWorld? |
| --- | --- | --- |
| `src/RimChronicles.Llm` | LLM client, prompt loading, validator, template fallbacks | No |
| `src/RimChronicles.Core` | World summarizer, seed detector logic, plot director, plot state machines, all on plain data | No |
| `src/RimChronicles.Game` | Engine adapter, Harmony patches, `GameComponent`, letters and UI | Yes |
| `harness/` | Stage 0 eval app and snapshots | No |
| `tests/` | Unit tests for Llm and Core | No |
| `prompts/` | Versioned prompt templates | No |
| `About/`, `Defs/`, `Languages/` | RimWorld mod metadata and XML content | Loaded by the game |

The decompiled game source stays outside the repo, in `reference/`.

### Rules for AI-written code

These go in the repo's `CLAUDE.md` (or equivalent) so every coding session follows them.

1. Check every RimWorld API against the decompiled 1.7 source in `reference/`. Never trust memory of 1.4–1.6.
2. Target `net10.0` and use Harmony 3 from the Workshop mod. Never bundle an old `0Harmony.dll`.
3. Never call Unity APIs in new code; the 1.7 shims exist only for old mods.
4. Never touch game state off the main thread. LLM results go through the main-thread queue.
5. Every saved class implements `ExposeData`, and loading tolerates missing or null data (the mod may be added to or removed from a save).
6. Game-engine calls live only in the adapter layer. Story logic never calls RimWorld directly.
7. Every LLM feature has a template fallback and a Dev Mode action.
8. Prompt templates live in versioned files, not in C# strings, and any change re-runs the Stage 0 harness.
9. The Llm and Core libraries have unit tests that run without RimWorld, and every change keeps them passing.

### Roadmap

1. **Stage 0:** model evaluation harness (no game needed). Done 2026-11-21.
2. **Stage 1:** 1.7 technical spike and snapshot exporter. Done 2026-11-21.
3. **Stage 2:** news agent (first playable release). Done 2026-11-21.
4. **Stage 3:** minor plots, starting with Nemesis. Done 2026-11-21.
5. **Stage 4:** plot director and quest templates
6. **Stage 5:** Awakened Empire and endings

Each stage ends with its exit criteria met and a playable build. Stage 2 is a good point for a first Steam Workshop release.

### Open questions

- None open.
