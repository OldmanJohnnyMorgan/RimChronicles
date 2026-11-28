// Lookups over a chronicle export. Pure functions: used by the templates and the Node scripts alike.

export const makeData = (chronicle) => {
  const byId = {};
  for (const p of chronicle.pawns) byId[p.id] = { ...p, type: 'pawn' };
  for (const f of chronicle.factions) byId[f.id] = { ...f, type: 'faction' };
  for (const e of chronicle.entries) byId[e.id] = e;
  const appearanceOf = (pawnId, atDay = Infinity) => {
    const looks = chronicle.entries
      .filter((e) => e.type === 'appearance' && e.pawn === pawnId && e.day <= atDay)
      .sort((a, b) => a.day - b.day);
    return looks[looks.length - 1] || looks[0];
  };
  const factionOf = (id) => byId[id];
  return {
    chronicle,
    byId,
    header: chronicle.header,
    reporter: chronicle.header.reporter,
    appearanceOf,
    factionOf,
    resolve: (path) => path.split('.').reduce((o, k) => (o == null ? o : o[k]), chronicle),
    epilogueParagraphs: () => chronicle.sealed.epilogue.text.split(/\n\s*\n/).map((p) => p.trim()),
    /** Every entity name, for the invented-names check. */
    names: () => [
      ...chronicle.pawns.map((p) => p.name),
      ...chronicle.factions.flatMap((f) => [f.name, f.name.replace(/^The /, ''), f.self_name].filter(Boolean)),
      chronicle.header.colony,
      chronicle.header.reporter.name,
    ],
  };
};

/** The day a shot is "set" on: its latest referenced entry. */
export const shotDay = (shot, data) => {
  const s = shot.slots;
  const ids = [s.event, s.bulletin, ...(s.events || [])].filter(Boolean);
  const days = ids.map((id) => data.byId[id]?.day).filter((d) => d != null);
  if (shot.template === 'ending_card' || shot.template === 'memorial_roll' || shot.template === 'credits')
    return data.chronicle.sealed.day;
  return days.length ? Math.max(...days) : null;
};

/** Events referenced by a shot, in order. */
export const shotEvents = (shot, data) => {
  const s = shot.slots;
  return [s.event, ...(s.events || [])].filter(Boolean).map((id) => data.byId[id]).filter(Boolean);
};
