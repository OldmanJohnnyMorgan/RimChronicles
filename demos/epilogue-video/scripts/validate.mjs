// Checks the shot manifest against the chronicle export (spec §7). Exit code 1 on any failure.
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeData } from '../src/data.js';
import { timeShot, totalSeconds } from '../src/timing.js';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const manifestPath = resolve(root, process.argv[2] || 'manifest.json');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
const chronicle = JSON.parse(readFileSync(resolve(root, manifest.chronicle), 'utf8'));
const data = makeData(chronicle);

const TEMPLATES = {
  news_bulletin: { min: 6, max: 18, ids: ['bulletin'] },
  title_card: { min: 4, max: 8, ids: [] },
  colony_card: { min: 6, max: 14, ids: ['event', 'history'] },
  empire_map: { min: 6, max: 14, ids: ['events'] },
  event_timeline: { min: 6, max: 16, ids: ['events'] },
  battle_report: { min: 7, max: 14, ids: ['events'] },
  letter_card: { min: 6, max: 12, ids: ['event', 'pawn'] },
  pawn_tribute: { min: 5, max: 10, ids: ['pawn', 'appearance', 'events'] },
  ending_card: { min: 15, max: 45, ids: [] },
  memorial_roll: { min: 5, max: 10, ids: ['pawns'] },
  credits: { min: 3, max: 8, ids: [] },
};

const errors = [];
const warn = [];
const fail = (i, msg) => errors.push(`shot ${i + 1} (${manifest.shots[i]?.template}): ${msg}`);

if (manifest.manifest_version !== 1) errors.push('manifest_version must be 1');
if (!['16:9', '9:16'].includes(manifest.format)) errors.push('format must be 16:9 or 9:16');

// Text the narration may draw on: every string in the chronicle.
const corpus = JSON.stringify(chronicle);
const allowedWords = new Set(corpus.match(/[A-Za-z][A-Za-z'-]*/g));
const numbers = new Set(corpus.match(/\d+/g));

let otherNewsLines = 0;
manifest.shots.forEach((shot, i) => {
  const t = TEMPLATES[shot.template];
  if (!t) return fail(i, `unknown template "${shot.template}"`);
  if (typeof shot.duration !== 'number' || shot.duration < t.min || shot.duration > t.max)
    fail(i, `duration ${shot.duration}s outside ${t.min}–${t.max}s`);
  const s = shot.slots || {};
  for (const key of t.ids) {
    const v = s[key];
    if (v == null) { fail(i, `missing slot "${key}"`); continue; }
    for (const id of [].concat(v)) {
      const e = data.byId[id];
      if (!e) fail(i, `${key}: no entry "${id}" in the chronicle`);
      else if (e.abandoned) fail(i, `${key}: "${id}" is on an abandoned timeline`);
    }
  }
  if (shot.template === 'ending_card' && !data.resolve(s.epilogue || '')?.text) fail(i, 'epilogue not found');

  // Voice rules: bulletins are always news; at most one other news line in the whole video.
  if (shot.template === 'news_bulletin' && s.voice && s.voice !== 'news') fail(i, 'a bulletin must use the news voice');
  if (shot.template !== 'news_bulletin' && s.voice === 'news') otherNewsLines += (s.narration || []).length;
  if (s.voice && !['narrator', 'news'].includes(s.voice)) fail(i, `voice must be narrator or news, not ${s.voice}`);

  // Lines must fit in the shot.
  const lines = timeShot(shot, data);
  const last = lines[lines.length - 1];
  if (last && last.end > shot.duration - 0.4)
    fail(i, `narration runs to ${last.end.toFixed(1)}s; the shot is ${shot.duration}s (needs ${(last.end + 0.4).toFixed(1)}s)`);

  // Invented facts: words and numbers in the narration and captions that the chronicle never uses.
  const said = [...(s.narration || []), s.caption, s.title, s.subtitle].filter(Boolean);
  for (const text of said) {
    for (const w of text.match(/[A-Za-z][A-Za-z'-]*/g) || []) {
      const proper = /^[A-Z]/.test(w) && !/^(It|The|Then|First|On|By|Both|Twice|A|And|Held|Brought|Made|In|At|Of)$/.test(w);
      if (proper && !allowedWords.has(w) && !allowedWords.has(w.replace(/'s$/, '')))
        fail(i, `"${w}" is not in the chronicle (invented name?)`);
    }
    for (const n of text.match(/\d+/g) || []) if (!numbers.has(n)) warn.push(`shot ${i + 1}: number ${n} isn't in the chronicle; check it is derived (e.g. a span of years)`);
  }
});
if (otherNewsLines > 1) errors.push(`the news voice has ${otherNewsLines} lines outside bulletins (at most 1)`);

const total = totalSeconds(manifest);
const [lo, hi] = manifest.duration_range || [60, 180];
if (total < lo || total > hi) errors.push(`total ${total}s outside ${lo}–${hi}s`);

for (const w of warn) console.log(`warn  ${w}`);
if (errors.length) {
  for (const e of errors) console.log(`FAIL  ${e}`);
  console.log(`\n${errors.length} problem(s); fix the manifest before rendering.`);
  process.exit(1);
}
console.log(`OK  ${manifest.shots.length} shots, ${total}s, every id found, voice rules hold, every line fits.`);
