import {
  bffErrorHeader,
  bffRequestHeader,
  branchHeader,
  scopeHeader,
  sessionOutcomeHeader,
  type BffErrorCode,
  type SessionOutcome,
} from '@/lib/bff-protocol';

/** BFF answers that mean the session must be re-read before anything else is sent. */
const sessionCodes: ReadonlySet<BffErrorCode> = new Set<BffErrorCode>([
  'session-lost',
  'branch-unavailable',
  'no-branch-session',
  'renewal-unconfirmed',
  'session-scope-changed',
]);
const sessionOutcomes: ReadonlySet<SessionOutcome> = new Set<SessionOutcome>([
  'lost',
  'branch-unavailable',
  'unconfirmed',
  'stale',
]);

export const sessionSignalEvent = 'loto:session-signal';
/** A scoped write refusal may reflect a changed grant/link; never classify localized messages. */
export const branchAccessSignalEvent = 'loto:branch-access-signal';

export interface SessionSignal {
  readonly code: BffErrorCode | null;
  readonly outcome: SessionOutcome | null;
}

let latestServerTime: number | null = null;

/**
 * The newest server time seen in a BFF response `Date` header (milliseconds), or null. It bounds
 * idempotency replay windows; it is never a business clock (that is `GET /branch/current`).
 */
export function latestServerTimeMs(): number | null {
  return latestServerTime;
}

/** For tests: forget the observed server time. */
export function resetServerTime(): void {
  latestServerTime = null;
}

export function observeResponse(response: Response): void {
  const date = response.headers.get('date');
  const parsed = date === null ? Number.NaN : Date.parse(date);
  if (Number.isFinite(parsed) && (latestServerTime === null || parsed > latestServerTime)) {
    latestServerTime = parsed;
  }
  const code = response.headers.get(bffErrorHeader) as BffErrorCode | null;
  const outcome = response.headers.get(sessionOutcomeHeader) as SessionOutcome | null;
  if (
    (code !== null && sessionCodes.has(code)) ||
    (outcome !== null && sessionOutcomes.has(outcome))
  ) {
    if (typeof window !== 'undefined') {
      window.dispatchEvent(
        new CustomEvent<SessionSignal>(sessionSignalEvent, { detail: { code, outcome } }),
      );
    }
  }
}

/**
 * Same-origin fetch to the BFF: adds `x-loto-bff`, `x-loto-scope` when a scope is known (so a
 * request started in an ended scope is refused before it reaches the API) and, from a branch tab,
 * `x-loto-branch` with the tab's route branch. It never sends credentials other than the browser's
 * own cookies, and observes session signals and server time.
 */
export async function bffFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
  scope: string | null = null,
  branchId: string | null = null,
): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set(bffRequestHeader, '1');
  if (scope !== null) headers.set(scopeHeader, scope);
  if (branchId !== null) headers.set(branchHeader, branchId);
  const response = await fetch(input, {
    ...init,
    headers,
    credentials: 'same-origin',
    cache: 'no-store',
  });
  observeResponse(response);
  if (
    branchId !== null &&
    (init.method ?? 'GET').toUpperCase() !== 'GET' &&
    (response.status === 400 || response.status === 403) &&
    typeof window !== 'undefined'
  ) {
    window.dispatchEvent(new CustomEvent(branchAccessSignalEvent, { detail: { branchId } }));
  }
  return response;
}
