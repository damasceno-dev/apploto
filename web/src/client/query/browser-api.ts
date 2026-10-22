import { configureApiClient, createApiClient, isApiError, type ApiClient } from '@loto/api';
import { bffApiPrefix, bffErrorHeader } from '@/lib/bff-protocol';
import { bffFetch } from '../bff-fetch';

/**
 * The browser's client core configuration for one session scope: the same-origin forwarder as base
 * URL and a token provider that always returns `null`, so no `Authorization` header (and no token)
 * ever exists in the browser. Each request snapshots the client it started with, so a request begun
 * in an ended scope still carries that scope and the forwarder refuses it.
 */
export function createBrowserApiClient(scope: string | null): ApiClient {
  return createApiClient({
    baseUrl: bffApiPrefix,
    getAccessToken: () => null,
    fetch: (input, init) => bffFetch(input, init, scope),
  });
}

let installed: ApiClient | null = null;

/**
 * Makes `client` the default for generated hooks — in the browser only. On the server this is a
 * no-op: server code always passes a per-request client, and no user state may live in the
 * process-wide default there.
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
