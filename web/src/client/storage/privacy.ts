/**
 * Device storage never receives credentials (catalog: Durable scoped input; Capability links):
 * passwords, bearer/refresh tokens, Google assertions and sign-in challenges, capability tokens or
 * URLs carrying secret query/fragment values. Values are checked before anything is written.
 */
export class DevicePrivacyError extends Error {
  constructor(readonly path: string) {
    super(`Refusing to store a credential-like value at ${path}.`);
    this.name = 'DevicePrivacyError';
  }
}

const forbiddenKeys = new Set([
  'password',
  'senha',
  'currentpassword',
  'newpassword',
  'confirmpassword',
  'repeatpassword',
  'token',
  'accesstoken',
  'refreshtoken',
  'identitytoken',
  'branchtoken',
  'idtoken',
  'bearer',
  'authorization',
  'credential',
  'credentials',
  'challengeid',
  'nonce',
  'capability',
  'capabilitytoken',
  'resettoken',
  'invitationtoken',
  'responsetoken',
]);

const jwtLike = /^eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*$/;
const secretUrlParameter = /[?#&](?:token|access_token|id_token|refresh_token|code|state)=/i;

export function assertDeviceSafe(value: unknown, path = '$', depth = 0): void {
  if (depth > 32) throw new DevicePrivacyError(path);
  if (typeof value === 'string') {
    if (jwtLike.test(value.trim()) || secretUrlParameter.test(value)) {
      throw new DevicePrivacyError(path);
    }
    return;
  }
  if (Array.isArray(value)) {
    value.forEach((item, index) => assertDeviceSafe(item, `${path}[${index}]`, depth + 1));
    return;
  }
  if (typeof value === 'object' && value !== null) {
    for (const [key, item] of Object.entries(value)) {
      if (forbiddenKeys.has(key.toLowerCase().replace(/[^a-z]/g, ''))) {
        throw new DevicePrivacyError(`${path}.${key}`);
      }
      assertDeviceSafe(item, `${path}.${key}`, depth + 1);
    }
  }
}
