# Rim Chronicles — Companion: Chronicle & Epilogue Video (Future Track)

**For:** Claude Code · **Owner:** John · **Date:** 2026-11-25 (rev. 7) · **Status:** Future track, on hold until War in Heaven is finished. The RCN intro demo is done (see §11). The mod-side chronicle writer (§3) is a text log, so it is small and can be built first.
**Parent spec:** Rim Chronicles — Design Spec. Where they disagree, the parent spec wins.

**Rev. 7 in one line:** there is no companion app. **Claude is the companion.** The mod logs the story; the player gives the chronicle to Claude with the Rim Chronicles video skill, and Claude makes the video (R6).

---

## 1. Goal

1. **The mod writes a chronicle** of the colony as it is played: events, plots and handoffs, the director's decisions, news bulletins and the data needed to draw pawns. It's text only.
2. **The player gives the chronicle to Claude** with the **Rim Chronicles video skill** (§4). Claude turns it into a short narrated video in the RCN style, and can also answer questions about the colony's history.

The main audience is **content creators**. They get a finished video, plus segments and overlays to cut into their own footage, and they can steer the result in plain words ("cut the raid", "make a 30 s vertical version", "linger on Mara's death").

### Non-goals

- A desktop app, installer or app shell. (Replaced by the skill, R6.)
- Training a model to plan shots. Claude plans them. (Rev. 6 §7 is dropped.)
- The mod calling Claude. The mod never talks to Claude or any cloud service; using the skill is a separate, optional step the player takes. Playing the mod stays fully local.
- Shipping Node/Chromium inside the mod.
- Screenshots or any other in-game image capture. Videos are built from the narrative alone: chronicle text and data, drawn in the RCN style. Creators bring their own gameplay footage (R1).
- Generated imagery as the default.
- Editing the game or the chronicle from Claude. The chronicle is read-only input.

---

## 2. Pipeline

```
Mod (during play)                 Player                      Claude + Rim Chronicles video skill
─────────────────                 ──────                      ───────────────────────────────────
chronicle writer (append-only,
  text + data only)
game end → seal
"Export for video" ──────────►  chronicle export  ──────►  1. Read + check the export
                                + request                  2. Write a shot manifest (§5)
                                ("2-minute video,          3. Validate it with the skill's validator; fix and repeat
                                  keep it sombre")         4. Optional: TTS voices on the player's PC (§6)
                                                           5. Render: Remotion templates ← manifest → MP4
                                                           6. Iterate on the player's notes
```

The supported path is **Claude Code on the player's PC**: it can install the template pack's Node dependencies, run Remotion and (optionally) local TTS. Whether the same works by uploading to claude.ai depends on its sandbox running Node and headless Chromium; that is open question 3.

---

## 3. Chronicle (mod → Claude)

### 3.1 Layout

One folder per colony, written by the mod during play:

```
Saves/RimChronicles/chronicle/<colony-id>/
  chronicle.jsonl        # append-only event log, one entry per line
  sealed.json            # written once at game end: ending type, epilogue text, final roster, memorial list
  export/                # written by "Export for video" (§3.6)
```

The mod writes no images. Everything on screen is drawn by the templates from this data.

### 3.2 Log entries

Every entry carries `tick`, `day`, `type` and entity ids. Types:

- `event`: raids, deaths, births, marriages, notable tales
- `plot`: beat transitions and protagonist handoffs
- `director`: the director's decision (allowed options, choice, reason, model and prompt version)
- `empire`: Awakened Empire state changes and territory
- `bulletin`: news-agent bulletin text (every 3 in-game days). Fields must cover what the `news_bulletin` template reads (§5.1): headline, kicker, ticker items, dateline, location, reporter id, narration lines.
- `appearance`: a pawn's look as data, enough for `Pawn.jsx` to draw them: skin, hair colour and style, apparel colours, notable gear. See §3.3.
- `faction`: name and colours, so templates can draw a faction's badge without the game's icon art

Every entity gets an id that maps to the data that draws it. Entity ids are also the allowed-reference list for the invented-facts check (§7).

The first line of every chronicle records `chronicle_version`. Logs written in play-tests will be read by skill versions written much later, so the skill must read every older version.

### 3.3 Appearance snapshots

Write an `appearance` entry when a pawn first becomes a protagonist, when they die, and for every colonist at founding. Tributes then show pawns as they were at the time, drawn in the RCN pawn style.

### 3.4 Performance and robustness

- Logging must never stall the game. Build each entry on the main thread (it reads game state), then append to the file from a background writer.
- Director entries record the prompt version, not the full prompt, so the log stays small (a few KB per in-game day).
- A chronicle can start part-way through a colony (mod added to an existing save) or be missing (save copied to another PC). The skill treats a partial chronicle as normal and says so in the video only if the player asks.

### 3.5 Reloads, save-scumming and sealing

Every entry is keyed by game tick. When a save is loaded, entries with `tick` greater than the loaded tick move to an `abandoned/` branch and are excluded from videos. A creator can still ask for them ("timelines that didn't happen").

At game end, the mod writes `sealed.json` and marks the chronicle read-only. A final epilogue video needs a sealed chronicle; a mid-game chronicle can still make bulletins and "the story so far" videos.

### 3.6 Export for video

A button in the mod (mod settings or the Rim Chronicles tab) writes `export/`:

- `chronicle-export.json`: one file, condensed for Claude. The main timeline only (no abandoned entries), ids resolved to names, director entries trimmed to choice and reason, plus a short header (colony name, years, ending, roster, `chronicle_version`).
- A plain-text `README.txt` telling the player what to do next: get the skill, open Claude Code in this folder (or upload the file), ask for a video.

The export is what the player chooses to share. It holds colony and pawn names and story text, nothing about the player.

Target size: small enough for one Claude context with room to work, even for a 200-day colony. If a long colony goes over, the export keeps every priority event (plot beats, handoffs, deaths, Empire changes, endings) and summarises the rest by quadrum.

---

## 4. The Rim Chronicles video skill

A Claude skill, published from this repo, that turns a chronicle export into video. It replaces the companion app.

```
skills/rim-chronicles-video/
  SKILL.md               # when to use it; the workflow; the rules in §4.2
  schema/
    manifest.schema.json # templates, slots, duration ranges (§5)
    chronicle.md         # what each entry type means
  templates/             # the Remotion project: RCN templates, Pawn.jsx, fonts, placeholder music
  scripts/
    validate             # checks a manifest against the schema and the export (§7)
    render               # renders a manifest to MP4 / segments / overlays
```

### 4.1 Workflow

1. Read the export. Summarise the colony's story back in a few lines and confirm the length, mood and format with the player.
2. Write a shot manifest (§5).
3. Run `validate`; fix every failure before rendering.
4. Render a draft. Show the player frames or the MP4, then iterate on their notes.
5. Offer the creator outputs (§6.1).

### 4.2 Rules the skill enforces

- **Facts come only from the chronicle.** Every name, event, date and outcome in narration and captions must trace to an entry. Colour and tone are fine; new events are not.
- **The look stays RCN.** Use the templates. Edit or add a template only when the player asks for something the set can't do, and keep the palette, type and pawn style (§5.1).
- **Voice rules:** bulletins are always the `news` voice; the news agent gets at most one other line, as a cold open.
- **Length:** 60–180 s for a full video unless the player asks otherwise.

### 4.3 History questions

Because Claude has the whole chronicle, the skill also answers questions about it: "why did the director send the Empire on day 40?", "who carried the Choir plot after Mara died?" This replaces most of the rev. 6 history viewer. Whether the mod also gets an in-game director-log window is open question 4.

### 4.4 Distribution

- The skill lives in this repo under `skills/` and is attached to each GitHub release as a zip.
- The Workshop page and the export's `README.txt` link to it.
- It needs a Claude plan. That's acceptable for an optional creator feature; the mod itself never needs one.

---

## 5. Shot manifest

The contract between Claude and the templates. Claude writes it; the templates render it. Keeping this step, rather than having Claude write video code each time, is what keeps every colony's video consistent.

```json
{
  "manifest_version": 1,
  "title": "The Last Winter of Hollow Reach",
  "format": "16:9",
  "shots": [
    { "template": "title_card", "duration": 5, "slots": { "title": "...", "subtitle": "5501–5516" } },
    { "template": "event_timeline", "duration": 8, "slots": { "events": ["event-0120", "event-0131"], "voice": "narrator", "narration": "..." } },
    { "template": "pawn_tribute", "duration": 7, "slots": { "pawn": "pawn-0042", "caption": "...", "voice": "narrator", "narration": "..." } },
    { "template": "news_bulletin", "duration": 6, "slots": { "bulletin": "bulletin-17", "voice": "news" } }
  ]
}
```

Claude picks templates, orders them, fills slots by id and writes narration and captions. The templates own layout, timing limits and asset lookup.

`voice` is an enum: `narrator` | `news`. `news_bulletin` is always `news`. Other templates default to `narrator`.

`format` is `16:9` or `9:16`. Every template supports both from the start, because vertical clips are where creator content spreads.

### Starting template set (~12)

`title_card`, `pawn_tribute` (drawn pawn from `appearance`), `event_timeline`, `plot_handoff` (protagonist A → B), `empire_map` (stylised territory map drawn from `empire` data), `news_bulletin` (news-agent lower third + voice), `battle_report` (attackers, defenders, losses, drawn pawns and faction badges), `colony_card` (founding, population and wealth over time as a drawn chart), `quote_card`, `memorial_roll`, `ending_card`, `credits`.

All templates draw from chronicle data; none needs an image from the game. Each declares its slots and duration range in `manifest.schema.json`.

### 5.1 `news_bulletin` — built (demo, 2026-11-25)

The first template exists as the RCN intro demo (§11). Its look is the reference for all RCN content:

- **Branding:** "RCN — Rim Chronicle News", tagline "Every three days. Every corner of the rim." Anton display type, Fira Sans body, Anonymous Pro for datelines. Palette: near-black field, RCN red `#c8312b`, gold `#e3b04b`, off-white `#f2efe8`.
- **Structure:** logo slam (2–6 s) → corner logo + LIVE badge + dateline → "RIM WIRE" ticker → reporter name plate → "TONIGHT ON RCN" headline + kicker.
- **Reporter on set:** the news agent appears as a **pawn drawn in a RimWorld-like style** (flat fills, thick outlines, no arms, mic floating beside him like a held weapon). Breathes, blinks, and talks while his lines play. Drawn as SVG in `Pawn.jsx` with colour props, so it can be re-coloured to match the in-game reporter.
- **Data in, video out:** all text comes from one JSON object (`bulletin.json` in the demo). The chronicle's `bulletin` entries (§3.2) must map onto it.
- **Full intro vs. short bulletin:** the 16 s demo is the full intro. Inside longer videos, `news_bulletin` uses a short version (corner logo + reporter + headline, ~6 s) and plays the full intro only once, as an opener or the first bulletin.
- **Captions:** each narration line also shows as an on-screen caption, speaker-labelled. Captions stay on as an accessibility option once voices exist.

---

## 6. Rendering

- **Renderer: Remotion** (React templates, props = manifest). **Confirmed working** by the RCN demo (Remotion 4.0.547, 1080p30 H.264). Licence: free for individuals and teams of up to 3 people; players rendering their own videos fall under that, but recheck before publishing the skill.
- **Fonts ship with the templates** (loaded in the project, not assumed installed), so the RCN look is the same on every PC.
- **Audio:**
  - **Default: captions only**, with the placeholder music bed. Claude can't produce speech itself.
  - **Optional voices:** with Claude Code, the skill can drive a local TTS (Piper or Kokoro) on the player's PC for two voices: **narrator** (measured, documentary) and **news agent** (brisker, broadcast style). They must sound clearly different.
  - News-agent lines get a light broadcast filter (band-pass + slight compression) so they read as "in-world" whichever voice model is used.
  - Music bed with ducking under both voices. The demo uses a placeholder sting + drone bed synthesized with ffmpeg (`make-sting.sh`); replace with real music before release.
- **Fallback renderer (optional):** ffmpeg-only, reading the same manifest. Simpler visuals (text cards, fades, burned-in captions), no Node/Chromium.

### 6.1 Creator outputs

- Full video, 16:9 or 9:16.
- **Segments:** each shot (or a chosen run of shots) as its own clip.
- **Overlays:** bulletins, lower thirds, name plates and captions rendered with a transparent background (ProRes 4444 or WebM with alpha), to lay over the creator's own gameplay footage.
- An optional end card: "Made with Rim Chronicles" and the Workshop link.
- Sharing is export-and-upload: the skill suggests a title, description and tags; the creator uploads. No direct posting to platforms.

---

## 7. Checks

No model is trained, so the rev. 6 eval bars become checks in `validate` (run on every manifest) and a small test set for the skill.

`validate` fails the manifest if:

- it doesn't match `manifest.schema.json`;
- any referenced id is missing from the export or is abandoned;
- a voice rule is broken (bulletin not on `news`; more than one other `news` line);
- total duration is outside the requested range.

**Invented facts:** the skill re-reads its narration against the export before rendering and lists every claim it can't trace. `validate` also flags proper nouns in narration that match no entity.

**Skill test set:** 3–5 committed chronicle exports (synthetic from `snapgen`, plus real sealed ones from play-tests). Before a skill release, John asks for a video from each and reviews the result.

---

## 8. Hardware

- **During play:** the chronicle is a text log, so the cost is tiny. Still check it in the Stage 4 200-day test: under 1 ms average per tick, and no visible frame hitches.
- **Video:** runs on the player's PC through Claude Code. Remotion rendering is mostly CPU; the demo rendered 16 s in about a minute on 2 cores. Local TTS, if used, is light. No local LLM is needed for video.

---

## 9. Build order

1. **Schemas:** chronicle entry types, `sealed.json`, the export format and `manifest.schema.json`. Start from the demo's `bulletin.json` for the `bulletin` entry.
2. **Mod-side chronicle writer:** events, plots, director decisions, bulletins, appearance snapshots, reload handling, sealing, "Export for video". Measure per-tick cost.
3. **Skill v0:**
   - ✅ **3a. `news_bulletin` template + RCN intro** — done 2026-11-25 (§11).
   - ⬜ **3b. Demo fixes** — fonts loaded in the project, caption label from `reporter.name`, `make-sting.sh` added (§11).
   - ⬜ **3c. `SKILL.md`, `validate`, `render`** around the bulletin template; make one bulletin from a real chronicle export in Claude Code.
4. **`title_card` + `pawn_tribute`** → one 60 s video from a real chronicle. Decide whether the format is worth finishing before going further.
5. Rest of the templates; 9:16 on all of them; segments and transparent overlays; the short (~6 s) `news_bulletin` variant; music.
6. Optional voices: Piper/Kokoro steps in the skill, broadcast filter.
7. Test the claude.ai upload path (open question 3) and document whichever paths work.
8. Skill test set (§7); publish the skill (repo `skills/` + release zip; Workshop link).
9. Optional: in-mod director-log window; ffmpeg fallback renderer; AI video for 1–3 hero shots.

---

## 10. Questions

### Resolved

| # | Question | Decision | Where it's applied |
| --- | --- | --- | --- |
| R1 | Where do key-event images come from: captured live during play, or reconstructed at the end? | **Neither: no screenshots.** Videos are built from the narrative and drawn from chronicle data. (2026-11-25; reverses "captured live", 2026-11-24.) Why: the main audience is content creators, who have their own footage; capture was the biggest technical risk (engine readback outside the Unity-API rule, GPU support, frame hitches, storage and pruning). | §1 non-goals; §3 chronicle; §5 templates |
| R2 | Is the companion app video only, or also the place to view the decision log and plot history? | **Superseded by R6** (2026-11-25): there is no app. Claude answers history questions from the chronicle (§4.3); an in-mod window is open question 4. | §4.3 |
| R3 | Narration voice: one narrator plus the news agent, or a voice per protagonist? | **One narrator plus the news agent.** (2026-11-24) | §5 `voice` enum; §6 audio; §7 checks |
| R4 | Default storage cap: 500 MB per colony, or scale with colony age? | **Withdrawn** with R1 (2026-11-25): a text-only chronicle needs no cap. | — |
| R5 | How does the news agent appear on screen? | **As a pawn in a RimWorld-like art style**, on set beside the headline. (2026-11-25) | §5.1; `demos/rcn-intro/src/Pawn.jsx` |
| R6 | Build a companion desktop app, or let Claude be the companion? | **Claude is the companion.** The mod logs and exports; a Claude skill plus the Remotion template pack makes the video. (2026-11-25) Why: no app, installer or code signing to maintain; no shot model to train; creators can steer the video in plain words; Claude can check narration against the whole chronicle. Cost: needs a Claude plan, which is fine for an optional creator feature. | Whole spec |

### Open

1. Should events from Dev Mode or god-mode actions be excluded or flagged?
2. What is the news agent's real name and look in the mod? The demo's "Desmond Vale" (navy suit, red tie, brown side-parted hair) is a placeholder. The pawn should match the in-game reporter, and the name in `bulletin.json` should come from the chronicle.
3. Can claude.ai render the video from an upload (Node + headless Chromium in its sandbox), or is claude.ai limited to planning the manifest while rendering needs Claude Code?
4. Does the mod also need an in-game director-log window (useful for play-tests), or is asking Claude about the chronicle enough?

---

## 11. Demo log

### 2026-11-25 — RCN intro (`news_bulletin` template)

**Result:** done. John's verdict: approved.

**What it is:** a 16 s, 1080p30 broadcast intro for Rim Chronicle News: logo slam, corner logo + LIVE, RIM WIRE ticker, reporter name plate, "TONIGHT ON RCN" headline, and the reporter pawn on set. Three news-agent lines as captions; placeholder sting + bed.

**Source:** `demos/rcn-intro/` in this project (to become the skill's `templates/`).

| File | Purpose |
| --- | --- |
| `src/RcnIntro.jsx` | The template: backdrop, logo slam, chrome, ticker, lower third, headline, reporter, captions |
| `src/Pawn.jsx` | Reporter pawn (SVG, RimWorld-like style), colour props, idle/blink/talk animation |
| `src/bulletin.json` | All content. The chronicle's `bulletin` entry will replace this |
| `src/Root.jsx`, `src/index.js`, `package.json` | Remotion entry and deps (Remotion 4.0.547, React 19) |
| `make-sting.sh` | Regenerates the placeholder audio with ffmpeg |
| `README.md` | Run commands, timeline, narration script, how to add the voice |

**Learned:**

- Remotion works for this: data-driven, fast to iterate, renders 16 s in about a minute on a 2-core machine.
- A data-file-driven template is the right shape. Swapping content needs no code change, which is exactly what a manifest written by Claude needs.
- The pawn style works on screen and reads clearly as RimWorld.
- Claude Code rendered the demo end to end in the codespace (2026-11-25): install, render, stills for review. That's the R6 workflow in miniature.

**Known gaps (found on the 2026-11-25 render):**

- Anton, Fira Sans and Anonymous Pro aren't loaded by the project, so they fall back to system fonts on PCs that lack them.
- The caption label `VALE:` is fixed in `RcnIntro.jsx` instead of coming from `reporter.name`.
- `make-sting.sh` isn't in the uploaded zip yet (John to add).

**Not done yet:** voice (TTS models couldn't be downloaded in the build environment, so lines are captions only); real music; short bulletin variant. Tracked as §9 steps 3b, 5 and 6.
