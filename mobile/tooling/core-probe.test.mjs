import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import ts from 'typescript';

const read = (name) => readFileSync(new URL(name, import.meta.url), 'utf8');
const probe = read('../src/probes/core-verification.ts');
const reference = JSON.parse(read('../src/probes/label-reference.json'));
const spec = read('../../design/specs/copy-guidelines.md')
  .split('## 5. Enum label maps')[1]
  .split('## 6. Error display')[0];
const expected = {};
for (const line of spec.split('\n')) {
  const name = /^\*\*(\w+)\*\*/.exec(line)?.[1];
  if (name)
    expected[name] = Object.fromEntries(
      [...line.matchAll(/`(\w+)` → "([^"]+)"/g)].map((match) => [match[1], match[2]]),
    );
}
test('native label oracle stays aligned to the independent copy contract', () => {
  assert.deepEqual(reference, expected);
});
function publicNames() {
  const entry = read('../../shared/core/src/index.ts');
  const source = ts.createSourceFile('index.ts', entry, ts.ScriptTarget.ES2022, true);
  const names = [];
  for (const node of source.statements) {
    if (ts.isExportDeclaration(node) && node.exportClause && ts.isNamedExports(node.exportClause)) {
      for (const item of node.exportClause.elements)
        if (!item.isTypeOnly) names.push(item.name.text);
    }
  }
  const labels = read('../../shared/core/src/labels/enums.ts');
  for (const match of labels.matchAll(/export (?:const|function) (\w+)/g)) names.push(match[1]);
  return names;
}
function uncovered(names, oracle, cases = probe) {
  const covered = new Set([...cases.matchAll(/\bc\.(\w+)/g)].map((match) => match[1]));
  for (const name of Object.keys(oracle))
    covered.add(name.charAt(0).toLowerCase() + name.slice(1) + 'Labels');
  return names.filter((name) => !covered.has(name));
}
test('every public runtime export has a native case or an exhaustive label-map case', () => {
  const names = publicNames();
  assert.deepEqual(uncovered(names, reference), []);
  // Shared core's pinned public surface (shared/core/test/boundary.test.ts): 124 runtime exports,
  // including the twelve custody/session and ten M9.7 settlement/classification label maps. A new
  // export must gain a native case.
  assert.equal(new Set(names).size, 124);
});
test('the oracle checks discriminate stale labels, missing values and missing maps', () => {
  const stale = structuredClone(reference);
  stale.SessionApprovalMode.Independent = 'Aprovação';
  assert.notDeepEqual(stale, expected);
  const missingValue = structuredClone(reference);
  delete missingValue.AccountType.Safe;
  assert.notDeepEqual(missingValue, expected);
  const missingMap = structuredClone(reference);
  delete missingMap.SessionCloseField;
  assert.notDeepEqual(missingMap, expected);
  assert.deepEqual(uncovered(publicNames(), missingMap), ['sessionCloseFieldLabels']);
  assert.deepEqual(uncovered(publicNames(), reference, probe.replace(/\bc\.formatBrl\b/g, 'x')), [
    'formatBrl',
  ]);
});
test('portable native core cases import only public core and the local copy oracle', () => {
  const source = ts.createSourceFile('probe.ts', probe, ts.ScriptTarget.ES2022, true);
  const imports = source.statements
    .filter(ts.isImportDeclaration)
    .map((node) => node.moduleSpecifier.text);
  assert.deepEqual(imports, ['@loto/core', './label-reference.json']);
});
