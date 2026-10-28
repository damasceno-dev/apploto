import { test } from 'node:test';
import assert from 'node:assert/strict';
import { nativeAppId } from './native-app.mjs';
test('keeps automation on the assigned app unless a disposable probe is selected', () => {
  assert.equal(nativeAppId({}), 'com.example.lotero.development');
  assert.equal(
    nativeAppId({ LOTO_NATIVE_APP_ID: 'com.example.lotero.nativeproof.m220261027' }),
    'com.example.lotero.nativeproof.m220261027',
  );
});
test('rejects unrelated app identifiers rather than stopping another app', () => {
  assert.throws(
    () => nativeAppId({ LOTO_NATIVE_APP_ID: 'com.example.other' }),
    /owned nativeproof/,
  );
});
