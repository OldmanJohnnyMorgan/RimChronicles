import { Easing } from 'remotion';

// RCN palette (spec §5.1) plus a RimWorld-like UI palette: dark slate windows, thin light borders, letter colours.
export const C = {
  bg0: '#07090d', bg1: '#121822', red: '#c8312b', redDark: '#7d1a16',
  gold: '#e3b04b', ink: '#f2efe8', mute: '#8f98a6', line: 'rgba(242,239,232,0.12)',
  win: 'rgba(28,31,35,0.94)', winHi: '#2a2e33', winEdge: '#6b7077', winEdgeDark: '#101214',
  tan: '#b9a77f', green: '#7fb069', blue: '#6a95c4', teal: '#3fd0c9', purple: '#8a5cc2',
};
export const LETTER = { threat: '#c8312b', death: '#7d7f86', positive: '#7fb069', neutral: '#6a95c4' };

export const DISPLAY = 'Anton, sans-serif';
export const SANS = '"Fira Sans", sans-serif';
export const MONO = '"Anonymous Pro", monospace';

export const clamp = { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' };
export const ease = Easing.bezier(0.22, 1, 0.36, 1);

export const OUT = '#16120f';
