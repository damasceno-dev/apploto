import { validateIntentPath } from '@/lib/route-policy';
import { bffSessionPath, parseScope, type SessionSnapshot } from '@/lib/bff-protocol';
import { bffFetch } from '../bff-fetch';

/** A refused session command: the backend's status, verbatim messages and advisory `Retry-After`. */
export interface SessionCommandFailure {
  readonly ok: false;
  readonly status: number;
  readonly errorMessages: readonly string[];
  readonly retryAfterSeconds: number | null;
  /** The BFF refused it before forwarding (`x-loto-bff-error`), or null. */
  readonly bffError: string | null;
}

export type SessionCommandResult =
  { readonly ok: true; readonly snapshot: SessionSnapshot } | SessionCommandFailure;

export interface LogoutResult {
  readonly snapshot: SessionSnapshot;
  /** False when the server's revocation was not confirmed; local credentials are cleared anyway. */
  readonly serverConfirmed: boolean;
}

const jsonHeaders = { 'content-type': 'application/json' };

/**
 * The session as the server resolves it: identity level, or — with `branchId` — the tab's branch.
 * `branches` (Branch picker) also lists the identity's branches and applies the membership rule.
 */
export async function readSession(
  options: {
    readonly signal?: AbortSignal;
    readonly branchId?: string | null;
    readonly branches?: boolean;
  } = {},
): Promise<SessionCommandResult> {
  const path = options.branches === true ? `${bffSessionPath}?branches=1` : bffSessionPath;
  return settle(
    await bffFetch(path, { method: 'GET', signal: options.signal }, null, options.branchId ?? null),
  );
}

// Every command declares the scope it was issued against (`declared`, null when signed out). The
// BFF applies it only if that is still this browser's current scope; otherwise it is superseded.

/** The password is sent once in this request body and is not retained by this module. */
export async function loginWithPassword(
  email: string,
  password: string,
  declared: string | null,
): Promise<SessionCommandResult> {
  return settle(
    await bffFetch(
      `${bffSessionPath}/login`,
      { method: 'POST', headers: jsonHeaders, body: JSON.stringify({ email, password }) },
      declared,
    ),
  );
}

export async function selectBranch(
  branchId: string,
  declared: string | null,
): Promise<SessionCommandResult> {
  return settle(
    await bffFetch(
      `${bffSessionPath}/branch`,
      { method: 'POST', headers: jsonHeaders, body: JSON.stringify({ branchId }) },
      declared,
    ),
  );
}

export async function logout(
  declared: string | null,
): Promise<LogoutResult | SessionCommandFailure> {
  const response = await bffFetch(
    `${bffSessionPath}/logout`,
    { method: 'POST', headers: jsonHeaders, body: '{}' },
    declared,
  );
  if (!response.ok) return failure(response);
  const body = (await response.json()) as SessionSnapshot & { serverConfirmed: boolean };
  const { serverConfirmed, ...snapshot } = body;
  if (typeof serverConfirmed !== 'boolean' || !isSessionSnapshot(snapshot))
    throw new Error('Invalid session response.');
  return { snapshot, serverConfirmed };
}

async function settle(response: Response): Promise<SessionCommandResult> {
  if (!response.ok) return failure(response);
  const snapshot: unknown = await response.json();
  if (!isSessionSnapshot(snapshot)) throw new Error('Invalid session response.');
  return { ok: true, snapshot };
}

async function failure(response: Response): Promise<SessionCommandFailure> {
  let errorMessages: readonly string[] = [];
  const text = await response.text();
  if (text.length > 0) {
    try {
      const parsed = JSON.parse(text) as { errorMessages?: unknown };
      if (Array.isArray(parsed.errorMessages)) {
        errorMessages = parsed.errorMessages.filter(
          (message): message is string => typeof message === 'string',
        );
      }
    } catch {
      errorMessages = [];
    }
  }
  const retryAfter = Number(response.headers.get('retry-after'));
  return {
    ok: false,
    status: response.status,
    errorMessages,
    retryAfterSeconds: Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : null,
    bffError: response.headers.get('x-loto-bff-error'),
  };
}

/** Session responses are a credential-free wire boundary; a malformed 2xx never confirms a command. */
export function isSessionSnapshot(value: unknown): value is SessionSnapshot {
  if (!value || typeof value !== 'object') return false;
  if (
    Object.keys(value).some(
      (key) =>
        !['state', 'scope', 'user', 'branch', 'branches', 'messages', 'resume'].includes(key),
    )
  )
    return false;
  const snapshot = value as SessionSnapshot;
  if (
    ![
      'anonymous',
      'no-memberships',
      'choose-branch',
      'branch-unavailable',
      'renewal-unconfirmed',
      'branch',
    ].includes(snapshot.state)
  )
    return false;
  if (
    snapshot.scope !== null &&
    (typeof snapshot.scope !== 'string' || parseScope(snapshot.scope) === null)
  )
    return false;
  if (
    snapshot.resume !== null &&
    validateIntentPath(snapshot.resume, { fixtures: true }) !== snapshot.resume
  )
    return false;
  if (
    !Array.isArray(snapshot.messages) ||
    !snapshot.messages.every((message) => typeof message === 'string')
  )
    return false;
  if (
    snapshot.user !== null &&
    (!snapshot.user ||
      typeof snapshot.user.userId !== 'string' ||
      typeof snapshot.user.name !== 'string' ||
      typeof snapshot.user.email !== 'string')
  )
    return false;
  const branchValid = (branch: SessionSnapshot['branch']) =>
    branch !== null &&
    typeof branch === 'object' &&
    typeof branch.id === 'string' &&
    branch.id.length > 0 &&
    typeof branch.name === 'string' &&
    ['Member', 'Manager', 'Admin'].includes(branch.role);
  if (snapshot.branch !== null && !branchValid(snapshot.branch)) return false;
  if (
    snapshot.branches !== null &&
    (!Array.isArray(snapshot.branches) || !snapshot.branches.every(branchValid))
  )
    return false;
  if (
    snapshot.state === 'branch' &&
    (snapshot.branch === null || snapshot.scope === null || snapshot.user === null)
  )
    return false;
  return true;
}
