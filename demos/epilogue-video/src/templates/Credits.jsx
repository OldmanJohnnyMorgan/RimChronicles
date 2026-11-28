import React from 'react';
import { AbsoluteFill, interpolate } from 'remotion';
import { C, DISPLAY, MONO, SANS, clamp } from '../theme.js';

// credits: the optional end card (spec §6.1).
export const Credits = ({ shot, data, t }) => {
  const [first, ...rest] = shot.slots.lines;
  const a = interpolate(t, [0.2, 1.2], [0, 1], clamp);
  return (
    <AbsoluteFill style={{ background: C.bg0, alignItems: 'center', justifyContent: 'center' }}>
      <div style={{ textAlign: 'center', opacity: a }}>
        <div style={{ fontFamily: DISPLAY, fontSize: 96, letterSpacing: 10, color: C.ink }}>{first.toUpperCase()}</div>
        <div style={{ height: 4, width: 520, background: C.gold, margin: '18px auto 24px' }} />
        {rest.map((l) => <div key={l} style={{ fontFamily: SANS, fontSize: 36, color: C.gold }}>{l}</div>)}
        <div style={{ fontFamily: MONO, fontSize: 22, color: C.mute, marginTop: 50 }}>
          The story of {data.header.colony}, from its chronicle · narration in murmurs, no voices
        </div>
      </div>
    </AbsoluteFill>
  );
};
