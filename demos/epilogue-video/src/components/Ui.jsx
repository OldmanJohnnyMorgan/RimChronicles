import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { C, LETTER, MONO, SANS, clamp, ease } from '../theme.js';
import { dateOf } from '../timing.js';

/** A RimWorld-like window: dark slate fill, a thin light edge over a dark one, square corners. */
export const Window = ({ title, children, style, width, accent }) => (
  <div style={{
    width, background: C.win, border: `2px solid ${C.winEdge}`, outline: `2px solid ${C.winEdgeDark}`,
    boxShadow: '0 18px 60px rgba(0,0,0,0.6), inset 0 1px 0 rgba(255,255,255,0.06)', ...style,
  }}>
    {title && (
      <div style={{
        display: 'flex', alignItems: 'center', gap: 14, padding: '14px 22px', borderBottom: `1px solid ${C.winEdge}`,
        background: C.winHi, fontFamily: SANS, fontWeight: 600, fontSize: 30, color: C.ink,
      }}>
        {accent && <span style={{ width: 8, alignSelf: 'stretch', background: accent }} />}
        <span style={{ flex: 1 }}>{title}</span>
      </div>
    )}
    <div style={{ padding: '20px 24px' }}>{children}</div>
  </div>
);

/** A flat, RimWorld-like button. */
export const Button = ({ children, pressed, hot }) => (
  <div style={{
    fontFamily: SANS, fontWeight: 600, fontSize: 30, color: hot ? '#fff' : C.ink, padding: '12px 0', width: 220, textAlign: 'center',
    background: pressed ? '#5e5544' : hot ? '#7d7058' : '#4e4a42', border: `2px solid ${hot ? C.tan : '#77705f'}`,
    boxShadow: pressed ? 'inset 0 3px 8px rgba(0,0,0,0.6)' : '0 2px 0 rgba(0,0,0,0.5)', transform: pressed ? 'translateY(2px)' : undefined,
  }}>{children}</div>
);

/** Dark field with drifting dust and a soft vignette, behind every non-news shot. */
export const Backdrop = ({ tint = 'rgba(40,52,64,0.35)' }) => {
  const f = useCurrentFrame();
  return (
    <AbsoluteFill style={{ background: `radial-gradient(ellipse at 50% 40%, #1a2028 0%, ${C.bg0} 75%)` }}>
      <AbsoluteFill style={{ background: `radial-gradient(ellipse at 30% 70%, ${tint} 0%, transparent 60%)` }} />
      <AbsoluteFill style={{
        backgroundImage: 'radial-gradient(rgba(255,255,255,0.18) 1px, transparent 1.5px)', backgroundSize: '90px 90px',
        backgroundPosition: `${f * 0.3}px ${f * 0.12}px`, opacity: 0.35,
      }} />
      <AbsoluteFill style={{ boxShadow: 'inset 0 0 280px rgba(0,0,0,0.9)' }} />
    </AbsoluteFill>
  );
};

/** Subtitles: the current line, speaker-labelled (spec §5.1 captions). */
export const Captions = ({ lines, speaker, t, right = 0 }) => {
  const cur = lines.find((l) => t >= l.start && t < l.end + 0.15);
  if (!cur) return null;
  const a = Math.max(0, Math.min(1, (t - cur.start) / 0.2, (cur.end + 0.15 - t) / 0.2));
  const name = cur.voice === 'news' ? speaker.toUpperCase() : 'NARRATOR';
  return (
    <div style={{ position: 'absolute', left: right ? 60 : 0, right, bottom: 78, display: 'flex', justifyContent: 'center', opacity: a }}>
      <div style={{
        maxWidth: 1340, fontFamily: SANS, fontSize: 40, lineHeight: 1.3, color: C.ink, textAlign: 'center',
        background: 'rgba(7,9,13,0.8)', padding: '12px 30px', border: `1px solid ${C.line}`,
      }}>
        <span style={{ color: cur.voice === 'news' ? C.red : C.gold, fontWeight: 700, fontSize: 28, letterSpacing: 3, marginRight: 16 }}>{name}</span>
        {cur.text}
      </div>
    </div>
  );
};

const Envelope = ({ colour }) => (
  <svg width="70" height="50" viewBox="0 0 70 50">
    <rect x="3" y="3" width="64" height="44" rx="4" fill={colour} stroke="#101214" strokeWidth="3" />
    <path d="M5 7 L35 30 L65 7" fill="none" stroke="rgba(0,0,0,0.45)" strokeWidth="4" />
    <rect x="3" y="3" width="64" height="44" rx="4" fill="none" stroke="rgba(255,255,255,0.25)" strokeWidth="1" />
  </svg>
);

/** RimWorld-like letters stacking up the right edge as events happen. `letters`: [{ id, label, letter, atFrame }]. */
export const LetterStack = ({ letters }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const shown = letters.filter((l) => f >= l.atFrame).slice(-7);
  return (
    <div style={{ position: 'absolute', right: 26, bottom: 168, display: 'flex', flexDirection: 'column-reverse', gap: 10, alignItems: 'flex-end' }}>
      {shown.slice().reverse().map((l, i) => {
        const s = spring({ frame: f - l.atFrame, fps, config: { damping: 13, stiffness: 120 } });
        const fresh = f - l.atFrame < fps * 2.5;
        return (
          <div key={l.id} style={{ display: 'flex', alignItems: 'center', gap: 12, transform: `translateX(${(1 - s) * 260}px)`, opacity: 1 - i * 0.09 }}>
            <div style={{
              fontFamily: SANS, fontSize: 22, color: C.ink, background: 'rgba(7,9,13,0.75)', padding: '4px 10px',
              opacity: fresh ? 1 : 0.55, whiteSpace: 'nowrap',
            }}>{l.label}</div>
            <Envelope colour={LETTER[l.letter] || LETTER.neutral} />
          </div>
        );
      })}
    </div>
  );
};

/** The in-game date, bottom right, as RimWorld shows it. */
export const DateReadout = ({ day }) => {
  if (day == null) return null;
  const d = dateOf(day);
  return (
    <div style={{
      position: 'absolute', right: 26, bottom: 96, textAlign: 'right', fontFamily: MONO, color: C.ink,
      background: 'rgba(7,9,13,0.7)', padding: '8px 14px', border: `1px solid ${C.line}`,
    }}>
      <div style={{ fontSize: 26 }}>{d.long}</div>
      <div style={{ fontSize: 20, color: C.mute }}>Day {day}</div>
    </div>
  );
};

/** Fade a shot in and out. */
export const ShotFade = ({ children, frames }) => {
  const f = useCurrentFrame();
  const a = interpolate(f, [0, 10, frames - 10, frames], [0, 1, 1, 0], clamp);
  return <AbsoluteFill style={{ opacity: a }}>{children}</AbsoluteFill>;
};

export const useAppear = (atSec, dur = 0.5) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  return interpolate(f, [atSec * fps, (atSec + dur) * fps], [0, 1], { ...clamp, easing: ease });
};

export const FactionBadge = ({ faction, size = 64 }) => {
  const [a, b] = faction?.colours || ['#888', '#333'];
  return (
    <svg width={size} height={size} viewBox="0 0 64 64">
      <path d="M32 4 L58 14 L54 44 L32 60 L10 44 L6 14 Z" fill={b} stroke="#101214" strokeWidth="3" />
      <path d="M32 12 L50 19 L47 41 L32 52 L17 41 L14 19 Z" fill={a} />
      {faction?.kind === 'Empire' && <path d="M22 36 L22 24 L27 30 L32 20 L37 30 L42 24 L42 36 Z" fill={b} />}
      {faction?.kind === 'HollowChoir' && <><circle cx="32" cy="32" r="8" fill={b} /><circle cx="32" cy="32" r="13" fill="none" stroke={b} strokeWidth="2" /></>}
      {faction?.kind === 'Colony' && <path d="M20 40 L20 28 L32 20 L44 28 L44 40 Z" fill={b} />}
    </svg>
  );
};
