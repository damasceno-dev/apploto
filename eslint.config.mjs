// Root ESLint 9 flat config (Shared M1, Phase 2). Workspace packages inherit it:
// ESLint resolves this file by walking up from each package's working directory.
//
// Scope: TypeScript files in `shared/` and the future `web/` and `mobile/` apps. The
// non-TS areas are ignored below. TS files are matched with `**/*` globs rather than
// per-area paths because `files` globs resolve relative to the config that applies them;
// this keeps the layer valid when `web/` and `mobile/` import it into their own configs.
//
// React, Next, and Expo lint layers are app-owned and live in `web/eslint.config.mjs`
// and `mobile/eslint.config.mjs` — never here.
import js from '@eslint/js';
import prettier from 'eslint-config-prettier/flat';
import { defineConfig, globalIgnores } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig(
  globalIgnores(['server/', 'design/', 'docs/', 'infra/', '**/dist/', '**/coverage/']),
  {
    name: 'loto/typescript',
    files: ['**/*.{ts,tsx,mts,cts}'],
    extends: [js.configs.recommended, tseslint.configs.recommended],
  },
  // Must stay last: turns off every rule that conflicts with Prettier.
  prettier,
);
