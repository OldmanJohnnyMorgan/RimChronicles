// Fonts ship with the project (spec §6): Fontsource packages, bundled by Remotion's webpack.
// The render waits until every face has loaded, so no frame falls back to a system font.
import { continueRender, delayRender } from 'remotion';
import '@fontsource/anton/400.css';
import '@fontsource/fira-sans/300.css';
import '@fontsource/fira-sans/400.css';
import '@fontsource/fira-sans/400-italic.css';
import '@fontsource/fira-sans/500.css';
import '@fontsource/fira-sans/600.css';
import '@fontsource/fira-sans/700.css';
import '@fontsource/anonymous-pro/400.css';
import '@fontsource/anonymous-pro/700.css';

const FACES = ['400 40px Anton', '300 40px "Fira Sans"', '400 40px "Fira Sans"', 'italic 400 40px "Fira Sans"',
  '500 40px "Fira Sans"', '600 40px "Fira Sans"', '700 40px "Fira Sans"', '400 40px "Anonymous Pro"', '700 40px "Anonymous Pro"'];

if (typeof document !== 'undefined') {
  const handle = delayRender('Loading fonts');
  Promise.all(FACES.map((f) => document.fonts.load(f)))
    .then(() => continueRender(handle))
    .catch((err) => { console.error('Font load failed', err); continueRender(handle); });
}
