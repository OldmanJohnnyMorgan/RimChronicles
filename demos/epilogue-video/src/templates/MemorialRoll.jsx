import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Backdrop } from '../components/Ui.jsx';
import { C, DISPLAY, MONO, OUT, SANS, clamp } from '../theme.js';
import { dateOf } from '../timing.js';

// memorial_roll: a grave for each colonist the chronicle lists in its memorial, in order of death.
const Grave = ({ name, day, backstory, flicker }) => (
  <div style={{ width: 360, textAlign: 'center' }}>
    <svg width="300" height="360" viewBox="0 0 300 360">
      <ellipse cx="150" cy="330" rx="140" ry="22" fill="#2a2119" />
      <path d="M40 330 L40 120 C40 50 260 50 260 120 L260 330 Z" fill="#6d6f73" stroke={OUT} strokeWidth="7" />
      <path d="M62 320 L62 126 C62 74 238 74 238 126" fill="none" stroke="#86898e" strokeWidth="4" />
      <text x="150" y="190" textAnchor="middle" fontFamily="Anton, sans-serif" fontSize="52" fill="#2a2c30">{name.toUpperCase()}</text>
      <text x="150" y="236" textAnchor="middle" fontFamily='"Anonymous Pro", monospace' fontSize="26" fill="#34363a">DAY {day}</text>
      <g transform="translate(250 300)">
        <rect x="-8" y="-10" width="16" height="34" fill="#e8e2d0" stroke={OUT} strokeWidth="3" />
        <ellipse cx="0" cy={-18 - flicker * 3} rx="6" ry={10 + flicker * 3} fill="#e3b04b" />
        <circle cx="0" cy="-16" r="30" fill="rgba(227,176,75,0.18)" />
      </g>
    </svg>
    <div style={{ fontFamily: SANS, fontSize: 26, color: C.mute }}>{backstory} · {dateOf(day).long}</div>
  </div>
);

export const MemorialRoll = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const pawns = shot.slots.pawns.map((id) => data.byId[id]).sort((a, b) => a.died.day - b.died.day);
  const titleA = interpolate(t, [0.2, 1], [0, 1], clamp);
  return (
    <AbsoluteFill>
      <Backdrop tint="rgba(227,176,75,0.08)" />
      <div style={{ position: 'absolute', top: 110, left: 0, right: 0, textAlign: 'center', fontFamily: DISPLAY, fontSize: 84, letterSpacing: 14, color: C.ink, opacity: titleA }}>IN MEMORY</div>
      <div style={{ position: 'absolute', top: 300, left: 0, right: 0, display: 'flex', justifyContent: 'center', gap: 60 }}>
        {pawns.map((p, i) => {
          const s = spring({ frame: f - (0.8 + i * 0.6) * fps, fps, config: { damping: 15 } });
          return (
            <div key={p.id} style={{ opacity: s, transform: `translateY(${(1 - s) * 60}px)` }}>
              <Grave name={p.name} day={p.died.day} backstory={p.backstory} flicker={Math.sin(f * 0.4 + i) * Math.sin(f * 0.13 + i * 2)} />
            </div>
          );
        })}
      </div>
      <div style={{ position: 'absolute', bottom: 200, left: 0, right: 0, textAlign: 'center', fontFamily: MONO, fontSize: 24, color: C.mute, opacity: titleA }}>
        {data.header.colony} · {data.header.founded.date} – {data.header.ended.date}
      </div>
    </AbsoluteFill>
  );
};
