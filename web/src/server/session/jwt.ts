import 'server-only';

/** Renew this long before `exp`; the API validates lifetimes with zero clock skew. */
export const renewalMarginMs = 30_000;

/**
 * Reads only the registered `exp` claim (seconds) to schedule renewal. The signature is not
 * verified and no other claim is read: identity, branch, role and permission come from API
 * responses, and the API rejects any token it does not accept.
 */
export function readTokenExpiryMs(token: string): number | null {
  const payload = token.split('.')[1];
  if (payload === undefined) return null;
  try {
    const claims: unknown = JSON.parse(Buffer.from(payload, 'base64url').toString('utf8'));
    if (typeof claims !== 'object' || claims === null) return null;
    const exp = (claims as { exp?: unknown }).exp;
    return typeof exp === 'number' && Number.isFinite(exp) ? exp * 1000 : null;
  } catch {
    return null;
  }
}

/** A missing token needs one; an unreadable expiry is left for the API to judge. */
export function needsRenewal(token: string | null, nowMs: number): boolean {
  if (token === null) return true;
  const expiry = readTokenExpiryMs(token);
  return expiry !== null && expiry - renewalMarginMs <= nowMs;
}

export function isExpired(token: string | null, nowMs: number): boolean {
  if (token === null) return true;
  const expiry = readTokenExpiryMs(token);
  return expiry !== null && expiry <= nowMs;
}
