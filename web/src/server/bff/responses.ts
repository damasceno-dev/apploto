import 'server-only';
import { bffErrorHeader, type BffErrorCode } from '@/lib/bff-protocol';
import { cookieAttributes, type CookieWrite } from '../session/cookies';

/** Authentication responses and personalized data never enter a shared or browser cache. */
export const noStoreHeaders: Readonly<Record<string, string>> = Object.freeze({
  'cache-control': 'no-store, private',
  'x-content-type-options': 'nosniff',
});

const bffErrorStatus: Readonly<Record<BffErrorCode, number>> = {
  'missing-bff-header': 400,
  'forbidden-origin': 403,
  'unsupported-media-type': 415,
  'unknown-operation': 404,
  'invalid-path': 404,
  'authorization-not-allowed': 400,
  'body-too-large': 413,
  'session-scope-changed': 409,
  'session-lost': 401,
  'branch-unavailable': 401,
  'no-branch-session': 401,
  'renewal-unconfirmed': 503,
};

/**
 * A response the BFF produced without forwarding: the request was not executed. The body is empty;
 * the code travels in `x-loto-bff-error` so it is never confused with a backend `ResponseErrorJson`.
 */
export function bffErrorResponse(code: BffErrorCode): Response {
  return new Response(null, {
    status: bffErrorStatus[code],
    headers: { ...noStoreHeaders, [bffErrorHeader]: code },
  });
}

export function jsonResponse(body: unknown, status = 200, headers: HeadersInit = {}): Response {
  const merged = new Headers(headers);
  for (const [name, value] of Object.entries(noStoreHeaders)) merged.set(name, value);
  merged.set('content-type', 'application/json; charset=utf-8');
  return new Response(JSON.stringify(body), { status, headers: merged });
}

/** The minimal cookie surface of `NextResponse.cookies`, so the same code serves tests. */
export interface ResponseCookieJar {
  set(name: string, value: string, options: ReturnType<typeof cookieAttributes>): unknown;
}

/**
 * Applies cookie writes. A deletion repeats `Secure; Path=/` with `Max-Age=0`: browsers ignore a
 * `__Host-` deletion that omits them, which would silently keep the credential.
 */
export function applyCookieWrites(jar: ResponseCookieJar, writes: readonly CookieWrite[]): void {
  for (const write of writes) {
    const attributes = cookieAttributes(write.name);
    if (write.value === null) jar.set(write.name, '', { ...attributes, maxAge: 0 });
    else jar.set(write.name, write.value, attributes);
  }
}
