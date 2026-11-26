# Rim Chronicles

A story mode for **RimWorld 1.7**. Your colony gets plots with acts, stakes and endings, grown from what actually
happened to it, and narrated by a language model running on your own PC. With the model switched off, everything
still plays, with written templates instead.

Your storyteller (Randy, Cassandra, Phoebe) keeps running underneath. Rim Chronicles adds a layer of story on top.

> **Status: early preview, not yet released.** The news, the minor plots, the Awakened Empire and the Imperial crisis
> have passed their in-game tests. The Hollow Choir and the War in Heaven are built and waiting for theirs. Expect
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
  force a treaty of independence. Each ending has an epilogue that names the colonists who mattered. With Biotech,
  the Empire's story leads on to the Hollow Choir and the War in Heaven below.
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
- **The Hollow Choir** (needs Biotech, with Royalty). The Empire's original enemy: an ancient mechanite power led by
  mechanitors, whose mechs hum in one voice. It stirs, wakes and takes territory like the Empire does, and either
  power can wake first; when one starts attacking, the other wakes too. Launching the ship wakes the Empire, and
  the **Royal Ascent** wakes the Choir.
  - **Sides:** declare for the Empire, for the Choir, or for **independence**. Joining one power means war with the
    other; independence means war with both.
  - **One kind per side:** an Imperial colony can't keep mechanitors, and a Choir colony can't keep psycasters. A
    shuttle comes for each one and brings them back five days later converted, at the matching level; their mechs
    shut down where they stand, for you to dismantle. Refuse, and you're independent.
  - **Rank from service:** jobs done for the Choir raise a colonist's standing, and a high rank opens the First
    Voice's offer, as Imperial titles do for the Empire.
  - **Its own crisis:** oppose the Choir and it comes for you the way the Empire does, with mechanitors commanding
    from the back: cluster drops, burrowing strikes and the Hum, cluster sieges, sites to raid (cluster outposts,
    Resonators, Signal spires), and its capital, **the Cathedral**, whose shield falls when its resonators do. The
    First Voice waits inside.
- **The War in Heaven.** Once both powers are awake and you're at war with one, they may turn on each other in full,
  and by game day 600 they certainly will. They take each other's settlements, every result makes the news, and some
  of their forces go to the front instead of to you. The director hands out war jobs: defend, strike, escort,
  sabotage. It ends with your side's victory:
  - **Imperial victory** or **Choral victory**: the power you serve wins, with your help.
  - **Independence:** destroy one power's capital, and the other offers a treaty recognising your colony as a free
    state. Accept it, or refuse and destroy the second power too, for a harder victory with its own epilogue.

  Each ending has its own victory screen and an epilogue that names the colonists who mattered.

**How it works:** the story's rules live in code, and the model only picks among allowed options and writes the
words, so a small model can't break a plot. A prompted model (any of the tiers below) decides every plot, the crises
included; a fine-tuned director decides only the plots it was trained on, and the rest pick by weighted chance. Every
call runs in the background, so the game never waits on it, and
generated text is saved with your game, so a reload never rewrites history.

## Requirements

- **RimWorld 1.7** (at the time of writing on Steam's unstable beta branch). Earlier versions aren't supported.
- **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** from the Steam Workshop.
- **Royalty** (optional) for the Awakened Empire and the Imperial crisis. Without it, you get the news and the minor
  plots.
- **Biotech** (optional, with Royalty) for the Hollow Choir and the War in Heaven. Without it, the Empire's story runs
  on its own and ends in one of its three endings.
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

1. Subscribe to Harmony, and put the mod in RimWorld's `Mods` folder: the packaged `release\RimChronicles` folder
   (`docs/release.md`), or this repository's folder.
2. In the mod list, load Rim Chronicles after Harmony.
3. For narration, install [Ollama](https://ollama.com/download). On Windows it runs in the background once installed.
4. In game: **Options → Mod settings → Rim Chronicles**. Pick a tier, check the server address
   (`http://localhost:11434/` for Ollama), press **Download model** (once; it fetches the tier's model, a few GB),
   then **Test connection**. You can also pull models yourself, for example `ollama pull gemma4:e4b`.
5. Optional: under **Advanced: separate models**, pick the fine-tuned director `rimchron-director-e4b` and press
   **Download model** again. It makes the director's decisions; the main model still writes.

The mod is meant to tolerate being added to a running save or removed from one, but back up first.

## Settings

- **Model tier** and **model server:** any OpenAI-compatible endpoint, with an optional API key.
- **Separate prose model** (optional): a second model writes bulletins, letters and epilogues, while the main one
  makes the director's decisions.
- **Separate director model** (optional): a model just for the director's decisions, such as a small fine-tuned
  director, at the prompt tier it was trained on. The fine-tuned directors are on the Ollama registry as
  `oldmanjohnnymorgan/rimchron-director-e4b` (it knows every plot, the War in Heaven included) and `-e2b` (the minor
  plots and the Awakened Empire; the crises pick by weight).
- **News cadence:** a bulletin every 1–7 days (3 by default).
- **Story intensity:** how often minor plots start, and how fast the Empire expands.
- **Story arcs:** turn Nemesis, Lost Family or Silent Press off. Plots already running still finish.
- **The Hollow Choir** on or off (off automatically without Biotech), and **the War in Heaven** on or off: the game
  day it starts for certain (600 by default), how steeply its odds rise before then, and how much of the crises'
  forces still come for the colony while it's on.
- **Major plot:** the Awakened Empire on or off (off automatically without Royalty).
- **Imperial crisis:** on or off; its difficulty (0.5–2×, on top of the storyteller's threat scale); the least number
  of days between Imperial operations; how long orbital strikes are telegraphed (6–10 seconds); the Citadel's map size
  (225–350, 275 by default); and, for slower PCs, spawning the Citadel's inner defenders only as you reach them.
- **Reset to defaults**, **Test connection** to check the model server, and **Download model** to have Ollama fetch
  every model the settings name.

With **Development mode** on, a *Rim Chronicles* debug menu can force a bulletin, start a Nemesis or Lost Family
plot, advance a plot one beat, test each quest type, trigger the Empire's awakening or an ending, and dump the world
to a snapshot. The director's decision log is in there too. For the crisis, it can show the crisis state, fire any
operation at any stage it allows, call an orbital strike, spawn units, captives or the High Stellarch, grant intel,
stamp pieces, generate a site or the Citadel (timed), and draw the siege planner's scores. For the Choir and the War
in Heaven, it can stir or wake the Choir, set the colony's side, send the shuttle or convert a colonist, grant Choir
rank, spawn Choir units and raids, fire any Choir operation, generate the Cathedral, start the war, move the front,
destroy a power and reach any ending. A Dev Mode setting records
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
| `src/RimChronicles.Core` | World snapshots, summaries, plot state machines, the director, the powers (the Empire and the Choir) and sides, the War in Heaven's odds and endings, and the crises' rules: stages and budget, operations, psyker and mech-commander scoring, strikes, the siege and site planners, captives (no RimWorld dependency) |
| `src/RimChronicles.Game` | The RimWorld side: Harmony patches, game component, letters, quests, UI, and the crises in game: units, lords, the shuttle, map generation for sites, the Citadel and the Cathedral |
| `Defs/` | The mod's XML: the Choir's faction and units, crisis units, pieces (hand-built rooms and emplacements), site archetypes, intel, quests |
| `prompts/` | Versioned prompt templates |
| `harness/` | The model evaluation harness (`rimchron-eval`) and its test snapshots |
| `training/` | Fine-tuning a small director model (Python, development only; never shipped) |
| `docs/` | The design spec, runbooks and stage results; the crisis and the War in Heaven each have their own spec, plan and runbooks in `docs/SPEC-crisis-combat-and-locations/` and `docs/SPEC-war-in-heaven/` |

Start with the [design spec](docs/Rim%20Chronicles%20—%20Design%20Spec.md), then the
[crisis spec](docs/SPEC-crisis-combat-and-locations/SPEC-crisis-combat-and-locations.md) and the
[War in Heaven spec](docs/SPEC-war-in-heaven/SPEC-war-in-heaven.md). [`CLAUDE.md`](CLAUDE.md) has the coding
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
