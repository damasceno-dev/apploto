// Tailwind 3 / NativeWind theme from design/tokens (Mobile M3). Colors are CSS variables whose
// per-theme values the ThemeProvider supplies through NativeWind `vars()`; the class names are the
// semantic/domain token paths (`bg-sem-surface-default`). The default palette stays cleared.
const path = require('node:path');
const {
  colorVariableNames,
  loadTokens,
  nativeTokens,
  readTokenDirectory,
} = require('./tooling/token-resolver.mjs');

const values = loadTokens(readTokenDirectory(path.join(__dirname, '..', 'design', 'tokens')));
const tokens = nativeTokens(values);
const flatten = (node, prefix = '') =>
  Object.fromEntries(
    Object.entries(node).flatMap(([key, value]) =>
      typeof value === 'object'
        ? Object.entries(flatten(value, `${prefix}${key}-`))
        : [[`${prefix}${key}`, `${value}px`]],
    ),
  );

module.exports = {
  content: ['./src/**/*.{ts,tsx}'],
  presets: [require('nativewind/preset')],
  theme: {
    colors: Object.fromEntries(colorVariableNames(values).map((name) => [name, `var(--${name})`])),
    // The 4px scale keeps its numeric keys (`p-4` = 16 dp); named uses read `p-inset-panel`.
    spacing: { ...flatten(tokens.spacing.scale), ...flatten({ ...tokens.spacing, scale: {} }) },
    borderRadius: flatten(tokens.radius),
    fontSize: Object.fromEntries(
      Object.entries(tokens.typography).map(([role, spec]) => [
        role,
        [
          `${spec.fontSize}px`,
          { lineHeight: `${spec.lineHeight}px`, letterSpacing: `${spec.letterSpacing}px` },
        ],
      ]),
    ),
    extend: {},
  },
  plugins: [],
};
