import lotoTokens from './tooling/token-css.mjs';

// The token plugin runs first: it expands `@loto-tokens;` into the generated theme before Tailwind.
const config = { plugins: [lotoTokens, '@tailwindcss/postcss'] };
export default config;
