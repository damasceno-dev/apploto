import { Buffer } from 'node:buffer';
import test from 'node:test';
import assert from 'node:assert/strict';
import { assertReleaseBoundary, releaseMarkers } from './check-bundles.mjs';
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
  for (const marker of releaseMarkers)
    for (const encoding of ['utf8', 'utf16le'])
      assert.throws(() =>
        assertReleaseBoundary({ sources: [] }, Buffer.from(marker, encoding), 'android'),
      );
});
test('production gate rejects a leaked component gallery by module path or by its strings', () => {
  assert.throws(
    () =>
      assertReleaseBoundary(
        { sources: ['/Project/mobile/src/probes/gallery/sections-data.tsx'] },
        Buffer.from('release'),
        'android',
      ),
    /technical probe module/,
  );
  // A gallery inlined into a shipped module (no probe path in the map) is still caught by bytecode.
  for (const encoding of ['utf8', 'utf16le'])
    assert.throws(
      () =>
        assertReleaseBoundary(
          { sources: ['/Project/mobile/src/ui/feedback.tsx'] },
          Buffer.concat([
            Buffer.from('header'),
            Buffer.from('Galeria técnica de componentes', encoding),
          ]),
          'ios',
        ),
      /technical marker/,
    );
  // Shipping the reusable components and theme is allowed.
  assertReleaseBoundary(
    { sources: ['/Project/mobile/src/ui/feedback.tsx', '/Project/mobile/src/theme/provider.tsx'] },
    Buffer.from('Resultado não confirmado'),
    'ios',
  );
});
