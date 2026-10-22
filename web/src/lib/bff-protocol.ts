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
 * - `branch-unavailable`: the selected branch could not be (re)opened → Branch picker, verbatim messages.
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
  /** Opaque id of the current user+branch context; changes on login, logout and branch change. */
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

export const anonymousSnapshot: SessionSnapshot = Object.freeze({
  state: 'anonymous',
  scope: null,
  user: null,
  branch: null,
  branches: null,
  messages: Object.freeze([]) as readonly string[],
  resume: null,
});
