import 'server-only';
import { cookies } from 'next/headers';
import { createApiClient, getBranchCurrent, isApiError, type ApiClient } from '@loto/api';
import { serverEnv } from '@/config/server-env';
import { toBranchClock, type BranchClock } from '@/lib/branch-clock';
import { gateDestinations } from '@/lib/route-policy';
import { gateRedirect } from './gate/guards';
import { readSessionCookies } from './session/cookies';

/**
 * A client for this request only, built from this request's cookies: Server Components call raw
 * `@loto/api` functions with `{ client }`. Nothing user-specific enters the process-wide default.
 * Server rendering cannot set cookies, so it never renews: `proxy.ts` renewed before this render.
 */
export async function requestApiClient(): Promise<ApiClient> {
  const session = readSessionCookies(await cookies());
  return createApiClient({
    baseUrl: serverEnv.apiBaseUrl,
    getAccessToken: ({ authTier }) =>
      authTier === 'branch' ? session.branch : authTier === 'identity' ? session.identity : null,
    fetch: (input, init) => fetch(input, { ...init, cache: 'no-store', redirect: 'manual' }),
  });
}

/**
 * Runs a server read. A 401 here means the credentials the proxy just checked were refused (for
 * example a removed membership): the branch gate takes over for branch work, Login otherwise.
 */
export async function serverRead<T>(
  tier: 'identity' | 'branch',
  read: (client: ApiClient) => Promise<T>,
): Promise<T> {
  const client = await requestApiClient();
  try {
    return await read(client);
  } catch (error) {
    if (isApiError(error) && error.status === 401) {
      gateRedirect(tier === 'branch' ? gateDestinations.branchPicker : gateDestinations.login);
    }
    throw error;
  }
}

/** The branch clock for server rendering; null when the response breaks the calendar contract. */
export async function serverBranchClock(): Promise<BranchClock | null> {
  const result = await serverRead('branch', (client) => getBranchCurrent({ client }));
  return toBranchClock(result.data);
}
