// When each event in a shot is revealed (and its letter lands in the stack), in seconds from the shot's start.
// Shared by the templates, the letter stack and make-audio's chimes.
import { shotEvents } from './data.js';

const REVEALS = new Set(['colony_card', 'empire_map', 'event_timeline', 'battle_report', 'letter_card']);

export const eventRevealTimes = (shot, data) => {
  if (!REVEALS.has(shot.template)) return [];
  const events = shotEvents(shot, data);
  const n = events.length;
  const span = Math.max(1, shot.duration - 3);
  // The manifest may time the reveals itself (slots.reveal_at, seconds), e.g. to land a death on its line.
  const fixed = shot.slots.reveal_at;
  return events.map((e, k) => ({ id: e.id, letter: e.letter || 'neutral', at: fixed?.[k] ?? 0.8 + (n > 1 ? (k * span) / n : 0) }));
};
