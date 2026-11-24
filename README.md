# Rim Chronicles

A story mode for **RimWorld 1.7**. Your colony gets plots with acts, stakes and endings, grown from what actually
happened to it, and narrated by a language model running on your own PC. With the model switched off, everything
still plays, with written templates instead.

Your storyteller (Randy, Cassandra, Phoebe) keeps running underneath. Rim Chronicles adds a layer of story on top.

> **Status: early preview, not yet released.** All six build stages are in; the last one (the Awakened Empire) is
> in its final in-game testing. The Imperial crisis below is built and waiting for its in-game checkpoints. Expect
> rough edges, and back up your saves. A Steam Workshop release is planned.

## What it adds

- **The Rim Dispatch.** A news reporter covers your corner of the rim every few days: raids, deaths, weddings,
  faction politics, the plots below. The reporter is a real pawn who visits the colony. If they die, a successor
  takes over, unless your colony was the one that killed them.
- **Minor plots from your colony's history.** They start from what has happened, and play out over weeks:
  - **Nemesis:** a raider who killed or maimed a colonist and got away comes back into the story.
  - **Lost Family:** a colonist's relative in another faction writes letters, is taken captive, and may be rescued.
  - **Silent Press:** killing the reporter has consequences, with clues and then an envoy demanding reparations.
- **A plot director.** Once a day it decides whether a plot moves on or waits, choosing only from the options the
  plot allows, and it holds back when the colony has just been hit. Plots reach you as letters, choices and quests,
  and the **Chronicle** tab keeps each story's letters and the colony's answers in order.
  When a plot's protagonist dies, someone close to them inherits the story.
- **The Awakened Empire** (needs Royalty). The Empire starts dormant, wakes, and starts taking territory on the world
  map. You choose to stand with it, oppose it or keep out of its way. A minor plot can become part of the Empire's
  story, and a game-long arc builds to one of three endings: storm the Imperial capital, accept a governorship, or
  force a treaty of independence. Each ending has an epilogue that names the colonists who mattered.
- **The Imperial crisis** (needs Royalty). Oppose the Empire and it comes for you, in four escalating stages, with
  troops that fight like a skilled player:
  - **Operations:** probing raids that map your doors, breaches led by grenadiers, psychic assaults, drop-pod strikes
    on your leader, sieges that dig in and build up in stages (strike the camp early, or face mortars, an orbital
    uplink, a shield and reinforcements), strike forces sent to retrieve captives, and a final assault led by the
    High Stellarch.
  - **Psykers who really cast:** adepts with fixed psycast kits (Skip, Control, Ward, Mind) choose their casts in
    battle, and obey neural heat and psyfocus like yours do.
  - **Orbital strikes:** called by a Herald who can see the target (or from an uplink), always marked and announced
    first, and called off if you take the Herald down in time.
  - **Imperial sites to raid:** forward outposts, psychic relays and orbital uplinks, each generated from hand-built
    rooms. Destroying a relay weakens Imperial psykers; destroying an uplink stops orbital strikes for a while.
  - **Captives and intel:** interrogate captured Imperials for their next operation, uplink codes, site locations or
    the Citadel's plans; recruit them as defectors; and expect the Empire to come for them.
  - **The Citadel:** storming the capital means a large three-ringed fortress map. Destroy the courtyard's psychic
    relays to bring down the keep's shield, then take the throne room and the High Stellarch.

  Every raid but the final assault retreats after heavy losses, and every site and siege can be reached and broken.

**How it works:** the story's rules live in code, and the model only picks among allowed options and writes the
words, so a small model can't break a plot. (The Imperial crisis picks its operations by weighted chance for now;
the director takes those choices over once its fine-tuned model ships.) Every call runs in the background, so the game never waits on it, and
generated text is saved with your game, so a reload never rewrites history.

## Requirements

- **RimWorld 1.7** (at the time of writing on Steam's unstable beta branch). Earlier versions aren't supported.
- **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** from the Steam Workshop.
- **Royalty** (optional) for the Awakened Empire and the Imperial crisis. Without it, you get the news and the minor
  plots.
- **A local model** (optional): [Ollama](https://ollama.com), or any server with an OpenAI-compatible API.

| Tier | Model (Ollama tag) | VRAM | What you get |
| --- | --- | --- | --- |
| Off | none | none | Written templates everywhere; the story still runs |
| Minimum | `gemma4:e2b` | about 2 GB | Director decisions and short bulletins |
| Recommended | `gemma4:e4b` | about 4 GB | Full narration (the default) |
| High | `gemma4:12b` | about 9 GB | Richer prose |

Leave room for RimWorld itself, which wants a few GB of VRAM too. The presets were picked by measuring models on the
mod's own prompts (`docs/stage0-results.md`), not from leaderboards.

## Install

Until the first release, build the mod from source (see [Building](#building)). Then:

1. Subscribe to Harmony, and put this repository's folder in RimWorld's `Mods` folder.
2. In the mod list, load Rim Chronicles after Harmony.
3. For narration, install Ollama and pull the model for your tier, for example:
   ```powershell
   ollama pull gemma4:e4b
   ```
4. In game: **Options → Mod settings → Rim Chronicles**. Pick a tier, check the server address
   (`http://localhost:11434/` for Ollama), and press **Test connection**.

The mod is meant to tolerate being added to a running save or removed from one, but back up first.

## Settings

- **Model tier** and **model server:** any OpenAI-compatible endpoint, with an optional API key.
- **Separate prose model** (optional): a second model writes bulletins, letters and epilogues, while the main one
  makes the director's decisions.
- **Separate director model** (optional): a model just for the director's decisions, such as a small fine-tuned
  director, at the prompt tier it was trained on.
- **News cadence:** a bulletin every 1–7 days (3 by default).
- **Story intensity:** how often minor plots start, and how fast the Empire expands.
- **Story arcs:** turn Nemesis, Lost Family or Silent Press off. Plots already running still finish.
- **Major plot:** the Awakened Empire on or off (off automatically without Royalty).
- **Imperial crisis:** on or off; its difficulty (0.5–2×, on top of the storyteller's threat scale); the least number
  of days between Imperial operations; how long orbital strikes are telegraphed (6–10 seconds); the Citadel's map size
  (225–350, 275 by default); and, for slower PCs, spawning the Citadel's inner defenders only as you reach them.
- **Reset to defaults**, and **Test connection** to check the model server.

With **Development mode** on, a *Rim Chronicles* debug menu can force a bulletin, start a Nemesis or Lost Family
plot, advance a plot one beat, test each quest type, trigger the Empire's awakening or an ending, and dump the world
to a snapshot. The director's decision log is in there too. For the crisis, it can show the crisis state, fire any
operation at any stage it allows, call an orbital strike, spawn units, captives or the High Stellarch, grant intel,
stamp pieces, generate a site or the Citadel (timed), and draw the siege planner's scores. A Dev Mode setting records
the director's decisions as training cases.

## Privacy

With a local model, nothing leaves your PC. If you point the mod at a remote or cloud endpoint, it sends that server
a summary of your colony: names, events, relationships and plot state. An API key, if you enter one, is saved in plain
text in RimWorld's `Config` folder.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com). The libraries, the evaluation harness and the tests build
and run without RimWorld:

```bash
dotnet build RimChronicles.slnx
dotnet test RimChronicles.slnx
```

To build the mod itself, copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set the paths to
your RimWorld and Harmony assemblies. The build puts `RimChronicles.dll` in `1.7/Assemblies`. The game's own DLLs are
never committed or redistributed.

| Folder | Holds |
| --- | --- |
| `src/RimChronicles.Llm` | Model client, prompt loading, output checks, template fallbacks |
| `src/RimChronicles.Core` | World snapshots, summaries, plot state machines, the director, and the crisis's rules: stages and budget, operations, psyker scoring, strikes, the siege and site planners, captives (no RimWorld dependency) |
| `src/RimChronicles.Game` | The RimWorld side: Harmony patches, game component, letters, quests, UI, and the crisis in game: units, lords, map generation for sites and the Citadel |
| `Defs/` | The mod's XML: crisis units, pieces (hand-built rooms and emplacements), site archetypes, intel, quests |
| `prompts/` | Versioned prompt templates |
| `harness/` | The model evaluation harness (`rimchron-eval`) and its test snapshots |
| `training/` | Fine-tuning a small director model (Python, development only; never shipped) |
| `docs/` | The design spec, runbooks and stage results; the crisis has its own spec, plan, milestone notes and runbooks in `docs/SPEC-crisis-combat-and-locations/` |

Start with the [design spec](docs/Rim%20Chronicles%20—%20Design%20Spec.md), and for the crisis its
[spec](docs/SPEC-crisis-combat-and-locations/SPEC-crisis-combat-and-locations.md). [`CLAUDE.md`](CLAUDE.md) has the coding
rules and the harness commands, and [`docs/ENGINE_NOTES.md`](docs/ENGINE_NOTES.md) records what RimWorld 1.7 changed.
This mod is developed with AI assistance (Claude Code), and every game API it uses is checked against the 1.7
assemblies rather than taken on trust.

## License

Rim Chronicles is released under the [MIT License](LICENSE): you may use, change and share it, including in your own
mods, as long as you keep the copyright notice.

This license covers this repository only:

- **RimWorld** and its DLCs are © Ludeon Studios. Rim Chronicles is an unofficial fan mod, not affiliated with or
  endorsed by Ludeon Studios, and it contains none of the game's code or assets.
- **Harmony** (by Andreas Pardeike, MIT) is a separate mod you install yourself. It isn't bundled.
- **Language models** aren't included. Models you download, such as Google's Gemma, come under their own terms.
- The evaluation harness's Claude provider uses the official **Anthropic SDK** (MIT). It's a development tool, never
  part of the mod.
