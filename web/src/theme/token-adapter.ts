// The web token adapter (Web M3 Phase 1): the single module that reads `design/tokens/*.json`. It
// runs at build time only — inside the PostCSS step (`tooling/token-css.mjs`) and the names
// generator (`tooling/tokens.mjs`) — and never ships to the browser. It validates the source,
// resolves `{path.to.token}` references and emits CSS whose app-facing names are semantic and domain
// tokens only: primitives are resolved here and never get a name of their own. It is the only file
// exempt from the raw-color lint rule. Pure TypeScript with erasable syntax, so Node runs it as is.

/** The eight token files and the one top-level group each must hold. */
export const tokenFiles = Object.freeze({
  'breakpoints.json': 'breakpoint',
  'colors.json': 'color',
  'layers.json': 'layer',
  'motion.json': 'motion',
  'radii.json': 'radius',
  'shadows.json': 'shadow',
  'spacing.json': 'spacing',
  'typography.json': 'typography',
});

export type TokenErrorCode =
  | 'missing-file'
  | 'unexpected-file'
  | 'invalid-json'
  | 'bad-group'
  | 'bad-key'
  | 'embedded-reference'
  | 'missing-reference'
  | 'group-reference'
  | 'cycle'
  | 'primitive-reference'
  | 'semantic-literal'
  | 'cross-theme'
  | 'layer-violation'
  | 'theme-asymmetry'
  | 'invalid-value'
  | 'name-collision';

export class TokenError extends Error {
  readonly code: TokenErrorCode;
  constructor(code: TokenErrorCode, message: string) {
    super(`${code}: ${message}`);
    this.code = code;
    this.name = 'TokenError';
  }
}

type Json = string | number | boolean | null | readonly Json[] | { readonly [key: string]: Json };
type Leaf = string | number | readonly (string | number)[];

/** A validated, fully resolved token set: every leaf is a literal. */
export interface ResolvedTokens {
  /** Dot path → resolved literal, for every leaf outside `$` metadata. */
  readonly values: ReadonlyMap<string, Leaf>;
  /** Dot path → the path its value was written as referencing (before resolution), if any. */
  readonly references: ReadonlyMap<string, string>;
  /** Domain groups: name → backend enum (or null for presentation groups) and value → parts. */
  readonly domains: ReadonlyMap<
    string,
    { readonly enumName: string | null; readonly values: ReadonlyMap<string, readonly string[]> }
  >;
}

const keyPattern = /^[A-Za-z0-9-]+$/;
const referencePattern = /^\{([^{}]+)\}$/;
const colorPattern = /^#[0-9a-f]{6}(?:[0-9a-f]{2})?$/;
const themes = ['light', 'dark'] as const;
type Theme = (typeof themes)[number];

/** Parses the eight file texts (`fileName → JSON text`) into one tree, checking the file set. */
export function parseTokenSources(sources: Readonly<Record<string, string>>): Record<string, Json> {
  const expected = Object.keys(tokenFiles);
  for (const name of Object.keys(sources)) {
    if (!expected.includes(name))
      throw new TokenError('unexpected-file', `${name} is not a token file.`);
  }
  const tree: Record<string, Json> = {};
  for (const [name, group] of Object.entries(tokenFiles)) {
    const text = sources[name];
    if (text === undefined) throw new TokenError('missing-file', `${name} is missing.`);
    let parsed: unknown;
    try {
      parsed = JSON.parse(text);
    } catch (error) {
      throw new TokenError('invalid-json', `${name}: ${(error as Error).message}`);
    }
    if (!isObject(parsed)) throw new TokenError('bad-group', `${name} must hold an object.`);
    const groups = Object.keys(parsed).filter((key) => !key.startsWith('$'));
    if (groups.length !== 1 || groups[0] !== group) {
      throw new TokenError('bad-group', `${name} must hold exactly the "${group}" group.`);
    }
    tree[group] = parsed[group] as Json;
  }
  return tree;
}

/** Validates layering and resolves every reference. Throws a `TokenError` on the first problem. */
export function resolveTokens(tree: Record<string, Json>): ResolvedTokens {
  const raw = new Map<string, Leaf>();
  const groups = new Set<string>();
  collectLeaves(tree, '', raw, groups);
  checkColorStructure(tree);

  const values = new Map<string, Leaf>();
  const references = new Map<string, string>();
  const resolve = (path: string, chain: readonly string[]): Leaf => {
    const done = values.get(path);
    if (done !== undefined) return done;
    const value = raw.get(path);
    if (value === undefined) {
      if (groups.has(path)) {
        throw new TokenError(
          'group-reference',
          `${chain.at(-1) ?? path} references the group ${path}.`,
        );
      }
      throw new TokenError(
        'missing-reference',
        `${chain.at(-1) ?? path} references ${path}, which does not exist.`,
      );
    }
    if (chain.includes(path)) {
      throw new TokenError('cycle', [...chain, path].join(' → '));
    }
    const target = referenceOf(value);
    if (target === null) {
      values.set(path, value);
      return value;
    }
    checkLayering(path, target);
    if (!references.has(path)) references.set(path, target);
    const resolved = resolve(target, [...chain, path]);
    values.set(path, resolved);
    return resolved;
  };
  for (const path of raw.keys()) resolve(path, []);

  for (const [path, value] of values) checkValue(path, value);
  return { values, references, domains: domainsOf(tree) };
}

/** Reads the eight files from a directory (build time only). */
export async function readTokenDirectory(directory: string): Promise<Record<string, string>> {
  const { readdir, readFile } = await import('node:fs/promises');
  const path = await import('node:path');
  const sources: Record<string, string> = {};
  for (const name of await readdir(directory)) {
    if (!name.endsWith('.json')) continue;
    sources[name] = await readFile(path.join(directory, name), 'utf8');
  }
  return sources;
}

export function loadTokens(sources: Readonly<Record<string, string>>): ResolvedTokens {
  return resolveTokens(parseTokenSources(sources));
}

function collectLeaves(node: Json, path: string, leaves: Map<string, Leaf>, groups: Set<string>) {
  if (!isObject(node)) {
    if (path === '') throw new TokenError('invalid-value', 'The token tree must be an object.');
    leaves.set(path, asLeaf(path, node));
    return;
  }
  if (path !== '') groups.add(path);
  for (const [key, child] of Object.entries(node)) {
    if (key.startsWith('$')) continue;
    if (!keyPattern.test(key)) throw new TokenError('bad-key', `${path ? `${path}.` : ''}${key}`);
    collectLeaves(child, path === '' ? key : `${path}.${key}`, leaves, groups);
  }
}

function asLeaf(path: string, value: Json): Leaf {
  if (typeof value === 'string') {
    if ((value.includes('{') || value.includes('}')) && !referencePattern.test(value)) {
      throw new TokenError(
        'embedded-reference',
        `${path}: "${value}" — a reference must be the whole value.`,
      );
    }
    return value;
  }
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (
    Array.isArray(value) &&
    value.every(
      (item) => typeof item === 'string' || (typeof item === 'number' && Number.isFinite(item)),
    )
  ) {
    if (
      value.some((item) => typeof item === 'string' && (item.includes('{') || item.includes('}')))
    ) {
      throw new TokenError('embedded-reference', `${path}: references cannot sit inside a list.`);
    }
    return value as readonly (string | number)[];
  }
  throw new TokenError('invalid-value', `${path} has an unsupported value.`);
}

function referenceOf(value: Leaf): string | null {
  return typeof value === 'string' ? (referencePattern.exec(value)?.[1] ?? null) : null;
}

function themeOf(path: string): Theme | null {
  const parts = path.split('.');
  if (parts[0] === 'color' && (parts[1] === 'semantic' || parts[1] === 'domain')) {
    return parts[2] === 'light' || parts[2] === 'dark' ? parts[2] : null;
  }
  if (parts[0] === 'shadow') return parts[1] === 'light' || parts[1] === 'dark' ? parts[1] : null;
  return null;
}

/**
 * Layering (tokens README): primitives are literals; semantic tokens reference primitives or
 * same-theme semantic tokens; domain tokens reference same-theme semantic tokens or primitives;
 * other categories reach color only through a same-theme semantic token.
 */
function checkLayering(path: string, target: string) {
  const layer = path.split('.').slice(0, 2).join('.');
  const targetLayer = target.split('.').slice(0, 2).join('.');
  const sourceTheme = themeOf(path);
  const targetTheme = themeOf(target);
  if (layer === 'color.primitive') {
    throw new TokenError('layer-violation', `${path}: primitives are literals.`);
  }
  if (sourceTheme !== null && targetTheme !== null && sourceTheme !== targetTheme) {
    throw new TokenError(
      'cross-theme',
      `${path} (${sourceTheme}) references ${target} (${targetTheme}).`,
    );
  }
  if (layer === 'color.semantic') {
    if (targetLayer !== 'color.primitive' && targetLayer !== 'color.semantic') {
      throw new TokenError(
        'layer-violation',
        `${path} may reference only primitives or semantic tokens.`,
      );
    }
    return;
  }
  if (layer === 'color.domain') {
    if (targetLayer !== 'color.primitive' && targetLayer !== 'color.semantic') {
      throw new TokenError(
        'layer-violation',
        `${path} may reference only semantic tokens or primitives.`,
      );
    }
    return;
  }
  if (target.startsWith('color.')) {
    if (targetLayer !== 'color.semantic') {
      throw new TokenError(
        'layer-violation',
        `${path} reaches color only through a semantic token.`,
      );
    }
    if (sourceTheme === null) {
      throw new TokenError('cross-theme', `${path} is theme-independent but references ${target}.`);
    }
  }
}

/** Both themes present with identical names; semantic and domain values are references. */
function checkColorStructure(tree: Record<string, Json>) {
  const color = tree.color;
  if (!isObject(color)) throw new TokenError('bad-group', 'color must be an object.');
  for (const layer of ['semantic', 'domain'] as const) {
    const node = color[layer];
    if (!isObject(node)) throw new TokenError('bad-group', `color.${layer} is missing.`);
    const keys = Object.keys(node)
      .filter((key) => !key.startsWith('$'))
      .sort();
    if (keys.join() !== 'dark,light') {
      throw new TokenError('theme-asymmetry', `color.${layer} must hold exactly light and dark.`);
    }
    const light = leafPaths(node.light, '');
    const dark = leafPaths(node.dark, '');
    const missing = [...light].filter((path) => !dark.has(path));
    const extra = [...dark].filter((path) => !light.has(path));
    if (missing.length > 0 || extra.length > 0) {
      throw new TokenError(
        'theme-asymmetry',
        `color.${layer}: ${[...missing.map((path) => `dark lacks ${path}`), ...extra.map((path) => `light lacks ${path}`)].join('; ')}`,
      );
    }
    for (const theme of themes) {
      for (const path of leafPaths(node[theme], `color.${layer}.${theme}`)) {
        const value = valueAt(tree, path);
        if (typeof value !== 'string' || !referencePattern.test(value)) {
          throw new TokenError(
            'semantic-literal',
            `${path} must reference a token, not hold a literal.`,
          );
        }
      }
    }
  }
}

function checkValue(path: string, value: Leaf) {
  if (path.startsWith('color.') || path.endsWith('.color')) {
    if (typeof value !== 'string' || !colorPattern.test(value)) {
      throw new TokenError(
        'invalid-value',
        `${path} must be a lowercase #rrggbb or #rrggbbaa color.`,
      );
    }
    return;
  }
  if (path.startsWith('motion.easing.') || path.endsWith('.easing')) {
    if (
      !Array.isArray(value) ||
      value.length !== 4 ||
      value.some((item) => typeof item !== 'number')
    ) {
      throw new TokenError('invalid-value', `${path} must be a cubic-bezier [x1, y1, x2, y2].`);
    }
    return;
  }
  if (path.startsWith('typography.family.')) {
    if (!Array.isArray(value) || value.some((item) => typeof item !== 'string')) {
      throw new TokenError('invalid-value', `${path} must be a list of font names.`);
    }
    return;
  }
  if (path.startsWith('layer.') && !path.startsWith('layer.backdrop.')) {
    if (typeof value !== 'number' || !Number.isInteger(value)) {
      throw new TokenError('invalid-value', `${path} must be an integer.`);
    }
    return;
  }
  if (path.endsWith('.textTransform') || path.endsWith('.numeric')) {
    if (typeof value !== 'string') throw new TokenError('invalid-value', `${path} must be text.`);
    return;
  }
  if (path.endsWith('.fontFamily')) return;
  if (typeof value !== 'number') throw new TokenError('invalid-value', `${path} must be a number.`);
}

function domainsOf(tree: Record<string, Json>): ResolvedTokens['domains'] {
  const domains = new Map<
    string,
    { enumName: string | null; values: Map<string, readonly string[]> }
  >();
  const light = (tree.color as Record<string, Json>).domain as Record<string, Json>;
  for (const [group, node] of Object.entries(light.light as Record<string, Json>)) {
    if (group.startsWith('$') || !isObject(node)) continue;
    const enumName = typeof node.$enum === 'string' ? node.$enum : null;
    const values = new Map<string, readonly string[]>();
    for (const [value, parts] of Object.entries(node)) {
      if (value.startsWith('$') || !isObject(parts)) continue;
      values.set(
        value,
        Object.keys(parts)
          .filter((part) => !part.startsWith('$'))
          .sort(),
      );
    }
    domains.set(group, { enumName, values });
  }
  return domains;
}

function leafPaths(node: Json | undefined, prefix: string): Set<string> {
  const paths = new Set<string>();
  const walk = (value: Json | undefined, path: string) => {
    if (!isObject(value)) {
      paths.add(path);
      return;
    }
    for (const [key, child] of Object.entries(value)) {
      if (!key.startsWith('$')) walk(child, path === '' ? key : `${path}.${key}`);
    }
  };
  walk(node, prefix);
  return paths;
}

function valueAt(tree: Record<string, Json>, path: string): Json | undefined {
  let node: Json | undefined = tree;
  for (const key of path.split('.')) node = isObject(node) ? node[key] : undefined;
  return node;
}

function isObject(value: unknown): value is { readonly [key: string]: Json } {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

// ---------------------------------------------------------------------------------------------
// Emission: CSS for the build (theme variables, Tailwind 4 theme names, role utilities) and the
// names-only module for TypeScript. App-facing names are semantic/domain paths; primitives have none.
// ---------------------------------------------------------------------------------------------

const rem = (px: number) => `${trimNumber(px / 16)}rem`;
const em = (px: number) => `${trimNumber(px / 16)}em`;
function trimNumber(value: number): string {
  return Number(value.toFixed(6)).toString();
}
const dashed = (path: string) => path.replaceAll('.', '-');

/** CSS variable carrying a semantic (`sem`) or domain (`dom`) color in the active theme. */
export function colorVariable(kind: 'sem' | 'dom', path: string): string {
  return `--loto-${kind}-${dashed(path)}`;
}

export interface TokenNames {
  readonly semanticColors: readonly string[];
  readonly domainGroups: readonly {
    readonly group: string;
    readonly enumName: string | null;
    readonly values: readonly { readonly value: string; readonly parts: readonly string[] }[];
  }[];
  readonly typographyRoles: readonly string[];
  readonly spacing: readonly string[];
  readonly radii: readonly string[];
  readonly shadows: readonly string[];
  readonly breakpoints: readonly string[];
  readonly transitions: readonly string[];
  readonly layers: readonly string[];
}

export function tokenNames(tokens: ResolvedTokens): TokenNames {
  const under = (prefix: string) =>
    [...tokens.values.keys()]
      .filter((path) => path.startsWith(prefix))
      .map((path) => path.slice(prefix.length));
  const unique = (items: readonly string[]) => [...new Set(items)];
  return {
    semanticColors: under('color.semantic.light.'),
    domainGroups: [...tokens.domains].map(([group, domain]) => ({
      group,
      enumName: domain.enumName,
      values: [...domain.values].map(([value, parts]) => ({ value, parts })),
    })),
    typographyRoles: unique(under('typography.role.').map((path) => path.split('.')[0] ?? '')),
    spacing: under('spacing.').map(spacingName),
    radii: under('radius.'),
    shadows: unique(under('shadow.light.').map((path) => path.split('.')[0] ?? '')),
    breakpoints: under('breakpoint.'),
    transitions: unique(under('motion.transition.').map((path) => path.split('.')[0] ?? '')),
    layers: under('layer.').filter((path) => !path.startsWith('backdrop.')),
  };
}

/** `spacing.scale.4` → `4`; `spacing.inset.panel` → `inset-panel` (Tailwind `p-inset-panel`). */
function spacingName(path: string): string {
  return path.startsWith('scale.') ? path.slice('scale.'.length) : dashed(path);
}

/**
 * The generated stylesheet that replaces `@loto-tokens;` in `globals.css`, before Tailwind runs:
 * per-theme variables (`data-theme-preference` on `<html>`, Sistema through `prefers-color-scheme`,
 * and `data-theme-scope` for a region pinned to one theme),
 * the Tailwind 4 theme (defaults cleared; semantic/domain names only) and the role utilities.
 */
export function buildTokenCss(tokens: ResolvedTokens): string {
  const names = tokenNames(tokens);
  const value = (path: string): Leaf => {
    const found = tokens.values.get(path);
    if (found === undefined) throw new TokenError('missing-reference', path);
    return found;
  };
  const number = (path: string) => value(path) as number;
  const seen = new Map<string, string>();
  const claim = (name: string, path: string) => {
    const other = seen.get(name);
    if (other !== undefined && other !== path) {
      throw new TokenError('name-collision', `${path} and ${other} both map to ${name}.`);
    }
    seen.set(name, path);
    return name;
  };

  const themeBlock = (theme: Theme) => {
    const lines: string[] = [`color-scheme: ${theme};`];
    for (const path of names.semanticColors) {
      lines.push(
        `${colorVariable('sem', path)}: ${value(`color.semantic.${theme}.${path}`) as string};`,
      );
    }
    for (const domain of names.domainGroups) {
      for (const entry of domain.values) {
        for (const part of entry.parts) {
          const path = `${domain.group}.${entry.value}.${part}`;
          lines.push(
            `${colorVariable('dom', path)}: ${value(`color.domain.${theme}.${path}`) as string};`,
          );
        }
      }
    }
    for (const shadow of names.shadows) {
      const at = (key: string) => number(`shadow.${theme}.${shadow}.${key}`);
      const color = tokens.references.get(`shadow.${theme}.${shadow}.color`) ?? '';
      const semantic = color.replace(`color.semantic.${theme}.`, '');
      lines.push(
        `--loto-shadow-${shadow}: ${at('offsetX')}px ${at('offsetY')}px ${at('blur')}px ${at('spread')}px var(${colorVariable('sem', semantic)});`,
      );
    }
    lines.push(`--loto-backdrop-opacity: ${number(`layer.backdrop.opacity.${theme}`)};`);
    return lines.map((line) => `  ${line}`).join('\n');
  };

  const theme: string[] = [];
  for (const path of names.semanticColors) {
    theme.push(`${claim(`--color-${dashed(path)}`, path)}: var(${colorVariable('sem', path)});`);
  }
  for (const domain of names.domainGroups) {
    for (const entry of domain.values) {
      for (const part of entry.parts) {
        const path = `${domain.group}.${entry.value}.${part}`;
        theme.push(
          `${claim(`--color-dom-${dashed(path)}`, path)}: var(${colorVariable('dom', path)});`,
        );
      }
    }
  }
  const family = (name: string) =>
    (value(`typography.family.${name}`) as readonly string[])
      .map((font) => (/^[a-z-]+$/i.test(font) && !font.includes(' ') ? font : `"${font}"`))
      .join(', ');
  theme.push(`--font-sans: ${family('sans')};`, `--font-mono: ${family('mono')};`);
  theme.push(
    '--default-font-family: var(--font-sans);',
    '--default-mono-font-family: var(--font-mono);',
  );
  for (const weight of ['regular', 'medium', 'semibold', 'bold']) {
    theme.push(`--font-weight-${weight}: ${number(`typography.weight.${weight}`)};`);
  }
  for (const path of tokens.values.keys()) {
    if (!path.startsWith('spacing.')) continue;
    const name = spacingName(path.slice('spacing.'.length));
    theme.push(`${claim(`--spacing-${name}`, path)}: ${rem(number(path))};`);
  }
  for (const size of ['dialog-max', 'drawer-max', 'menu-max', 'form-max', 'content-max']) {
    theme.push(`--container-${size}: ${rem(number(`spacing.size.${size}`))};`);
  }
  for (const radius of names.radii) {
    const px = number(`radius.${radius}`);
    theme.push(`--radius-${radius}: ${px >= 9999 ? '9999px' : rem(px)};`);
  }
  for (const shadow of names.shadows)
    theme.push(`--shadow-${shadow}: var(--loto-shadow-${shadow});`);
  for (const breakpoint of names.breakpoints) {
    theme.push(`--breakpoint-${breakpoint}: ${em(number(`breakpoint.${breakpoint}`))};`);
  }
  for (const easing of ['standard', 'enter', 'exit', 'linear']) {
    theme.push(
      `--ease-${easing}: cubic-bezier(${(value(`motion.easing.${easing}`) as number[]).join(', ')});`,
    );
  }

  const motion = (set: 'transition' | 'reduced') =>
    names.transitions
      .map((name) => {
        const duration = number(`motion.${set}.${name}.duration`);
        const easing = (value(`motion.${set}.${name}.easing`) as number[]).join(', ');
        return `  --loto-motion-${name}-duration: ${duration}ms;\n  --loto-motion-${name}-easing: cubic-bezier(${easing});`;
      })
      .join('\n');

  const utilities: string[] = [];
  for (const role of names.typographyRoles) {
    const at = (key: string) => tokens.values.get(`typography.role.${role}.${key}`);
    const fontFamily = tokens.references
      .get(`typography.role.${role}.fontFamily`)
      ?.endsWith('.mono')
      ? 'var(--font-mono)'
      : 'var(--font-sans)';
    const declarations = [
      `font-family: ${fontFamily};`,
      `font-size: ${rem(at('fontSize') as number)};`,
      `line-height: ${rem(at('lineHeight') as number)};`,
      `font-weight: ${at('fontWeight') as number};`,
      `letter-spacing: ${trimNumber(at('letterSpacing') as number)}em;`,
    ];
    const transform = at('textTransform');
    if (typeof transform === 'string') declarations.push(`text-transform: ${transform};`);
    if (at('numeric') === 'tabular-lining') {
      declarations.push('font-variant-numeric: tabular-nums lining-nums;');
    }
    utilities.push(`@utility type-${role} {\n  ${declarations.join('\n  ')}\n}`);
  }
  for (const name of names.transitions) {
    utilities.push(
      `@utility motion-${name} {\n  transition-duration: var(--loto-motion-${name}-duration);\n  transition-timing-function: var(--loto-motion-${name}-easing);\n}`,
    );
  }
  for (const layer of names.layers) {
    utilities.push(`@utility layer-${layer} {\n  z-index: ${number(`layer.${layer}`)};\n}`);
  }

  return [
    '/* Generated at build time by src/theme/token-adapter.ts from design/tokens. Do not edit. */',
    '@theme {',
    '  --color-*: initial;',
    '  --font-*: initial;',
    '  --font-weight-*: initial;',
    '  --text-*: initial;',
    '  --tracking-*: initial;',
    '  --leading-*: initial;',
    '  --spacing-*: initial;',
    '  --container-*: initial;',
    '  --radius-*: initial;',
    '  --shadow-*: initial;',
    '  --inset-shadow-*: initial;',
    '  --drop-shadow-*: initial;',
    '  --text-shadow-*: initial;',
    '  --breakpoint-*: initial;',
    '  --ease-*: initial;',
    '  --animate-*: initial;',
    '}',
    `@theme inline {\n${theme.map((line) => `  ${line}`).join('\n')}\n}`,
    `:root,\n:root[data-theme-preference='light'] {\n${themeBlock('light')}\n}`,
    `:root[data-theme-preference='dark'] {\n${themeBlock('dark')}\n}`,
    `@media (prefers-color-scheme: dark) {\n  :root:not([data-theme-preference='light']):not([data-theme-preference='dark']) {\n${themeBlock(
      'dark',
    )
      .split('\n')
      .map((line) => `  ${line}`)
      .join('\n')}\n  }\n}`,
    // A region rendered in a fixed theme whatever the page's (the gallery's side-by-side panels).
    `[data-theme-scope='light'] {\n${themeBlock('light')}\n}`,
    `[data-theme-scope='dark'] {\n${themeBlock('dark')}\n}`,
    `:root {\n${motion('transition')}\n}`,
    `@media (prefers-reduced-motion: reduce) {\n  :root {\n${motion('reduced')
      .split('\n')
      .map((line) => `  ${line}`)
      .join('\n')}\n  }\n}`,
    ...utilities,
    '',
  ].join('\n');
}

/** The names-only TypeScript module (`src/theme/tokens.generated.ts`): no values, no primitives. */
export function buildTokenNamesModule(tokens: ResolvedTokens): string {
  const names = tokenNames(tokens);
  const list = (items: readonly string[]) =>
    `[\n${items.map((item) => `  ${JSON.stringify(item)},`).join('\n')}\n] as const`;
  const domains = names.domainGroups
    .map(
      (domain) =>
        `  ${JSON.stringify(domain.group)}: {\n    enumName: ${JSON.stringify(domain.enumName)},\n    values: {\n${domain.values
          .map((entry) => `      ${JSON.stringify(entry.value)}: ${JSON.stringify(entry.parts)},`)
          .join('\n')}\n    },\n  },`,
    )
    .join('\n');
  return [
    '// Generated by `pnpm --filter @loto/web generate:tokens` from design/tokens through',
    '// src/theme/token-adapter.ts. Names only: values reach the app as CSS variables. Do not edit.',
    '',
    `export const semanticColors = ${list(names.semanticColors)};`,
    '',
    `export const domainColors = {\n${domains}\n} as const;`,
    '',
    `export const typographyRoles = ${list(names.typographyRoles)};`,
    '',
    `export const spacingTokens = ${list(names.spacing)};`,
    '',
    `export const radiusTokens = ${list(names.radii)};`,
    '',
    `export const shadowTokens = ${list(names.shadows)};`,
    '',
    `export const breakpointTokens = ${list(names.breakpoints)};`,
    '',
    `export const transitionTokens = ${list(names.transitions)};`,
    '',
    `export const layerTokens = ${list(names.layers)};`,
    '',
  ].join('\n');
}
