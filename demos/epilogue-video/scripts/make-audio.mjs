// Synthesizes the video's sound from the manifest: no TTS, no samples, no ffmpeg.
//   public/audio/music.wav  score + stings + letter chimes, following each shot's mood
// There is no voice track: narration is subtitles only.
// Deterministic: the same manifest always gives the same audio.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeData } from '../src/data.js';
import { timeline, totalSeconds } from '../src/timing.js';
import { eventRevealTimes } from '../src/reveal.js';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const manifest = JSON.parse(readFileSync(resolve(root, 'manifest.json'), 'utf8'));
const data = makeData(JSON.parse(readFileSync(resolve(root, manifest.chronicle), 'utf8')));
const tl = timeline(manifest, data);
const SR = 44100;
const total = totalSeconds(manifest) + 1;
const N = Math.ceil(total * SR);

// ── helpers ───────────────────────────────────────────────────────────
let seed = 20261128;
const rnd = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296);
const rr = (a, b) => a + (b - a) * rnd();
const midi = (m) => 440 * 2 ** ((m - 69) / 12);

/** RBJ biquad. */
const biquad = (type, f, q, sr = SR) => {
  const w = (2 * Math.PI * Math.min(f, sr * 0.45)) / sr, a = Math.sin(w) / (2 * q), c = Math.cos(w);
  let b0, b1, b2, a0, a1, a2;
  if (type === 'bp') { b0 = a; b1 = 0; b2 = -a; a0 = 1 + a; a1 = -2 * c; a2 = 1 - a; }
  else if (type === 'lp') { b0 = (1 - c) / 2; b1 = 1 - c; b2 = (1 - c) / 2; a0 = 1 + a; a1 = -2 * c; a2 = 1 - a; }
  else { b0 = (1 + c) / 2; b1 = -(1 + c); b2 = (1 + c) / 2; a0 = 1 + a; a1 = -2 * c; a2 = 1 - a; }
  const k = { b0: b0 / a0, b1: b1 / a0, b2: b2 / a0, a1: a1 / a0, a2: a2 / a0 };
  let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  const run = (x) => {
    const y = k.b0 * x + k.b1 * x1 + k.b2 * x2 - k.a1 * y1 - k.a2 * y2;
    x2 = x1; x1 = x; y2 = y1; y1 = y; return y;
  };
  run.set = (f2, q2 = q) => { const n = biquad(type, f2, q2, sr); Object.assign(k, n.k); };
  run.k = k;
  return run;
};
const filterAll = (buf, ...filters) => { for (let i = 0; i < buf.length; i++) { let x = buf[i]; for (const f of filters) x = f(x); buf[i] = x; } };

const writeWav = (path, buf, gain = 1) => {
  const out = Buffer.alloc(44 + buf.length * 2);
  out.write('RIFF', 0); out.writeUInt32LE(36 + buf.length * 2, 4); out.write('WAVE', 8);
  out.write('fmt ', 12); out.writeUInt32LE(16, 16); out.writeUInt16LE(1, 20); out.writeUInt16LE(1, 22);
  out.writeUInt32LE(SR, 24); out.writeUInt32LE(SR * 2, 28); out.writeUInt16LE(2, 32); out.writeUInt16LE(16, 34);
  out.write('data', 36); out.writeUInt32LE(buf.length * 2, 40);
  for (let i = 0; i < buf.length; i++) out.writeInt16LE(Math.round(Math.tanh(buf[i] * gain) * 32000), 44 + i * 2);
  writeFileSync(path, out);
};

// ── music ─────────────────────────────────────────────────────────────
const music = new Float32Array(N);
const BPM = 76, BEAT = 60 / BPM, BAR = BEAT * 4;
const MOOD = {
  news_bulletin: 'news', title_card: 'calm', colony_card: 'calm', empire_map: 'uneasy', event_timeline: 'uneasy',
  battle_report: 'battle', letter_card: 'still', pawn_tribute: 'sombre', ending_card: 'hope', memorial_roll: 'sombre', credits: 'hope',
};
const moodAt = (t) => {
  const s = tl.find((x) => t >= x.start && t < x.end) || tl[tl.length - 1];
  if (s.shot.template === 'empire_map' && s.shot.slots.stage === 'war') return 'tense';
  return MOOD[s.shot.template];
};
// D minor world. Chords as MIDI roots + quality.
const PROG = {
  calm: [[50, 'm'], [46, 'M'], [53, 'M'], [48, 'M']],
  uneasy: [[50, 'm'], [55, 'm'], [50, 'm'], [45, 'M']],
  tense: [[50, 'm'], [50, 'm'], [46, 'M'], [45, 'M']],
  battle: [[50, 'm'], [48, 'M'], [46, 'M'], [45, 'M']],
  still: [[50, 'm'], [50, 'm'], [55, 'm'], [55, 'm']],
  sombre: [[50, 'm'], [46, 'M'], [43, 'm'], [45, 'M']],
  hope: [[53, 'M'], [48, 'M'], [50, 'm'], [46, 'M']],
  news: [[50, 'm'], [50, 'm'], [50, 'm'], [50, 'm']],
};
const chordNotes = ([r, q]) => [r, r + (q === 'M' ? 4 : 3), r + 7];

const pluck = (t, m, amp, decay = 0.996, bright = 0.5) => {
  const f = midi(m), L = Math.max(2, Math.round(SR / f));
  const buf = new Float32Array(L);
  let prev = 0;
  for (let i = 0; i < L; i++) { const n = rnd() * 2 - 1; prev = prev + bright * (n - prev); buf[i] = prev; }
  const i0 = Math.floor(t * SR), n = Math.floor(3.2 * SR);
  let idx = 0;
  for (let i = 0; i < n && i0 + i < N; i++) {
    const nx = (idx + 1) % L;
    const y = buf[idx];
    buf[idx] = decay * 0.5 * (buf[idx] + buf[nx]);
    idx = nx;
    music[i0 + i] += y * amp;
  }
};
const padLayer = new Float32Array(N);
const pad = (t0, len, notes, amp) => {
  const i0 = Math.floor(t0 * SR), n = Math.floor(len * SR);
  const phases = notes.flatMap(() => [rnd(), rnd(), rnd()]);
  for (let i = 0; i < n && i0 + i < N; i++) {
    const tt = i / SR;
    const env = Math.min(1, tt / 1.2) * Math.min(1, (len - tt) / 1.0);
    let x = 0;
    notes.forEach((m, j) => {
      for (let d = 0; d < 3; d++) {
        const p = j * 3 + d; const f = midi(m) * (1 + (d - 1) * 0.004);
        phases[p] += f / SR; if (phases[p] >= 1) phases[p] -= 1;
        x += (2 * phases[p] - 1);
      }
    });
    padLayer[i0 + i] += x * env * amp;
  }
};
const drum = (t, f0, f1, len, amp, noise = 0) => {
  const i0 = Math.floor(t * SR), n = Math.floor(len * SR);
  let ph = 0;
  for (let i = 0; i < n && i0 + i < N; i++) {
    const tt = i / SR; const f = f1 + (f0 - f1) * Math.exp(-tt * 18);
    ph += f / SR;
    const env = Math.exp(-tt * (4.5 / len));
    music[i0 + i] += (Math.sin(2 * Math.PI * ph) + noise * (rnd() * 2 - 1) * Math.exp(-tt * 40)) * env * amp;
  }
};
const chime = (t, m, amp, len = 1.6) => {
  const i0 = Math.floor(t * SR), n = Math.floor(len * SR), f = midi(m);
  for (let i = 0; i < n && i0 + i < N; i++) {
    const tt = i / SR, env = Math.min(1, tt / 0.004) * Math.exp(-tt * 3.2);
    music[i0 + i] += (Math.sin(2 * Math.PI * f * tt) + 0.3 * Math.sin(2 * Math.PI * f * 2.76 * tt)) * env * amp;
  }
};
const whoosh = (t, len, amp) => {
  const i0 = Math.floor(t * SR), n = Math.floor(len * SR), hp = biquad('bp', 800, 0.7);
  for (let i = 0; i < n && i0 + i < N; i++) {
    const tt = i / SR; hp.set(400 + 4000 * (tt / len) ** 2, 0.8);
    music[i0 + i] += hp(rnd() * 2 - 1) * (tt / len) ** 2 * amp;
  }
};

const bars = Math.ceil(total / BAR);
for (let b = 0; b < bars; b++) {
  const t = b * BAR;
  const mood = moodAt(t + 0.01);
  const chord = PROG[mood][b % 4];
  const notes = chordNotes(chord);
  const padAmp = { calm: 0.018, uneasy: 0.02, tense: 0.022, battle: 0.02, still: 0.016, sombre: 0.02, hope: 0.022, news: 0.012 }[mood];
  pad(t, BAR + 1.0, [notes[0] - 12, ...notes], padAmp);
  if (mood === 'calm' || mood === 'hope') {
    const arp = [notes[0], notes[1], notes[2], notes[1] + 12, notes[2], notes[1], notes[0] + 12, notes[2]];
    arp.forEach((m, i) => pluck(t + i * BEAT / 2, m + (mood === 'hope' ? 12 : 0), 0.16, 0.996, 0.45));
    pluck(t, notes[0] - 12, 0.2, 0.997, 0.3);
  } else if (mood === 'uneasy') {
    [0, 1.5, 2, 3].forEach((q, i) => pluck(t + q * BEAT, [notes[0], notes[2], notes[1], notes[0] + 12][i], 0.13, 0.995, 0.4));
    pluck(t, notes[0] - 24, 0.22, 0.998, 0.25);
  } else if (mood === 'tense' || mood === 'battle') {
    for (let i = 0; i < 8; i++) pluck(t + i * BEAT / 2, notes[0] - 12 + (i % 4 === 3 ? 7 : 0), 0.11, 0.993, 0.35);
    const hits = mood === 'battle' ? [0, 1.5, 2, 3, 3.5] : [0, 2.5];
    hits.forEach((q, i) => drum(t + q * BEAT, 120, 44, 0.55, i === 0 ? 0.42 : 0.28, 0.25));
    if (mood === 'battle') [1, 3].forEach((q) => drum(t + q * BEAT, 220, 140, 0.18, 0.12, 0.6));
  } else if (mood === 'still' || mood === 'sombre') {
    pluck(t, notes[0], 0.14, 0.997, 0.35);
    pluck(t + 2 * BEAT, notes[2], 0.1, 0.997, 0.35);
  } else if (mood === 'news') {
    [0, 1, 2, 3].forEach((q) => drum(t + q * BEAT, 90, 50, 0.25, 0.08, 0.1));
  }
}
filterAll(padLayer, biquad('lp', 900, 0.6), biquad('lp', 1400, 0.6));
for (let i = 0; i < N; i++) music[i] += padLayer[i];

// Stings for bulletins, letter chimes when a letter lands.
for (const s of tl) {
  if (s.shot.template === 'news_bulletin') {
    if (s.shot.slots.variant === 'full') {
      whoosh(s.start + 0.2, 1.8, 0.5); drum(s.start + 2.0, 110, 40, 1.4, 0.7, 0.4);
      [76, 79, 83].forEach((m, i) => chime(s.start + 2.3 + i * 0.28, m, 0.13));
    } else {
      whoosh(s.start, 0.6, 0.35); drum(s.start + 0.6, 110, 42, 0.9, 0.45, 0.3);
      [76, 83].forEach((m, i) => chime(s.start + 0.75 + i * 0.22, m, 0.1));
    }
  }
  for (const r of eventRevealTimes(s.shot, data)) {
    const tone = { threat: [69, 64], death: [62, 57], positive: [74, 79], neutral: [74, 74] }[r.letter] || [74, 74];
    chime(s.start + r.at, tone[0] + 12, 0.07, 1.0); chime(s.start + r.at + 0.12, tone[1] + 12, 0.06, 1.0);
  }
}
// Fade the score in and out.
const fadeEnd = totalSeconds(manifest);
for (let i = 0; i < N; i++) {
  const t = i / SR;
  music[i] *= Math.min(1, t / 0.5, Math.max(0, (fadeEnd - t) / 3));
}

mkdirSync(resolve(root, 'public/audio'), { recursive: true });
writeWav(resolve(root, 'public/audio/music.wav'), music, 1.8);
console.log(`audio: ${total.toFixed(0)} s -> public/audio/music.wav`);
