import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Pawn } from '../components/Pawn.jsx';
import { Backdrop, FactionBadge, Window } from '../components/Ui.jsx';
import { C, LETTER, MONO, SANS, clamp, ease } from '../theme.js';
import { eventRevealTimes } from '../reveal.js';
import { dateOf } from '../timing.js';

// event_timeline: a day axis; each event drops a marker and a letter-style card, alternating above and below.
const LABEL = { TitleGranted: 'Title granted', Beat: 'An envoy', Independence: 'Independence', CrisisBegan: 'The crisis', Death: 'Death', WarInHeaven: 'War in Heaven', Raid: 'Raid' };

export const EventTimeline = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const reveals = eventRevealTimes(shot, data);
  const events = reveals.map((r) => ({ ...data.byId[r.id], at: r.at }));
  const d0 = events[0].day - 40, d1 = events[events.length - 1].day + 40;
  const X0 = 180, X1 = 1600;
  const x = (d) => X0 + ((d - d0) / (d1 - d0)) * (X1 - X0);
  const axisA = interpolate(f, [0, 0.8 * fps], [0, 1], { ...clamp, easing: ease });
  const pan = interpolate(t, [0, shot.duration], [30, -30]);
  const Y = 500;
  return (
    <AbsoluteFill>
      <Backdrop />
      <div style={{ position: 'absolute', inset: 0, transform: `translateX(${pan}px)` }}>
        <svg width="1920" height="1080" style={{ position: 'absolute', inset: 0 }}>
          <line x1={X0 - 80} x2={X0 - 80 + (X1 - X0 + 160) * axisA} y1={Y} y2={Y} stroke={C.winEdge} strokeWidth="4" />
          {Array.from({ length: 12 }, (_, i) => Math.ceil(d0 / 50) * 50 + i * 50).filter((d) => d <= d1).map((d) => (
            <g key={d} opacity={axisA}>
              <line x1={x(d)} x2={x(d)} y1={Y - 10} y2={Y + 10} stroke={C.winEdge} strokeWidth="3" />
              <text x={x(d)} y={Y + 44} fill={C.mute} fontFamily={MONO} fontSize="22" textAnchor="middle">{d}</text>
            </g>
          ))}
        </svg>
        {events.map((e, i) => {
          const s = spring({ frame: f - e.at * fps, fps, config: { damping: 14, stiffness: 110 } });
          if (t < e.at) return null;
          const up = i % 2 === 0;
          const colour = LETTER[e.letter];
          const cx = x(e.day);
          const pawns = (e.pawns || []).map((id) => data.byId[id]);
          const factions = (e.factions || []).map((id) => data.byId[id]);
          return (
            <React.Fragment key={e.id}>
              <div style={{ position: 'absolute', left: cx - 18, top: Y - 18, width: 36, height: 36, borderRadius: 18, background: colour, border: '4px solid #101214', transform: `scale(${s})` }} />
              <div style={{ position: 'absolute', left: cx - 2, top: up ? Y - 90 : Y + 18, width: 4, height: 72 * s, background: colour, opacity: 0.7 }} />
              <div style={{
                position: 'absolute', left: Math.min(Math.max(cx - 300, 60), 1240), width: 600,
                top: up ? Y - 90 - 260 : Y + 90, opacity: s, transform: `translateY(${(1 - s) * (up ? 40 : -40)}px)`,
              }}>
                <Window title={LABEL[e.kind] || e.kind} accent={colour}>
                  <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
                    <div style={{ flex: 1 }}>
                      <div style={{ fontFamily: SANS, fontSize: 21, color: C.mute, letterSpacing: 2 }}>DAY {e.day} · {dateOf(e.day).long.toUpperCase()}</div>
                      <div style={{ fontFamily: SANS, fontSize: 29, lineHeight: 1.3, color: C.ink, marginTop: 6 }}>{e.text}</div>
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, alignItems: 'center' }}>
                      {pawns.slice(0, 1).map((p) => <Pawn key={p.id} look={data.appearanceOf(p.id, e.day)} size={96} still />)}
                      {!pawns.length && factions.slice(0, 2).map((fa) => <FactionBadge key={fa.id} faction={fa} size={58} />)}
                    </div>
                  </div>
                </Window>
              </div>
            </React.Fragment>
          );
        })}
      </div>
    </AbsoluteFill>
  );
};
