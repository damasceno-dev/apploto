import { Buffer } from 'node:buffer';
import test from 'node:test';
import assert from 'node:assert/strict';
import { assertReleaseBoundary } from './check-bundles.mjs';
test('production gate rejects a technical module even when its exports are unreachable', () => {
  assert.throws(() =>
    assertReleaseBoundary(
      { sources: ['/mobile/src/probes/native-fixture.ts'] },
      Buffer.from('release'),
      'ios',
    ),
  );
  assertReleaseBoundary(
    { sources: ['/mobile/src/session/provider.tsx'] },
    Buffer.from('release'),
    'ios',
  );
});
test('production gate rejects fault/probe strings in either Hermes string encoding', () => {
  for (const marker of [
    'Medir Intl no Hermes',
    'DEV fixture response intentionally lost',
    '/__native-fixture',
  ])
    for (const encoding of ['utf8', 'utf16le'])
      assert.throws(() =>
        assertReleaseBoundary({ sources: [] }, Buffer.from(marker, encoding), 'android'),
      );
});
