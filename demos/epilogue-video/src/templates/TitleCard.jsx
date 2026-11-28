import React from 'react';
import { AbsoluteFill, interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { C, DISPLAY, MONO, clamp, ease } from '../theme.js';

// title_card: a slowly turning rim-world of hexes, the colony's name and its years.
const Planet = () => {
  const f = useCurrentFrame();
  return (
    <svg width="1300" height="1300" viewBox="0 0 1300 1300" style={{ position: 'absolute', left: 310, top: 360 }}>
      <defs>
        <pattern id="hex" width="52" height="90" patternUnits="userSpaceOnUse" patternTransform={`translate(${f * 0.6} 0)`}>
          <path d="M26 0 L52 15 L52 45 L26 60 L0 45 L0 15 Z M26 60 L26 90" fill="none" stroke="rgba(242,239,232,0.13)" strokeWidth="1.5" />
        </pattern>
        <radialGradient id="globe" cx="35%" cy="30%" r="75%">
          <stop offset="0%" stopColor="#4c6a52" /><stop offset="45%" stopColor="#2f4436" /><stop offset="100%" stopColor="#0b1210" />
        </radialGradient>
        <radialGradient id="shadow" cx="30%" cy="25%" r="80%">
          <stop offset="55%" stopColor="rgba(0,0,0,0)" /><stop offset="100%" stopColor="rgba(0,0,0,0.85)" />
        </radialGradient>
        <clipPath id="ball"><circle cx="650" cy="650" r="620" /></clipPath>
      </defs>
      <circle cx="650" cy="650" r="640" fill="rgba(127,176,105,0.10)" />
      <circle cx="650" cy="650" r="620" fill="url(#globe)" />
      <g clipPath="url(#ball)">
        {[[300, 420, 220], [820, 300, 160], [620, 760, 260], [1040, 640, 140], [240, 860, 120]].map(([x, y, r], i) => (
          <ellipse key={i} cx={x + ((f * 0.6) % 1300) - 200} cy={y} rx={r} ry={r * 0.6} fill="rgba(20,34,46,0.55)" />
        ))}
        <rect width="1300" height="1300" fill="url(#hex)" />
      </g>
      <circle cx="650" cy="650" r="620" fill="url(#shadow)" />
    </svg>
  );
};

export const TitleCard = ({ shot }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const a = interpolate(f, [0.3 * fps, 1.3 * fps], [0, 1], { ...clamp, easing: ease });
  const sub = interpolate(f, [1.1 * fps, 1.9 * fps], [0, 1], clamp);
  const zoom = interpolate(f, [0, shot.duration * fps], [1, 1.06]);
  return (
    <AbsoluteFill style={{ background: C.bg0 }}>
      <AbsoluteFill style={{ transform: `scale(${zoom})` }}><Planet /></AbsoluteFill>
      <AbsoluteFill style={{ background: 'linear-gradient(180deg, rgba(7,9,13,0.2) 0%, rgba(7,9,13,0) 40%, rgba(7,9,13,0.85) 100%)' }} />
      <AbsoluteFill style={{ alignItems: 'center', top: 170 }}>
        <div style={{ fontFamily: DISPLAY, fontSize: 200, letterSpacing: 30 * a, color: C.ink, opacity: a, textShadow: '0 12px 50px rgba(0,0,0,0.8)' }}>
          {shot.slots.title.toUpperCase()}
        </div>
        <div style={{ height: 4, width: 700 * a, background: C.gold, marginTop: 6 }} />
        <div style={{ fontFamily: MONO, fontSize: 44, color: C.gold, marginTop: 22, opacity: sub, letterSpacing: 8 }}>{shot.slots.subtitle}</div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
