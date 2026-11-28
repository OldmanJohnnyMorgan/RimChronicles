import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Pawn } from '../components/Pawn.jsx';
import { Backdrop, Window } from '../components/Ui.jsx';
import { C, DISPLAY, MONO, SANS, clamp, ease } from '../theme.js';
import { dateOf } from '../timing.js';

// pawn_tribute: a character card in the game's style: portrait, backstory, traits, and the deeds the chronicle records.
export const PawnTribute = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const p = data.byId[shot.slots.pawn];
  const look = data.byId[shot.slots.appearance] || data.appearanceOf(p.id);
  const deeds = (shot.slots.events || []).map((id) => data.byId[id]);
  const a = spring({ frame: f - 0.3 * fps, fps, config: { damping: 16 } });
  const candle = 0.85 + 0.15 * Math.sin(f * 0.37) * Math.sin(f * 0.11);
  const dead = !!p.died;
  return (
    <AbsoluteFill>
      <Backdrop tint={`rgba(227,176,75,${0.14 * candle})`} />
      <div style={{ position: 'absolute', left: 150, top: 120, opacity: a, transform: `translateY(${(1 - a) * 40}px)` }}>
        <Window width={1380} title={`${p.name}${dead ? ' †' : ''}`} accent={C.gold}>
          <div style={{ display: 'flex', gap: 50 }}>
            <div style={{ width: 420, height: 520, background: 'rgba(7,9,13,0.6)', border: `2px solid ${C.winEdge}`, display: 'flex', alignItems: 'flex-end', justifyContent: 'center', overflow: 'hidden' }}>
              <Pawn look={look} size={390} still />
            </div>
            <div style={{ flex: 1, fontFamily: SANS, color: C.ink }}>
              <div style={{ fontFamily: DISPLAY, fontSize: 110, letterSpacing: 6, lineHeight: 1 }}>{p.name.toUpperCase()}</div>
              <div style={{ fontSize: 34, color: C.mute, marginTop: 10 }}>{p.backstory}{p.died ? `, died ${dateOf(p.died.day).long}` : `, age ${p.age_at_end}`}</div>
              <div style={{ display: 'flex', gap: 12, marginTop: 22 }}>
                {p.traits.map((tr) => (
                  <span key={tr} style={{ fontSize: 28, padding: '6px 16px', background: '#3a3f46', border: `1px solid ${C.winEdge}` }}>{tr}</span>
                ))}
              </div>
              <div style={{ fontSize: 24, color: C.mute, letterSpacing: 4, marginTop: 40 }}>IN THE CHRONICLE</div>
              {deeds.map((d, i) => {
                const da = interpolate(t, [1.4 + i * 1.3, 1.9 + i * 1.3], [0, 1], { ...clamp, easing: ease });
                return (
                  <div key={d.id} style={{ display: 'flex', gap: 18, marginTop: 14, opacity: da, transform: `translateX(${(1 - da) * 30}px)` }}>
                    <span style={{ fontFamily: MONO, fontSize: 26, color: C.gold, width: 150, whiteSpace: 'nowrap' }}>Day {d.day}</span>
                    <span style={{ fontSize: 32, lineHeight: 1.3, flex: 1 }}>{d.text}</span>
                  </div>
                );
              })}
              <div style={{ fontStyle: 'italic', fontSize: 34, color: C.gold, marginTop: 36, opacity: interpolate(t, [4, 4.8], [0, 1], clamp) }}>{shot.slots.caption}</div>
            </div>
          </div>
        </Window>
      </div>
    </AbsoluteFill>
  );
};
