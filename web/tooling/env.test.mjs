import assert from 'node:assert/strict';
import test from 'node:test';
import { validateApiBaseUrl } from '../src/config/env-validation.ts';

test('server URL fails with useful errors and accepts inert configuration', () => {
  assert.throws(() => validateApiBaseUrl(undefined), /obrigatória/);
  for (const value of [
    'relative',
    'ftp://localhost',
    'http://name:password@localhost',
    'https://example.test?key=x',
  ])
    assert.throws(() => validateApiBaseUrl(value), /LOTO_API_BASE_URL/);
  assert.equal(validateApiBaseUrl('http://127.0.0.1:59999'), 'http://127.0.0.1:59999');
});
