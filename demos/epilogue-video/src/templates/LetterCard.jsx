import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Pawn } from '../components/Pawn.jsx';
import { Backdrop, Button, FactionBadge, Window } from '../components/Ui.jsx';
import { C, DISPLAY, LETTER, SANS, clamp, ease } from '../theme.js';
import { dateOf } from '../timing.js';

// letter_card: a choice letter as the game shows one. The cursor moves to the chronicle's choice and clicks it.

const Cursor = ({ x, y, down }) => (
  <svg width="44" height="56" viewBox="0 0 44 56" style={{ position: 'absolute', left: x, top: y, transform: `scale(${down ? 0.88 : 1})` }}>
    <path d="M4 4 L4 44 L14 34 L22 52 L30 48 L22 31 L36 31 Z" fill="#f2efe8" stroke="#101214" strokeWidth="4" strokeLinejoin="round" />
  </svg>
);

export const LetterCard = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const e = data.byId[shot.slots.event];
  const pawn = data.byId[shot.slots.pawn];
  const faction = data.byId[e.factions[0]];
  const pop = spring({ frame: f - 0.4 * fps, fps, config: { damping: 15, stiffness: 120 } });
  const choice = shot.slots.choice;
  const clickAt = 4.6;
  const move = interpolate(t, [2.6, clickAt - 0.15], [0, 1], { ...clamp, easing: ease });
  const down = t > clickAt && t < clickAt + 0.25;
  const pressed = t > clickAt;
  const stamp = spring({ frame: f - (clickAt + 0.35) * fps, fps, config: { damping: 10, stiffness: 160 } });
  const pawnS = spring({ frame: f - 1.0 * fps, fps, config: { damping: 14 } });
  const target = choice === 'Refuse' ? { x: 1240, y: 780 } : { x: 990, y: 780 };
  return (
    <AbsoluteFill>
      <Backdrop tint="rgba(138,92,194,0.18)" />
      <div style={{ position: 'absolute', left: 150, top: 230, opacity: pawnS, transform: `translateX(${(1 - pawnS) * -200}px)`, textAlign: 'center' }}>
        <Pawn look={data.appearanceOf(pawn.id, e.day)} size={360} talking={pressed && t < clickAt + 2.2} />
        <div style={{ fontFamily: SANS, fontWeight: 600, fontSize: 36, color: C.ink, background: 'rgba(7,9,13,0.75)', padding: '4px 14px' }}>{pawn.name}</div>
      </div>
      <div style={{ position: 'absolute', left: 660, top: 170, transform: `scale(${0.85 + 0.15 * pop})`, opacity: pop }}>
        <Window title={e.letter_title} accent={LETTER[e.letter]} width={980}>
          <div style={{ display: 'flex', gap: 18, alignItems: 'center', marginBottom: 14 }}>
            <FactionBadge faction={faction} size={70} />
            <div>
              <div style={{ fontFamily: SANS, fontSize: 22, color: C.mute, letterSpacing: 3 }}>FROM {faction.name.toUpperCase()}</div>
              <div style={{ fontFamily: SANS, fontSize: 22, color: C.mute, letterSpacing: 3 }}>DAY {e.day} · {dateOf(e.day).long.toUpperCase()}</div>
            </div>
          </div>
          <div style={{ fontFamily: SANS, fontSize: 34, lineHeight: 1.4, color: C.ink, minHeight: 200 }}>{e.letter_body}</div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 30, marginTop: 24 }}>
            <Button hot={choice === 'Accept' && move > 0.9} pressed={choice === 'Accept' && pressed}>Accept</Button>
            <Button hot={choice === 'Refuse' && move > 0.9} pressed={choice === 'Refuse' && pressed}>Refuse</Button>
          </div>
        </Window>
        <div style={{
          position: 'absolute', left: 300, top: 150, transform: `rotate(-10deg) scale(${interpolate(stamp, [0, 1], [2.6, 1])})`, opacity: stamp,
          fontFamily: DISPLAY, fontSize: 130, letterSpacing: 12, color: C.red, border: `8px solid ${C.red}`, padding: '0 30px', background: 'rgba(7,9,13,0.4)',
        }}>{choice === 'Refuse' ? 'REFUSED' : 'ACCEPTED'}</div>
      </div>
      {t > 2.4 && <Cursor x={interpolate(move, [0, 1], [1500, target.x])} y={interpolate(move, [0, 1], [1000, target.y])} down={down} />}
    </AbsoluteFill>
  );
};
