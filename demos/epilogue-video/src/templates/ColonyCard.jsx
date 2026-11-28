import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Pawn } from '../components/Pawn.jsx';
import { Backdrop, Window } from '../components/Ui.jsx';
import { C, MONO, SANS, clamp, ease } from '../theme.js';

// colony_card: the founders in a RimWorld-like colonist bar, and a History window charting population and wealth.

const ColonistBar = ({ pawns, data }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  return (
    <div style={{ position: 'absolute', top: 60, left: 0, right: 0, display: 'flex', justifyContent: 'center', gap: 22 }}>
      {pawns.map((p, i) => {
        const s = spring({ frame: f - (0.5 + i * 0.35) * fps, fps, config: { damping: 14 } });
        return (
          <div key={p.id} style={{ textAlign: 'center', transform: `translateY(${(1 - s) * -160}px)`, opacity: s }}>
            <div style={{ width: 168, height: 168, overflow: 'hidden', background: 'rgba(28,31,35,0.9)', border: `2px solid ${C.winEdge}`, outline: `2px solid ${C.winEdgeDark}`, position: 'relative' }}>
              <div style={{ position: 'absolute', left: 6, top: 8 }}><Pawn look={data.appearanceOf(p.id, 0)} size={150} phase={i} /></div>
            </div>
            <div style={{ fontFamily: SANS, fontSize: 28, color: C.ink, marginTop: 8, background: 'rgba(7,9,13,0.7)', padding: '2px 8px' }}>{p.name}</div>
            <div style={{ fontFamily: SANS, fontSize: 20, color: C.mute }}>{p.backstory}</div>
          </div>
        );
      })}
    </div>
  );
};

const Chart = ({ series, progress }) => {
  const W = 1100, H = 330, maxDay = series[series.length - 1][0];
  const maxPop = 18, maxWealth = Math.max(...series.map((r) => r[2])) * 1.08;
  const x = (d) => (d / maxDay) * W;
  const line = (k, max) => series.map((r, i) => `${i ? 'L' : 'M'}${x(r[0]).toFixed(1)} ${(H - (r[k] / max) * H).toFixed(1)}`).join(' ');
  const clip = W * progress;
  const last = series.filter((r) => x(r[0]) <= clip).pop() || series[0];
  return (
    <svg width={W + 90} height={H + 60} style={{ overflow: 'visible' }}>
      <g transform="translate(60 10)">
        {[0, 0.25, 0.5, 0.75, 1].map((g) => <line key={g} x1="0" x2={W} y1={H * g} y2={H * g} stroke={C.line} />)}
        {[0, 200, 400, 600, 800, 1000].map((d) => (
          <text key={d} x={x(d)} y={H + 34} fill={C.mute} fontFamily={MONO} fontSize="22" textAnchor="middle">day {d}</text>
        ))}
        <defs><clipPath id="draw"><rect x="-4" y="-10" width={clip + 4} height={H + 20} /></clipPath></defs>
        <g clipPath="url(#draw)">
          <path d={line(2, maxWealth)} fill="none" stroke={C.gold} strokeWidth="4" opacity="0.8" />
          <path d={line(1, maxPop)} fill="none" stroke={C.green} strokeWidth="6" />
        </g>
        <circle cx={x(last[0])} cy={H - (last[1] / maxPop) * H} r="9" fill={C.green} stroke="#101214" strokeWidth="3" />
      </g>
    </svg>
  );
};

export const ColonyCard = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const ev = data.byId[shot.slots.event];
  const hist = data.byId[shot.slots.history].series;
  const founders = ev.pawns.map((id) => data.byId[id]);
  const win = interpolate(f, [2.0 * fps, 2.6 * fps], [0, 1], { ...clamp, easing: ease });
  const progress = interpolate(t, [3.2, shot.duration - 1.2], [0, 1], { ...clamp, easing: ease });
  const visible = hist.filter((r) => r[0] <= progress * hist[hist.length - 1][0]);
  const now = visible[visible.length - 1] || hist[0];
  return (
    <AbsoluteFill>
      <Backdrop tint="rgba(127,176,105,0.18)" />
      <ColonistBar pawns={founders} data={data} />
      <div style={{ position: 'absolute', left: 150, top: 380, opacity: win, transform: `translateY(${(1 - win) * 40}px)` }}>
        <Window title={`${data.header.colony}: history`} width={1300} accent={C.green}>
          <div style={{ display: 'flex', gap: 40, fontFamily: SANS, fontSize: 28, color: C.ink, marginBottom: 6 }}>
            <span><span style={{ color: C.green }}>■</span> Colonists <b style={{ fontFamily: MONO }}>{now[1]}</b></span>
            <span><span style={{ color: C.gold }}>■</span> Wealth <b style={{ fontFamily: MONO }}>{Math.round(now[2] / 1000)}k</b></span>
            <span style={{ color: C.mute, marginLeft: 'auto' }}>Founded {data.header.founded.date}</span>
          </div>
          <Chart series={hist} progress={progress} />
        </Window>
      </div>
    </AbsoluteFill>
  );
};
