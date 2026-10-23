import 'server-only';
import {
  bffApiPrefix,
  branchHeader,
  parseScope,
  scopeHeader,
  sessionOutcomeHeader,
  type BffErrorCode,
  type SessionOutcome,
} from '@/lib/bff-protocol';
import { isBranchId } from '@/lib/branch-route';
import { clearAllWrites, type CookieWrite, type SessionCookies } from '../session/cookies';
import {
  branchLostWrites,
  ensureCredentials,
  type CredentialFailure,
  type SessionContext,
} from '../session/service';
import { apiSegments, matchOperation, type ForwardedOperation } from './operations';
import { checkBffRequest, isStateChanging } from './policy';
import { bffErrorResponse, noStoreHeaders } from './responses';

export const maxForwardBodyBytes = 1024 * 1024;

/** Request headers the contract depends on; everything else (cookies, hop-by-hop) stays behind. */
const forwardedRequestHeaders = ['accept', 'content-type'] as const;
/**
 * Response headers the client core reads; backend `Set-Cookie` and hop-by-hop headers never pass.
 * `Date` is the API's own clock, which bounds idempotency replay windows.
 */
const forwardedResponseHeaders = ['content-type', 'date', 'etag', 'retry-after'] as const;

export interface ForwarderConfig {
  readonly context: SessionContext;
  readonly apiBaseUrl: string;
  readonly allowedOrigin: string;
  readonly fetch?: typeof globalThis.fetch;
}

export interface ForwardResult {
  readonly response: Response;
  /** Cookie changes from renewal/recovery, not yet fenced. */
  readonly writes: readonly CookieWrite[];
}

/**
 * The cookie-to-bearer forwarder. It reaches only the configured API, only for operations in the
 * generated allowlist, with the bearer its tier needs. A request refused before dispatch carries
 * `x-loto-bff-error` (nothing executed); a forwarded response carries the backend's status, body
 * bytes and contract headers unchanged. A forwarded 401 starts one recovery cycle: a GET is sent
 * once more after a successful recovery; any other method is never replayed.
 */
export async function forward(
  request: Request,
  cookies: SessionCookies,
  config: ForwarderConfig,
): Promise<ForwardResult> {
  const method = request.method.toUpperCase();
  const refused = (code: BffErrorCode, writes: readonly CookieWrite[] = []): ForwardResult => ({
    response: bffErrorResponse(code),
    writes,
  });

  const policy = checkBffRequest(request, config.allowedOrigin, isStateChanging(method));
  if (policy !== null) return refused(policy);

  const url = new URL(request.url);
  if (!url.pathname.startsWith(`${bffApiPrefix}/`)) return refused('invalid-path');
  const rawPath = url.pathname.slice(bffApiPrefix.length);
  const segments = apiSegments(rawPath);
  if (segments === null) return refused('invalid-path');
  const operation = matchOperation(method, segments);
  if (operation === null) return refused('unknown-operation');
  if (request.headers.has('authorization')) return refused('authorization-not-allowed');

  // The tab's branch (its route): which of this identity's own branch sessions to use. Untrusted
  // context — it never grants anything the API would not grant that identity for that branch.
  const branchId = request.headers.get(branchHeader);
  if (branchId !== null && !isBranchId(branchId)) return refused('invalid-branch');
  let declaredGeneration: string | null = null;
  if (operation.authTier !== 'anonymous') {
    if (cookies.meta === null) return refused('session-lost');
    const declared = parseScope(request.headers.get(scopeHeader));
    if (declared === null || declared.identity !== cookies.meta.scope) {
      return refused('session-scope-changed');
    }
    if (operation.authTier === 'branch') {
      if (branchId === null || declared.branch === null) return refused('no-branch-session');
      declaredGeneration = declared.branch;
    }
  }

  let body: ArrayBuffer | null = null;
  if (isStateChanging(method)) {
    const declared = Number(request.headers.get('content-length') ?? '0');
    if (declared > maxForwardBodyBytes) return refused('body-too-large');
    body = await request.arrayBuffer();
    if (body.byteLength > maxForwardBodyBytes) return refused('body-too-large');
    if (body.byteLength === 0) body = null;
  }

  const credentials = await ensureCredentials(config.context, cookies, operation.authTier, {
    write: isStateChanging(method),
    branchId,
    declaredGeneration,
  });
  if (!credentials.ok) return refused(refusalFor(credentials.failure), credentials.writes);
  const writes: CookieWrite[] = [...credentials.writes];

  const send = (token: string | null) =>
    dispatch(config, operation, rawPath + url.search, method, request, body, token);
  let upstream = await send(credentials.token);
  let outcome: SessionOutcome | null = null;

  if (
    upstream instanceof Response &&
    upstream.status === 401 &&
    operation.authTier !== 'anonymous'
  ) {
    const recovery = await ensureCredentials(
      config.context,
      credentials.cookies,
      operation.authTier,
      { force: true, write: isStateChanging(method), branchId, declaredGeneration },
    );
    writes.push(...recovery.writes);
    outcome = recovery.ok ? 'renewed' : outcomeFor(recovery.failure);
    if (recovery.ok && method === 'GET') {
      upstream = await send(recovery.token);
      if (upstream instanceof Response && upstream.status === 401) {
        // A second 401 ends recovery: the branch gate for branch work, Login for identity work.
        if (operation.authTier === 'branch' && branchId !== null) {
          outcome = 'branch-unavailable';
          writes.push(...branchLostWrites(config.context, branchId, recovery.slot));
        } else {
          outcome = 'lost';
          writes.push(...clearAllWrites(cookies));
        }
      }
    }
  }

  if (!(upstream instanceof Response)) {
    // Dispatched without a usable answer: a write's outcome is unknown, so no BFF marker is added.
    return { response: new Response(null, { status: 502, headers: noStoreHeaders }), writes };
  }
  return { response: await relay(upstream, outcome), writes };
}

async function dispatch(
  config: ForwarderConfig,
  operation: ForwardedOperation,
  pathAndQuery: string,
  method: string,
  request: Request,
  body: ArrayBuffer | null,
  token: string | null,
): Promise<Response | Error> {
  const headers = new Headers();
  for (const name of [...forwardedRequestHeaders, ...operation.headers]) {
    const value = request.headers.get(name);
    if (value !== null) headers.set(name, value);
  }
  if (token !== null) headers.set('authorization', `Bearer ${token}`);
  try {
    return await (config.fetch ?? globalThis.fetch)(targetUrl(config.apiBaseUrl, pathAndQuery), {
      method,
      headers,
      body,
      redirect: 'manual',
      cache: 'no-store',
      signal: request.signal,
    });
  } catch (error) {
    return error instanceof Error ? error : new Error(String(error));
  }
}

/** Joins the configured base (origin plus optional path prefix) and the validated API path. */
export function targetUrl(apiBaseUrl: string, pathAndQuery: string): URL {
  const base = new URL(apiBaseUrl);
  const [path = '', query] = pathAndQuery.split('?', 2) as [string, string | undefined];
  const target = new URL(base.origin);
  target.pathname = base.pathname.replace(/\/+$/, '') + path;
  if (query !== undefined) target.search = `?${query}`;
  if (target.origin !== base.origin) throw new Error('Forwarding target left the configured API.');
  return target;
}

async function relay(upstream: Response, outcome: SessionOutcome | null): Promise<Response> {
  const headers = new Headers(noStoreHeaders);
  for (const name of forwardedResponseHeaders) {
    const value = upstream.headers.get(name);
    if (value !== null) headers.set(name, value);
  }
  if (outcome !== null) headers.set(sessionOutcomeHeader, outcome);
  if (upstream.status >= 300 && upstream.status < 400) {
    // Redirects are not followed or exposed: the API never answers a contract call with one.
    await upstream.body?.cancel();
    return new Response(null, { status: 502, headers });
  }
  const empty = upstream.status === 204 || upstream.status === 205 || upstream.status === 304;
  const bytes = empty ? null : await upstream.arrayBuffer();
  return new Response(bytes, { status: upstream.status, headers });
}

function refusalFor(failure: CredentialFailure): BffErrorCode {
  switch (failure) {
    case 'session-lost':
      return 'session-lost';
    case 'branch-unavailable':
      return 'branch-unavailable';
    case 'no-branch-session':
      return 'no-branch-session';
    case 'renewal-unconfirmed':
      return 'renewal-unconfirmed';
    case 'stale':
      return 'session-scope-changed';
  }
}

function outcomeFor(failure: CredentialFailure): SessionOutcome {
  switch (failure) {
    case 'session-lost':
      return 'lost';
    case 'branch-unavailable':
    case 'no-branch-session':
      return 'branch-unavailable';
    case 'renewal-unconfirmed':
      return 'unconfirmed';
    case 'stale':
      return 'stale';
  }
}
