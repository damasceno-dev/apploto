import { test } from 'node:test';
import assert from 'node:assert/strict';
import { imeWindowVisible } from './native-ime.mjs';
test('a created input view does not justify Back when the actual IME window is hidden', () => {
  assert.equal(
    imeWindowVisible(
      'mInputShown=false\nmDecorViewVisible=false mWindowVisible=false\nmIsInputViewShown=true',
    ),
    false,
  );
});
test('an actually visible IME window can be dismissed without changing its settings', () => {
  assert.equal(
    imeWindowVisible(
      'mInputShown=true\nmDecorViewVisible=true mWindowVisible=true\nmIsInputViewShown=true',
    ),
    true,
  );
});
test('unknown window diagnostics fail rather than guessing whether Back would leave the app', () => {
  assert.throws(
    () => imeWindowVisible('mInputShown=true mIsInputViewShown=true'),
    /window visibility is missing/,
  );
});
