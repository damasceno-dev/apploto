/** Android InputMethodService keeps mIsInputViewShown true after its window is hidden. */
export function imeWindowVisible(diagnostics) {
  const state = /\bmWindowVisible=(true|false)\b/.exec(diagnostics);
  if (!state) throw new Error('Unsupported native IME diagnostics: window visibility is missing.');
  return state[1] === 'true';
}
