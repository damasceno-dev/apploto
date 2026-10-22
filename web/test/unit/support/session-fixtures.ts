import {
  ApiError,
  type RequestUserLoginJson,
  type ResponseBranchSummaryJson,
  type ResponseCreateBranchSessionJson,
  type ResponseTokenJson,
  type ResponseUserLoginJson,
} from '@loto/api';
import type { AuthBackend, BackendResult } from '@/server/session/backend';
import {
  cookieNames,
  encodeIntent,
  encodeMeta,
  readSessionCookies,
  type CookieWrite,
  type SessionCookies,
  type SessionMeta,
} from '@/server/session/cookies';
import { createSessionRegistry, type SessionRegistry } from '@/server/session/registry';
import type { SessionContext } from '@/server/session/service';

/** A token shaped like the API's JWTs: only `exp` matters to the BFF. */
export function jwt(label: string, expiresAtMs: number): string {
  const encode = (value: object) => Buffer.from(JSON.stringify(value)).toString('base64url');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ exp: Math.floor(expiresAtMs / 1000), label })}.c2ln`;
}

export function branch(
  id: string,
  role: ResponseBranchSummaryJson['role'] = 'Admin',
): ResponseBranchSummaryJson {
  return { id, name: `Lotérica ${id}`, role };
}

export function httpError(
  method: string,
  path: string,
  status: number,
  messages: string[],
  headers: HeadersInit = {},
): ApiError {
  return new ApiError({
    kind: 'http',
    method,
    path,
    dispatched: true,
    status,
    headers: new Headers(headers),
    body: 'error-json',
    errorMessages: messages,
  });
}

export function networkError(method: string, path: string): ApiError {
  return new ApiError({ kind: 'network', method, path, dispatched: true });
}

type Handler<A extends unknown[], T> = (...args: A) => Promise<BackendResult<T>> | BackendResult<T>;

/** A programmable `AuthBackend` that records every call. */
export class FakeBackend implements AuthBackend {
  calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];
  /** Clock the fake mints tokens against. */
  now: () => number;
  ttlMs = 60 * 60_000;
  private counter = 0;

  loginHandler: Handler<[RequestUserLoginJson], ResponseUserLoginJson>;
  renewHandler: Handler<[string], ResponseTokenJson>;
  logoutHandler: Handler<[string, string], void> = () => ok(undefined);
  branchHandler: Handler<[string, string], ResponseCreateBranchSessionJson>;
  branchesHandler: Handler<[string], readonly ResponseBranchSummaryJson[]>;

  constructor(now: () => number) {
    this.now = now;
    this.loginHandler = (body) =>
      ok({
        userId: `user-${body.email}`,
        name: `Pessoa ${body.email}`,
        email: body.email,
        responseToken: this.tokenPair(body.email),
      });
    this.renewHandler = (refresh) => ok(this.tokenPair(`renewed-from-${refresh}`));
    this.branchHandler = (identity, branchId) =>
      ok({
        token: this.token(`branch-${branchId}-${identity.slice(-6)}`),
        branch: branch(branchId),
      });
    this.branchesHandler = () => ok([branch('b1')]);
  }

  token(label: string): string {
    this.counter += 1;
    return jwt(`${label}#${this.counter}`, this.now() + this.ttlMs);
  }

  tokenPair(label: string): ResponseTokenJson {
    this.counter += 1;
    return {
      token: this.token(`identity-${label}`),
      refreshToken: `refresh-${label}-${this.counter}`,
    };
  }

  private record(method: string, args: unknown[]) {
    this.calls.push({ method, args });
  }

  count(method: string): number {
    return this.calls.filter((call) => call.method === method).length;
  }

  async login(body: RequestUserLoginJson) {
    this.record('login', [body]);
    return this.loginHandler(body);
  }
  async renew(refreshToken: string) {
    this.record('renew', [refreshToken]);
    return this.renewHandler(refreshToken);
  }
  async logout(identityToken: string, refreshToken: string) {
    this.record('logout', [identityToken, refreshToken]);
    return this.logoutHandler(identityToken, refreshToken);
  }
  async createBranchSession(identityToken: string, branchId: string) {
    this.record('createBranchSession', [identityToken, branchId]);
    return this.branchHandler(identityToken, branchId);
  }
  async myBranches(identityToken: string) {
    this.record('myBranches', [identityToken]);
    return this.branchesHandler(identityToken);
  }
}

export function ok<T>(data: T): BackendResult<T> {
  return { ok: true, data, headers: new Headers() };
}

export function fail<T>(error: ApiError): BackendResult<T> {
  return { ok: false, error };
}

/** A manually advanced clock shared by the registry and the fake backend. */
export class Clock {
  constructor(public ms = Date.UTC(2026, 9, 21, 12, 0, 0)) {}
  now = () => this.ms;
  advance(ms: number) {
    this.ms += ms;
  }
}

export function setup(clock = new Clock()) {
  const registry: SessionRegistry = createSessionRegistry(clock.now);
  const backend = new FakeBackend(clock.now);
  let ids = 0;
  const context = (): SessionContext => ({
    registry,
    backend,
    owner: {},
    newId: () => `id-${(ids += 1)}`,
  });
  return { clock, registry, backend, context };
}

export function meta(overrides: Partial<SessionMeta> = {}): SessionMeta {
  return {
    v: 1,
    sid: 'sid-1',
    scope: 'scope-1',
    user: { userId: 'user-ana', name: 'Ana', email: 'ana@example.invalid' },
    branchId: 'b1',
    branch: branch('b1'),
    notice: null,
    ...overrides,
  };
}

export function sessionCookies(values: Partial<SessionCookies>): SessionCookies {
  return {
    identity: null,
    refresh: null,
    branch: null,
    meta: null,
    intent: null,
    device: null,
    ...values,
  };
}

/** Applies writes to a cookie map the way a browser would, for chained requests. */
export function applyWrites(
  cookies: SessionCookies,
  writes: readonly CookieWrite[],
): SessionCookies {
  const jar = new Map<string, string>();
  const put = (name: string, value: string | null) =>
    value === null ? jar.delete(name) : jar.set(name, value);
  put(cookieNames.identity, cookies.identity);
  put(cookieNames.refresh, cookies.refresh);
  put(cookieNames.branch, cookies.branch);
  put(cookieNames.meta, cookies.meta ? encodeMeta(cookies.meta) : null);
  put(cookieNames.intent, cookies.intent ? encodeIntent(cookies.intent) : null);
  for (const write of writes) put(write.name, write.value);
  return readSessionCookies({
    get: (name) => (jar.has(name) ? { value: jar.get(name) ?? '' } : undefined),
  });
}
