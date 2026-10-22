import 'server-only';
import type { BranchSummary, SessionUser } from '@/lib/bff-protocol';

// Every session cookie is host-only (`__Host-`), `HttpOnly`, `Secure`, `SameSite=Lax` and `Path=/`.
// The prefix makes the browser refuse the cookie unless those attributes hold, so a weakened
// attribute fails loudly instead of silently. Local development uses `http://localhost`, which
// browsers treat as a secure context (see `docs/milestones.md`, Web M2 evidence).
export const cookieNames = Object.freeze({
  identity: '__Host-loto-id',
  refresh: '__Host-loto-rt',
  branch: '__Host-loto-br',
  meta: '__Host-loto-meta',
  intent: '__Host-loto-intent',
});

/**
 * A random, non-secret browser id (not a credential, not cleared at logout). It lets one process
 * serialize this browser's session commands; nothing is authorized by it.
 */
export const deviceCookieName = '__Host-loto-device';
const deviceMaxAgeSeconds = 400 * 24 * 60 * 60;

/** The refresh token lives 7 days from its rotation (`SharedValidators.RefreshTokenExpirationTimeInDays`). */
export const sessionCookieMaxAgeSeconds = 7 * 24 * 60 * 60;
const intentMaxAgeSeconds = 30 * 60;

export interface CookieAttributes {
  readonly httpOnly: true;
  readonly secure: true;
  readonly sameSite: 'lax';
  readonly path: '/';
  readonly maxAge: number;
}

export function cookieAttributes(name: string): CookieAttributes {
  return {
    httpOnly: true,
    secure: true,
    sameSite: 'lax',
    path: '/',
    maxAge:
      name === cookieNames.intent
        ? intentMaxAgeSeconds
        : name === deviceCookieName
          ? deviceMaxAgeSeconds
          : sessionCookieMaxAgeSeconds,
  };
}

/** Session facts kept beside the tokens. Not authority: the server rechecks every request. */
export interface SessionMeta {
  readonly v: 1;
  /** Identity generation: minted at login, kept through renewal, ended by logout or another login. */
  readonly sid: string;
  /** Scope generation: changes with every login and branch selection/leave. */
  readonly scope: string;
  readonly user: SessionUser;
  /** The selected branch, kept to recreate its session after renewal. */
  readonly branchId: string | null;
  readonly branch: BranchSummary | null;
  /** Why the last branch (re)opening failed, verbatim, until the next selection. */
  readonly notice: readonly string[] | null;
}

/** A gated deep link held until the gate passes, bound to what was known when it was captured. */
export interface NavigationIntent {
  readonly v: 1;
  readonly path: string;
  readonly branchId: string | null;
  readonly email: string | null;
}

export interface SessionCookies {
  readonly identity: string | null;
  readonly refresh: string | null;
  readonly branch: string | null;
  readonly meta: SessionMeta | null;
  readonly intent: NavigationIntent | null;
  readonly device: string | null;
}

export interface CookieReader {
  get(name: string): { readonly value: string } | undefined;
}

/** A pending change: a value to set, or `null` to delete. */
export interface CookieWrite {
  readonly name: string;
  readonly value: string | null;
}

export function readSessionCookies(reader: CookieReader): SessionCookies {
  const text = (name: string) => {
    const value = reader.get(name)?.value;
    return value === undefined || value.length === 0 ? null : value;
  };
  return {
    identity: text(cookieNames.identity),
    refresh: text(cookieNames.refresh),
    branch: text(cookieNames.branch),
    meta: decodeJson(text(cookieNames.meta), isSessionMeta),
    intent: decodeJson(text(cookieNames.intent), isNavigationIntent),
    device: validDevice(text(deviceCookieName)),
  };
}

export function encodeMeta(meta: SessionMeta): string {
  return encodeJson(meta);
}

export function encodeIntent(intent: NavigationIntent): string {
  return encodeJson(intent);
}

/** Deletes every session cookie (logout, a definitively lost session). */
export function clearAllWrites(): CookieWrite[] {
  return Object.values(cookieNames).map((name) => ({ name, value: null }));
}

function validDevice(value: string | null): string | null {
  return value !== null && /^[0-9a-f-]{36}$/.test(value) ? value : null;
}

function encodeJson(value: unknown): string {
  return Buffer.from(JSON.stringify(value), 'utf8').toString('base64url');
}

function decodeJson<T>(raw: string | null, guard: (value: unknown) => value is T): T | null {
  if (raw === null) return null;
  try {
    const parsed: unknown = JSON.parse(Buffer.from(raw, 'base64url').toString('utf8'));
    return guard(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isString(value: unknown): value is string {
  return typeof value === 'string';
}

function isUser(value: unknown): value is SessionUser {
  return isRecord(value) && isString(value.userId) && isString(value.name) && isString(value.email);
}

function isBranch(value: unknown): value is BranchSummary {
  return (
    isRecord(value) &&
    isString(value.id) &&
    isString(value.name) &&
    (value.role === 'Admin' || value.role === 'Manager' || value.role === 'Member')
  );
}

function isSessionMeta(value: unknown): value is SessionMeta {
  return (
    isRecord(value) &&
    value.v === 1 &&
    isString(value.sid) &&
    isString(value.scope) &&
    isUser(value.user) &&
    (value.branchId === null || isString(value.branchId)) &&
    (value.branch === null || isBranch(value.branch)) &&
    (value.notice === null || (Array.isArray(value.notice) && value.notice.every(isString)))
  );
}

function isNavigationIntent(value: unknown): value is NavigationIntent {
  return (
    isRecord(value) &&
    value.v === 1 &&
    isString(value.path) &&
    (value.branchId === null || isString(value.branchId)) &&
    (value.email === null || isString(value.email))
  );
}
