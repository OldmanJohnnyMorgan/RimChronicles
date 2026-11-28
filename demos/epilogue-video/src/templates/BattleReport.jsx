import React from 'react';
import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Mech } from '../components/Mech.jsx';
import { Pawn, genericLook } from '../components/Pawn.jsx';
import { Backdrop, FactionBadge, Window } from '../components/Ui.jsx';
import { C, DISPLAY, LETTER, MONO, SANS, clamp, ease } from '../theme.js';
import { eventRevealTimes } from '../reveal.js';
import { dateOf } from '../timing.js';

// battle_report: one or more battles from the chronicle. Each battle event gets the panel in turn: both sides
// lined up, losses falling over, then the result stamped. Events without a battle (a death, a march) show as a banner,
// and the named pawn stands beside the panel.

const MAX_UNITS = 12;
const Unit = ({ kind, i, fallen, look }) => {
  // The fallen stay where they stood, greyed and tilted, so the rows keep their shape.
  const tilt = { transform: fallen ? `rotate(${i % 2 ? 14 : -14}deg)` : undefined, transformOrigin: '50% 95%', opacity: fallen ? 0.5 : 1 };
  if (kind === 'HollowChoir') return <div style={tilt}><Mech size={88} phase={i} glow={fallen ? '#2a2f33' : undefined} /></div>;
  const l = look || genericLook(i, kind === 'Empire' ? 'imperial' : 'colony');
  return <div style={tilt}><Pawn look={l} size={72} dead={fallen} still phase={i} helmet={kind === 'Empire'} /></div>;
};

const Side = ({ side, faction, label, progress, roster }) => {
  const n = Math.min(MAX_UNITS, side.count);
  const lostShown = Math.round(Math.min(n, (side.lost / side.count) * n) * progress);
  return (
    <div style={{ width: 640 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 12 }}>
        <FactionBadge faction={faction} size={60} />
        <div>
          <div style={{ fontFamily: SANS, fontSize: 22, color: C.mute, letterSpacing: 3 }}>{label}</div>
          <div style={{ fontFamily: SANS, fontWeight: 600, fontSize: 32, color: C.ink }}>{faction.name}</div>
        </div>
        <div style={{ marginLeft: 'auto', textAlign: 'right', fontFamily: MONO, color: C.ink }}>
          <div style={{ fontSize: 38 }}>{side.count}</div>
          <div style={{ fontSize: 22, color: side.lost ? C.red : C.mute }}>lost {Math.round(side.lost * progress)}</div>
        </div>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px 6px', minHeight: 230, alignContent: 'flex-start' }}>
        {Array.from({ length: n }, (_, i) => (
          <div key={i} style={{ width: 96, height: 108, display: 'flex', alignItems: 'flex-end', justifyContent: 'center' }}>
            <Unit kind={faction.kind} i={i} fallen={i >= n - lostShown} look={faction.kind === 'Colony' ? roster[i % roster.length] : undefined} />
          </div>
        ))}
      </div>
    </div>
  );
};

export const BattleReport = ({ shot, data, t }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const reveals = eventRevealTimes(shot, data);
  const events = reveals.map((r) => ({ ...data.byId[r.id], at: r.at }));
  const battles = events.filter((e) => e.battle);
  const battle = battles.filter((e) => t >= e.at).pop() || battles[0];
  // The first battle holds the panel from the start of the shot; later ones take it over when they land.
  const bStart = battles.indexOf(battle) === 0 ? Math.min(battle.at, 0.4) : battle.at;
  const bEnd = (battles[battles.indexOf(battle) + 1]?.at ?? shot.duration) - 0.2;
  const roster = data.chronicle.pawns.filter((p) => !p.died || p.died.day >= battle.day).map((p) => data.appearanceOf(p.id, battle.day));
  const att = battle.battle.attackers, def = battle.battle.defenders;
  const local = t - bStart;
  const fight = interpolate(local, [0.9, Math.max(1.6, bEnd - bStart - 2.2)], [0, 1], clamp);
  const stampAt = Math.max(1.8, bEnd - bStart - 1.8);
  const stamp = spring({ frame: f - (bStart + stampAt) * fps, fps, config: { damping: 10, stiffness: 160 } });
  const panelA = interpolate(t, [bStart, bStart + 0.5], [0, 1], { ...clamp, easing: ease });
  const title = shot.slots.title || battle.battle.name || battle.kind;
  const result = battle.battle.result === 'held' ? 'HELD' : 'FALLEN';
  const banner = events.filter((e) => t >= e.at && !e.battle).pop();
  const hero = [...events].reverse().find((e) => t >= e.at && e.pawns?.length);
  const heroId = hero?.pawns[0];
  const heroDead = heroId && data.byId[heroId].died && t >= (events.find((e) => e.id === data.byId[heroId].died.event)?.at ?? Infinity);
  const heroS = spring({ frame: f - (hero?.at ?? 0) * fps, fps, config: { damping: 14 } });
  const sideFaction = (s) => data.factionOf(s.faction);

  return (
    <AbsoluteFill>
      <Backdrop tint="rgba(200,49,43,0.16)" />
      <div style={{ position: 'absolute', left: 60, top: 70, opacity: panelA }}>
        <Window title={`Battle: ${title}`} accent={LETTER[battle.letter]} width={1420}>
          <div style={{ fontFamily: SANS, fontSize: 22, color: C.mute, letterSpacing: 3, marginBottom: 14 }}>
            DAY {battle.day} · {dateOf(battle.day).long.toUpperCase()}{battles.length > 1 ? ` · ${battles.indexOf(battle) + 1} OF ${battles.length}` : ''}
          </div>
          <div style={{ display: 'flex', gap: 40, alignItems: 'flex-start' }}>
            <Side side={att} faction={sideFaction(att)} label="ATTACKERS" progress={fight} roster={roster} />
            <div style={{ fontFamily: DISPLAY, fontSize: 64, color: C.mute, alignSelf: 'center' }}>VS</div>
            <Side side={def} faction={sideFaction(def)} label="DEFENDERS" progress={fight} roster={roster} />
          </div>
          <div style={{ fontFamily: SANS, fontSize: 28, color: C.ink, marginTop: 8, lineHeight: 1.3 }}>{battle.text}</div>
        </Window>
        <div style={{
          position: 'absolute', right: 70, top: 120, transform: `rotate(-12deg) scale(${interpolate(stamp, [0, 1], [2.4, 1])})`, opacity: stamp,
          fontFamily: DISPLAY, fontSize: 120, letterSpacing: 10, color: result === 'HELD' ? C.green : C.gold,
          border: `8px solid ${result === 'HELD' ? C.green : C.gold}`, padding: '0 28px', background: 'rgba(7,9,13,0.55)',
        }}>{result}</div>
      </div>
      {hero && (
        <div style={{ position: 'absolute', right: 70, top: 40, textAlign: 'center', opacity: heroS, transform: `translateX(${(1 - heroS) * 200}px)` }}>
          <Pawn look={data.appearanceOf(heroId, hero.day)} size={250} dead={heroDead} />
          <div style={{ fontFamily: SANS, fontWeight: 600, fontSize: 34, color: heroDead ? C.mute : C.ink, background: 'rgba(7,9,13,0.75)', padding: '4px 14px', marginTop: -10 }}>
            {data.byId[heroId].name}{heroDead ? ' †' : ''}
          </div>
        </div>
      )}
      {banner && (
        <div style={{ position: 'absolute', left: 60, top: 18, fontFamily: SANS, fontSize: 26, color: C.ink, display: 'flex', alignItems: 'center', gap: 12, opacity: interpolate(t, [banner.at, banner.at + 0.4], [0, 1], clamp) }}>
          <span style={{ width: 14, height: 14, background: LETTER[banner.letter] }} />
          <span style={{ color: C.mute, fontFamily: MONO }}>Day {banner.day}</span> {banner.text}
        </div>
      )}
    </AbsoluteFill>
  );
};
