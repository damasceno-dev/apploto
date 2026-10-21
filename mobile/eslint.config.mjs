import { defineConfig, globalIgnores } from 'eslint/config';
import expo from 'eslint-config-expo/flat.js';
import prettier from 'eslint-config-prettier/flat';
import root from '../eslint.config.mjs';
import { plugin as typescriptPlugin } from 'typescript-eslint';
import { rawColorRule } from './tooling/colors.mjs';

export default defineConfig(
  root,
  // Expo may resolve a different plugin copy. Keep its rules with the root plugin identity.
  expo.map((config) => ({
    ...config,
    ...(config.plugins?.['@typescript-eslint']
      ? { plugins: { ...config.plugins, '@typescript-eslint': typescriptPlugin } }
      : {}),
  })),
  globalIgnores(['.expo/**', 'dist/**', 'ios/**', 'android/**', 'expo-env.d.ts']),
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
          message: 'Use named exports outside Expo Router conventions.',
        },
        {
          selector: 'ExportNamedDeclaration > ExportSpecifier[exported.name="default"]',
          message: 'Use named exports.',
        },
      ],
    },
  },
  {
    files: ['src/app/**/{_layout,index,+not-found}.tsx'],
    rules: { 'no-restricted-syntax': 'off' },
  },
  { files: ['src/theme/token-adapter.ts'], rules: { 'loto/raw-color': 'off' } },
  {
    files: ['*.cjs'],
    languageOptions: {
      globals: { __dirname: 'readonly', module: 'readonly', require: 'readonly' },
    },
  },
  prettier,
);
