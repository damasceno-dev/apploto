import 'server-only';
import { bffRequestHeader, type BffErrorCode } from '@/lib/bff-protocol';

/**
 * Request rules shared by every BFF route:
 * - the `x-loto-bff: 1` header (a cross-site page cannot add it without a CORS preflight, which the
 *   BFF never grants);
 * - for state changes, an `Origin` equal to the application's origin and, when the browser sends it,
 *   `Sec-Fetch-Site: same-origin`. SameSite=Lax cookies are the third layer, not the only one.
 */
export function checkBffRequest(
  request: Request,
  allowedOrigin: string,
  stateChanging: boolean,
): BffErrorCode | null {
  if (request.headers.get(bffRequestHeader) !== '1') return 'missing-bff-header';
  if (!stateChanging) return null;
  const origin = request.headers.get('origin');
  if (origin === null || origin !== allowedOrigin) return 'forbidden-origin';
  const site = request.headers.get('sec-fetch-site');
  if (site !== null && site !== 'same-origin') return 'forbidden-origin';
  return null;
}

export function isStateChanging(method: string): boolean {
  return method !== 'GET' && method !== 'HEAD';
}

/** `LOTO_WEB_ORIGIN` when configured (behind a proxy), otherwise the request's own origin. */
export function allowedOriginFor(request: Request, configured: string | null): string {
  return configured ?? new URL(request.url).origin;
}
