import type { CredentialEnvelope } from './types';
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function isServerId(value: unknown): value is string {
  return typeof value === 'string' && guid.test(value);
}
const text = (value: unknown): value is string =>
  typeof value === 'string' && value.length > 0 && value.length <= 16000;
export function parseCredentials(raw: string): CredentialEnvelope | null {
  try {
    const item = JSON.parse(raw) as Partial<CredentialEnvelope>;
    if (
      item.v !== 1 ||
      !item.user ||
      !isServerId(item.user.userId) ||
      !text(item.user.email) ||
      !text(item.user.name) ||
      !item.tokens ||
      !text(item.tokens.token) ||
      !text(item.tokens.refreshToken)
    )
      return null;
    if (
      item.branch !== null &&
      (!item.branch || !isServerId(item.branch.id) || !text(item.branch.token))
    )
      return null;
    // Copy only the versioned schema. Locally stored identity metadata confers no authority.
    return {
      v: 1,
      user: { userId: item.user.userId, email: item.user.email, name: item.user.name },
      tokens: { token: item.tokens.token, refreshToken: item.tokens.refreshToken },
      branch: item.branch === null ? null : { id: item.branch.id, token: item.branch.token },
    };
  } catch {
    return null;
  }
}
