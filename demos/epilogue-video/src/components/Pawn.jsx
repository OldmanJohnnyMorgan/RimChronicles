import React from 'react';
import { interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { OUT } from '../theme.js';

// A pawn drawn in a RimWorld-like style (spec §5.1, R5): flat fills, thick dark outlines, no arms,
// and the held item floats beside the body. Everything comes from a chronicle `appearance` entry.
const SW = 5;

const shade = (hex, k) => {
  const n = parseInt(hex.slice(1), 16);
  const c = [n >> 16, (n >> 8) & 255, n & 255].map((v) => Math.max(0, Math.min(255, Math.round(v * k))));
  return `#${c.map((v) => v.toString(16).padStart(2, '0')).join('')}`;
};

const HairBack = ({ style, hair }) => {
  if (style === 'long')
    return <path d="M52 78 C46 130 50 168 62 186 L148 186 C160 168 164 130 158 78 Z" fill={shade(hair, 0.85)} stroke={OUT} strokeWidth={SW} strokeLinejoin="round" />;
  if (style === 'bun')
    return <circle cx="105" cy="26" r="20" fill={hair} stroke={OUT} strokeWidth={SW} />;
  return null;
};

const HairFront = ({ style, hair }) => {
  const p = { fill: hair, stroke: OUT, strokeWidth: SW, strokeLinejoin: 'round' };
  switch (style) {
    case 'bald': return null;
    case 'crop': return <path d="M58 76 C56 44 80 30 106 30 C132 30 156 44 152 76 C140 58 124 52 105 52 C86 52 70 58 58 76 Z" {...p} />;
    case 'short': return <path d="M56 84 C50 44 78 26 108 27 C140 28 160 48 155 84 C148 70 140 62 128 58 L120 66 L112 57 L100 66 L92 57 L80 64 C70 66 62 74 56 84 Z" {...p} />;
    case 'long': return <path d="M55 92 C48 46 78 26 108 27 C140 28 162 48 155 92 C150 72 138 58 118 52 C100 62 78 64 64 64 C60 72 57 82 55 92 Z" {...p} />;
    case 'bun': return <path d="M57 80 C52 46 80 30 106 30 C134 30 158 46 153 80 C144 62 126 54 105 54 C84 54 66 62 57 80 Z" {...p} />;
    default: return (
      <>
        <path d="M56 84 C50 44 78 24 110 26 C140 28 160 46 156 80 C150 64 138 56 124 54 C112 62 88 60 74 56 C66 64 60 74 56 84 Z" {...p} />
        <path d="M96 30 C104 40 120 46 138 48" fill="none" stroke={shade(hair, 1.5)} strokeWidth={3} strokeLinecap="round" />
      </>
    );
  }
};

const Body = ({ apparel, c0, c1, accent }) => {
  const o = { stroke: OUT, strokeWidth: SW, strokeLinejoin: 'round' };
  const torso = 'M42 246 C38 200 44 160 70 146 C84 139 126 139 140 146 C166 160 172 200 168 246 Z';
  switch (apparel) {
    case 'armor':
    case 'imperial':
      return (
        <>
          <path d={torso} fill={c0} {...o} />
          <path d="M70 160 C80 152 130 152 140 160 L136 222 C120 232 90 232 74 222 Z" fill={c1} {...o} strokeWidth={4} />
          {apparel === 'imperial' && <path d="M74 186 L136 186 M105 160 L105 228" stroke={accent} strokeWidth={5} />}
          <circle cx="56" cy="164" r="17" fill={c1} {...o} strokeWidth={4} />
          <circle cx="154" cy="164" r="17" fill={c1} {...o} strokeWidth={4} />
          {apparel === 'imperial' && <><circle cx="56" cy="164" r="7" fill={accent} /><circle cx="154" cy="164" r="7" fill={accent} /></>}
        </>
      );
    case 'robe':
      return (
        <>
          <path d="M38 250 C36 200 44 158 70 146 C84 139 126 139 140 146 C166 158 174 200 172 250 Z" fill={c0} {...o} />
          <path d="M72 148 L150 236" stroke={c1} strokeWidth={14} />
          <path d="M72 148 L150 236" stroke={OUT} strokeWidth={2} opacity={0.5} />
          <circle cx="105" cy="200" r="10" fill={accent || '#9fe3ff'} stroke={OUT} strokeWidth={3} />
        </>
      );
    case 'duster':
      return (
        <>
          <path d="M36 250 C34 200 44 160 70 146 C84 139 126 139 140 146 C166 160 176 200 174 250 Z" fill={c0} {...o} />
          <path d="M105 150 L105 248" stroke={c1} strokeWidth={6} />
          <path d="M70 146 L92 178 L105 150 L118 178 L140 146" fill={c1} {...o} strokeWidth={4} />
          <rect x="44" y="204" width="122" height="12" fill={shade(c1, 0.7)} stroke={OUT} strokeWidth={3} />
        </>
      );
    case 'apron':
      return (
        <>
          <path d={torso} fill={c0} {...o} />
          <path d="M78 168 L132 168 L138 246 L72 246 Z" fill={shade(c1, 1.35)} {...o} strokeWidth={4} />
          <path d="M78 168 L70 150 M132 168 L140 150" stroke={OUT} strokeWidth={4} />
        </>
      );
    case 'suit':
      return (
        <>
          <path d={torso} fill={c0} {...o} />
          <path d="M150 158 C162 176 165 210 163 244 L146 244 C148 210 148 182 150 158 Z" fill={c1} />
          <path d="M86 143 L105 186 L124 143 Z" fill="#ece8df" stroke={OUT} strokeWidth={4} strokeLinejoin="round" />
          <path d="M100 152 L110 152 L113 182 L105 194 L97 182 Z" fill={accent} stroke={OUT} strokeWidth={3.5} strokeLinejoin="round" />
          <path d="M86 143 L98 170 L90 178 L78 150 Z" fill={c1} stroke={OUT} strokeWidth={3.5} strokeLinejoin="round" />
          <path d="M124 143 L112 170 L120 178 L132 150 Z" fill={c1} stroke={OUT} strokeWidth={3.5} strokeLinejoin="round" />
          <rect x="128" y="190" width="24" height="15" rx="2" fill="#ece8df" stroke={OUT} strokeWidth={3} />
          <rect x="131" y="193" width="18" height="4" fill={accent} />
        </>
      );
    default: // tunic
      return (
        <>
          <path d={torso} fill={c0} {...o} />
          <rect x="46" y="206" width="118" height="11" fill={c1} stroke={OUT} strokeWidth={3} />
          <path d="M88 144 L105 166 L122 144" fill="none" stroke={OUT} strokeWidth={4} />
        </>
      );
  }
};

const Gear = ({ gear, accent, label }) => {
  const o = { stroke: OUT, strokeWidth: 4, strokeLinejoin: 'round' };
  switch (gear) {
    case 'mic':
      return (
        <g transform="rotate(-28 178 160)">
          <rect x="171" y="150" width="14" height="62" rx="5" fill="#2a2a2a" {...o} />
          <rect x="165" y="132" width="26" height="22" rx="3" fill={accent} {...o} />
          <text x="178" y="148" textAnchor="middle" fontFamily="Anton, sans-serif" fontSize="12" fill="#fff">{label}</text>
          <circle cx="178" cy="120" r="17" fill="#8e949c" {...o} />
          <path d="M166 114 L190 114 M164 121 L192 121 M166 128 L190 128" stroke="#5d636b" strokeWidth={2} />
        </g>
      );
    case 'rifle':
      return (
        <g transform="rotate(-62 186 170)">
          <rect x="120" y="164" width="120" height="13" rx="3" fill="#3a3d40" {...o} />
          <rect x="196" y="160" width="40" height="20" rx="3" fill="#5c4630" {...o} />
          <rect x="150" y="174" width="12" height="20" fill="#3a3d40" {...o} />
        </g>
      );
    case 'hammer':
      return (
        <g transform="rotate(-20 182 170)">
          <rect x="176" y="130" width="12" height="90" rx="4" fill="#6b4b2e" {...o} />
          <rect x="158" y="112" width="48" height="26" rx="4" fill="#8e949c" {...o} />
        </g>
      );
    case 'staff':
      return (
        <g>
          <rect x="182" y="70" width="10" height="170" rx="4" fill="#5c4630" {...o} />
          <circle cx="187" cy="62" r="13" fill={accent || '#9fe3ff'} {...o} />
        </g>
      );
    case 'bow':
      return (
        <g>
          <path d="M176 92 C214 130 214 200 176 238" fill="none" stroke="#6b4b2e" strokeWidth={9} strokeLinecap="round" />
          <path d="M176 92 C214 130 214 200 176 238" fill="none" stroke={OUT} strokeWidth={2} />
          <path d="M176 92 L176 238" stroke="#d8d0bc" strokeWidth={2} />
        </g>
      );
    default: return null;
  }
};

export const Pawn = ({ look = {}, talking = false, dead = false, size = 420, still = false, label = 'RD', helmet = false, phase = 0 }) => {
  const f = useCurrentFrame() + phase * 37;
  const { fps } = useVideoConfig();
  const skin = look.skin || '#e6b38c';
  const hair = look.hair || '#3a2416';
  const [c0, c1] = look.apparel_colours || ['#2c3c58', '#223049'];
  const accent = look.accent || '#e3b04b';
  const bob = still || dead ? 0 : Math.sin((f / fps) * Math.PI * 1.2) * 2.5;
  const nod = talking ? Math.sin((f / fps) * Math.PI * 5) * 2.2 : 0;
  const cycle = f % 100;
  const blink = cycle > 95 ? interpolate(cycle, [95, 97, 99], [1, 0.1, 1], { extrapolateRight: 'clamp' }) : 1;
  const gearY = still ? 0 : Math.sin((f / fps) * Math.PI * 0.9 + 1) * 3;

  return (
    <svg viewBox="0 0 220 270" width={size} height={(size * 270) / 220}
      style={{ overflow: 'visible', filter: dead ? 'grayscale(0.85) brightness(0.75)' : undefined }}>
      <ellipse cx="105" cy="252" rx="62" ry="10" fill="rgba(0,0,0,0.45)" />
      <g transform={`translate(0 ${bob})`}>
        <Body apparel={look.apparel} c0={c0} c1={c1} accent={accent} />
        <g transform={`translate(0 ${nod})`}>
          <HairBack style={look.hair_style} hair={hair} />
          <ellipse cx="105" cy="88" rx="50" ry="56" fill={skin} stroke={OUT} strokeWidth={SW} />
          {look.beard && <path d="M62 100 C66 138 86 146 105 146 C124 146 144 138 148 100 C138 120 124 126 105 126 C86 126 72 120 62 100 Z" fill={hair} stroke={OUT} strokeWidth={4} strokeLinejoin="round" />}
          {helmet ? (
            <>
              <path d="M52 92 C48 40 78 22 105 22 C132 22 162 40 158 92 L146 92 C144 70 130 60 105 60 C80 60 66 70 64 92 Z" fill={c1} stroke={OUT} strokeWidth={SW} strokeLinejoin="round" />
              <path d="M105 22 C98 6 112 -4 126 2" fill="none" stroke={accent} strokeWidth={7} strokeLinecap="round" />
            </>
          ) : <HairFront style={look.hair_style} hair={hair} />}
          {dead ? (
            <g stroke={OUT} strokeWidth={4} strokeLinecap="round">
              <path d="M82 89 L94 99 M94 89 L82 99" /><path d="M118 89 L130 99 M130 89 L118 99" />
            </g>
          ) : (
            <>
              <ellipse cx="88" cy="94" rx="5" ry={6.5 * blink} fill={OUT} />
              <ellipse cx="124" cy="94" rx="5" ry={6.5 * blink} fill={OUT} />
            </>
          )}
          {talking && <ellipse cx="106" cy="120" rx="6" ry={1.5 + Math.abs(Math.sin(f * 0.9)) * 3.5} fill="#5b2a22" />}
        </g>
      </g>
      <g transform={`translate(0 ${gearY})`}><Gear gear={look.gear} accent={accent} label={label} /></g>
    </svg>
  );
};

/** A generic look for unnamed colonists and soldiers, varied by index. */
export const genericLook = (i, kind) => {
  const skins = ['#e8c1a0', '#c89670', '#a8704c', '#f0d0b4', '#7a4e34', '#d9a882'];
  const hairs = ['#2b2420', '#6b4a2a', '#c0582e', '#1c1612', '#8e8a86', '#e8e2d0'];
  const styles = ['crop', 'short', 'parted', 'long', 'bun', 'bald'];
  if (kind === 'imperial')
    return { skin: skins[i % 6], hair: hairs[i % 6], hair_style: 'crop', apparel: 'imperial', apparel_colours: ['#5a3d82', '#3f2a5c'], accent: '#e3b04b', gear: 'rifle' };
  return { skin: skins[i % 6], hair: hairs[(i * 5) % 6], hair_style: styles[(i * 7) % 6], beard: i % 4 === 1,
    apparel: ['tunic', 'armor', 'duster', 'apron'][i % 4], apparel_colours: [['#5d6b52', '#46523e'], ['#6a5a42', '#4e4230'], ['#4a5a6e', '#384658'], ['#7a5a3a', '#5e4430']][i % 4], gear: ['rifle', 'bow', 'hammer', 'rifle'][i % 4] };
};
