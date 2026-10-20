#!/usr/bin/env node
// Regression suite for scripts/check-design-docs.mjs. Each scenario copies design/ and scripts/ into
// a temp directory (other areas are linked read-only, because the checker only reads them), edits
// the copy, and expects a pass or a specific failure. design/ is hashed before and after.
//
//   node scripts/test-check-design-docs.mjs

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { appendFileSync, cpSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const designRoot = join(repoRoot, 'design');

function hashTree(dir) {
  const hash = createHash('sha256');
  const walk = (current) => {
    for (const name of readdirSync(current).sort()) {
      const full = join(current, name);
      if (statSync(full).isDirectory()) walk(full);
      else hash.update(`${relative(dir, full)}\n`).update(readFileSync(full));
    }
  };
  walk(dir);
  return hash.digest('hex');
}

const before = hashTree(designRoot);
const root = mkdtempSync(join(tmpdir(), 'loto-docs-check-'));
let passed = 0;

function fixture() {
  const dir = mkdtempSync(join(root, 'case-'));
  cpSync(designRoot, join(dir, 'design'), { recursive: true });
  cpSync(join(repoRoot, 'scripts'), join(dir, 'scripts'), { recursive: true });
  for (const area of ['docs', 'server', 'shared', 'web', 'mobile', 'infra']) {
    try {
      symlinkSync(join(repoRoot, area), join(dir, area));
    } catch {
      // An absent area is fine; the checker reports any link that needed it.
    }
  }
  return dir;
}
const edit = (dir, file, change) => {
  const path = join(dir, file);
  writeFileSync(path, change(readFileSync(path, 'utf8')));
};
function run(dir, args = []) {
  try {
    return { code: 0, out: execFileSync(process.execPath, [join(dir, 'scripts/check-design-docs.mjs'), ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }) };
  } catch (error) {
    return { code: error.status ?? 1, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
}
function expect(label, diagnostic, prepare = () => {}, args = []) {
  const dir = fixture();
  prepare(dir);
  const { code, out } = run(dir, args);
  if (diagnostic === null && code !== 0) throw new Error(`FAIL: ${label} should pass\n${out}`);
  if (diagnostic !== null && code === 0) throw new Error(`FAIL: ${label} unexpectedly passed`);
  if (diagnostic !== null && !out.includes(diagnostic)) throw new Error(`FAIL: ${label} returned the wrong diagnostic (wanted "${diagnostic}")\n${out}`);
  passed += 1;
  console.log(`PASS: ${label}`);
}
const replaceOnce = (from, to) => (text) => {
  if (!text.includes(from)) throw new Error(`fixture edit did not match: ${from}`);
  return text.replace(from, to);
};

try {
  expect('true source passes in a disposable copy', null);
  expect('broken relative link', 'broken link ../missing.md', (dir) =>
    appendFileSync(join(dir, 'design/specs/components/card.md'), '\nSee [missing](../missing.md).\n'),
  );
  expect('missing heading anchor', 'missing anchor #nao-existe', (dir) =>
    appendFileSync(join(dir, 'design/specs/components/card.md'), '\nSee [catalog](../../docs/screens.md#nao-existe).\n'),
  );
  expect('unknown semantic token', 'unknown token `sem.text.mutedx`', (dir) =>
    edit(dir, 'design/specs/components/form-field.md', replaceOnce('`sem.text.muted`', '`sem.text.mutedx`')),
  );
  expect('unknown domain group', 'unknown token `dom.aging-buckets.*`', (dir) =>
    edit(dir, 'design/specs/components/badge.md', replaceOnce('`dom.aging-bucket.*`', '`dom.aging-buckets.*`')),
  );
  expect('primitive reference in a spec', 'references a primitive color', (dir) =>
    appendFileSync(join(dir, 'design/specs/components/card.md'), '\nUse `color.primitive.blue.600`.\n'),
  );
  expect('raw color literal in a spec', 'raw color literal #005ca9', (dir) =>
    appendFileSync(join(dir, 'design/specs/screens/operator-day.md'), '\nFill #005ca9.\n'),
  );
  expect('screen without an index row', 'Screen "Open day" has no row', (dir) =>
    edit(dir, 'design/specs/screens/README.md', (text) => text.split('\n').filter((line) => !line.startsWith('| Open day |')).join('\n')),
  );
  expect('blueprint without the screen heading', 'has no heading for "Open day"', (dir) =>
    edit(dir, 'design/specs/screens/operator-day.md', replaceOnce('\n## Open day\n', '\n## Opening\n')),
  );
  expect('screen missing from the M4 inventory', 'is missing from the M4 blueprint inventory', (dir) =>
    edit(dir, 'design/docs/milestones.md', (text) => text.split('\n').filter((line) => !line.startsWith('| Open day |')).join('\n')),
  );
  expect('contract enum value without a copy label', 'MonthlyReconciliationBlockerType has no label for MissingExpectedClose', (dir) =>
    edit(dir, 'design/specs/copy-guidelines.md', replaceOnce(' · `MissingExpectedClose` → "Fechamento ausente"', '')),
  );
  // M2/M3 in isolation: committed catalog/copy/milestones and no blueprints.
  const isolate = (dir) => {
    for (const file of ['design/docs/screens.md', 'design/docs/product.md', 'design/docs/milestones.md', 'design/specs/copy-guidelines.md']) {
      writeFileSync(join(dir, file), execFileSync('git', ['show', `HEAD:${file}`], { cwd: repoRoot, encoding: 'utf8' }));
    }
    rmSync(join(dir, 'design/specs/screens'), { recursive: true, force: true });
  };
  expect('components scope passes on an M2/M3-only tree', null, isolate, ['--scope', 'components']);
  expect('full scope still demands the catalog and blueprints', 'design/specs/screens/README.md is missing', isolate);
  expect('components scope still rejects an unknown token', 'unknown token `sem.selection.indicatorx`', (dir) => {
    isolate(dir);
    edit(dir, 'design/specs/components/tabs.md', replaceOnce('`sem.selection.indicator`', '`sem.selection.indicatorx`'));
  }, ['--scope', 'components']);
} finally {
  rmSync(root, { recursive: true, force: true });
}

if (hashTree(designRoot) !== before) {
  console.error('FAIL: design/ changed during the test run');
  process.exit(1);
}
console.log(`${passed} design-doc check scenarios passed (disposable copies only; design/ unchanged).`);
