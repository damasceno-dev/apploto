import { configureApiClient, createApiClient, isApiError, type ApiClient } from '@loto/api';
import { bffApiPrefix, bffErrorHeader } from '@/lib/bff-protocol';
import { bffFetch } from '../bff-fetch';

/**
 * The browser's client core configuration for one session scope: the same-origin forwarder as base
 * URL and a token provider that always returns `null`, so no `Authorization` header (and no token)
 * ever exists in the browser. A branch tab's client also names its route branch, fixed for the
 * client's life. Each request snapshots the client it started with, so a request begun in an ended
 * scope still carries that scope (and branch) and the forwarder refuses it — it is never re-sent
 * under another branch.
 */
export function createBrowserApiClient(
  scope: string | null,
  branchId: string | null = null,
): ApiClient {
  return createApiClient({
    baseUrl: bffApiPrefix,
    getAccessToken: () => null,
    fetch: (input, init) => bffFetch(input, init, scope, branchId),
  });
}

let installed: ApiClient | null = null;

/** The client generated hooks currently use by default (browser only; null before any scope). */
export function installedBrowserApiClient(): ApiClient | null {
  return installed;
}

interface MountedScope {
  readonly client: ApiClient;
  readonly depth: number;
  readonly order: number;
}
const mountedScopes: MountedScope[] = [];
let mountOrder = 0;

/**
 * Registers a mounted scope (identity scope at depth 0, a branch tab's scope nested at depth 1);
 * the deepest one — the newest among equals — is the tab's default for generated hooks. Returns the
 * unregistration. Called from layout effects, so the default is settled before any passive effect
 * (where queries subscribe and fetch) runs, whatever order React mounts and unmounts scopes in.
 */
export function mountBrowserApiClient(client: ApiClient, depth: number): () => void {
  const entry: MountedScope = { client, depth, order: (mountOrder += 1) };
  mountedScopes.push(entry);
  installDeepest();
  return () => {
    const index = mountedScopes.indexOf(entry);
    if (index !== -1) mountedScopes.splice(index, 1);
    installDeepest();
  };
}

function installDeepest(): void {
  let deepest: MountedScope | null = null;
  for (const entry of mountedScopes) {
    if (
      deepest === null ||
      entry.depth > deepest.depth ||
      (entry.depth === deepest.depth && entry.order > deepest.order)
    ) {
      deepest = entry;
    }
  }
  if (deepest !== null) installBrowserApiClient(deepest.client);
}

/**
 * Makes `client` the default for generated hooks — in the browser only. On the server this is a
 * no-op: server code always passes a per-request client, and no user state may live in the
 * process-wide default there. Each tab is its own JavaScript realm, so this default is per tab.
 */
export function installBrowserApiClient(client: ApiClient): void {
  if (typeof window === 'undefined' || installed === client) return;
  configureApiClient({
    baseUrl: client.baseUrl,
    getAccessToken: client.getAccessToken,
    fetch: client.fetch,
  });
  installed = client;
}

/** Reads retry once for a transient failure; never for a 4xx, a BFF refusal or a write. */
export function shouldRetryRead(failureCount: number, error: unknown): boolean {
  if (failureCount >= 1 || !isApiError(error) || error.method !== 'GET') return false;
  if (error.headers?.has(bffErrorHeader)) return false;
  if (error.kind === 'network' || error.kind === 'timeout') return true;
  return error.kind === 'http' && error.status !== undefined && error.status >= 500;
}
