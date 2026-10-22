import { bffSessionPath, type SessionSnapshot } from '@/lib/bff-protocol';
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

export async function readSession(signal?: AbortSignal): Promise<SessionCommandResult> {
  return settle(await bffFetch(bffSessionPath, { method: 'GET', signal }));
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

export async function leaveBranch(declared: string | null): Promise<SessionCommandResult> {
  return settle(await bffFetch(`${bffSessionPath}/branch`, { method: 'DELETE' }, declared));
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
  return { snapshot, serverConfirmed };
}

async function settle(response: Response): Promise<SessionCommandResult> {
  if (!response.ok) return failure(response);
  return { ok: true, snapshot: (await response.json()) as SessionSnapshot };
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
