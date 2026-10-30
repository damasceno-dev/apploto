import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('every branch catalog template owns a concrete guarded Expo route', async () => {
  const source = await readFile(new URL('../src/shell/policy.ts', import.meta.url), 'utf8');
  const paths = [
    ...source.matchAll(
      /'((?:\/(?:gestao|turno|fechamentos|transacoes|clientes|relatorios))[^']*)'/g,
    ),
  ].map((match) => match[1]);
  assert.ok(new Set(paths).size >= 35);
  for (const path of new Set(paths)) {
    const filename = path.replace(/:(id|year|month)/g, '[$1]');
    const screen = await readFile(new URL(`../src/app${filename}.tsx`, import.meta.url), 'utf8');
    assert.match(screen, /MobileScreen/);
    assert.doesNotMatch(screen, /@loto\/api|fetch\(/);
  }
});
