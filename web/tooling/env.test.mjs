import assert from 'node:assert/strict';
import test from 'node:test';
import { validateApiBaseUrl, validateWebOrigin } from '../src/config/env-validation.ts';

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

test('web origin is optional and must be a bare HTTP(S) origin', () => {
  assert.equal(validateWebOrigin(undefined), null);
  assert.equal(validateWebOrigin(''), null);
  assert.equal(validateWebOrigin('https://app.example'), 'https://app.example');
  for (const value of [
    'app.example',
    'https://app.example/',
    'https://app.example/path',
    'ftp://x',
  ])
    assert.throws(() => validateWebOrigin(value), /LOTO_WEB_ORIGIN/);
});
