import 'server-only';
import { cookies } from 'next/headers';
import { createApiClient, getBranchCurrent, isApiError, type ApiClient } from '@loto/api';
import { serverEnv } from '@/config/server-env';
import { toBranchClock, type BranchClock } from '@/lib/branch-clock';
import { gateDestinations } from '@/lib/route-policy';
import { gateRedirect } from './gate/guards';
import { readSessionCookies } from './session/cookies';
import { branchTokenFromCookies } from './session/service';

/**
 * A client for this request only, built from this request's cookies: Server Components call raw
 * `@loto/api` functions with `{ client }`. Branch work uses the signed-in identity's own session for
 * `branchId` — the route's branch, never a process-wide or "current" branch — so two tabs rendering
 * two branches never share a credential. Nothing user-specific enters the process-wide default.
 * Server rendering cannot set cookies, so it never renews: `proxy.ts` renewed before this render.
 */
export async function requestApiClient(branchId: string | null = null): Promise<ApiClient> {
  const session = readSessionCookies(await cookies());
  const branchToken = branchId === null ? null : branchTokenFromCookies(session, branchId);
  return createApiClient({
    baseUrl: serverEnv.apiBaseUrl,
    getAccessToken: ({ authTier }) =>
      authTier === 'branch' ? branchToken : authTier === 'identity' ? session.identity : null,
    fetch: (input, init) => fetch(input, { ...init, cache: 'no-store', redirect: 'manual' }),
  });
}

/**
 * Runs a server read. A 401 here means the credentials the proxy just checked were refused (for
 * example a removed membership): the branch gate takes over for branch work, Login otherwise.
 */
export async function serverRead<T>(
  scope: { readonly tier: 'identity' } | { readonly tier: 'branch'; readonly branchId: string },
  read: (client: ApiClient) => Promise<T>,
): Promise<T> {
  const client = await requestApiClient(scope.tier === 'branch' ? scope.branchId : null);
  try {
    return await read(client);
  } catch (error) {
    if (isApiError(error) && error.status === 401) {
      gateRedirect(scope.tier === 'branch' ? gateDestinations.branchPicker : gateDestinations.login);
    }
    throw error;
  }
}

/** The branch clock for server rendering; null when the response breaks the calendar contract. */
export async function serverBranchClock(branchId: string): Promise<BranchClock | null> {
  const result = await serverRead({ tier: 'branch', branchId }, (client) =>
    getBranchCurrent({ client }),
  );
  return toBranchClock(result.data);
}
