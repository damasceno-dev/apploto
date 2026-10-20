#!/usr/bin/env node
// Design M2 token gate: validates design/tokens/*.json and checks WCAG contrast in both themes.
// Zero dependencies (Node built-ins only). It never writes to the token files.
//
//   node scripts/check-contrast.mjs                 validate + check, print a summary
//   node scripts/check-contrast.mjs --report        also print every checked pair as a Markdown table
//   node scripts/check-contrast.mjs --tokens <dir> --openapi <file>   check another copy (used by the tests)
//
// The token model, the reference syntax and every rule enforced here are documented in
// design/tokens/README.md. Exit code 0 means every rule passed; 1 means at least one failure.

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

const REQUIRED_FILES = {
  'colors.json': 'color',
  'typography.json': 'typography',
  'spacing.json': 'spacing',
  'radii.json': 'radius',
  'shadows.json': 'shadow',
  'breakpoints.json': 'breakpoint',
  'motion.json': 'motion',
  'layers.json': 'layer',
};
const THEMES = ['light', 'dark'];
// Colored domains every theme must define. Enum groups are matched value-for-value against the
// published OpenAPI enum (no missing or extra values); the others are signed-value presentations.
const REQUIRED_ENUM_DOMAINS = {
  'transaction-status': 'TransactionStatus',
  'daily-close-status': 'DailyCloseStatus',
  direction: 'Direction',
  'aging-bucket': 'AgingBucket',
  'time-entry-status': 'TimeEntryStatus',
  'installment-plan-status': 'InstallmentPlanStatus',
  'invitation-status': 'InvitationStatus',
  'mail-delivery-status': 'MailDeliveryStatus',
  'operator-link-request-status': 'OperatorLinkRequestStatus',
  'review-event-type': 'DailyCloseReviewEventType',
};
const REQUIRED_VALUE_DOMAINS = {
  'cash-variance': ['shortage', 'surplus', 'zero'],
  'fiado-balance': ['debt', 'settled', 'credit'],
  'hour-balance': ['positive', 'negative', 'zero'],
  'generated-movement': ['automatic'],
};
const DOMAIN_VALUE_KEYS = new Set(['fg', 'bg', 'border']);
// Every domain value is checked on each of these surfaces (labels as written in $contrast.domainSurfaces).
const REQUIRED_DOMAIN_SURFACES = ['background', 'surface.default', 'surface.subtle', 'surface.raised', 'surface.hover', 'surface.default + surface.selected'];
// Every action variant must define these states; the checker generates their label checks itself.
const REQUIRED_ACTION_STATES = ['bg', 'bg-hover', 'bg-active', 'fg'];
const REQUIRED_DISABLED_STATES = ['bg', 'fg', 'border'];
// Bases a translucent action fill is composited onto when the variant has no opaque default fill.
const ACTION_BASES = ['surface.default', 'surface.subtle', 'background'];
const KINDS = new Set(['text', 'large-text', 'non-text', 'disabled']);

const KEY_RE = /^[A-Za-z0-9][A-Za-z0-9-]*$/;
const REF_RE = /^\{([A-Za-z0-9][A-Za-z0-9-]*(?:\.[A-Za-z0-9][A-Za-z0-9-]*)*)\}$/;
const HEX_RE = /^#(?:[0-9a-f]{6}|[0-9a-f]{8})$/;

function parseArgs(argv) {
  const args = {
    tokens: join(repoRoot, 'design/tokens'),
    openapi: join(repoRoot, 'shared/api/openapi/server-api.openapi.json'),
    report: false,
  };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--report') args.report = true;
    else if (arg === '--tokens' || arg === '--openapi') {
      const value = argv[i + 1];
      if (!value) throw new Error(`${arg} needs a path`);
      args[arg.slice(2)] = resolve(value);
      i += 1;
    } else throw new Error(`Unknown argument: ${arg}`);
  }
  return args;
}

const errors = [];
const fail = (message) => errors.push(message);

// ---------------------------------------------------------------------------------------------
// Loading and structural validation

function loadTokens(dir) {
  const tree = {};
  if (!existsSync(dir)) {
    fail(`Token directory not found: ${dir}`);
    return tree;
  }
  const present = new Set(readdirSync(dir).filter((name) => name.endsWith('.json')));
  for (const [file, root] of Object.entries(REQUIRED_FILES)) {
    if (!present.has(file)) {
      fail(`Missing required token file: ${file}`);
      continue;
    }
    let data;
    try {
      data = JSON.parse(readFileSync(join(dir, file), 'utf8'));
    } catch (error) {
      fail(`${file}: invalid JSON (${error.message})`);
      continue;
    }
    const roots = Object.keys(data).filter((key) => !key.startsWith('$'));
    if (roots.length !== 1 || roots[0] !== root) {
      fail(`${file}: must contain exactly one top-level group "${root}" (found ${roots.join(', ') || 'none'})`);
      continue;
    }
    tree[root] = data[root];
    if (data.$contrast) tree.$contrast = data.$contrast;
  }
  for (const name of present) {
    if (!(name in REQUIRED_FILES)) fail(`Unexpected token file: ${name} (one file per category)`);
  }
  return tree;
}

// Every leaf in deterministic (sorted-key) order, with its dot path. `$`-keys are metadata.
function collectLeaves(node, path, out) {
  if (node === null || typeof node !== 'object' || Array.isArray(node)) {
    out.push([path, node]);
    return out;
  }
  const keys = Object.keys(node).sort();
  if (keys.filter((key) => !key.startsWith('$')).length === 0) fail(`${path}: empty group`);
  for (const key of keys) {
    if (key.startsWith('$')) continue;
    if (!KEY_RE.test(key)) {
      fail(`${path ? `${path}.` : ''}${key}: malformed key (letters, digits and hyphens only; no dots or braces)`);
      continue;
    }
    collectLeaves(node[key], path ? `${path}.${key}` : key, out);
  }
  return out;
}

function lookup(tree, path) {
  let node = tree;
  for (const segment of path.split('.')) {
    if (node === null || typeof node !== 'object' || Array.isArray(node) || !(segment in node)) return undefined;
    node = node[segment];
  }
  return node;
}

function validateLeafShape(path, value) {
  const ok = (v) => typeof v === 'string' || (typeof v === 'number' && Number.isFinite(v));
  if (Array.isArray(value)) {
    if (value.length === 0 || !value.every(ok)) fail(`${path}: arrays must hold strings or finite numbers`);
    return;
  }
  if (!ok(value)) fail(`${path}: invalid value ${JSON.stringify(value)}`);
  if (typeof value === 'string' && (value.includes('{') || value.includes('}')) && !REF_RE.test(value)) {
    fail(`${path}: malformed reference ${JSON.stringify(value)} (expected {path.to.token})`);
  }
}

function makeResolver(tree) {
  const cache = new Map();
  const resolveValue = (path, stack = []) => {
    if (cache.has(path)) return cache.get(path);
    if (stack.includes(path)) {
      fail(`Reference cycle: ${[...stack, path].join(' -> ')}`);
      return undefined;
    }
    const raw = lookup(tree, path);
    if (raw === undefined) return undefined;
    if (raw !== null && typeof raw === 'object' && !Array.isArray(raw)) {
      fail(`${stack.at(-1) ?? path}: reference to group "${path}" is ambiguous (point at a single token)`);
      return undefined;
    }
    let value = raw;
    const match = typeof raw === 'string' ? REF_RE.exec(raw) : null;
    if (match) {
      const target = match[1];
      if (lookup(tree, target) === undefined) {
        fail(`${path}: broken reference {${target}}`);
        value = undefined;
      } else value = resolveValue(target, [...stack, path]);
    }
    cache.set(path, value);
    return value;
  };
  return resolveValue;
}

// ---------------------------------------------------------------------------------------------
// Per-category value rules

function validateValues(leaves, resolveValue) {
  const isNum = (v) => typeof v === 'number' && Number.isFinite(v);
  for (const [path, raw] of leaves) {
    const value = resolveValue(path);
    if (value === undefined) continue;
    const [root] = path.split('.');
    const key = path.split('.').at(-1);
    if (root === 'color') {
      if (typeof value !== 'string' || !HEX_RE.test(value)) fail(`${path}: invalid color ${JSON.stringify(value)} (lowercase #rrggbb or #rrggbbaa)`);
      if (path.startsWith('color.primitive.') && typeof raw === 'string' && REF_RE.test(raw)) fail(`${path}: primitives hold literal values, not references`);
      if (/^color\.(semantic|domain)\./.test(path) && !(typeof raw === 'string' && REF_RE.test(raw))) fail(`${path}: semantic and domain tokens must be references, not literals`);
    } else if (['spacing', 'radius', 'breakpoint'].includes(root)) {
      if (!isNum(value) || value < 0) fail(`${path}: expected a non-negative number`);
    } else if (root === 'layer') {
      if (key === 'opacity' || path.includes('.opacity.')) {
        if (!isNum(value) || value < 0 || value > 1) fail(`${path}: opacity must be between 0 and 1`);
      } else if (!Number.isInteger(value)) fail(`${path}: z-index must be an integer`);
    } else if (root === 'motion') {
      if (path.includes('.duration.') && (!Number.isInteger(value) || value < 0)) fail(`${path}: duration must be a non-negative integer (ms)`);
      if (path.includes('.easing.') && !(Array.isArray(value) && value.length === 4 && value.every(isNum) && value[0] >= 0 && value[0] <= 1 && value[2] >= 0 && value[2] <= 1)) {
        fail(`${path}: easing must be a cubic-bezier [x1, y1, x2, y2] with x in 0..1`);
      }
    } else if (root === 'typography') {
      if (key === 'fontWeight' && !(Number.isInteger(value) && value >= 100 && value <= 900 && value % 100 === 0)) fail(`${path}: fontWeight must be 100..900 in steps of 100`);
      if ((key === 'fontSize' || key === 'lineHeight') && (!isNum(value) || value <= 0)) fail(`${path}: ${key} must be a positive number`);
      if (key === 'letterSpacing' && !isNum(value)) fail(`${path}: letterSpacing must be a number (em)`);
    } else if (root === 'shadow') {
      if (key === 'color' && (typeof value !== 'string' || !HEX_RE.test(value))) fail(`${path}: shadow color must resolve to a hex color`);
      if (['offsetX', 'offsetY', 'blur', 'spread'].includes(key) && !isNum(value)) fail(`${path}: ${key} must be a number`);
    }
  }
}

// ---------------------------------------------------------------------------------------------
// Theme structure, layering and domain coverage

function themeLeafSet(tree, base, theme) {
  const node = lookup(tree, `${base}.${theme}`);
  if (!node) return null;
  return new Set(collectLeaves(node, '', []).map(([path]) => path));
}

function validateThemes(tree, leaves) {
  for (const base of ['color.semantic', 'color.domain']) {
    const group = lookup(tree, base);
    if (!group) {
      fail(`Missing ${base}`);
      continue;
    }
    const themes = Object.keys(group).filter((key) => !key.startsWith('$'));
    for (const theme of THEMES) if (!themes.includes(theme)) fail(`${base}: missing required theme "${theme}"`);
    for (const theme of themes) if (!THEMES.includes(theme)) fail(`${base}: unknown theme "${theme}"`);
    const [light, dark] = THEMES.map((theme) => themeLeafSet(tree, base, theme));
    if (light && dark) {
      for (const path of light) if (!dark.has(path)) fail(`${base}.dark: missing ${path} (present in light)`);
      for (const path of dark) if (!light.has(path)) fail(`${base}.light: missing ${path} (present in dark)`);
    }
  }
  // Layering: semantic -> primitive or same-theme semantic; domain -> same-theme semantic or primitive.
  for (const [path, raw] of leaves) {
    const match = typeof raw === 'string' ? REF_RE.exec(raw) : null;
    if (!match) continue;
    const target = match[1];
    const own = /^color\.(semantic|domain)\.([a-z]+)\./.exec(path);
    if (own) {
      const [, layer, theme] = own;
      const allowed = [`color.primitive.`, `color.semantic.${theme}.`];
      if (!allowed.some((prefix) => target.startsWith(prefix))) fail(`${path}: ${layer} token may reference only primitives or ${theme} semantic tokens (got {${target}})`);
    } else if (!path.startsWith('color.') && /^color\.(semantic|domain)\./.test(target) === false && target.startsWith('color.')) {
      fail(`${path}: non-color tokens may reference color primitives only through a semantic token (got {${target}})`);
    }
  }
}

function loadOpenApiEnums(file) {
  if (!existsSync(file)) {
    fail(`OpenAPI document not found: ${file}`);
    return {};
  }
  try {
    const schemas = JSON.parse(readFileSync(file, 'utf8')).components?.schemas ?? {};
    return Object.fromEntries(Object.entries(schemas).filter(([, schema]) => Array.isArray(schema.enum)).map(([name, schema]) => [name, schema.enum]));
  } catch (error) {
    fail(`OpenAPI document unreadable: ${error.message}`);
    return {};
  }
}

function validateDomains(tree, enums) {
  for (const theme of THEMES) {
    const domain = lookup(tree, `color.domain.${theme}`);
    if (!domain) continue;
    for (const [group, enumName] of Object.entries(REQUIRED_ENUM_DOMAINS)) {
      const node = domain[group];
      if (!node) {
        fail(`color.domain.${theme}: missing required domain "${group}" (${enumName})`);
        continue;
      }
      if (node.$enum !== enumName) fail(`color.domain.${theme}.${group}: $enum must be "${enumName}"`);
      const expected = enums[enumName];
      if (!expected) {
        fail(`color.domain.${theme}.${group}: enum ${enumName} not found in the OpenAPI document`);
        continue;
      }
      const actual = Object.keys(node).filter((key) => !key.startsWith('$'));
      for (const value of expected) if (!actual.includes(value)) fail(`color.domain.${theme}.${group}: missing enum value ${value}`);
      for (const value of actual) if (!expected.includes(value)) fail(`color.domain.${theme}.${group}: ${value} is not a ${enumName} value`);
    }
    for (const [group, values] of Object.entries(REQUIRED_VALUE_DOMAINS)) {
      const node = domain[group];
      if (!node) {
        fail(`color.domain.${theme}: missing required domain "${group}"`);
        continue;
      }
      for (const value of values) if (!node[value]) fail(`color.domain.${theme}.${group}: missing value ${value}`);
    }
    for (const [group, node] of Object.entries(domain)) {
      if (group.startsWith('$')) continue;
      for (const [value, token] of Object.entries(node)) {
        if (value.startsWith('$')) continue;
        if (token === null || typeof token !== 'object' || !token.fg) {
          fail(`color.domain.${theme}.${group}.${value}: needs an fg token`);
          continue;
        }
        for (const key of Object.keys(token)) if (!DOMAIN_VALUE_KEYS.has(key)) fail(`color.domain.${theme}.${group}.${value}: unexpected key "${key}" (fg, bg, border)`);
      }
    }
  }
}

// ---------------------------------------------------------------------------------------------
// Contrast

function rgba(hex) {
  const n = (i) => parseInt(hex.slice(i, i + 2), 16) / 255;
  return { r: n(1), g: n(3), b: n(5), a: hex.length === 9 ? n(7) : 1 };
}
const over = (top, bottom) => ({
  r: top.a * top.r + (1 - top.a) * bottom.r,
  g: top.a * top.g + (1 - top.a) * bottom.g,
  b: top.a * top.b + (1 - top.a) * bottom.b,
  a: 1,
});
const channel = (c) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const luminance = ({ r, g, b }) => 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
function ratio(fg, bg) {
  const [high, low] = [luminance(fg), luminance(bg)].sort((x, y) => y - x);
  return (high + 0.05) / (low + 0.05);
}
const toHex = ({ r, g, b }) => `#${[r, g, b].map((c) => Math.round(c * 255).toString(16).padStart(2, '0')).join('')}`;

function checkContrast(tree, resolveValue) {
  const contrast = tree.$contrast;
  const results = [];
  if (!contrast || !Array.isArray(contrast.pairs)) {
    fail('colors.json: missing $contrast.pairs');
    return results;
  }
  const thresholds = contrast.thresholds ?? {};
  for (const kind of KINDS) if (typeof thresholds[kind] !== 'number') fail(`$contrast.thresholds.${kind} must be a number`);
  if (thresholds.text < 4.5 || thresholds['non-text'] < 3 || thresholds['large-text'] < 3) fail('$contrast.thresholds may not be relaxed below WCAG AA (text 4.5, large-text 3, non-text 3)');

  for (const theme of THEMES) {
    const base = `color.semantic.${theme}`;
    const color = (relative) => {
      const path = `${base}.${relative}`;
      const value = lookup(tree, path) === undefined ? undefined : resolveValue(path);
      if (value === undefined) fail(`$contrast (${theme}): unknown token ${relative}`);
      return typeof value === 'string' && HEX_RE.test(value) ? rgba(value) : undefined;
    };
    const stack = (layers, label) => {
      const list = Array.isArray(layers) ? layers : [layers];
      let result;
      for (const [index, layer] of list.entries()) {
        const c = color(layer);
        if (!c) return undefined;
        if (index === 0 && c.a < 1) {
          fail(`$contrast (${theme}) ${label}: translucent background ${layer} needs an opaque base under it`);
          return undefined;
        }
        result = index === 0 ? c : over(c, result);
      }
      return result;
    };
    const record = (id, kind, fgLabel, bgLabel, fg, bg) => {
      const value = ratio(fg.a < 1 ? over(fg, bg) : fg, bg);
      const target = thresholds[kind];
      const passed = value + 1e-9 >= target;
      results.push({ theme, id, kind, fg: fgLabel, bg: bgLabel, ratio: value, target, passed, bgHex: toHex(bg) });
      if (!passed) fail(`Contrast (${theme}) "${id}": ${fgLabel} on ${bgLabel} = ${value.toFixed(2)}:1, needs ${target}:1`);
    };

    // Declared pairs (cross product of fg x bg).
    for (const pair of contrast.pairs) {
      if (!KINDS.has(pair.kind)) {
        fail(`$contrast pair "${pair.id}": unknown kind ${pair.kind}`);
        continue;
      }
      if (pair.kind === 'large-text' && !(typeof pair.justification === 'string' && pair.justification.trim())) {
        fail(`$contrast pair "${pair.id}": a large-text exemption needs a justification naming the actual size/weight`);
      }
      for (const fgPath of pair.fg ?? []) {
        const fg = color(fgPath);
        for (const layers of pair.bg ?? []) {
          const label = Array.isArray(layers) ? layers.join(' + ') : layers;
          const bg = stack(layers, label);
          if (fg && bg) record(pair.id, pair.kind, fgPath, label, fg, bg);
        }
      }
    }

    // Action states are checked structurally: each variant's label on its default, hover and pressed
    // fills (and fg-hover on the hover fill), so removing or skipping a state cannot pass.
    const actions = lookup(tree, `${base}.action`) ?? {};
    for (const [variant, node] of Object.entries(actions)) {
      if (variant.startsWith('$')) continue;
      const required = variant === 'disabled' ? REQUIRED_DISABLED_STATES : REQUIRED_ACTION_STATES;
      const missing = required.filter((state) => !(state in node));
      if (missing.length) {
        fail(`color.semantic.${theme}.action.${variant}: missing state token(s) ${missing.join(', ')}`);
        continue;
      }
      const kind = variant === 'disabled' ? 'disabled' : 'text';
      const states = variant === 'disabled' ? ['bg'] : ['bg', 'bg-hover', 'bg-active'];
      const defaultFill = color(`action.${variant}.bg`);
      const bases = defaultFill && defaultFill.a === 1 ? [[`action.${variant}.bg`]] : ACTION_BASES.map((surface) => [surface]);
      for (const state of states) {
        const fill = color(`action.${variant}.${state}`);
        if (!fill) continue;
        const fgPaths = [`action.${variant}.fg`, ...(state === 'bg-hover' && 'fg-hover' in node ? [`action.${variant}.fg-hover`] : [])];
        const stacks = fill.a === 1 ? [[`action.${variant}.${state}`]] : bases.map((b) => (b[0] === `action.${variant}.${state}` ? b : [...b, `action.${variant}.${state}`]));
        for (const layers of stacks) {
          const label = layers.join(' + ');
          const bg = stack(layers, label);
          for (const fgPath of fgPaths) {
            const fg = color(fgPath);
            if (fg && bg) record(`action ${variant} ${state}`, kind, fgPath, label, fg, bg);
          }
        }
      }
    }

    // Domain tokens are checked structurally: every value, on every required surface.
    const surfaceLabels = (contrast.domainSurfaces ?? []).map((surface) => (Array.isArray(surface) ? surface.join(' + ') : surface));
    for (const required of REQUIRED_DOMAIN_SURFACES) if (!surfaceLabels.includes(required)) fail(`$contrast.domainSurfaces (${theme}): missing required surface "${required}"`);
    const domain = lookup(tree, `color.domain.${theme}`) ?? {};
    for (const [group, node] of Object.entries(domain)) {
      if (group.startsWith('$')) continue;
      for (const [value, token] of Object.entries(node)) {
        if (value.startsWith('$') || !token?.fg) continue;
        const fgPath = `color.domain.${theme}.${group}.${value}.fg`;
        const fgHex = resolveValue(fgPath);
        if (typeof fgHex !== 'string' || !HEX_RE.test(fgHex)) continue;
        const bgHex = token.bg ? resolveValue(`color.domain.${theme}.${group}.${value}.bg`) : undefined;
        for (const surface of contrast.domainSurfaces ?? []) {
          const base = stack(surface, Array.isArray(surface) ? surface.join(' + ') : surface);
          if (!base) continue;
          const bg = typeof bgHex === 'string' && HEX_RE.test(bgHex) ? over(rgba(bgHex), base) : base;
          const label = `${Array.isArray(surface) ? surface.join(' + ') : surface}${token.bg ? ' + badge bg' : ''}`;
          record(`domain ${group}.${value}`, 'text', `${group}.${value}.fg`, label, rgba(fgHex), bg);
        }
        const checked = new Set(results.filter((r) => r.theme === theme && r.id === `domain ${group}.${value}`).map((r) => r.bg.replace(/ \+ badge bg$/, '')));
        for (const required of REQUIRED_DOMAIN_SURFACES) if (!checked.has(required)) fail(`Coverage (${theme}): domain ${group}.${value} is never checked on ${required}`);
      }
    }

    // Coverage: no text/fg, control-boundary, focus or selection token may escape the checks.
    const checkedFg = new Set(results.filter((r) => r.theme === theme).map((r) => `${r.kind}:${r.fg}`));
    const semanticLeaves = collectLeaves(lookup(tree, base) ?? {}, '', []).map(([path]) => path);
    for (const path of semanticLeaves) {
      const needsText = path.startsWith('text.') || /(^|\.)fg(-hover)?$/.test(path) || /^nav\.text/.test(path) || path === 'accent.secondary-fg';
      const needsNonText = /^border\.control/.test(path) || /^focus\.ring/.test(path) || /^selection\./.test(path) || /^action\.(?!disabled\.)[a-z-]+\.border$/.test(path);
      if (needsText && !checkedFg.has(`text:${path}`) && !checkedFg.has(`disabled:${path}`)) fail(`Coverage (${theme}): ${path} is never checked as text`);
      if (needsNonText && !checkedFg.has(`non-text:${path}`)) fail(`Coverage (${theme}): ${path} is never checked as a non-text indicator`);
    }
  }
  return results;
}

// ---------------------------------------------------------------------------------------------

function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    process.exit(2);
  }
  const tree = loadTokens(args.tokens);
  const leaves = collectLeaves(Object.fromEntries(Object.entries(tree).filter(([key]) => !key.startsWith('$'))), '', []);
  for (const [path, value] of leaves) validateLeafShape(path, value);
  const resolveValue = makeResolver(tree);
  for (const [path] of leaves) resolveValue(path);
  validateValues(leaves, resolveValue);
  validateThemes(tree, leaves);
  validateDomains(tree, loadOpenApiEnums(args.openapi));
  const results = errors.length === 0 ? checkContrast(tree, resolveValue) : [];

  if (args.report && results.length) {
    console.log('| Theme | Check | Foreground | Background | Ratio | Target |');
    console.log('|---|---|---|---|---|---|');
    for (const r of results) console.log(`| ${r.theme} | ${r.id} | \`${r.fg}\` | \`${r.bg}\` | ${r.ratio.toFixed(2)} | ${r.target} |`);
  }
  if (errors.length) {
    for (const message of errors) console.error(`FAIL: ${message}`);
    console.error(`${errors.length} token check failure(s).`);
    process.exit(1);
  }
  const lowest = (theme) => Math.min(...results.filter((r) => r.theme === theme && r.kind === 'text').map((r) => r.ratio));
  console.log(`Tokens valid: ${leaves.length} tokens, ${Object.keys(REQUIRED_FILES).length} files, references resolved, themes and domains complete.`);
  console.log(`Contrast passed: ${results.length} pairs (light ${results.filter((r) => r.theme === 'light').length}, dark ${results.filter((r) => r.theme === 'dark').length}); lowest text ratio light ${lowest('light').toFixed(2)}, dark ${lowest('dark').toFixed(2)}.`);
}

main();
