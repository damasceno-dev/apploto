// Mobile token resolver (Mobile M3 Phase 2): reads design/tokens/*.json at build time, validates
// the three-layer model and resolves `{path.to.token}` references. Emission (`tooling/tokens.mjs`)
// exposes semantic and domain colors only; primitives are resolved here and never get an app name.
// Zero dependencies so tailwind.config.cjs can load it synchronously.
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

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

export class TokenError extends Error {
  constructor(code, message) {
    super(`${code}: ${message}`);
    this.code = code;
    this.name = 'TokenError';
  }
}

const keyPattern = /^[A-Za-z0-9-]+$/;
const referencePattern = /^\{([^{}]+)\}$/;
const colorPattern = /^#[0-9a-f]{6}(?:[0-9a-f]{2})?$/;
const themes = ['light', 'dark'];

const isObject = (value) => typeof value === 'object' && value !== null && !Array.isArray(value);

/** Reads every `.json` file of a token directory synchronously (build time only). */
export function readTokenDirectory(directory) {
  const sources = {};
  for (const name of readdirSync(directory)) {
    if (name.endsWith('.json')) sources[name] = readFileSync(path.join(directory, name), 'utf8');
  }
  return sources;
}

/** Parses `fileName → JSON text` into one tree, checking the exact file set and groups. */
export function parseTokenSources(sources) {
  for (const name of Object.keys(sources)) {
    if (!Object.hasOwn(tokenFiles, name))
      throw new TokenError('unexpected-file', `${name} is not a token file.`);
  }
  const tree = {};
  for (const [name, group] of Object.entries(tokenFiles)) {
    const text = sources[name];
    if (text === undefined) throw new TokenError('missing-file', `${name} is missing.`);
    let parsed;
    try {
      parsed = JSON.parse(text);
    } catch (error) {
      throw new TokenError('invalid-json', `${name}: ${error.message}`);
    }
    if (!isObject(parsed)) throw new TokenError('bad-group', `${name} must hold an object.`);
    const groups = Object.keys(parsed).filter((key) => !key.startsWith('$'));
    if (groups.length !== 1 || groups[0] !== group)
      throw new TokenError('bad-group', `${name} must hold exactly the "${group}" group.`);
    tree[group] = parsed[group];
  }
  return tree;
}

function collectLeaves(node, prefix, leaves, groups) {
  if (!isObject(node)) {
    leaves.set(prefix, asLeaf(prefix, node));
    return;
  }
  if (prefix !== '') groups.add(prefix);
  for (const [key, child] of Object.entries(node)) {
    if (key.startsWith('$')) continue;
    if (!keyPattern.test(key))
      throw new TokenError('bad-key', `${prefix ? `${prefix}.` : ''}${key}`);
    collectLeaves(child, prefix === '' ? key : `${prefix}.${key}`, leaves, groups);
  }
}

function asLeaf(where, value) {
  if (typeof value === 'string') {
    if ((value.includes('{') || value.includes('}')) && !referencePattern.test(value))
      throw new TokenError(
        'embedded-reference',
        `${where}: "${value}" — a reference must be the whole value.`,
      );
    return value;
  }
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (
    Array.isArray(value) &&
    value.every(
      (item) => typeof item === 'string' || (typeof item === 'number' && Number.isFinite(item)),
    )
  ) {
    if (value.some((item) => typeof item === 'string' && /[{}]/.test(item)))
      throw new TokenError('embedded-reference', `${where}: references cannot sit inside a list.`);
    return value;
  }
  throw new TokenError('invalid-value', `${where} has an unsupported value.`);
}

const referenceOf = (value) =>
  typeof value === 'string' ? (referencePattern.exec(value)?.[1] ?? null) : null;

function themeOf(tokenPath) {
  const parts = tokenPath.split('.');
  if (parts[0] === 'color' && (parts[1] === 'semantic' || parts[1] === 'domain'))
    return themes.includes(parts[2]) ? parts[2] : null;
  if (parts[0] === 'shadow') return themes.includes(parts[1]) ? parts[1] : null;
  return null;
}

// Tokens README layering: primitives are literals; semantic → primitive/semantic; domain →
// semantic/primitive; other categories reach color only through a same-theme semantic token.
function checkLayering(source, target) {
  const layer = source.split('.').slice(0, 2).join('.');
  const targetLayer = target.split('.').slice(0, 2).join('.');
  const sourceTheme = themeOf(source);
  const targetTheme = themeOf(target);
  if (layer === 'color.primitive')
    throw new TokenError('layer-violation', `${source}: primitives are literals.`);
  if (sourceTheme !== null && targetTheme !== null && sourceTheme !== targetTheme)
    throw new TokenError(
      'cross-theme',
      `${source} (${sourceTheme}) references ${target} (${targetTheme}).`,
    );
  if (layer === 'color.semantic' || layer === 'color.domain') {
    if (targetLayer !== 'color.primitive' && targetLayer !== 'color.semantic')
      throw new TokenError(
        'layer-violation',
        `${source} may reference only primitives or semantic tokens.`,
      );
    return;
  }
  if (target.startsWith('color.')) {
    if (targetLayer !== 'color.semantic')
      throw new TokenError(
        'layer-violation',
        `${source} reaches color only through a semantic token.`,
      );
    if (sourceTheme === null)
      throw new TokenError(
        'cross-theme',
        `${source} is theme-independent but references ${target}.`,
      );
  }
}

function leafPaths(node, prefix) {
  const paths = new Set();
  const walk = (value, where) => {
    if (!isObject(value)) {
      paths.add(where);
      return;
    }
    for (const [key, child] of Object.entries(value))
      if (!key.startsWith('$')) walk(child, where === '' ? key : `${where}.${key}`);
  };
  walk(node, prefix);
  return paths;
}

function checkColorStructure(tree) {
  if (!isObject(tree.color)) throw new TokenError('bad-group', 'color must be an object.');
  for (const layer of ['semantic', 'domain']) {
    const node = tree.color[layer];
    if (!isObject(node)) throw new TokenError('bad-group', `color.${layer} is missing.`);
    const keys = Object.keys(node)
      .filter((key) => !key.startsWith('$'))
      .sort();
    if (keys.join() !== 'dark,light')
      throw new TokenError('theme-asymmetry', `color.${layer} must hold exactly light and dark.`);
    const light = leafPaths(node.light, '');
    const dark = leafPaths(node.dark, '');
    const problems = [
      ...[...light].filter((item) => !dark.has(item)).map((item) => `dark lacks ${item}`),
      ...[...dark].filter((item) => !light.has(item)).map((item) => `light lacks ${item}`),
    ];
    if (problems.length > 0)
      throw new TokenError('theme-asymmetry', `color.${layer}: ${problems.join('; ')}`);
    for (const theme of themes)
      for (const leaf of leafPaths(node[theme], '')) {
        let value = node[theme];
        for (const key of leaf.split('.')) value = value?.[key];
        if (typeof value !== 'string' || !referencePattern.test(value))
          throw new TokenError(
            'semantic-literal',
            `color.${layer}.${theme}.${leaf} must reference a token, not hold a literal.`,
          );
      }
  }
}

function checkValue(where, value) {
  if (where.startsWith('color.') || where.endsWith('.color')) {
    if (typeof value !== 'string' || !colorPattern.test(value))
      throw new TokenError(
        'invalid-value',
        `${where} must be a lowercase #rrggbb or #rrggbbaa color.`,
      );
    return;
  }
  if (where.startsWith('motion.easing.') || where.endsWith('.easing')) {
    if (
      !Array.isArray(value) ||
      value.length !== 4 ||
      value.some((item) => typeof item !== 'number')
    )
      throw new TokenError('invalid-value', `${where} must be a cubic-bezier [x1, y1, x2, y2].`);
    return;
  }
  if (where.startsWith('typography.family.') || where.endsWith('.fontFamily')) {
    if (!Array.isArray(value) || value.some((item) => typeof item !== 'string'))
      throw new TokenError('invalid-value', `${where} must be a list of font names.`);
    return;
  }
  if (where.endsWith('.textTransform') || where.endsWith('.numeric')) {
    if (typeof value !== 'string') throw new TokenError('invalid-value', `${where} must be text.`);
    return;
  }
  if (typeof value !== 'number')
    throw new TokenError('invalid-value', `${where} must be a number.`);
  if (
    where.startsWith('layer.') &&
    !where.startsWith('layer.backdrop.') &&
    !Number.isInteger(value)
  )
    throw new TokenError('invalid-value', `${where} must be an integer.`);
  if (where.startsWith('layer.backdrop.') && (value < 0 || value > 1))
    throw new TokenError('invalid-value', `${where} must be an opacity between 0 and 1.`);
}

/** Validates layering and resolves every reference; returns `Map<path, literal>`. */
export function resolveTokens(tree) {
  const raw = new Map();
  const groups = new Set();
  collectLeaves(tree, '', raw, groups);
  checkColorStructure(tree);
  const values = new Map();
  const resolve = (where, chain) => {
    if (values.has(where)) return values.get(where);
    if (!raw.has(where)) {
      const from = chain.at(-1) ?? where;
      if (groups.has(where))
        throw new TokenError('group-reference', `${from} references the group ${where}.`);
      throw new TokenError(
        'missing-reference',
        `${from} references ${where}, which does not exist.`,
      );
    }
    if (chain.includes(where)) throw new TokenError('cycle', [...chain, where].join(' → '));
    const value = raw.get(where);
    const target = referenceOf(value);
    if (target === null) {
      values.set(where, value);
      return value;
    }
    checkLayering(where, target);
    const resolved = resolve(target, [...chain, where]);
    values.set(where, resolved);
    return resolved;
  };
  for (const where of raw.keys()) resolve(where, []);
  for (const [where, value] of values) checkValue(where, value);
  return values;
}

export const loadTokens = (sources) => resolveTokens(parseTokenSources(sources));

// ------------------------------------------------------------------------------------------------
// Native emission. Units: logical px → dp/pt (unchanged numbers); font sizes stay unscaled here so
// the OS font scale applies at render (`allowFontScaling` on); letterSpacing em → px of its own
// size; `tabular-lining` → fontVariant; shadows → iOS offset/radius/opacity + Android elevation.
// ------------------------------------------------------------------------------------------------

function subtree(values, prefix) {
  const out = {};
  for (const [where, value] of values) {
    if (!where.startsWith(`${prefix}.`)) continue;
    const parts = where.slice(prefix.length + 1).split('.');
    let node = out;
    for (const part of parts.slice(0, -1)) node = node[part] ??= {};
    node[parts.at(-1)] = value;
  }
  return out;
}

const weightName = (value) => String(value);

/** iOS shadow color carries its own alpha: split `#rrggbbaa` into an opaque color plus opacity. */
function nativeShadow(recipe) {
  const color = recipe.color;
  const opacity = color.length === 9 ? Number((parseInt(color.slice(7), 16) / 255).toFixed(3)) : 1;
  return {
    shadowColor: color.slice(0, 7),
    shadowOffset: { width: recipe.offsetX, height: recipe.offsetY },
    shadowRadius: recipe.blur / 2,
    shadowOpacity: opacity,
    elevation: recipe.elevation,
  };
}

/** The app-facing native token set: semantic/domain colors per theme plus the other scales. */
export function nativeTokens(values) {
  const theme = (name) => ({
    sem: subtree(values, `color.semantic.${name}`),
    dom: subtree(values, `color.domain.${name}`),
    shadow: Object.fromEntries(
      Object.entries(subtree(values, `shadow.${name}`)).map(([key, recipe]) => [
        key,
        nativeShadow(recipe),
      ]),
    ),
    backdropOpacity: values.get(`layer.backdrop.opacity.${name}`),
  });
  const roles = {};
  for (const [role, spec] of Object.entries(subtree(values, 'typography.role'))) {
    roles[role] = {
      fontSize: spec.fontSize,
      lineHeight: spec.lineHeight,
      fontWeight: weightName(spec.fontWeight),
      letterSpacing: Number((spec.letterSpacing * spec.fontSize).toFixed(3)),
      ...(spec.textTransform ? { textTransform: spec.textTransform } : {}),
      ...(spec.numeric === 'tabular-lining'
        ? { fontVariant: ['tabular-nums', 'lining-nums'] }
        : {}),
      ...(spec.fontFamily.includes('ui-monospace') ? { mono: true } : {}),
    };
  }
  const motion = subtree(values, 'motion');
  const layers = subtree(values, 'layer');
  delete layers.backdrop;
  return {
    themes: { light: theme('light'), dark: theme('dark') },
    typography: roles,
    spacing: subtree(values, 'spacing'),
    radius: subtree(values, 'radius'),
    motion: { transition: motion.transition, reduced: motion.reduced },
    layer: layers,
    breakpoint: subtree(values, 'breakpoint'),
  };
}

/** Flattened `sem-…`/`dom-…` names → CSS variable names for NativeWind classes. */
export function colorVariableNames(values) {
  const names = [];
  for (const where of values.keys()) {
    const match = /^color\.(semantic|domain)\.light\.(.+)$/.exec(where);
    if (match)
      names.push(`${match[1] === 'semantic' ? 'sem' : 'dom'}-${match[2].replaceAll('.', '-')}`);
  }
  return names.sort();
}

/** Theme → `{ '--sem-…': '#…' }` for NativeWind `vars()`. */
export function colorVariables(values, theme) {
  const out = {};
  for (const [where, value] of values) {
    const match = /^color\.(semantic|domain)\.(light|dark)\.(.+)$/.exec(where);
    if (match && match[2] === theme)
      out[`--${match[1] === 'semantic' ? 'sem' : 'dom'}-${match[3].replaceAll('.', '-')}`] = value;
  }
  return out;
}
