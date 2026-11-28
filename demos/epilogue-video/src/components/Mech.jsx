import React from 'react';
import { useCurrentFrame, useVideoConfig } from 'remotion';
import { OUT } from '../theme.js';

// A Hollow Choir mech: an original design, not a game sprite. A hunched hull on three legs with one bright
// eye and a tuning-fork resonator; it hums (expanding rings). `broken` tips it over and puts the eye out.
export const Mech = ({ size = 160, broken = false, phase = 0, glow = '#3fd0c9' }) => {
  const f = useCurrentFrame() + phase * 23;
  const { fps } = useVideoConfig();
  const t = f / fps;
  const bob = broken ? 0 : Math.sin(t * Math.PI * 1.6) * 2;
  const ring = (t * 0.8) % 1;
  return (
    <svg viewBox="0 0 220 220" width={size} height={size} style={{ overflow: 'visible' }}>
      <ellipse cx="110" cy="206" rx="70" ry="9" fill="rgba(0,0,0,0.45)" />
      <g transform={broken ? 'rotate(78 110 200) translate(0 6)' : `translate(0 ${bob})`} style={{ filter: broken ? 'grayscale(1) brightness(0.55)' : undefined }}>
        {!broken && <circle cx="110" cy="40" r={20 + ring * 46} fill="none" stroke={glow} strokeWidth={3} opacity={(1 - ring) * 0.5} />}
        <path d="M70 160 L52 202 M110 168 L110 204 M150 160 L168 202" stroke={OUT} strokeWidth={11} strokeLinecap="round" />
        <path d="M70 160 L52 202 M110 168 L110 204 M150 160 L168 202" stroke="#4a5660" strokeWidth={6} strokeLinecap="round" />
        <path d="M40 150 C40 92 70 66 110 66 C150 66 180 92 180 150 C180 166 160 174 110 174 C60 174 40 166 40 150 Z"
          fill="#2c3943" stroke={OUT} strokeWidth={6} strokeLinejoin="round" />
        <path d="M56 140 C60 104 80 86 110 84" fill="none" stroke="#45545f" strokeWidth={6} strokeLinecap="round" />
        <path d="M98 68 L96 40 M122 68 L124 40" stroke={OUT} strokeWidth={9} strokeLinecap="round" />
        <path d="M98 68 L96 40 M122 68 L124 40" stroke="#6b7a86" strokeWidth={4} strokeLinecap="round" />
        <circle cx="110" cy="124" r="22" fill="#0e1418" stroke={OUT} strokeWidth={5} />
        <circle cx="110" cy="124" r="13" fill={broken ? '#2a2f33' : glow} />
        {!broken && glow !== '#2a2f33' && <circle cx="110" cy="124" r="26" fill={glow} opacity={0.18 + 0.1 * Math.sin(t * 6)} />}
      </g>
    </svg>
  );
};
