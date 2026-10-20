#!/usr/bin/env node
// Regression suite for scripts/check-contrast.mjs. Every scenario edits a disposable copy of
// design/tokens (and, for enum cases, of the OpenAPI document) in a temp directory, runs the
// checker against that copy, and expects a pass or a specific failure. The real token files are
// hashed before and after the run to prove they were never modified.
//
//   node scripts/test-check-contrast.mjs

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { cpSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync, unlinkSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const checker = join(repoRoot, 'scripts/check-contrast.mjs');
const sourceTokens = join(repoRoot, 'design/tokens');
const sourceOpenApi = join(repoRoot, 'shared/api/openapi/server-api.openapi.json');

const hashTree = () =>
  readdirSync(sourceTokens)
    .sort()
    .map((name) => `${name}:${createHash('sha256').update(readFileSync(join(sourceTokens, name))).digest('hex')}`)
    .join('\n');

const before = hashTree();
const root = mkdtempSync(join(tmpdir(), 'loto-token-check-'));
let passed = 0;

function fixture() {
  const dir = mkdtempSync(join(root, 'case-'));
  cpSync(sourceTokens, join(dir, 'tokens'), { recursive: true });
  cpSync(sourceOpenApi, join(dir, 'openapi.json'));
  return dir;
}
const readJson = (file) => JSON.parse(readFileSync(file, 'utf8'));
const writeJson = (file, data) => writeFileSync(file, `${JSON.stringify(data, null, 2)}\n`);
function editColors(dir, edit) {
  const file = join(dir, 'tokens/colors.json');
  const data = readJson(file);
  edit(data);
  writeJson(file, data);
}
function run(dir) {
  try {
    const out = execFileSync(process.execPath, [checker, '--tokens', join(dir, 'tokens'), '--openapi', join(dir, 'openapi.json')], {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    return { code: 0, out };
  } catch (error) {
    return { code: error.status ?? 1, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
}
function expectPass(label, prepare = () => {}) {
  const dir = fixture();
  prepare(dir);
  const { code, out } = run(dir);
  if (code !== 0) throw new Error(`FAIL: ${label} should pass\n${out}`);
  passed += 1;
  console.log(`PASS: ${label}`);
}
function expectFailure(label, diagnostic, prepare) {
  const dir = fixture();
  prepare(dir);
  const { code, out } = run(dir);
  if (code === 0) throw new Error(`FAIL: ${label} unexpectedly passed`);
  if (!out.includes(diagnostic)) throw new Error(`FAIL: ${label} returned the wrong diagnostic (wanted "${diagnostic}")\n${out}`);
  passed += 1;
  console.log(`PASS: ${label}`);
}

try {
  expectPass('true source passes in a disposable copy');

  expectFailure('missing reference target', 'broken reference {color.primitive.neutral.999}', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.light.text.primary = '{color.primitive.neutral.999}';
    }),
  );
  expectFailure('reference cycle', 'Reference cycle', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.light.text.secondary = '{color.semantic.light.text.muted}';
      d.color.semantic.light.text.muted = '{color.semantic.light.text.secondary}';
    }),
  );
  expectFailure('malformed reference', 'malformed reference', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.dark.text.link = '{color.primitive.blue.300';
    }),
  );
  expectFailure('reference to a group is ambiguous', 'is ambiguous', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.light.border.default = '{color.primitive.neutral}';
    }),
  );
  expectFailure('malformed key', 'malformed key', (dir) =>
    editColors(dir, (d) => {
      d.color.primitive.neutral['9.5'] = '#123456';
    }),
  );
  expectFailure('invalid color value', 'invalid color', (dir) =>
    editColors(dir, (d) => {
      d.color.primitive.blue['600'] = '#00GGZZ';
    }),
  );
  expectFailure('literal in the semantic layer', 'must be references', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.light.surface.default = '#ffffff';
    }),
  );
  expectFailure('cross-theme reference', 'may reference only primitives or light semantic tokens', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.light.text.link = '{color.semantic.dark.text.link}';
    }),
  );
  expectFailure('missing dark value', 'color.semantic.dark: missing text.muted', (dir) =>
    editColors(dir, (d) => {
      delete d.color.semantic.dark.text.muted;
    }),
  );
  expectFailure('missing dark theme', 'missing required theme "dark"', (dir) =>
    editColors(dir, (d) => {
      delete d.color.domain.dark;
    }),
  );
  expectFailure('missing required domain', 'missing required domain "aging-bucket"', (dir) =>
    editColors(dir, (d) => {
      delete d.color.domain.dark['aging-bucket'];
    }),
  );
  expectFailure('missing enum value in a domain', 'missing enum value Rejected', (dir) =>
    editColors(dir, (d) => {
      delete d.color.domain.light['daily-close-status'].Rejected;
      delete d.color.domain.dark['daily-close-status'].Rejected;
    }),
  );
  expectFailure('invented enum value', 'Reopened is not a DailyCloseStatus value', (dir) =>
    editColors(dir, (d) => {
      for (const theme of ['light', 'dark']) d.color.domain[theme]['daily-close-status'].Reopened = { ...d.color.domain[theme]['daily-close-status'].Draft };
    }),
  );
  expectFailure('enum renamed in the contract', 'enum InvitationStatus not found in the OpenAPI document', (dir) => {
    const file = join(dir, 'openapi.json');
    const data = readJson(file);
    data.components.schemas.InvitationStatusRenamed = data.components.schemas.InvitationStatus;
    delete data.components.schemas.InvitationStatus;
    writeJson(file, data);
  });
  expectFailure('failing text pair', 'Contrast (light) "body text on every surface": text.muted', (dir) =>
    editColors(dir, (d) => {
      d.color.primitive.neutral['500'] = '#9aa9b3';
    }),
  );
  expectFailure('failing essential control boundary', 'Contrast (dark) "essential control boundary": border.control', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.dark.border.control = '{color.primitive.night.500}';
    }),
  );
  expectFailure('failing domain badge', 'Contrast (dark) "domain daily-close-status.Submitted"', (dir) =>
    editColors(dir, (d) => {
      d.color.domain.dark['daily-close-status'].Submitted.fg = '{color.primitive.amber.700}';
    }),
  );
  expectFailure('translucent background without an opaque base', 'needs an opaque base', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.pairs.push({ id: 'bad stack', kind: 'text', fg: ['text.primary'], bg: ['surface.selected'] });
    }),
  );
  expectFailure('unjustified large-text exemption', 'needs a justification', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.pairs.push({ id: 'large', kind: 'large-text', fg: ['text.muted'], bg: ['surface.default'] });
    }),
  );
  expectFailure('relaxed threshold', 'may not be relaxed', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.thresholds.text = 4;
    }),
  );
  expectFailure('token escaping the pair list', 'Coverage (light): nav.text-muted is never checked as text', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.pairs = d.$contrast.pairs.filter((pair) => pair.id !== 'navigation group labels');
    }),
  );
  expectFailure('domain surfaces emptied', 'missing required surface "surface.default"', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.domainSurfaces = [];
    }),
  );
  expectFailure('selected-row surface dropped from domain checks', 'missing required surface "surface.default + surface.selected"', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.domainSurfaces = d.$contrast.domainSurfaces.filter((surface) => !Array.isArray(surface));
    }),
  );
  expectFailure('white text on the light Submitted badge', 'Contrast (light) "domain daily-close-status.Submitted"', (dir) =>
    editColors(dir, (d) => {
      d.color.domain.light['daily-close-status'].Submitted.fg = '{color.primitive.neutral.0}';
    }),
  );
  expectFailure('primary hover state removed', 'action.primary: missing state token(s) bg-hover', (dir) =>
    editColors(dir, (d) => {
      delete d.color.semantic.light.action.primary['bg-hover'];
      delete d.color.semantic.dark.action.primary['bg-hover'];
    }),
  );
  expectFailure('ghost pressed state removed', 'action.ghost: missing state token(s) bg-active', (dir) =>
    editColors(dir, (d) => {
      delete d.color.semantic.light.action.ghost['bg-active'];
      delete d.color.semantic.dark.action.ghost['bg-active'];
    }),
  );
  expectFailure('failing hover fill', 'Contrast (dark) "action primary bg-hover"', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.dark.action.primary['bg-hover'] = '{color.primitive.blue.400}';
    }),
  );
  expectFailure('selection indicator failing on a selected row', 'Contrast (dark) "selection indicator', (dir) =>
    editColors(dir, (d) => {
      d.color.semantic.dark.selection.indicator = '{color.primitive.blue.500}';
    }),
  );
  expectFailure('selection indicator escaping the checks', 'selection.indicator is never checked as a non-text indicator', (dir) =>
    editColors(dir, (d) => {
      d.$contrast.pairs = d.$contrast.pairs.filter((pair) => !pair.id.startsWith('selection indicator'));
    }),
  );
  expectFailure('missing category file', 'Missing required token file: motion.json', (dir) => unlinkSync(join(dir, 'tokens/motion.json')));
  expectFailure('invalid JSON', 'layers.json: invalid JSON', (dir) => writeFileSync(join(dir, 'tokens/layers.json'), '{ "layer": { "base": 0, } }'));
  expectFailure('primitive color used outside the color layer', 'non-color tokens may reference color primitives only through a semantic token', (dir) => {
    const file = join(dir, 'tokens/shadows.json');
    const data = readJson(file);
    data.shadow.light.raised.color = '{color.primitive.alpha.ink-05}';
    writeJson(file, data);
  });
  expectFailure('invalid motion easing', 'easing must be a cubic-bezier', (dir) => {
    const file = join(dir, 'tokens/motion.json');
    const data = readJson(file);
    data.motion.easing.standard = [2, 0, 0, 1];
    writeJson(file, data);
  });
} finally {
  rmSync(root, { recursive: true, force: true });
}

if (hashTree() !== before) {
  console.error('FAIL: design/tokens changed during the test run');
  process.exit(1);
}
console.log(`${passed} token-check scenarios passed (disposable copies only; source tokens unchanged).`);
