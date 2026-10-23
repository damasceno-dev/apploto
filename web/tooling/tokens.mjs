// Writes (or with --check, verifies) src/theme/tokens.generated.ts — the names-only token module —
// from design/tokens through the adapter. `lint` runs the check, so a token change that adds,
// renames or removes a name fails until this module is regenerated.
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import prettier from 'prettier';
import {
  buildTokenNamesModule,
  loadTokens,
  readTokenDirectory,
} from '../src/theme/token-adapter.ts';
import { tokenDirectory } from './token-css.mjs';

const target = fileURLToPath(new URL('../src/theme/tokens.generated.ts', import.meta.url));
const source = buildTokenNamesModule(loadTokens(await readTokenDirectory(tokenDirectory)));
const formatted = await prettier.format(source, {
  ...(await prettier.resolveConfig(target)),
  filepath: target,
});

if (process.argv.includes('--check')) {
  const current = await readFile(target, 'utf8').catch(() => '');
  if (current !== formatted) {
    console.error(
      'src/theme/tokens.generated.ts is out of date: run `pnpm --filter @loto/web generate:tokens`.',
    );
    process.exitCode = 1;
  }
} else {
  await writeFile(target, formatted);
}
