import 'server-only';
import { randomUUID } from 'node:crypto';
import type {
  ApiError,
  AuthTier,
  RequestUserLoginJson,
  ResponseBranchSummaryJson,
} from '@loto/api';
import { anonymousSnapshot, type SessionSnapshot, type SessionState } from '@/lib/bff-protocol';
import { validateIntentPath } from '@/lib/route-policy';
import type { AuthBackend } from './backend';
import {
  clearAllWrites,
  cookieNames,
  encodeMeta,
  type CookieWrite,
  type SessionCookies,
  type SessionMeta,
} from './cookies';
import { isExpired, needsRenewal } from './jwt';
import {
  endIdentity,
  endScope,
  isIdentityEnded,
  isScopeEnded,
  noteDeviceScope,
  type RequestOwner,
  type SessionRegistry,
} from './registry';
import { openBranchSession, renewIdentity } from './renewal';

/** Everything one BFF request needs; `owner` is unique per request. */
export interface SessionContext {
  readonly registry: SessionRegistry;
  readonly backend: AuthBackend;
  readonly owner: RequestOwner;
  /** The browser's device id, when known: scope changes are recorded against it. */
  readonly device?: string;
  readonly fixtures?: boolean;
  readonly newId?: () => string;
}

export type CredentialFailure =
  | 'session-lost'
  | 'branch-unavailable'
  | 'no-branch-session'
  | 'renewal-unconfirmed'
  /** Credentials older than a rotation or than a generation another request ended: not a loss. */
  | 'stale';

export type Credentials =
  | {
      readonly ok: true;
      readonly token: string | null;
      readonly writes: readonly CookieWrite[];
      /** The cookies after any renewal in this request. */
      readonly cookies: SessionCookies;
      readonly renewed: boolean;
    }
  | {
      readonly ok: false;
      readonly failure: CredentialFailure;
      readonly messages: readonly string[];
      readonly writes: readonly CookieWrite[];
    };

/**
 * Supplies the token an operation's tier needs. It renews first when the token is missing, expires
 * within the margin, or when `force`d by a 401 (one recovery cycle); identity renewal and
 * branch-session recreation each run through their single flight. It never retries a business call.
 */
export interface CredentialOptions {
  /** A 401 started one recovery cycle: renew even if the token looks valid. */
  readonly force?: boolean;
  /**
   * The request would change server state. While a renewal's outcome is unknown, writes stay
   * paused even on a still-valid bearer (catalog: lost renewal response); reads may use it.
   */
  readonly write?: boolean;
}

export async function ensureCredentials(
  context: SessionContext,
  cookies: SessionCookies,
  tier: AuthTier,
  options: CredentialOptions = {},
): Promise<Credentials> {
  const force = options.force === true;
  const write = options.write === true;
  if (tier === 'anonymous') {
    return { ok: true, token: null, writes: [], cookies, renewed: false };
  }
  const { registry, backend, owner } = context;
  const meta = cookies.meta;
  if (meta === null || (cookies.identity === null && cookies.refresh === null)) {
    return failure('session-lost', [], meta === null && hasTokens(cookies) ? clearAllWrites() : []);
  }
  if (isIdentityEnded(registry, meta.sid, owner) || isScopeEnded(registry, meta.scope, owner)) {
    return failure('stale', [], []);
  }

  const now = registry.now();
  const writes: CookieWrite[] = [];
  let current = cookies;
  let renewed = false;
  if (force || needsRenewal(current.identity, now)) {
    const usable = !force && current.identity !== null && !isExpired(current.identity, now);
    if (current.refresh === null) {
      if (!usable) return failure('session-lost', [], clearAllWrites());
    } else {
      const outcome = await renewIdentity(registry, backend, current.refresh);
      switch (outcome.kind) {
        case 'renewed':
          renewed = true;
          current = { ...current, identity: outcome.identity, refresh: outcome.refresh };
          writes.push(
            { name: cookieNames.identity, value: outcome.identity },
            { name: cookieNames.refresh, value: outcome.refresh },
          );
          break;
        case 'stale':
          return failure('stale', [], []);
        case 'rejected':
          return failure('session-lost', outcome.messages, clearAllWrites());
        case 'unknown':
          if (!usable || write) return failure('renewal-unconfirmed', [], writes);
          break;
      }
    }
  }
  const identity = current.identity;
  if (tier === 'identity' || identity === null) {
    return { ok: true, token: identity, writes, cookies: current, renewed };
  }

  if (meta.branchId === null) return failure('no-branch-session', [], writes);
  if (force || needsRenewal(current.branch, now)) {
    const usable = !force && current.branch !== null && !isExpired(current.branch, now);
    const outcome = await openBranchSession(registry, backend, identity, meta.branchId);
    switch (outcome.kind) {
      case 'created': {
        const nextMeta: SessionMeta = { ...meta, branch: outcome.branch, notice: null };
        current = { ...current, branch: outcome.token, meta: nextMeta };
        writes.push(
          { name: cookieNames.branch, value: outcome.token },
          { name: cookieNames.meta, value: encodeMeta(nextMeta) },
        );
        renewed = true;
        break;
      }
      case 'unavailable':
        return failure('branch-unavailable', outcome.messages, [
          ...writes,
          ...branchLostWrites(context, meta, outcome.messages),
        ]);
      case 'rejected':
        if (outcome.status === 401) {
          return failure('session-lost', outcome.messages, clearAllWrites());
        }
        return failure('branch-unavailable', outcome.messages, [
          ...writes,
          ...branchLostWrites(context, meta, outcome.messages),
        ]);
      case 'unknown':
        if (!usable || write) return failure('renewal-unconfirmed', [], writes);
        break;
    }
  }
  return { ok: true, token: current.branch, writes, cookies: current, renewed };
}

/**
 * The fence. `base` is the generation the request arrived with. If **another** request ended that
 * identity generation (logout, another login), nothing is written; if it ended that scope (branch
 * change elsewhere), only identity rotation is written. A late renewal can therefore never write old
 * credentials back after logout, login or a branch switch. Clearing a lost session ends it.
 */
export function fenceWrites(
  context: Pick<SessionContext, 'registry' | 'owner'>,
  base: Pick<SessionMeta, 'sid' | 'scope'> | null,
  writes: readonly CookieWrite[],
): CookieWrite[] {
  const { registry, owner } = context;
  if (base === null) return [...writes];
  if (isIdentityEnded(registry, base.sid, owner)) return [];
  const scopeLive = !isScopeEnded(registry, base.scope, owner);
  const allowed = writes.filter(
    (write) =>
      scopeLive || write.name === cookieNames.identity || write.name === cookieNames.refresh,
  );
  if (allowed.some((write) => write.name === cookieNames.refresh && write.value === null)) {
    endIdentity(registry, base.sid, owner);
    endScope(registry, base.scope, owner);
  }
  return allowed;
}

export interface SessionResolution {
  readonly snapshot: SessionSnapshot;
  readonly writes: readonly CookieWrite[];
}

/**
 * Resolves the authority state for `GET /bff/session` and the page gate: renews when needed,
 * recreates the selected branch's session, or — with no selected branch — reads the identity's
 * branches (zero → empty state, one → opened automatically, several → choice).
 */
export async function resolveSession(
  context: SessionContext,
  cookies: SessionCookies,
  options: { readonly needBranches?: boolean } = {},
): Promise<SessionResolution> {
  const meta = cookies.meta;
  if (meta === null || (cookies.identity === null && cookies.refresh === null)) {
    return {
      snapshot: anonymousSnapshot,
      writes: meta === null && hasTokens(cookies) ? clearAllWrites() : [],
    };
  }
  if (meta.branchId !== null) {
    const credentials = await ensureCredentials(context, cookies, 'branch');
    if (credentials.ok) {
      return {
        snapshot: snapshotOf(credentials.cookies.meta ?? meta, 'branch'),
        writes: credentials.writes,
      };
    }
    return resolveFailure(context, cookies, meta, credentials);
  }

  const identity = await ensureCredentials(context, cookies, 'identity');
  if (!identity.ok) return resolveFailure(context, cookies, meta, identity);
  if (options.needBranches === false) {
    return {
      snapshot: snapshotOf(meta, meta.notice ? 'branch-unavailable' : 'choose-branch'),
      writes: identity.writes,
    };
  }
  const listed = await listBranches(context, identity.token ?? '');
  const resolved = await chooseOrOpen(context, identity.cookies, meta, listed, false);
  return { snapshot: resolved.snapshot, writes: [...identity.writes, ...resolved.writes] };
}

async function resolveFailure(
  context: SessionContext,
  cookies: SessionCookies,
  meta: SessionMeta,
  credentials: Extract<Credentials, { ok: false }>,
): Promise<SessionResolution> {
  switch (credentials.failure) {
    case 'session-lost':
      return {
        snapshot: { ...anonymousSnapshot, messages: credentials.messages },
        writes: credentials.writes,
      };
    case 'renewal-unconfirmed':
      return { snapshot: snapshotOf(meta, 'renewal-unconfirmed'), writes: credentials.writes };
    case 'stale': {
      // These cookies belong to a generation that ended or to a rotation already superseded: they
      // never establish authority, and nothing is written (newer cookies may be on their way). An
      // ended identity reads as signed out; an ended scope as identity without a branch; a
      // superseded rotation as an unconfirmed renewal (writes paused, Login offered).
      const { registry, owner } = context;
      const snapshot = isIdentityEnded(registry, meta.sid, owner)
        ? anonymousSnapshot
        : isScopeEnded(registry, meta.scope, owner)
          ? { ...snapshotOf(meta, 'choose-branch'), scope: null }
          : { ...snapshotOf(meta, 'renewal-unconfirmed'), scope: null };
      return { snapshot, writes: [] };
    }
    case 'no-branch-session':
      return { snapshot: snapshotOf(meta, 'choose-branch'), writes: credentials.writes };
    case 'branch-unavailable': {
      const identity = await ensureCredentials(context, { ...cookies, branch: null }, 'identity');
      const listed = identity.ok ? await listBranches(context, identity.token ?? '') : null;
      const left = metaFromWrites(credentials.writes) ?? leftBranchMeta(meta, credentials.messages);
      return {
        snapshot: {
          ...snapshotOf(left, 'branch-unavailable'),
          branches: listed?.ok ? listed.branches : null,
        },
        writes: [...credentials.writes, ...(identity.ok ? identity.writes : [])],
      };
    }
  }
}

type BranchList =
  | { readonly ok: true; readonly branches: readonly ResponseBranchSummaryJson[] }
  | { readonly ok: false; readonly messages: readonly string[] };

async function listBranches(context: SessionContext, identityToken: string): Promise<BranchList> {
  const result = await context.backend.myBranches(identityToken);
  return result.ok
    ? { ok: true, branches: result.data }
    : { ok: false, messages: result.error.errorMessages };
}

async function chooseOrOpen(
  context: SessionContext,
  cookies: SessionCookies,
  meta: SessionMeta,
  listed: BranchList,
  keepScope: boolean,
): Promise<SessionResolution> {
  if (!listed.ok) {
    return {
      snapshot: { ...snapshotOf(meta, 'choose-branch'), messages: listed.messages },
      writes: [],
    };
  }
  if (listed.branches.length === 0) {
    return { snapshot: { ...snapshotOf(meta, 'no-memberships'), branches: [] }, writes: [] };
  }
  const only = listed.branches.length === 1 ? listed.branches[0] : undefined;
  if (only === undefined) {
    return {
      snapshot: { ...snapshotOf(meta, 'choose-branch'), branches: listed.branches },
      writes: [],
    };
  }
  // Exactly one branch: open it without the picker (catalog, Branch picker / session).
  const opened = await selectBranch(context, { ...cookies, meta }, only.id, { keepScope });
  return {
    snapshot: opened.snapshot ?? {
      ...snapshotOf(meta, 'choose-branch'),
      branches: listed.branches,
    },
    writes: opened.writes,
  };
}

export type CommandError =
  /** A definite backend answer, passed through: status, verbatim messages, `Retry-After`. */
  | {
      readonly kind: 'refused';
      readonly status: number;
      readonly messages: readonly string[];
      readonly retryAfter: string | null;
    }
  /** Dispatched without a definite answer (network loss, timeout). */
  | { readonly kind: 'unconfirmed' }
  | { readonly kind: 'credentials'; readonly failure: CredentialFailure };

export interface SessionCommandResult {
  /** The resulting authority state; null when `error` describes a refusal instead. */
  readonly snapshot: SessionSnapshot | null;
  readonly writes: readonly CookieWrite[];
  readonly error?: CommandError;
  readonly serverConfirmed?: boolean;
}

/**
 * `POST /user/login`, then the membership rule. It starts a new identity generation and ends the
 * previous one, so a late response of the old session can no longer write cookies.
 */
export async function login(
  context: SessionContext,
  cookies: SessionCookies,
  body: RequestUserLoginJson,
): Promise<SessionCommandResult> {
  const result = await context.backend.login(body);
  if (!result.ok) return { snapshot: null, writes: [], error: refusal(result.error) };
  const newId = context.newId ?? randomUUID;
  const previous = cookies.meta;
  if (previous !== null) {
    endIdentity(context.registry, previous.sid, context.owner);
    endScope(context.registry, previous.scope, context.owner);
  }
  const meta: SessionMeta = {
    v: 1,
    sid: newId(),
    scope: newId(),
    user: { userId: result.data.userId, name: result.data.name, email: result.data.email },
    branchId: null,
    branch: null,
    notice: null,
  };
  const { token, refreshToken } = result.data.responseToken;
  const writes: CookieWrite[] = [
    { name: cookieNames.identity, value: token },
    { name: cookieNames.refresh, value: refreshToken },
    { name: cookieNames.branch, value: null },
    { name: cookieNames.meta, value: encodeMeta(meta) },
  ];
  const fresh: SessionCookies = {
    ...cookies,
    identity: token,
    refresh: refreshToken,
    branch: null,
    meta,
  };
  const listed = await listBranches(context, token);
  const resolved = await chooseOrOpen(context, fresh, meta, listed, true);
  return { snapshot: resolved.snapshot, writes: [...writes, ...resolved.writes] };
}

/**
 * Opens (or switches to) `branchId`. A new branch context starts a new scope generation and ends
 * the old one: its late responses can no longer write cookies or pass the forwarder.
 */
export async function selectBranch(
  context: SessionContext,
  cookies: SessionCookies,
  branchId: string,
  options: { readonly keepScope?: boolean } = {},
): Promise<SessionCommandResult> {
  const meta = cookies.meta;
  if (meta === null) {
    return { snapshot: null, writes: [], error: { kind: 'credentials', failure: 'session-lost' } };
  }
  const identity = await ensureCredentials(context, cookies, 'identity');
  if (!identity.ok) {
    return {
      snapshot: null,
      writes: identity.writes,
      error: { kind: 'credentials', failure: identity.failure },
    };
  }
  const outcome = await openBranchSession(
    context.registry,
    context.backend,
    identity.token ?? '',
    branchId,
  );
  if (outcome.kind === 'created') {
    const scope = options.keepScope ? meta.scope : newScope(context, meta);
    const nextMeta: SessionMeta = {
      ...meta,
      scope,
      branchId: outcome.branch.id,
      branch: outcome.branch,
      notice: null,
    };
    return {
      snapshot: {
        ...snapshotOf(nextMeta, 'branch'),
        resume: resumableIntent(context, cookies, nextMeta),
      },
      writes: [
        ...identity.writes,
        { name: cookieNames.branch, value: outcome.token },
        { name: cookieNames.meta, value: encodeMeta(nextMeta) },
        { name: cookieNames.intent, value: null },
      ],
    };
  }
  if (outcome.kind === 'unavailable') {
    const writes = branchLostWrites(context, meta, outcome.messages);
    const left = metaFromWrites(writes) ?? leftBranchMeta(meta, outcome.messages);
    const listed = await listBranches(context, identity.token ?? '');
    return {
      snapshot: {
        ...snapshotOf(left, 'branch-unavailable'),
        branches: listed.ok ? listed.branches : null,
      },
      writes: [...identity.writes, ...writes],
    };
  }
  return {
    snapshot: null,
    writes: identity.writes,
    error:
      outcome.kind === 'unknown'
        ? { kind: 'credentials', failure: 'renewal-unconfirmed' }
        : outcome.status === 401
          ? { kind: 'credentials', failure: 'session-lost' }
          : {
              kind: 'refused',
              status: outcome.status,
              messages: outcome.messages,
              retryAfter: null,
            },
  };
}

/** Switch branch, first half: drop the branch token and all branch context; back to the choice. */
export async function leaveBranch(
  context: SessionContext,
  cookies: SessionCookies,
): Promise<SessionCommandResult> {
  const meta = cookies.meta;
  if (meta === null) return { snapshot: anonymousSnapshot, writes: [] };
  const nextMeta: SessionMeta = {
    ...meta,
    scope: newScope(context, meta),
    branchId: null,
    branch: null,
    notice: null,
  };
  const writes: CookieWrite[] = [
    { name: cookieNames.branch, value: null },
    { name: cookieNames.intent, value: null },
    { name: cookieNames.meta, value: encodeMeta(nextMeta) },
  ];
  const identity = await ensureCredentials(context, { ...cookies, meta: nextMeta }, 'identity');
  if (!identity.ok) {
    return { snapshot: snapshotOf(nextMeta, 'choose-branch'), writes };
  }
  const listed = await listBranches(context, identity.token ?? '');
  const empty = listed.ok && listed.branches.length === 0;
  return {
    snapshot: {
      ...snapshotOf(nextMeta, empty ? 'no-memberships' : 'choose-branch'),
      branches: listed.ok ? listed.branches : null,
      messages: listed.ok ? [] : listed.messages,
    },
    writes: [...writes, ...identity.writes],
  };
}

/**
 * Revokes the refresh session through `POST /user/logout` (renewing an expired identity first),
 * then clears every cookie whatever happened. `serverConfirmed` is true only for the 204.
 */
export async function logout(
  context: SessionContext,
  cookies: SessionCookies,
): Promise<SessionCommandResult> {
  const meta = cookies.meta;
  let serverConfirmed = false;
  if (meta !== null && cookies.refresh !== null) {
    const identity = await ensureCredentials(context, cookies, 'identity');
    if (identity.ok && identity.token !== null) {
      const refresh = identity.cookies.refresh ?? cookies.refresh;
      serverConfirmed = (await context.backend.logout(identity.token, refresh)).ok;
    }
  }
  if (meta !== null) {
    endIdentity(context.registry, meta.sid, context.owner);
    endScope(context.registry, meta.scope, context.owner);
  }
  return { snapshot: anonymousSnapshot, writes: clearAllWrites(), serverConfirmed };
}

export function snapshotOf(meta: SessionMeta, state: SessionState): SessionSnapshot {
  return {
    state,
    scope: meta.scope,
    user: meta.user,
    branch: state === 'branch' ? meta.branch : null,
    branches: null,
    messages: meta.notice ?? [],
    resume: null,
  };
}

/** What the cookies describe, without network or renewal: for server rendering and guards. */
export function snapshotFromCookies(cookies: SessionCookies): SessionSnapshot {
  const meta = cookies.meta;
  if (meta === null || (cookies.identity === null && cookies.refresh === null)) {
    return anonymousSnapshot;
  }
  if (meta.branchId !== null && meta.branch !== null && cookies.branch !== null) {
    return snapshotOf(meta, 'branch');
  }
  return snapshotOf(meta, meta.notice ? 'branch-unavailable' : 'choose-branch');
}

function resumableIntent(
  context: SessionContext,
  cookies: SessionCookies,
  meta: SessionMeta,
): string | null {
  const intent = cookies.intent;
  if (intent === null) return null;
  if (intent.email !== null && intent.email !== meta.user.email) return null;
  if (intent.branchId !== null && intent.branchId !== meta.branchId) return null;
  return validateIntentPath(intent.path, { fixtures: context.fixtures });
}

function newScope(context: SessionContext, meta: SessionMeta): string {
  endScope(context.registry, meta.scope, context.owner);
  const scope = (context.newId ?? randomUUID)();
  noteDeviceScope(context.registry, context.device, meta.scope, scope);
  return scope;
}

/** The scope `writes` leave the browser in: null when signed out, undefined when unchanged. */
export function scopeAfter(writes: readonly CookieWrite[]): string | null | undefined {
  if (writes.some((write) => write.name === cookieNames.refresh && write.value === null)) {
    return null;
  }
  return metaFromWrites(writes)?.scope;
}

function leftBranchMeta(meta: SessionMeta, notice: readonly string[]): SessionMeta {
  return { ...meta, branchId: null, branch: null, notice: [...notice] };
}

/** The branch is gone: drop its token and context under a new scope, keeping the identity. */
export function branchLostWrites(
  context: SessionContext,
  meta: SessionMeta,
  notice: readonly string[],
): CookieWrite[] {
  const left: SessionMeta = { ...leftBranchMeta(meta, notice), scope: newScope(context, meta) };
  return [
    { name: cookieNames.branch, value: null },
    { name: cookieNames.intent, value: null },
    { name: cookieNames.meta, value: encodeMeta(left) },
  ];
}

function metaFromWrites(writes: readonly CookieWrite[]): SessionMeta | null {
  const raw = writes.findLast((write) => write.name === cookieNames.meta)?.value;
  if (raw === undefined || raw === null) return null;
  return JSON.parse(Buffer.from(raw, 'base64url').toString('utf8')) as SessionMeta;
}

function refusal(error: ApiError): CommandError {
  if (error.kind !== 'http' || error.status === undefined) return { kind: 'unconfirmed' };
  return {
    kind: 'refused',
    status: error.status,
    messages: error.errorMessages,
    retryAfter: error.headers?.get('Retry-After') ?? null,
  };
}

function failure(
  kind: CredentialFailure,
  messages: readonly string[],
  writes: readonly CookieWrite[],
): Extract<Credentials, { ok: false }> {
  return { ok: false, failure: kind, messages, writes };
}

function hasTokens(cookies: SessionCookies): boolean {
  return cookies.identity !== null || cookies.refresh !== null || cookies.branch !== null;
}
