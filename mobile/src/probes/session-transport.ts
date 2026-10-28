// DEV-only fault injection for an owned loopback stack. No credential or response is logged.
let loseNextCreate = false;
const permitted = () => __DEV__ && process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF === '1';
export function loseNextFixtureResponse() {
  if (permitted()) loseNextCreate = true;
}
export const sessionTransport: typeof fetch = async (input, init) => {
  const response = await globalThis.fetch(input, init);
  const url = new URL(String(input));
  if (
    permitted() &&
    loseNextCreate &&
    ['127.0.0.1', 'localhost', '10.0.2.2'].includes(url.hostname) &&
    init?.method === 'POST' &&
    url.pathname.toLowerCase() === '/transaction'
  ) {
    loseNextCreate = false;
    await response.text();
    throw new TypeError('DEV fixture response intentionally lost after server response');
  }
  return response;
};
