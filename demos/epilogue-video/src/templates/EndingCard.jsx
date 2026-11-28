import React from 'react';
import { AbsoluteFill, interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { C, DISPLAY, MONO, SANS, clamp, ease } from '../theme.js';

// ending_card: the victory screen. The ending's name, then the epilogue paragraph by paragraph as the narrator
// murmurs it; the paragraph being read is bright, the rest dim. Below, Varrowgate at night.

const Skyline = ({ t }) => {
  const houses = [[60, 120, 160], [240, 90, 130], [360, 150, 200], [540, 80, 110], [660, 130, 240], [820, 100, 150], [960, 140, 180], [1130, 90, 120], [1250, 160, 220], [1440, 110, 140], [1580, 90, 170], [1700, 140, 130]];
  return (
    <svg width="1920" height="300" style={{ position: 'absolute', left: 0, bottom: 0 }}>
      <rect x="0" y="220" width="1920" height="80" fill="#05070a" />
      <path d="M0 230 L1920 230" stroke="#2a2f36" strokeWidth="18" />
      {Array.from({ length: 48 }, (_, i) => <rect key={i} x={i * 40} y={212} width="26" height="16" fill="#1b1f25" stroke="#05070a" strokeWidth="2" />)}
      {houses.map(([x, w, h], i) => (
        <g key={i}>
          <rect x={x} y={230 - h} width={w} height={h} fill="#0a0d12" stroke="#05070a" strokeWidth="3" />
          {Array.from({ length: Math.floor(h / 60) }, (_, j) => {
            const on = Math.sin(i * 7.1 + j * 3.3) > -0.2;
            const flick = 0.75 + 0.25 * Math.sin(t * 2 + i + j);
            return <rect key={j} x={x + w / 2 - 10} y={230 - h + 24 + j * 56} width="20" height="22" fill={on ? `rgba(227,176,75,${0.75 * flick})` : '#111'} />;
          })}
        </g>
      ))}
    </svg>
  );
};

export const EndingCard = ({ shot, data, lines, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const paras = data.epilogueParagraphs();
  const title = data.chronicle.sealed.ending_title;
  const titleA = interpolate(t, [0.2, 1.6], [0, 1], { ...clamp, easing: ease });
  const reading = lines.findIndex((l) => t >= l.start - 0.2 && t < l.end + 0.6);
  const end = shot.duration;
  return (
    <AbsoluteFill style={{ background: 'linear-gradient(180deg, #05070c 0%, #0d1522 60%, #142033 100%)' }}>
      <AbsoluteFill style={{ backgroundImage: 'radial-gradient(rgba(255,255,255,0.7) 1px, transparent 1.6px)', backgroundSize: '140px 110px', backgroundPosition: `${-f * 0.15}px 0`, opacity: 0.35 }} />
      <AbsoluteFill style={{ backgroundImage: 'radial-gradient(rgba(255,255,255,0.5) 1px, transparent 1.4px)', backgroundSize: '73px 91px', backgroundPosition: `${-f * 0.07}px 13px`, opacity: 0.3 }} />
      <Skyline t={t} />
      <div style={{ position: 'absolute', top: 70, left: 0, right: 0, textAlign: 'center', opacity: titleA }}>
        <div style={{ fontFamily: SANS, fontSize: 26, letterSpacing: 10, color: C.mute }}>{data.header.colony.toUpperCase()} · {data.header.ended.date.toUpperCase()}</div>
        <div style={{ fontFamily: DISPLAY, fontSize: 116, letterSpacing: 8, color: C.gold, marginTop: 8, textShadow: '0 0 40px rgba(227,176,75,0.35)' }}>{title.toUpperCase()}</div>
      </div>
      <div style={{ position: 'absolute', top: 300, left: 260, right: 260 }}>
        {paras.map((p, i) => {
          const l = lines[i];
          const shown = l ? interpolate(t, [l.start - 0.6, l.start], [0, 1], clamp) : 1;
          const bright = reading === -1 || i === reading ? 1 : 0.42;
          return (
            <p key={i} style={{
              fontFamily: SANS, fontSize: 38, lineHeight: 1.45, color: C.ink, margin: '0 0 26px',
              opacity: shown * bright * interpolate(t, [end - 1.6, end - 0.4], [1, 0], clamp),
              transform: `translateY(${(1 - shown) * 16}px)`,
            }}>{p}</p>
          );
        })}
      </div>
      <div style={{ position: 'absolute', right: 40, bottom: 20, fontFamily: MONO, fontSize: 18, color: 'rgba(143,152,166,0.6)' }}>
        epilogue · {data.chronicle.sealed.epilogue.prompt}
      </div>
    </AbsoluteFill>
  );
};
