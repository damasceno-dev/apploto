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
  encodeSlot,
  readSessionCookies,
  slotCookieName,
  type BranchSlot,
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

/** Branch ids are GUIDs, as the API returns them (`/f/{branchId}` accepts nothing else). */
export const B1 = '11111111-1111-4111-8111-111111111111';
export const B2 = '22222222-2222-4222-8222-222222222222';
export const B3 = '33333333-3333-4333-8333-333333333333';
export const B9 = '99999999-9999-4999-8999-999999999999';

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
    this.branchesHandler = () => ok([branch(B1)]);
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
    v: 2,
    sid: 'sid-1',
    scope: 'scope-1',
    user: { userId: 'user-ana', name: 'Ana', email: 'ana@example.invalid' },
    ...overrides,
  };
}

/** A branch session held by identity generation `sid-1` unless overridden. */
export function slot(
  branchId: string,
  token: string,
  overrides: Partial<BranchSlot> = {},
): BranchSlot {
  return {
    v: 1,
    sid: 'sid-1',
    gen: `gen-${branchId.slice(0, 2)}`,
    token,
    branch: branch(branchId),
    at: 0,
    ...overrides,
  };
}

export function sessionCookies(
  values: Partial<Omit<SessionCookies, 'slots' | 'branchCookieNames'>> & {
    readonly slots?: readonly BranchSlot[];
  },
): SessionCookies {
  const { slots = [], ...rest } = values;
  return {
    identity: null,
    refresh: null,
    meta: null,
    intent: null,
    device: null,
    ...rest,
    slots: new Map(slots.map((held) => [held.branch.id, held])),
    branchCookieNames: slots.map((held) => slotCookieName(held.branch.id)),
  };
}

/** A cookie reader over a plain map (both `get` and `getAll`, as Next's cookie stores offer). */
export function cookieReader(jar: ReadonlyMap<string, string>) {
  return {
    get: (name: string) => (jar.has(name) ? { value: jar.get(name) ?? '' } : undefined),
    getAll: () => [...jar].map(([name, value]) => ({ name, value })),
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
  put(cookieNames.meta, cookies.meta ? encodeMeta(cookies.meta) : null);
  put(cookieNames.intent, cookies.intent ? encodeIntent(cookies.intent) : null);
  for (const held of cookies.slots.values()) put(slotCookieName(held.branch.id), encodeSlot(held));
  for (const write of writes) put(write.name, write.value);
  return readSessionCookies(cookieReader(jar));
}
