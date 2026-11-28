import React from 'react';
import { AbsoluteFill, Sequence, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Pawn } from '../components/Pawn.jsx';
import { Captions } from '../components/Ui.jsx';
import { C, DISPLAY, MONO, SANS, clamp, ease } from '../theme.js';

// news_bulletin (spec §5.1), from the approved RCN intro. Outlet, reporter and every text come from the chronicle:
// the game's outlet is "the Rim Dispatch" and its reporter Ilse Varrow, so the brand follows them.
// variant "full": logo slam first (once per video); "short": straight to the desk.

const brand = (outlet) => {
  const words = outlet.replace(/^the\s+/i, '').toUpperCase().split(' ');
  return { first: words.slice(0, -1).join(' '), last: words[words.length - 1], initials: words.map((w) => w[0]).join('') };
};
const Logo = ({ b, size }) => (
  <span style={{ fontFamily: DISPLAY, fontSize: size, letterSpacing: size * 0.06, lineHeight: 0.95, color: C.ink }}>
    {b.first} <span style={{ color: C.red }}>{b.last}</span>
  </span>
);

const Set = () => {
  const f = useCurrentFrame();
  return (
    <AbsoluteFill style={{ background: `radial-gradient(ellipse at 60% 45%, ${C.bg1} 0%, ${C.bg0} 70%)` }}>
      <AbsoluteFill style={{
        backgroundImage: `linear-gradient(${C.line} 1px, transparent 1px), linear-gradient(90deg, ${C.line} 1px, transparent 1px)`,
        backgroundSize: '80px 80px', opacity: 0.35,
        maskImage: 'radial-gradient(ellipse at center, black 30%, transparent 75%)', WebkitMaskImage: 'radial-gradient(ellipse at center, black 30%, transparent 75%)',
      }} />
      <div style={{
        position: 'absolute', width: 900, height: 900, right: -180, top: 90, borderRadius: '50%', opacity: 0.55,
        background: 'radial-gradient(circle at 35% 35%, #2a3444 0%, #0c1017 62%)',
        boxShadow: 'inset -40px -30px 120px rgba(0,0,0,0.8), 0 0 120px rgba(200,49,43,0.18)', transform: `rotate(${f * 0.05}deg)`,
      }} />
      <div style={{ position: 'absolute', width: 1500, height: 260, right: -480, top: 410, borderRadius: '50%', border: '2px solid rgba(227,176,75,0.35)', transform: 'rotate(-12deg)' }} />
      <AbsoluteFill style={{ backgroundImage: 'repeating-linear-gradient(0deg, rgba(0,0,0,0.18) 0 2px, transparent 2px 4px)', opacity: 0.4 }} />
      <AbsoluteFill style={{ boxShadow: 'inset 0 0 260px rgba(0,0,0,0.85)' }} />
    </AbsoluteFill>
  );
};

const LogoSlam = ({ b, tagline }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const s = spring({ frame: f, fps, config: { damping: 12, stiffness: 140, mass: 0.8 } });
  const bar = interpolate(f, [6, 26], [0, 1], { ...clamp, easing: ease });
  const tag = interpolate(f, [30, 50], [0, 1], clamp);
  const out = interpolate(f, [80, 105], [0, 1], { ...clamp, easing: ease });
  const flash = interpolate(f, [0, 3, 12], [0, 0.55, 0], clamp);
  return (
    <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center' }}>
      <AbsoluteFill style={{ background: '#fff', opacity: flash }} />
      <div style={{
        display: 'flex', flexDirection: 'column', alignItems: 'center', opacity: 1 - out,
        transform: `translate(${-640 * out}px, ${-420 * out}px) scale(${interpolate(s, [0, 1], [2.2, 1]) * (1 - 0.7 * out)})`,
      }}>
        <Logo b={b} size={210} />
        <div style={{ height: 14, width: 1100 * bar, background: C.red, marginTop: 18, boxShadow: `0 0 30px ${C.red}` }} />
        <div style={{ fontFamily: SANS, fontStyle: 'italic', fontWeight: 300, fontSize: 38, color: C.gold, marginTop: 24, opacity: tag }}>{tagline}</div>
      </div>
    </AbsoluteFill>
  );
};

const Chrome = ({ b, d }) => {
  const f = useCurrentFrame();
  const a = interpolate(f, [0, 12], [0, 1], clamp);
  const blink = Math.floor(f / 15) % 2 === 0 ? 1 : 0.35;
  return (
    <AbsoluteFill style={{ opacity: a }}>
      <div style={{ position: 'absolute', left: 70, top: 56, display: 'flex', alignItems: 'center', gap: 18 }}>
        <Logo b={b} size={64} />
        <div style={{ fontFamily: SANS, fontWeight: 700, fontSize: 22, color: '#fff', background: C.red, padding: '6px 14px', letterSpacing: 4, display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ width: 12, height: 12, borderRadius: 6, background: '#fff', opacity: blink }} /> LIVE
        </div>
      </div>
      <div style={{ position: 'absolute', right: 70, top: 62, textAlign: 'right', fontFamily: MONO, color: C.ink }}>
        <div style={{ fontSize: 30 }}>{d.dateline}</div>
        <div style={{ fontSize: 22, color: C.mute, marginTop: 6 }}>{d.location}</div>
      </div>
    </AbsoluteFill>
  );
};

const Ticker = ({ items }) => {
  const f = useCurrentFrame();
  const rise = interpolate(f, [0, 18], [80, 0], { ...clamp, easing: ease });
  const text = items.join('   ◆   ');
  return (
    <div style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: 64, transform: `translateY(${rise}px)`, display: 'flex', background: 'rgba(7,9,13,0.92)', borderTop: `3px solid ${C.red}` }}>
      <div style={{ background: C.red, color: '#fff', fontFamily: DISPLAY, fontSize: 32, letterSpacing: 4, padding: '0 28px', display: 'flex', alignItems: 'center', zIndex: 2, boxShadow: '12px 0 24px rgba(0,0,0,0.6)' }}>RIM WIRE</div>
      <div style={{ position: 'relative', flex: 1, overflow: 'hidden' }}>
        <div style={{ position: 'absolute', top: 0, bottom: 0, display: 'flex', alignItems: 'center', whiteSpace: 'nowrap', transform: `translateX(${1700 - f * 5}px)`, fontFamily: SANS, fontWeight: 500, fontSize: 30, color: C.ink }}>
          {text}   ◆   {text}
        </div>
      </div>
    </div>
  );
};

const LowerThird = ({ reporter, initials }) => {
  const f = useCurrentFrame();
  const bar = interpolate(f, [0, 16], [0, 1], { ...clamp, easing: ease });
  const txt = interpolate(f, [10, 26], [0, 1], clamp);
  const out = interpolate(f, [110, 125], [0, 1], clamp);
  return (
    <div style={{ position: 'absolute', left: 70, top: 690, opacity: 1 - out }}>
      <div style={{ background: C.ink, color: C.bg0, padding: '12px 30px 8px', fontFamily: DISPLAY, fontSize: 52, letterSpacing: 2, clipPath: `inset(0 ${100 - bar * 100}% 0 0)` }}>
        {reporter.name.toUpperCase()}
      </div>
      <div style={{ background: C.red, color: '#fff', padding: '8px 30px', fontFamily: SANS, fontWeight: 600, fontSize: 24, letterSpacing: 6, display: 'inline-block', opacity: txt }}>
        {reporter.title.toUpperCase()} · {initials}
      </div>
    </div>
  );
};

const Headline = ({ d, label }) => {
  const f = useCurrentFrame();
  const a = interpolate(f, [0, 18], [0, 1], { ...clamp, easing: ease });
  const k = interpolate(f, [20, 36], [0, 1], clamp);
  return (
    <div style={{ position: 'absolute', left: 70, top: 250, width: 1080, transform: `translateX(${(1 - a) * -80}px)`, opacity: a }}>
      <div style={{ display: 'inline-block', fontFamily: SANS, fontWeight: 700, fontSize: 26, letterSpacing: 6, color: C.bg0, background: C.gold, padding: '6px 16px' }}>{label}</div>
      <div style={{ fontFamily: DISPLAY, fontSize: 112, lineHeight: 1.02, color: C.ink, marginTop: 20, textShadow: '0 8px 30px rgba(0,0,0,0.7)' }}>{d.headline}</div>
      <div style={{ fontFamily: SANS, fontStyle: 'italic', fontSize: 46, color: C.gold, marginTop: 16, opacity: k }}>{d.kicker}</div>
    </div>
  );
};

const Reporter = ({ look, lines, startSec, initials }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const t = f / fps + startSec;
  const s = spring({ frame: f, fps, config: { damping: 15, stiffness: 90 } });
  const talking = lines.some((l) => t >= l.start && t < l.end - 0.3);
  return (
    <div style={{ position: 'absolute', right: 150, bottom: 50, transform: `translateX(${interpolate(s, [0, 1], [500, 0])}px)` }}>
      <div style={{ position: 'absolute', left: -120, right: -120, top: 40, bottom: -10, background: 'radial-gradient(ellipse at 50% 55%, rgba(227,176,75,0.22) 0%, rgba(200,49,43,0.10) 40%, transparent 70%)' }} />
      <Pawn look={look} talking={talking} size={540} label={initials} />
    </div>
  );
};

export const NewsBulletin = ({ shot, data, lines, t }) => {
  const { fps } = useVideoConfig();
  const d = data.byId[shot.slots.bulletin];
  const reporter = data.reporter;
  const b = brand(reporter.outlet);
  const look = data.appearanceOf('reporter', d.day);
  const full = shot.slots.variant === 'full';
  const desk = full ? Math.round(3.6 * fps) : 0;
  return (
    <AbsoluteFill style={{ background: C.bg0 }}>
      <Set />
      {full && <Sequence durationInFrames={desk + 10}><LogoSlam b={b} tagline="Every three days. Every corner of the rim." /></Sequence>}
      <Sequence from={desk}><Chrome b={b} d={d} /></Sequence>
      <Sequence from={desk + 6}><Reporter look={look} lines={lines} startSec={desk / fps + 0.2} initials={b.initials} /></Sequence>
      <Sequence from={desk}><Ticker items={d.ticker} /></Sequence>
      <Sequence from={desk + 14} durationInFrames={130}><LowerThird reporter={reporter} initials={b.initials} /></Sequence>
      <Sequence from={desk + (full ? 40 : 8)}><Headline d={d} label={full ? 'TONIGHT' : 'BULLETIN'} /></Sequence>
      <Captions lines={lines} speaker={reporter.name} t={t} />
    </AbsoluteFill>
  );
};
