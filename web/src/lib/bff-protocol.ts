// The wire protocol between the browser and the route-handler BFF (Web M2). Isomorphic: names and
// types only, no secrets. Tokens never cross this boundary.
import type { ResponseBranchSummaryJson } from '@loto/api';

/** Same-origin prefix the browser client core uses as its base URL. */
export const bffApiPrefix = '/bff/api';
export const bffSessionPath = '/bff/session';

/** Required on every BFF request (`1`): a cross-site page cannot send it without a CORS preflight. */
export const bffRequestHeader = 'x-loto-bff';
/** The session scope the browser believes it is in; the forwarder refuses any other. */
export const scopeHeader = 'x-loto-scope';
/**
 * The branch a tab is working in (its `/f/{branchId}` route), on BFF calls. Untrusted navigation
 * context: it only selects which of the signed-in user's own branch credentials is used.
 */
export const branchHeader = 'x-loto-branch';
/** Set only on a response the BFF produced **without forwarding** the request: nothing executed. */
export const bffErrorHeader = 'x-loto-bff-error';
/** Set on a forwarded response when its 401 started a recovery cycle. */
export const sessionOutcomeHeader = 'x-loto-session';

export type BffErrorCode =
  | 'missing-bff-header'
  | 'forbidden-origin'
  | 'unsupported-media-type'
  | 'unknown-operation'
  | 'invalid-path'
  | 'authorization-not-allowed'
  | 'body-too-large'
  | 'invalid-branch'
  | 'session-scope-changed'
  | 'session-lost'
  | 'branch-unavailable'
  | 'no-branch-session'
  | 'renewal-unconfirmed';

/** What a forwarded 401's recovery cycle ended in. */
export type SessionOutcome = 'renewed' | 'lost' | 'branch-unavailable' | 'unconfirmed' | 'stale';

/**
 * Authority states the session gate resolves before any branch-scoped fetch:
 * - `anonymous`: no usable identity → Login.
 * - `no-memberships`: identity with zero branches → Branch picker's empty state.
 * - `choose-branch`: identity awaiting a branch choice (several branches, or after leaving one).
 * - `branch-unavailable`: the route's branch could not be opened for this identity (no membership,
 *   removed, inactive) → 403 with the verbatim messages and the way back to Branch picker.
 * - `renewal-unconfirmed`: renewal outcome unknown (lost response/5xx); writes stay paused.
 * - `branch`: an authorized branch session.
 */
export type SessionState =
  | 'anonymous'
  | 'no-memberships'
  | 'choose-branch'
  | 'branch-unavailable'
  | 'renewal-unconfirmed'
  | 'branch';

export interface SessionUser {
  readonly userId: string;
  readonly name: string;
  /** Exactly as returned by the successful login: the device-input partition key (catalog). */
  readonly email: string;
}

export type BranchSummary = ResponseBranchSummaryJson;

export interface SessionSnapshot {
  readonly state: SessionState;
  /**
   * Opaque id of the context a tab acts in: the identity generation (changes on login and logout)
   * and, inside a branch route, that branch's generation (changes when the branch authority is
   * re-established: lost and regained, or a changed role). See `scopeOf`.
   */
  readonly scope: string | null;
  readonly user: SessionUser | null;
  /** The branch session's summary; its role is the session-creation value, not the live one. */
  readonly branch: BranchSummary | null;
  /** The identity's branches, when the state needs a choice (null when unknown). */
  readonly branches: readonly BranchSummary[] | null;
  /** Backend `ErrorMessages`, verbatim, explaining the state (for example `BRANCH_NOT_FOUND`). */
  readonly messages: readonly string[];
  /** An authorized navigation intent to resume (a path), never an API request. */
  readonly resume: string | null;
}

/** A branch tab's scope: the identity generation, then the branch generation. */
export function branchScopeOf(identityScope: string, branchGeneration: string): string {
  return `${identityScope}.${branchGeneration}`;
}

/** Splits a declared scope; `branch` is null for an identity-level scope. */
export function parseScope(
  scope: string | null,
): { readonly identity: string; readonly branch: string | null } | null {
  if (scope === null || scope.length === 0 || scope.length > 200) return null;
  const dot = scope.indexOf('.');
  if (dot === -1) return { identity: scope, branch: null };
  const identity = scope.slice(0, dot);
  const branch = scope.slice(dot + 1);
  if (identity.length === 0 || branch.length === 0 || branch.includes('.')) return null;
  return { identity, branch };
}

export const anonymousSnapshot: SessionSnapshot = Object.freeze({
  state: 'anonymous',
  scope: null,
  user: null,
  branch: null,
  branches: null,
  messages: Object.freeze([]) as readonly string[],
  resume: null,
});
