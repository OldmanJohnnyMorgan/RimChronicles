import React from 'react';
import { AbsoluteFill, interpolate, useCurrentFrame, useVideoConfig } from 'remotion';
import { PLACES, WorldMap } from '../components/WorldMap.jsx';
import { Window } from '../components/Ui.jsx';
import { C, LETTER, SANS, clamp, ease } from '../theme.js';
import { eventRevealTimes } from '../reveal.js';
import { dateOf } from '../timing.js';

// empire_map: the powers on a drawn hex map. stage "woke": each power spreads from its capital as its event lands.
// stage "war": both hold their lands, raids converge on Varrowgate, and the front lines burn once the war begins.

const Arrow = ({ from, to, p, colour }) => {
  if (p <= 0) return null;
  const x = from.x + (to.x - from.x) * p, y = from.y + (to.y - from.y) * p;
  return (
    <g>
      <line x1={from.x} y1={from.y} x2={x} y2={y} stroke={colour} strokeWidth="7" strokeDasharray="18 12" opacity="0.9" />
      <circle cx={x} cy={y} r="10" fill={colour} stroke="#101214" strokeWidth="3" />
    </g>
  );
};

export const EmpireMap = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const reveals = eventRevealTimes(shot, data);
  const events = reveals.map((r) => ({ ...data.byId[r.id], at: r.at }));
  const war = shot.slots.stage === 'war';
  const at = (id) => events.find((e) => e.id === id)?.at ?? 0;
  let empire, choir, warP = 0, raids = 0;
  if (!war) {
    empire = interpolate(t, [at('event-0003'), at('event-0003') + 3], [0.05, 0.75], { ...clamp, easing: ease });
    choir = interpolate(t, [at('event-0004'), at('event-0004') + 3], [0.05, 0.75], { ...clamp, easing: ease });
  } else {
    empire = interpolate(t, [0, shot.duration], [0.8, 1.0]);
    choir = interpolate(t, [0, shot.duration], [0.8, 1.0]);
    raids = interpolate(t, [0.6, 3.5], [0, 1], { ...clamp, easing: ease });
    warP = interpolate(t, [at('event-0010'), at('event-0010') + 1.5], [0, 1], clamp);
  }
  const zoom = interpolate(f, [0, shot.duration * fps], [1.0, 1.07]);
  const cur = events.filter((e) => t >= e.at).pop();
  const curA = cur ? interpolate(t, [cur.at, cur.at + 0.5], [0, 1], clamp) : 0;
  return (
    <AbsoluteFill style={{ background: '#0b1118' }}>
      <AbsoluteFill style={{ transform: `scale(${zoom})`, transformOrigin: '55% 50%' }}>
        <WorldMap empire={empire} choir={choir} war={warP} free={war ? 1 : 0} />
        {war && (
          <svg width="1920" height="1080" style={{ position: 'absolute', inset: 0 }}>
            <Arrow from={PLACES.citadel} to={{ x: PLACES.varrowgate.x - 70, y: PLACES.varrowgate.y }} p={raids} colour={C.purple} />
            <Arrow from={PLACES.cathedral} to={{ x: PLACES.varrowgate.x + 30, y: PLACES.varrowgate.y - 75 }} p={raids} colour={C.teal} />
          </svg>
        )}
      </AbsoluteFill>
      <AbsoluteFill style={{ boxShadow: 'inset 0 0 240px rgba(0,0,0,0.85)' }} />
      {cur && (
        <div style={{ position: 'absolute', left: 70, top: 60, opacity: curA, transform: `translateY(${(1 - curA) * -30}px)` }}>
          <Window width={760} accent={LETTER[cur.letter]}>
            <div style={{ fontFamily: SANS, fontSize: 22, color: C.mute, letterSpacing: 3 }}>{dateOf(cur.day).long.toUpperCase()}</div>
            <div style={{ fontFamily: SANS, fontSize: 34, lineHeight: 1.3, color: C.ink, marginTop: 6 }}>{cur.text}</div>
          </Window>
        </div>
      )}
    </AbsoluteFill>
  );
};
