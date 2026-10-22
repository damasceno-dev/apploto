import { defineConfig, globalIgnores } from 'eslint/config';
import next from 'eslint-config-next/core-web-vitals';
import prettier from 'eslint-config-prettier/flat';
import root from '../eslint.config.mjs';
import { rawColorRule } from './tooling/colors.mjs';

export default defineConfig(
  root,
  next,
  globalIgnores([
    '.next/**',
    '.next-e2e/**',
    'out/**',
    'next-env.d.ts',
    'playwright-report/**',
    'test-results/**',
    'e2e/.state/**',
  ]),
  {
    files: ['**/*.{ts,tsx}', '*.{mjs,cjs}'],
    plugins: { loto: { rules: { 'raw-color': rawColorRule } } },
    rules: {
      'loto/raw-color': 'error',
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'ExportDefaultDeclaration',
          message: 'Use named exports outside framework conventions.',
        },
        {
          selector: 'ExportNamedDeclaration > ExportSpecifier[exported.name="default"]',
          message: 'Use named exports.',
        },
      ],
    },
  },
  {
    files: [
      'src/app/**/{page,layout,loading,error,global-error,not-found}.tsx',
      'src/app/**/{page,layout}.e2e.tsx',
    ],
    rules: { 'no-restricted-syntax': 'off' },
  },
  { files: ['src/theme/token-adapter.ts'], rules: { 'loto/raw-color': 'off' } },
  prettier,
);
