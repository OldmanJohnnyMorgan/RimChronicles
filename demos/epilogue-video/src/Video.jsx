import React, { useMemo } from 'react';
import { AbsoluteFill, Audio, Sequence, staticFile, useCurrentFrame, useVideoConfig } from 'remotion';
import { Captions, DateReadout, LetterStack, ShotFade } from './components/Ui.jsx';
import { makeData, shotDay } from './data.js';
import { eventRevealTimes } from './reveal.js';
import { timeline } from './timing.js';
import { BattleReport } from './templates/BattleReport.jsx';
import { ColonyCard } from './templates/ColonyCard.jsx';
import { Credits } from './templates/Credits.jsx';
import { EmpireMap } from './templates/EmpireMap.jsx';
import { EndingCard } from './templates/EndingCard.jsx';
import { EventTimeline } from './templates/EventTimeline.jsx';
import { LetterCard } from './templates/LetterCard.jsx';
import { MemorialRoll } from './templates/MemorialRoll.jsx';
import { NewsBulletin } from './templates/NewsBulletin.jsx';
import { PawnTribute } from './templates/PawnTribute.jsx';
import { TitleCard } from './templates/TitleCard.jsx';

const TEMPLATES = {
  news_bulletin: NewsBulletin, title_card: TitleCard, colony_card: ColonyCard, empire_map: EmpireMap,
  event_timeline: EventTimeline, battle_report: BattleReport, letter_card: LetterCard, pawn_tribute: PawnTribute,
  ending_card: EndingCard, memorial_roll: MemorialRoll, credits: Credits,
};
// Shots that draw their own text or chrome get no game overlays (letters, date) or subtitles.
const NO_OVERLAY = new Set(['news_bulletin', 'title_card', 'ending_card', 'credits']);
const OWN_CAPTIONS = new Set(['news_bulletin', 'ending_card']);

const LETTER_LABEL = {
  Founding: 'Colony founded', Raid: 'Raid', PowerWoke: 'A power wakes', TitleGranted: 'Title granted', Beat: 'Envoy',
  Independence: 'Independence', CrisisBegan: 'Crisis', Death: 'Death', WarInHeaven: 'War in Heaven', Siege: 'Siege',
  CapitalFell: 'Capital fallen', TreatyRefused: 'Treaty', FinalPush: 'Final push', Ending: 'Ending',
};

const Shot = ({ entry, data }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  const t = f / fps;
  const T = TEMPLATES[entry.shot.template];
  const frames = Math.round(entry.shot.duration * fps);
  const overlay = !NO_OVERLAY.has(entry.shot.template);
  return (
    <ShotFade frames={frames}>
      <T shot={entry.shot} data={data} lines={entry.lines} t={t} />
      {overlay && <DateReadout day={shotDay(entry.shot, data)} />}
      {!OWN_CAPTIONS.has(entry.shot.template) && <Captions lines={entry.lines} speaker={data.reporter.name} t={t} right={overlay ? 380 : 0} />}
    </ShotFade>
  );
};

// Props arrive as JSON, so the lookups (which hold functions) are built here.
export const Video = ({ manifest, chronicle }) => {
  const { fps } = useVideoConfig();
  const data = useMemo(() => makeData(chronicle), [chronicle]);
  const tl = timeline(manifest, data);
  // Letters pile up across the whole video, as they would in a game.
  const letters = tl.flatMap((s) => eventRevealTimes(s.shot, data).map((r) => ({
    id: r.id, letter: r.letter, atFrame: Math.round((s.start + r.at) * fps),
    label: LETTER_LABEL[data.byId[r.id].kind] || data.byId[r.id].kind,
  })));
  // Hide the stack during shots without game overlays.
  const overlayRanges = tl.filter((s) => !NO_OVERLAY.has(s.shot.template)).map((s) => [Math.round(s.start * fps), Math.round(s.end * fps)]);
  return (
    <AbsoluteFill style={{ background: '#07090d' }}>
      {tl.map((s) => (
        <Sequence key={s.index} from={Math.round(s.start * fps)} durationInFrames={Math.round(s.shot.duration * fps)}>
          <Shot entry={s} data={data} />
        </Sequence>
      ))}
      {overlayRanges.map(([a, b]) => (
        <Sequence key={a} from={a} durationInFrames={b - a} layout="none">
          <OffsetStack letters={letters} offset={a} frames={b - a} />
        </Sequence>
      ))}
      <Audio src={staticFile('audio/music.wav')} />
    </AbsoluteFill>
  );
};

// LetterStack reads the frame inside its Sequence, so shift the letters into that Sequence's time.
const OffsetStack = ({ letters, offset, frames }) => {
  const f = useCurrentFrame();
  const fade = Math.min(1, f / 10, (frames - f) / 10);
  return (
    <AbsoluteFill style={{ opacity: fade }}>
      <LetterStack letters={letters.map((l) => ({ ...l, atFrame: l.atFrame - offset }))} />
    </AbsoluteFill>
  );
};
