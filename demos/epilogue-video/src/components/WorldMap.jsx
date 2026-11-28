import React, { useMemo } from 'react';
import { useCurrentFrame, useVideoConfig } from 'remotion';
import { C, OUT, SANS } from '../theme.js';

// A stylised hex world map (drawn, not taken from the game): terrain from seeded noise, and each power's
// territory spreading out from its capital. Positions are in a 1920×1080 frame.
const R = 30;
const HW = Math.sqrt(3) * R;
export const PLACES = {
  citadel: { x: 430, y: 610, label: 'Imperial Citadel', kind: 'empire' },
  cathedral: { x: 1250, y: 190, label: 'The Cathedral', kind: 'choir' },
  varrowgate: { x: 1020, y: 580, label: 'Varrowgate', kind: 'colony' },
};

const hash = (x, y) => { const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return s - Math.floor(s); };
const noise = (x, y) => {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const sm = (t) => t * t * (3 - 2 * t);
  const a = hash(xi, yi), b = hash(xi + 1, yi), c = hash(xi, yi + 1), d = hash(xi + 1, yi + 1);
  return a + (b - a) * sm(xf) + (c - a) * sm(yf) + (a - b - c + d) * sm(xf) * sm(yf);
};
const hexPath = (cx, cy) => {
  let d = '';
  for (let i = 0; i < 6; i++) {
    const a = (Math.PI / 180) * (60 * i - 30);
    d += `${i ? 'L' : 'M'}${(cx + R * Math.cos(a)).toFixed(1)} ${(cy + R * Math.sin(a)).toFixed(1)}`;
  }
  return d + 'Z';
};

const useHexes = () => useMemo(() => {
  const out = [];
  for (let row = -1; row < 1080 / (R * 1.5) + 1; row++) {
    for (let col = -1; col < 1920 / HW + 1; col++) {
      const cx = col * HW + (row % 2 ? HW / 2 : 0), cy = row * R * 1.5;
      const n = noise(cx / 260, cy / 260) * 0.65 + noise(cx / 90, cy / 90) * 0.35;
      const land = n > 0.36;
      const north = cy < 330;
      const terrain = !land ? 'sea' : n > 0.74 ? 'mountain' : north ? 'tundra' : n > 0.55 ? 'forest' : 'plain';
      const dEmp = Math.hypot(cx - PLACES.citadel.x, cy - PLACES.citadel.y);
      const dCho = Math.hypot((cx - PLACES.cathedral.x) * 0.8, (cy - PLACES.cathedral.y) * 1.25);
      const dCol = Math.hypot(cx - PLACES.varrowgate.x, cy - PLACES.varrowgate.y);
      out.push({ cx, cy, d: hexPath(cx, cy), land, terrain, dEmp, dCho, dCol, j: hash(col, row) });
    }
  }
  return out;
}, []);

const TERRAIN = { sea: '#16222e', plain: '#5c6a45', forest: '#3f5a3a', tundra: '#7c8478', mountain: '#6e655a' };

/** empire, choir: how far each power has spread (0–1). war: 0–1, front lines flicker. free: Varrowgate's ring. */
export const WorldMap = ({ empire = 0, choir = 0, war = 0, free = 0, places = ['citadel', 'cathedral', 'varrowgate'], fallen = [] }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const hexes = useHexes();
  const EMP = 720 * empire, CHO = 620 * choir;
  return (
    <svg width="1920" height="1080" viewBox="0 0 1920 1080" style={{ position: 'absolute', inset: 0 }}>
      {hexes.map((h, i) => {
        let fill = TERRAIN[h.terrain];
        let own = null;
        if (h.land && h.dCol > 95) {
          const e = h.dEmp + h.j * 60 < EMP, c = h.dCho + h.j * 60 < CHO;
          if (e && c) own = h.dEmp / 720 < h.dCho / 620 ? 'e' : 'c';
          else own = e ? 'e' : c ? 'c' : null;
        }
        const front = war > 0 && own && hexes.length && Math.abs(h.dEmp / 720 - h.dCho / 620) < 0.06 && h.dEmp < EMP && h.dCho < CHO;
        return (
          <g key={i}>
            <path d={h.d} fill={fill} stroke="rgba(0,0,0,0.35)" strokeWidth="1.5" />
            {own && <path d={h.d} fill={own === 'e' ? C.purple : C.teal} opacity={0.42 + 0.08 * h.j} />}
            {front && <path d={h.d} fill="#ff6a3a" opacity={war * (0.25 + 0.35 * Math.abs(Math.sin(f / fps * 6 + h.j * 9)))} />}
          </g>
        );
      })}
      {free > 0 && (
        <circle cx={PLACES.varrowgate.x} cy={PLACES.varrowgate.y} r={95} fill="none" stroke={C.green} strokeWidth={5}
          strokeDasharray={`${597 * free} 600`} transform={`rotate(-90 ${PLACES.varrowgate.x} ${PLACES.varrowgate.y})`} />
      )}
      {places.map((k) => {
        const p = PLACES[k];
        const col = p.kind === 'empire' ? C.purple : p.kind === 'choir' ? C.teal : C.green;
        const down = fallen.includes(k);
        return (
          <g key={k} transform={`translate(${p.x} ${p.y})`}>
            <circle r="24" fill={down ? '#3a3a3a' : col} stroke={OUT} strokeWidth="5" />
            {p.kind === 'empire' && <path d="M-12 8 L-12 -6 L-6 0 L0 -12 L6 0 L12 -6 L12 8 Z" fill={OUT} />}
            {p.kind === 'choir' && <><circle r="7" fill={OUT} /><circle r="13" fill="none" stroke={OUT} strokeWidth="3" /></>}
            {p.kind === 'colony' && <path d="M-11 9 L-11 -3 L0 -11 L11 -3 L11 9 Z" fill={OUT} />}
            {down && <path d="M-20 -20 L20 20 M20 -20 L-20 20" stroke={C.red} strokeWidth="6" />}
            <text y="54" textAnchor="middle" fontFamily={SANS} fontWeight="600" fontSize="28" fill={C.ink}
              stroke="rgba(0,0,0,0.85)" strokeWidth="6" paintOrder="stroke">{p.label}</text>
          </g>
        );
      })}
    </svg>
  );
};
