# Epilogue video demo: Varrowgate, total independence

A 3-minute example of the epilogue video from `docs/SPEC-video-companion-app/` (rev. 7: Claude is the companion).
It plays the part the spec gives Claude: read a chronicle export, write a shot manifest, validate it, then render it with
Remotion templates in a RimWorld-like style. Narration is subtitles only: no voice track and no text to speech.

## The simulated game

`chronicle/chronicle-export.json` is a made-up game in the spec's export format (§3.6); no save wrote it. Varrowgate is
founded by Cassia, Henrik, Mags and Aurel on day 0 (5500). The Shattered Empire wakes on day 92 and the Hollow Choir on
day 118. Varrowgate declares itself free of both on day 310 (Cassia gives back her Imperial title). The War in Heaven
begins on day 600. Cassia holds the walls through two cluster sieges (Wren dies in the second), Aurel leads the storming
of the Cathedral on day 790, Mags refuses the Empire's treaty on day 806, and on day 1041 Henrik brings down the High
Stellarch at the Imperial Citadel and dies; the Citadel falls on day 1043. The ending is **Total independence**.

The people, deeds and events of the ending match `harness/snapshots/snap-27-ending-independence.json`. The
epilogue in `sealed.epilogue` follows the `epilogue/v3` prompt's rules. It was written by Claude Code as a stand-in,
because there is no model server in the codespace. To use the local model's real epilogue, run this on the PC and paste
the text from `results/<runId>/raw/` into `sealed.epilogue.text`:

```powershell
dotnet run --project harness/RimChronicles.Eval -- run --models gemma4-e4b --tasks epilogue --snapshots snap-27-ending-independence
```

The news outlet and reporter come from the chronicle: the game's own **Rim Dispatch** and **Ilse Varrow**, drawn in the
approved RCN look (spec §5.1).

## Run it

```bash
npm install
npm run validate            # the manifest against the chronicle (spec §7)
npm run audio               # synthesize public/audio/music.wav
npm run studio              # live preview
npm run render              # validate + audio + render out/varrowgate.mp4 (about 15 min on 2 cores)
npx remotion still src/index.js Epilogue out/frame.png --frame=900   # one frame
```

## How it fits together

| File | Role |
| --- | --- |
| `chronicle/chronicle-export.json` | The input: header, factions, pawns, appearance entries, events, bulletins, director decisions, colony history, sealed ending and epilogue |
| `manifest.json` | The shot list (spec §5): template, duration and slots per shot. This is the file Claude writes |
| `scripts/validate.mjs` | Spec §7 checks: templates and durations, every id found, the voice rules, every line fits its shot, total length, and no proper noun or number in the narration that the chronicle doesn't have |
| `scripts/make-audio.mjs` | The music from the manifest, in plain Node (no ffmpeg, no samples) |
| `src/timing.js`, `src/reveal.js` | When each line is spoken and each letter lands; shared by the video and the audio, so they stay in sync |
| `src/templates/` | The 11 templates in the spec's starting set (all but `quote_card`, with `letter_card` added) |
| `src/components/` | `Pawn` (drawn from an `appearance` entry), `Mech` (the Choir's mechs, an original design), RimWorld-like windows, letters, date readout and captions, the hex world map |

### Sound

No voices: every narration line is a subtitle, labelled with its speaker. A synthesized score (plucked strings and a pad
in D minor, drums for battles, stings for bulletins, a chime when a letter lands) follows each shot's mood. A murmur
track (wordless synthesized babble per line) was tried and dropped: it sounded bad.

### The look

RimWorld-like, drawn from data with no game art: dark slate windows with thin light edges, coloured letters stacking up
the right edge as events happen, the date in the bottom right, a colonist bar, a choice letter with Accept and Refuse,
and pawns in flat fills with thick outlines, no arms and their gear floating beside them.

## Not done yet

- 9:16, segments and transparent overlays (spec §6.1).
- The `quote_card` template.
- A real chronicle: the mod doesn't write one yet (spec §9 step 2).
