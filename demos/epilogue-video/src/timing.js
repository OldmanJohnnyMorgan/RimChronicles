// Shared by the Remotion templates and the Node scripts (validate, make-audio), so subtitles,
// letters and music all agree on when each line shows.

export const QUADRUMS = ['Aprimay', 'Jugust', 'Septober', 'Decembary'];
const ORD = (n) => {
  const s = ['th', 'st', 'nd', 'rd'];
  const v = n % 100;
  return n + (s[(v - 20) % 10] || s[v] || s[0]);
};

/** RimWorld calendar: 60-day years of four 15-day quadrums, starting 5500. */
export const dateOf = (day) => ({
  year: 5500 + Math.floor(day / 60),
  quadrum: QUADRUMS[Math.floor((day % 60) / 15)],
  dayOfQuadrum: (day % 15) + 1,
  long: `${ORD((day % 15) + 1)} of ${QUADRUMS[Math.floor((day % 60) / 15)]}, ${5500 + Math.floor(day / 60)}`,
});

// Reading pace. Narration is slower and calmer than the news read.
const PACE = { narrator: 0.36, news: 0.30 };
const LINE_PAD = { narrator: 0.7, news: 0.5 };
const GAP = 0.35;

export const words = (s) => s.trim().split(/\s+/).filter(Boolean).length;
export const lineSeconds = (text, voice) => LINE_PAD[voice] + words(text) * PACE[voice];

/** The bulletin template's lines: the full intro starts after the logo slam, the short one almost at once. */
export const NEWS_START = { full: 5.0, short: 1.2 };

/** Lines a shot speaks: [{ voice, text }]. Bulletins speak their own narration; the ending reads the epilogue. */
export const shotLines = (shot, data) => {
  const s = shot.slots;
  if (shot.template === 'news_bulletin') {
    const b = data.byId[s.bulletin];
    const intro = s.variant === 'full' ? [`This is ${data.reporter.outlet}. I'm ${data.reporter.name}.`] : [];
    return [...intro, ...b.narration].map((text) => ({ voice: 'news', text }));
  }
  if (shot.template === 'ending_card' && s.read_aloud) {
    return data.epilogueParagraphs().map((text) => ({ voice: 'narrator', text, paragraph: true }));
  }
  return (s.narration || []).map((text) => ({ voice: s.voice || 'narrator', text }));
};

/** Timed lines for a shot, in seconds from the shot's start. */
export const timeShot = (shot, data) => {
  const lines = shotLines(shot, data);
  let t = shot.template === 'news_bulletin' ? NEWS_START[shot.slots.variant || 'short']
    : shot.template === 'ending_card' ? 2.6 : 0.7;
  return lines.map((l) => {
    const dur = lineSeconds(l.text, l.voice) * (l.paragraph ? 0.92 : 1);
    const out = { ...l, start: t, end: t + dur };
    t += dur + (l.paragraph ? 0.9 : GAP);
    return out;
  });
};

/** Whole-video timeline: each shot's start and timed lines, in seconds. */
export const timeline = (manifest, data) => {
  let t = 0;
  return manifest.shots.map((shot, i) => {
    const lines = timeShot(shot, data).map((l) => ({ ...l, absStart: t + l.start, absEnd: t + l.end }));
    const out = { index: i, shot, start: t, end: t + shot.duration, lines };
    t += shot.duration;
    return out;
  });
};

export const totalSeconds = (manifest) => manifest.shots.reduce((a, s) => a + s.duration, 0);
