#!/usr/bin/env node
// Design documentation gate (zero dependencies). Checks that:
//   1. every relative Markdown link under design/ points at an existing file (and heading anchor);
//   2. every token named in design/specs/components and design/specs/screens exists in both themes,
//      and those specs contain no primitive color reference and no hex color literal;
//   3. every screen entry in design/docs/screens.md is mapped to an existing blueprint in
//      design/specs/screens/README.md, that blueprint has a heading with the screen's name, and the
//      M4 blueprint inventory in design/docs/milestones.md lists it;
//   4. every enum in the committed OpenAPI document has a pt-BR label for each value in
//      design/specs/copy-guidelines.md §5.
//
//   node scripts/check-design-docs.mjs                     full design scope (rules 1–4)
//   node scripts/check-design-docs.mjs --scope components  M2/M3 only: links and token references in
//                                                          design/tokens and design/specs/components;
//                                                          no catalog, blueprint or copy coverage

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const designRoot = join(repoRoot, 'design');
const scopeIndex = process.argv.indexOf('--scope');
const scope = scopeIndex >= 0 ? process.argv[scopeIndex + 1] : 'full';
if (!['full', 'components'].includes(scope) || process.argv.length > (scopeIndex >= 0 ? 4 : 2)) {
  console.error('Usage: node scripts/check-design-docs.mjs [--scope full|components]');
  process.exit(2);
}
const componentScope = scope === 'components';
const errors = [];
const fail = (message) => errors.push(message);
const rel = (file) => relative(repoRoot, file);

function walk(dir, predicate, out = []) {
  for (const name of readdirSync(dir).sort()) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, predicate, out);
    else if (predicate(full)) out.push(full);
  }
  return out;
}

// GitHub-style heading slug: lowercase, drop punctuation except hyphens/underscores, spaces -> '-'.
const slug = (text) =>
  text
    .trim()
    .toLowerCase()
    .replace(/[`*_~]/g, '')
    .replace(/[^\p{L}\p{N}\s-]/gu, '')
    .replace(/\s/g, '-');
const headingCache = new Map();
function anchors(file) {
  if (!headingCache.has(file)) {
    const seen = new Map();
    const set = new Set();
    for (const line of readFileSync(file, 'utf8').split('\n')) {
      const match = /^#{1,6}\s+(.*)$/.exec(line);
      if (!match) continue;
      const base = slug(match[1]);
      const count = seen.get(base) ?? 0;
      set.add(count ? `${base}-${count}` : base);
      seen.set(base, count + 1);
    }
    headingCache.set(file, set);
  }
  return headingCache.get(file);
}

// 1. Links ------------------------------------------------------------------------------------
const markdown = componentScope
  ? [join(designRoot, 'tokens'), join(designRoot, 'specs/components')].filter((dir) => existsSync(dir)).flatMap((dir) => walk(dir, (file) => file.endsWith('.md')))
  : walk(designRoot, (file) => file.endsWith('.md'));
let linkCount = 0;
for (const file of markdown) {
  const text = readFileSync(file, 'utf8').replace(/```[\s\S]*?```/g, '');
  for (const match of text.matchAll(/\[[^\]]*\]\(([^)\s]+)\)/g)) {
    const target = match[1];
    if (/^(https?:|mailto:)/.test(target)) continue;
    linkCount += 1;
    const [path, anchor] = target.split('#');
    const destination = path ? resolve(dirname(file), decodeURIComponent(path)) : file;
    if (!existsSync(destination)) {
      fail(`${rel(file)}: broken link ${target}`);
      continue;
    }
    if (anchor && destination.endsWith('.md') && !anchors(destination).has(anchor)) {
      fail(`${rel(file)}: missing anchor #${anchor} in ${rel(destination)}`);
    }
  }
}

// 2. Token references ---------------------------------------------------------------------------
const tokens = {};
for (const name of readdirSync(join(designRoot, 'tokens')).filter((n) => n.endsWith('.json'))) {
  const data = JSON.parse(readFileSync(join(designRoot, 'tokens', name), 'utf8'));
  for (const [key, value] of Object.entries(data)) if (!key.startsWith('$')) tokens[key] = value;
}
function exists(node, segments) {
  if (segments.length === 0) return true;
  if (node === null || typeof node !== 'object' || Array.isArray(node)) return false;
  const [head, ...rest] = segments;
  if (head === '*') return Object.keys(node).some((key) => !key.startsWith('$') && exists(node[key], rest));
  return head in node && exists(node[head], rest);
}
function tokenExists(reference) {
  const segments = reference.split('.');
  const [prefix, ...rest] = segments;
  const themed = (base) => ['light', 'dark'].every((theme) => exists(tokens, [...base, theme, ...rest]));
  if (prefix === 'sem') return themed(['color', 'semantic']);
  if (prefix === 'dom') return themed(['color', 'domain']);
  if (prefix === 'shadow') return themed(['shadow']);
  return exists(tokens, segments);
}
const specFiles = [join(designRoot, 'specs/components'), ...(componentScope ? [] : [join(designRoot, 'specs/screens')])]
  .filter((dir) => existsSync(dir))
  .flatMap((dir) => walk(dir, (file) => file.endsWith('.md')));
const TOKEN_RE = /^(sem|dom|typography|spacing|radius|shadow|breakpoint|motion|layer)\.[A-Za-z0-9*-]+(\.[A-Za-z0-9*-]+)*$/;
let tokenRefs = 0;
for (const file of specFiles) {
  const text = readFileSync(file, 'utf8');
  for (const match of text.matchAll(/`([^`\n]+)`/g)) {
    const span = match[1].trim();
    if (!TOKEN_RE.test(span)) continue;
    tokenRefs += 1;
    if (!tokenExists(span)) fail(`${rel(file)}: unknown token \`${span}\``);
  }
  if (/color\.primitive/.test(text)) fail(`${rel(file)}: references a primitive color (specs use semantic/domain tokens only)`);
  for (const match of text.matchAll(/#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?\b/g)) fail(`${rel(file)}: raw color literal ${match[0]}`);
}

let mapped = 0;
let screens = [];
let enums = [];
if (!componentScope) {
// 3. Screen coverage ----------------------------------------------------------------------------
const catalog = readFileSync(join(designRoot, 'docs/screens.md'), 'utf8');
const start = catalog.indexOf('\n## Login\n');
const end = catalog.indexOf('\n## Backend contract gaps');
screens = [...catalog.slice(start, end).matchAll(/^## (.+)$/gm)].map((m) => m[1].trim());
const indexFile = join(designRoot, 'specs/screens/README.md');
if (!existsSync(indexFile)) fail('design/specs/screens/README.md is missing');
else {
  const index = readFileSync(indexFile, 'utf8');
  const rows = new Map();
  for (const line of index.split('\n')) {
    const cells = line.split('|').map((cell) => cell.trim());
    if (cells.length < 4) continue;
    const link = /\[[^\]]+\]\(([^)#]+)(?:#([^)]+))?\)/.exec(cells[2] ?? '');
    if (link) rows.set(cells[1], { file: link[1], anchor: link[2] });
  }
  for (const screen of screens) {
    const row = rows.get(screen);
    if (!row) {
      fail(`Screen "${screen}" has no row in design/specs/screens/README.md`);
      continue;
    }
    const blueprint = resolve(dirname(indexFile), row.file);
    if (!existsSync(blueprint)) {
      fail(`Screen "${screen}" maps to a missing blueprint ${row.file}`);
      continue;
    }
    const headings = [...readFileSync(blueprint, 'utf8').matchAll(/^#{2,4} (.+)$/gm)].map((m) => m[1].trim());
    if (!headings.some((heading) => heading === screen || heading.startsWith(`${screen} `))) {
      fail(`Blueprint ${row.file} has no heading for "${screen}"`);
    }
    mapped += 1;
  }
}

// The M4 inventory in design/docs/milestones.md is the coverage authority: it must list every entry.
const milestones = readFileSync(join(designRoot, 'docs/milestones.md'), 'utf8');
const inventoryStart = milestones.indexOf('### Blueprint inventory');
if (inventoryStart < 0) fail('design/docs/milestones.md has no "### Blueprint inventory" under M4');
else {
  const inventory = new Set(
    milestones
      .slice(inventoryStart)
      .split('\n')
      .filter((line) => line.startsWith('| '))
      .map((line) => line.split('|')[1].trim()),
  );
  for (const screen of screens) if (!inventory.has(screen)) fail(`Screen "${screen}" is missing from the M4 blueprint inventory`);
}

// 4. Copy labels for every contract enum --------------------------------------------------------
const openapi = JSON.parse(readFileSync(join(repoRoot, 'shared/api/openapi/server-api.openapi.json'), 'utf8'));
enums = Object.entries(openapi.components?.schemas ?? {}).filter(([, schema]) => Array.isArray(schema.enum));
const copy = readFileSync(join(designRoot, 'specs/copy-guidelines.md'), 'utf8');
for (const [name, schema] of enums) {
  const line = copy.split('\n').find((l) => l.startsWith(`**${name}**`));
  if (!line) {
    fail(`copy-guidelines.md §5 has no label map for ${name}`);
    continue;
  }
  for (const value of schema.enum) if (!line.includes(`\`${value}\``)) fail(`copy-guidelines.md §5 ${name} has no label for ${value}`);
}

}

if (errors.length) {
  for (const message of errors) console.error(`FAIL: ${message}`);
  console.error(`${errors.length} design-doc check failure(s).`);
  process.exit(1);
}
console.log(
  componentScope
    ? `Design docs valid (components scope): ${linkCount} local links, ${tokenRefs} token references in ${specFiles.length} component specs.`
    : `Design docs valid: ${linkCount} local links, ${tokenRefs} token references in ${specFiles.length} specs, ${mapped}/${screens.length} screens mapped to blueprints, ${enums.length} contract enums labelled.`,
);
