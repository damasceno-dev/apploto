export class DevicePrivacyError extends Error {
  constructor() {
    super('Este conteúdo não pode ser guardado como rascunho neste dispositivo.');
    this.name = 'DevicePrivacyError';
  }
}
const forbidden =
  /^(?:.*(?:password|senha|passwd|token|secret|credential|authorization|bearer|assertion|capability|challenge|nonce|signature|apikey|etag|ifmatch).*|version|code|state|sig|jwt)$/i;
const credential =
  /(?:^eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*$|[?#&](?:[^=&#]*(?:token|secret|password|senha|auth|key|code|state|challenge)[^=&#]*)=)/i;
export function assertDeviceSafe(value: unknown, depth = 0): void {
  if (depth > 32) throw new DevicePrivacyError();
  if (typeof value === 'string') {
    if (credential.test(value.trim())) throw new DevicePrivacyError();
    return;
  }
  if (typeof value === 'number' && !Number.isFinite(value)) throw new DevicePrivacyError();
  if (Array.isArray(value)) {
    for (const item of value) assertDeviceSafe(item, depth + 1);
    return;
  }
  if (value !== null && typeof value === 'object') {
    if (Object.getPrototypeOf(value) !== Object.prototype && Object.getPrototypeOf(value) !== null)
      throw new DevicePrivacyError();
    for (const [key, item] of Object.entries(value)) {
      if (forbidden.test(key.replace(/[^a-z]/gi, ''))) throw new DevicePrivacyError();
      assertDeviceSafe(item, depth + 1);
    }
  } else if (
    typeof value === 'function' ||
    typeof value === 'symbol' ||
    typeof value === 'bigint' ||
    typeof value === 'undefined'
  )
    throw new DevicePrivacyError();
}
