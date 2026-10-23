import 'server-only';
import { randomUUID } from 'node:crypto';
import type {
  ApiError,
  AuthTier,
  RequestUserLoginJson,
  ResponseBranchSummaryJson,
} from '@loto/api';
import {
  anonymousSnapshot,
  branchScopeOf,
  type SessionSnapshot,
  type SessionState,
} from '@/lib/bff-protocol';
import { isBranchId } from '@/lib/branch-route';
import { routeBranch, routeTier, validateIntentPath } from '@/lib/route-policy';
import type { AuthBackend } from './backend';
import {
  clearAllWrites,
  clearSlotWrites,
  cookieNames,
  encodeMeta,
  slotDeletion,
  slotWrites,
  type BranchSlot,
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
  type RequestOwner,
  type SessionRegistry,
} from './registry';
import { openBranchSession, renewIdentity } from './renewal';

/** Everything one BFF request needs; `owner` is unique per request. */
export interface SessionContext {
  readonly registry: SessionRegistry;
  readonly backend: AuthBackend;
  readonly owner: RequestOwner;
  /** The browser's device id, when known: session commands are serialized against it. */
  readonly device?: string;
  readonly fixtures?: boolean;
  readonly newId?: () => string;
}

export type CredentialFailure =
  | 'session-lost'
  | 'branch-unavailable'
  | 'no-branch-session'
  | 'renewal-unconfirmed'
  /**
   * Credentials older than a rotation, or than a generation another request ended (identity, or the
   * branch generation the tab declared): not a loss — the tab must re-read where it stands.
   */
  | 'stale';

export type Credentials =
  | {
      readonly ok: true;
      readonly token: string | null;
      readonly writes: readonly CookieWrite[];
      /** The cookies after any renewal in this request. */
      readonly cookies: SessionCookies;
      /** The branch session used (branch tier only). */
      readonly slot: BranchSlot | null;
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
 * branch-session creation each run through their single flight. It never retries a business call.
 */
export interface CredentialOptions {
  /** A 401 started one recovery cycle: renew even if the token looks valid. */
  readonly force?: boolean;
  /**
   * The request would change server state. While a renewal's outcome is unknown, writes stay
   * paused even on a still-valid bearer (catalog: lost renewal response); reads may use it.
   */
  readonly write?: boolean;
  /**
   * Branch tier: the branch the tab is working in (its route). Navigation context only — the slot
   * used is always the signed-in identity's own, created through `POST /branch/session`, which
   * verifies the membership; the API re-verifies membership and role on every call.
   */
  readonly branchId?: string | null;
  /** The branch generation the tab declared in its scope (null: none, e.g. a page request). */
  readonly declaredGeneration?: string | null;
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
    return { ok: true, token: null, writes: [], cookies, slot: null, renewed: false };
  }
  const { registry, backend, owner } = context;
  const meta = cookies.meta;
  if (meta === null || (cookies.identity === null && cookies.refresh === null)) {
    const orphaned = meta === null && hasTokens(cookies);
    return failure('session-lost', [], orphaned ? clearAllWrites(cookies) : []);
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
      if (!usable) return failure('session-lost', [], clearAllWrites(cookies));
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
          return failure('session-lost', outcome.messages, clearAllWrites(cookies));
        case 'unknown':
          if (!usable || write) return failure('renewal-unconfirmed', [], writes);
          break;
      }
    }
  }
  const identity = current.identity;
  if (tier === 'identity' || identity === null) {
    return { ok: true, token: identity, writes, cookies: current, slot: null, renewed };
  }

  const branchId = options.branchId ?? null;
  if (branchId === null || !isBranchId(branchId)) return failure('no-branch-session', [], writes);
  // A slot whose generation another request ended (role changed, branch lost) never authorizes; its
  // replacement may be on its way. A tab that declared an ended or superseded generation is stale.
  let held = heldSlot(current, meta, branchId);
  if (held !== null && isScopeEnded(registry, held.gen, owner)) held = null;
  const declared = options.declaredGeneration ?? null;
  if (declared !== null) {
    if (isScopeEnded(registry, declared, owner)) return failure('stale', [], writes);
    if (held !== null && held.gen !== declared) return failure('stale', [], writes);
  }
  let slot = held;
  if (force || slot === null || needsRenewal(slot.token, now)) {
    const usable = !force && slot !== null && !isExpired(slot.token, now);
    const outcome = await openBranchSession(registry, backend, identity, branchId);
    switch (outcome.kind) {
      case 'created': {
        // A slot evicted while a tab still works in the branch is reopened under the generation
        // that tab declared, so the tab is not reset by a cookie-size bound.
        const generation = generationFor(context, held, outcome.branch, declared);
        slot = {
          v: 1,
          sid: meta.sid,
          gen: generation,
          token: outcome.token,
          branch: outcome.branch,
          at: now,
        };
        writes.push(...slotWrites(current, slot));
        current = { ...current, slots: new Map(current.slots).set(branchId, slot) };
        renewed = true;
        break;
      }
      case 'unavailable':
        return failure('branch-unavailable', outcome.messages, [
          ...writes,
          ...branchLostWrites(context, branchId, held),
        ]);
      case 'rejected':
        if (outcome.status === 401) {
          return failure('session-lost', outcome.messages, clearAllWrites(cookies));
        }
        return failure('branch-unavailable', outcome.messages, [
          ...writes,
          ...branchLostWrites(context, branchId, held),
        ]);
      case 'unknown':
        if (!usable || write) return failure('renewal-unconfirmed', [], writes);
        break;
    }
  }
  if (slot === null) return failure('renewal-unconfirmed', [], writes);
  return { ok: true, token: slot.token, writes, cookies: current, slot, renewed };
}

/** The slot this identity generation holds for `branchId` (a slot of another login never counts). */
function heldSlot(cookies: SessionCookies, meta: SessionMeta, branchId: string): BranchSlot | null {
  const slot = cookies.slots.get(branchId);
  return slot !== undefined && slot.sid === meta.sid ? slot : null;
}

/**
 * The branch generation of a (re)opened branch session: kept while the same role holds, so a renewal
 * or another tab opening the same branch never resets a tab; new when the role changed (permission-
 * dependent state must reset everywhere) or when nothing is known.
 */
function generationFor(
  context: SessionContext,
  held: BranchSlot | null,
  branch: ResponseBranchSummaryJson,
  declared: string | null,
): string {
  if (held !== null) {
    if (held.branch.role === branch.role) return held.gen;
    endScope(context.registry, held.gen, context.owner);
    return newGeneration(context);
  }
  return declared ?? newGeneration(context);
}

/**
 * The fence. `base` is the identity generation the request arrived with. If **another** request
 * ended it (logout, another login), nothing is written; a branch-slot write is also dropped when
 * another request ended its branch generation. A late renewal can therefore never write old
 * credentials back after logout, login or a branch loss. Clearing a lost session ends it.
 */
export function fenceWrites(
  context: Pick<SessionContext, 'registry' | 'owner'>,
  base: Pick<SessionMeta, 'sid' | 'scope'> | null,
  writes: readonly CookieWrite[],
): CookieWrite[] {
  const { registry, owner } = context;
  if (base === null) return [...writes];
  if (isIdentityEnded(registry, base.sid, owner) || isScopeEnded(registry, base.scope, owner)) {
    return [];
  }
  const allowed = writes.filter(
    (write) => write.generation === undefined || !isScopeEnded(registry, write.generation, owner),
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

export interface ResolveOptions {
  /** Resolve this branch's session (a branch route or a branch tab's re-read). */
  readonly branchId?: string | null;
  /**
   * Identity level only: list the branches and apply the membership rule (zero → empty state,
   * one → opened automatically, several → choice). Without it, the identity state alone.
   */
  readonly needBranches?: boolean;
}

/**
 * Resolves the authority state for `GET /bff/session` and the page gate: renews when needed and,
 * for a branch, opens or reuses that branch's session for the signed-in identity.
 */
export async function resolveSession(
  context: SessionContext,
  cookies: SessionCookies,
  options: ResolveOptions = {},
): Promise<SessionResolution> {
  const meta = cookies.meta;
  if (meta === null || (cookies.identity === null && cookies.refresh === null)) {
    return {
      snapshot: anonymousSnapshot,
      writes: meta === null && hasTokens(cookies) ? clearAllWrites(cookies) : [],
    };
  }
  const branchId = options.branchId ?? null;
  if (branchId !== null) {
    const credentials = await ensureCredentials(context, cookies, 'branch', { branchId });
    if (credentials.ok && credentials.slot !== null) {
      return { snapshot: snapshotOf(meta, 'branch', credentials.slot), writes: credentials.writes };
    }
    return resolveFailure(context, meta, credentials as Extract<Credentials, { ok: false }>);
  }

  const identity = await ensureCredentials(context, cookies, 'identity');
  if (!identity.ok) return resolveFailure(context, meta, identity);
  if (options.needBranches !== true) {
    return { snapshot: snapshotOf(meta, 'choose-branch'), writes: identity.writes };
  }
  const listed = await listBranches(context, identity.token ?? '');
  const resolved = await chooseOrOpen(context, identity.cookies, meta, listed);
  return { snapshot: resolved.snapshot, writes: [...identity.writes, ...resolved.writes] };
}

function resolveFailure(
  context: SessionContext,
  meta: SessionMeta,
  credentials: Extract<Credentials, { ok: false }>,
): SessionResolution {
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
      // ended identity reads as signed out; a superseded rotation as an unconfirmed renewal.
      const { registry, owner } = context;
      const ended =
        isIdentityEnded(registry, meta.sid, owner) || isScopeEnded(registry, meta.scope, owner);
      return {
        snapshot: ended
          ? anonymousSnapshot
          : { ...snapshotOf(meta, 'renewal-unconfirmed'), scope: null },
        writes: [],
      };
    }
    case 'no-branch-session':
    case 'branch-unavailable':
      return {
        snapshot: { ...snapshotOf(meta, 'branch-unavailable'), messages: credentials.messages },
        writes: credentials.writes,
      };
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
    // An intent that names its branch in the address resumes directly: the route selects the
    // branch and the gate verifies it there. An unscoped one waits for the choice.
    const resume = resumableIntent(context, cookies, meta, null);
    return {
      snapshot: { ...snapshotOf(meta, 'choose-branch'), branches: listed.branches, resume },
      writes: resume === null ? [] : [{ name: cookieNames.intent, value: null }],
    };
  }
  // Exactly one branch: open it without the picker (catalog, Branch picker / session).
  const opened = await selectBranch(context, { ...cookies, meta }, only.id);
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
 * previous one — with every branch session the previous identity held — so a late response of the
 * old session can no longer write cookies.
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
  for (const slot of cookies.slots.values()) endScope(context.registry, slot.gen, context.owner);
  const meta: SessionMeta = {
    v: 2,
    sid: newId(),
    scope: newId(),
    user: { userId: result.data.userId, name: result.data.name, email: result.data.email },
  };
  const { token, refreshToken } = result.data.responseToken;
  const writes: CookieWrite[] = [
    ...clearSlotWrites(cookies),
    { name: cookieNames.identity, value: token },
    { name: cookieNames.refresh, value: refreshToken },
    { name: cookieNames.meta, value: encodeMeta(meta) },
  ];
  const fresh: SessionCookies = {
    ...cookies,
    identity: token,
    refresh: refreshToken,
    meta,
    slots: new Map(),
    branchCookieNames: [],
  };
  const listed = await listBranches(context, token);
  const resolved = await chooseOrOpen(context, fresh, meta, listed);
  return { snapshot: resolved.snapshot, writes: [...writes, ...resolved.writes] };
}

/**
 * Opens a branch for this identity (Branch picker, or a role change seen live): always a fresh
 * `POST /branch/session`, which re-verifies membership and role. It changes only that branch's
 * slot — other tabs keep their branches. The branch generation survives while the role is the same.
 */
export async function selectBranch(
  context: SessionContext,
  cookies: SessionCookies,
  branchId: string,
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
  const held = isBranchId(branchId) ? heldSlot(identity.cookies, meta, branchId) : null;
  if (outcome.kind === 'created' && isBranchId(outcome.branch.id)) {
    const slot: BranchSlot = {
      v: 1,
      sid: meta.sid,
      gen: generationFor(context, held, outcome.branch, null),
      token: outcome.token,
      branch: outcome.branch,
      at: context.registry.now(),
    };
    const resume = resumableIntent(context, cookies, meta, slot.branch.id);
    return {
      snapshot: { ...snapshotOf(meta, 'branch', slot), resume },
      writes: [
        ...identity.writes,
        ...slotWrites(identity.cookies, slot),
        ...(resume === null && cookies.intent === null
          ? []
          : [{ name: cookieNames.intent, value: null }]),
      ],
    };
  }
  if (outcome.kind === 'unavailable') {
    const listed = await listBranches(context, identity.token ?? '');
    return {
      snapshot: {
        ...snapshotOf(meta, 'branch-unavailable'),
        branches: listed.ok ? listed.branches : null,
        messages: outcome.messages,
      },
      writes: [
        ...identity.writes,
        ...(isBranchId(branchId) ? branchLostWrites(context, branchId, held) : []),
      ],
    };
  }
  return {
    snapshot: null,
    writes: identity.writes,
    error:
      outcome.kind === 'unknown'
        ? { kind: 'credentials', failure: 'renewal-unconfirmed' }
        : outcome.kind === 'created' || outcome.status === 401
          ? { kind: 'credentials', failure: 'session-lost' }
          : {
              kind: 'refused',
              status: outcome.status,
              messages: outcome.messages,
              retryAfter: null,
            },
  };
}

/**
 * Revokes the refresh session through `POST /user/logout` (renewing an expired identity first),
 * then clears every cookie — every branch slot included — whatever happened. `serverConfirmed` is
 * true only for the 204.
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
  for (const slot of cookies.slots.values()) endScope(context.registry, slot.gen, context.owner);
  return { snapshot: anonymousSnapshot, writes: clearAllWrites(cookies), serverConfirmed };
}

/** A tab's view: the identity, plus one branch when `slot` is given (state `branch`). */
export function snapshotOf(
  meta: SessionMeta,
  state: SessionState,
  slot: BranchSlot | null = null,
): SessionSnapshot {
  return {
    state,
    scope: slot === null ? meta.scope : branchScopeOf(meta.scope, slot.gen),
    user: meta.user,
    branch: state === 'branch' && slot !== null ? slot.branch : null,
    branches: null,
    messages: [],
    resume: null,
  };
}

/**
 * What the cookies describe, without network or renewal: for server rendering and guards. With a
 * branch id, that branch's view when this identity holds a slot for it.
 */
export function snapshotFromCookies(
  cookies: SessionCookies,
  branchId: string | null = null,
): SessionSnapshot {
  const meta = cookies.meta;
  if (meta === null || (cookies.identity === null && cookies.refresh === null)) {
    return anonymousSnapshot;
  }
  if (branchId === null) return snapshotOf(meta, 'choose-branch');
  const slot = isBranchId(branchId) ? heldSlot(cookies, meta, branchId) : null;
  return slot === null ? snapshotOf(meta, 'choose-branch') : snapshotOf(meta, 'branch', slot);
}

/** The branch token this identity holds for `branchId` (server reads of a branch route). */
export function branchTokenFromCookies(cookies: SessionCookies, branchId: string): string | null {
  const meta = cookies.meta;
  if (meta === null || !isBranchId(branchId)) return null;
  return heldSlot(cookies, meta, branchId)?.token ?? null;
}

/**
 * The held deep link to offer once authority allows it — only for the e-mail it was captured
 * under. One that names its branch in the address resumes as is (the gate re-verifies that branch
 * there); an unscoped branch link waits for a chosen branch and is bound to the branch known at
 * capture, if any.
 */
function resumableIntent(
  context: SessionContext,
  cookies: SessionCookies,
  meta: SessionMeta,
  openedBranch: string | null,
): string | null {
  const intent = cookies.intent;
  if (intent === null) return null;
  if (intent.email !== null && intent.email !== meta.user.email) return null;
  const path = validateIntentPath(intent.path, { fixtures: context.fixtures });
  if (path === null) return null;
  const pathname = path.split('?', 1)[0] ?? '';
  const named = routeBranch(pathname);
  if (named !== null) return openedBranch === null || openedBranch === named ? path : null;
  if (routeTier(pathname, { fixtures: context.fixtures }) === 'identity') return path;
  if (openedBranch === null) return null;
  return intent.branchId === null || intent.branchId === openedBranch ? path : null;
}

function newGeneration(context: SessionContext): string {
  return (context.newId ?? randomUUID)();
}

/** The scope `writes` leave the browser in: null when signed out, undefined when unchanged. */
export function scopeAfter(writes: readonly CookieWrite[]): string | null | undefined {
  if (writes.some((write) => write.name === cookieNames.refresh && write.value === null)) {
    return null;
  }
  return metaFromWrites(writes)?.scope;
}

/** A branch was refused for this identity: drop its slot and end its generation. */
export function branchLostWrites(
  context: SessionContext,
  branchId: string,
  held: BranchSlot | null,
): CookieWrite[] {
  if (held !== null) endScope(context.registry, held.gen, context.owner);
  return [slotDeletion(branchId)];
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
  return (
    cookies.identity !== null || cookies.refresh !== null || cookies.branchCookieNames.length > 0
  );
}
