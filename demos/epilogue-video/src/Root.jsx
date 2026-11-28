import React from 'react';
import { Composition } from 'remotion';
import './fonts.js';
import chronicle from '../chronicle/chronicle-export.json';
import manifest from '../manifest.json';
import { totalSeconds } from './timing.js';
import { Video } from './Video.jsx';

const size = manifest.format === '9:16' ? { width: 1080, height: 1920 } : { width: 1920, height: 1080 };

export const RemotionRoot = () => (
  <Composition
    id="Epilogue"
    component={Video}
    durationInFrames={Math.round(totalSeconds(manifest) * manifest.fps)}
    fps={manifest.fps}
    {...size}
    defaultProps={{ manifest, chronicle }}
  />
);
