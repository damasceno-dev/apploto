// PostCSS plugin (Web M3 Phase 1): replaces the `@loto-tokens;` marker in the app stylesheet with
// the CSS the token adapter generates from design/tokens, before Tailwind processes the file. Token
// values therefore exist only in the design JSON and in build output — never in a source CSS file,
// so the raw-color gate needs no CSS exemption. Each token file is reported as a dependency.
import { existsSync } from 'node:fs';
import path from 'node:path';
import postcss from 'postcss';
import {
  buildTokenCss,
  loadTokens,
  readTokenDirectory,
  tokenFiles,
} from '../src/theme/token-adapter.ts';

/**
 * `design/tokens`. Next bundles the PostCSS config (a module URL is not a file path there), so the
 * directory is found from the working directory: the app directory for every app script, or the
 * repository root. A missing directory fails the build naming the places looked at.
 */
export const tokenDirectory = (() => {
  const candidates = [
    path.resolve(process.cwd(), '../design/tokens'),
    path.resolve(process.cwd(), 'design/tokens'),
  ];
  const found = candidates.find((candidate) => existsSync(path.join(candidate, 'colors.json')));
  if (found === undefined) {
    throw new Error(`design/tokens not found (looked in ${candidates.join(', ')}).`);
  }
  return found;
})();

export async function generatedTokenCss() {
  return buildTokenCss(loadTokens(await readTokenDirectory(tokenDirectory)));
}

export default function lotoTokens() {
  return {
    postcssPlugin: 'loto-tokens',
    async Once(root, { result }) {
      const markers = [];
      root.walkAtRules('loto-tokens', (rule) => {
        markers.push(rule);
      });
      if (markers.length === 0) return;
      if (markers.length > 1) throw new Error('@loto-tokens may appear once.');
      const generated = postcss.parse(await generatedTokenCss(), { from: undefined });
      markers[0].replaceWith(generated.nodes);
      for (const name of Object.keys(tokenFiles)) {
        result.messages.push({
          type: 'dependency',
          plugin: 'loto-tokens',
          file: path.join(tokenDirectory, name),
          parent: result.opts.from,
        });
      }
    },
  };
}
lotoTokens.postcss = true;
