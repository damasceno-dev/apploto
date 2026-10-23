import 'server-only';
import type { BranchSummary, SessionUser } from '@/lib/bff-protocol';
import { isBranchId } from '@/lib/branch-route';

// Every session cookie is host-only (`__Host-`), `HttpOnly`, `Secure`, `SameSite=Lax` and `Path=/`.
// The prefix makes the browser refuse the cookie unless those attributes hold, so a weakened
// attribute fails loudly instead of silently. Local development uses `http://localhost`, which
// browsers treat as a secure context (see `docs/milestones.md`, Web M2 evidence).
export const cookieNames = Object.freeze({
  identity: '__Host-loto-id',
  refresh: '__Host-loto-rt',
  meta: '__Host-loto-meta',
  intent: '__Host-loto-intent',
});

/**
 * One cookie per branch the signed-in identity is working in (D-M2-1: each tab keeps its own
 * branch context through its `/f/{branchId}` route, so one browser can hold several branch sessions
 * at once without one tab's choice overwriting another's). The cookie name carries the branch id;
 * the value is a `BranchSlot` bound to the identity generation that created it.
 */
export const branchSlotPrefix = '__Host-loto-b-';
/** Bound on concurrently held branch sessions (cookie-header size); the oldest is evicted. */
export const maxBranchSlots = 4;
/** The single branch cookie of the earlier one-branch-per-browser model: always cleared. */
const legacyBranchCookie = '__Host-loto-br';

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

/** Identity facts kept beside the tokens. Not authority: the server rechecks every request. */
export interface SessionMeta {
  readonly v: 2;
  /** Identity generation: minted at login, kept through renewal, ended by logout or another login. */
  readonly sid: string;
  /** Identity scope: changes with every login and logout; a tab's scope starts with it. */
  readonly scope: string;
  readonly user: SessionUser;
}

/** One branch session of the signed-in identity. Never readable by page JavaScript. */
export interface BranchSlot {
  readonly v: 1;
  /** The identity generation that opened it: a slot of another login never authorizes. */
  readonly sid: string;
  /** Branch generation: new when this branch's authority is re-established (lost, role changed). */
  readonly gen: string;
  readonly token: string;
  /** The session-creation summary (its role is not the live one: `GET /branch/current` is). */
  readonly branch: BranchSummary;
  /** When this slot was opened (server clock, ms): eviction order. */
  readonly at: number;
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
  readonly meta: SessionMeta | null;
  readonly intent: NavigationIntent | null;
  readonly device: string | null;
  /** Readable slots by branch id (any identity generation; callers check `sid`). */
  readonly slots: ReadonlyMap<string, BranchSlot>;
  /** Every branch cookie present, readable or not: what a full clear must delete. */
  readonly branchCookieNames: readonly string[];
}

export interface CookieReader {
  get(name: string): { readonly value: string } | undefined;
  getAll(): readonly { readonly name: string; readonly value: string }[];
}

/**
 * A pending change: a value to set, or `null` to delete. A branch-slot write names the branch
 * generation it belongs to, so the fence can drop it when another request ended that generation.
 */
export interface CookieWrite {
  readonly name: string;
  readonly value: string | null;
  readonly generation?: string;
}

export function slotCookieName(branchId: string): string {
  if (!isBranchId(branchId)) throw new RangeError('Not a branch id.');
  return `${branchSlotPrefix}${branchId}`;
}

export function readSessionCookies(reader: CookieReader): SessionCookies {
  const text = (name: string) => {
    const value = reader.get(name)?.value;
    return value === undefined || value.length === 0 ? null : value;
  };
  const slots = new Map<string, BranchSlot>();
  const branchCookieNames: string[] = [];
  for (const { name, value } of reader.getAll()) {
    if (name === legacyBranchCookie) {
      branchCookieNames.push(name);
      continue;
    }
    if (!name.startsWith(branchSlotPrefix)) continue;
    branchCookieNames.push(name);
    const branchId = name.slice(branchSlotPrefix.length);
    const slot = decodeJson(value.length === 0 ? null : value, isBranchSlot);
    if (isBranchId(branchId) && slot !== null && slot.branch.id === branchId) {
      slots.set(branchId, slot);
    }
  }
  return {
    identity: text(cookieNames.identity),
    refresh: text(cookieNames.refresh),
    meta: decodeJson(text(cookieNames.meta), isSessionMeta),
    intent: decodeJson(text(cookieNames.intent), isNavigationIntent),
    device: validDevice(text(deviceCookieName)),
    slots,
    branchCookieNames,
  };
}

export function encodeMeta(meta: SessionMeta): string {
  return encodeJson(meta);
}

export function encodeIntent(intent: NavigationIntent): string {
  return encodeJson(intent);
}

export function encodeSlot(slot: BranchSlot): string {
  return encodeJson(slot);
}

/** Opens or refreshes one branch slot; evicts the oldest other slots beyond `maxBranchSlots`. */
export function slotWrites(cookies: SessionCookies, slot: BranchSlot): CookieWrite[] {
  const writes: CookieWrite[] = [
    { name: slotCookieName(slot.branch.id), value: encodeSlot(slot), generation: slot.gen },
  ];
  const others = [...cookies.slots.values()]
    .filter((other) => other.branch.id !== slot.branch.id)
    .sort((a, b) => b.at - a.at);
  for (const evicted of others.slice(maxBranchSlots - 1)) {
    writes.push({ name: slotCookieName(evicted.branch.id), value: null });
  }
  return writes;
}

/** Drops one branch slot (its branch was refused for this identity). */
export function slotDeletion(branchId: string): CookieWrite {
  return { name: slotCookieName(branchId), value: null };
}

/** Deletes every session cookie (logout, a definitively lost session, a new login). */
export function clearAllWrites(cookies: Pick<SessionCookies, 'branchCookieNames'>): CookieWrite[] {
  return [
    ...Object.values(cookieNames).map((name) => ({ name, value: null })),
    ...cookies.branchCookieNames.map((name) => ({ name, value: null })),
  ];
}

/** Deletes every branch slot (a new login starts without another identity's branch sessions). */
export function clearSlotWrites(cookies: Pick<SessionCookies, 'branchCookieNames'>): CookieWrite[] {
  return cookies.branchCookieNames.map((name) => ({ name, value: null }));
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
    isBranchId(value.id) &&
    isString(value.name) &&
    (value.role === 'Admin' || value.role === 'Manager' || value.role === 'Member')
  );
}

function isSessionMeta(value: unknown): value is SessionMeta {
  return (
    isRecord(value) &&
    value.v === 2 &&
    isString(value.sid) &&
    isString(value.scope) &&
    isUser(value.user)
  );
}

function isBranchSlot(value: unknown): value is BranchSlot {
  return (
    isRecord(value) &&
    value.v === 1 &&
    isString(value.sid) &&
    isString(value.gen) &&
    value.gen.length > 0 &&
    !value.gen.includes('.') &&
    isString(value.token) &&
    value.token.length > 0 &&
    isBranch(value.branch) &&
    typeof value.at === 'number' &&
    Number.isFinite(value.at)
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
