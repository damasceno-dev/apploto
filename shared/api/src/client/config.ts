import { ApiConfigurationError } from './errors';
import type { AuthTier } from './operations';

/** What the token provider learns about each request before it is sent. */
export interface AccessTokenRequest {
  /** Upper-case HTTP method. */
  readonly method: string;
  /** Request path without the base URL or query string, for example `/transaction/7c…`. */
  readonly path: string;
  /** The token the operation requires, from its published `security` (see `auth-tier.ts`). */
  readonly authTier: AuthTier;
}

/**
 * Returns the bearer token for one request, or `null` to send no `Authorization` header (for
 * example when a web browser runtime calls a cookie-to-bearer BFF route). It is called on every
 * request and never cached: apps own token storage, renewal and branch sessions.
 */
export type AccessTokenProvider = (
  request: AccessTokenRequest,
) => string | null | Promise<string | null>;

export interface ApiClientConfig {
  /** Absolute API origin (`https://api.example`) or a same-origin prefix (`/api`). */
  readonly baseUrl: string;
  readonly getAccessToken: AccessTokenProvider;
  /** Defaults to the runtime's global `fetch`, looked up per request (never at import time). */
  readonly fetch?: typeof globalThis.fetch;
}

/** An immutable, validated configuration. Pass one per call for request-scoped credentials. */
export interface ApiClient {
  readonly baseUrl: string;
  readonly getAccessToken: AccessTokenProvider;
  readonly fetch: typeof globalThis.fetch | undefined;
}

const clientBrand = new WeakSet<ApiClient>();

/**
 * Validates `config` and returns a frozen client. Nothing global changes, so concurrent server
 * requests can each pass their own client (`{ client }` in a generated function's options) and
 * never see another request's token provider.
 */
export function createApiClient(config: ApiClientConfig): ApiClient {
  if (typeof config !== 'object' || config === null) {
    throw new ApiConfigurationError('API client configuration must be an object.');
  }
  if (typeof config.baseUrl !== 'string' || config.baseUrl.trim().length === 0) {
    throw new ApiConfigurationError('API client configuration needs a non-empty baseUrl.');
  }
  if (typeof config.getAccessToken !== 'function') {
    throw new ApiConfigurationError('API client configuration needs a getAccessToken function.');
  }
  if (config.fetch !== undefined && typeof config.fetch !== 'function') {
    throw new ApiConfigurationError('API client fetch must be a function when provided.');
  }

  const client: ApiClient = Object.freeze({
    baseUrl: config.baseUrl,
    getAccessToken: config.getAccessToken,
    fetch: config.fetch,
  });
  clientBrand.add(client);
  return client;
}

let defaultClient: ApiClient | undefined;

/**
 * Sets the default client used by calls that pass no `client` option — convenient for a browser
 * or mobile app with one credential source. Each request snapshots the default when it starts,
 * so reconfiguring never changes a request already in flight.
 */
export function configureApiClient(config: ApiClientConfig): ApiClient {
  defaultClient = createApiClient(config);
  return defaultClient;
}

/** Removes the default client; later calls without a `client` option fail with a configuration error. */
export function clearApiClientConfiguration(): void {
  defaultClient = undefined;
}

/** The client for one request: the per-call client, else the default, else a configuration error. */
export function resolveApiClient(client: ApiClient | undefined): ApiClient {
  if (client !== undefined) {
    if (!clientBrand.has(client)) {
      throw new ApiConfigurationError('Per-call clients must come from createApiClient().');
    }
    return client;
  }
  if (defaultClient === undefined) {
    throw new ApiConfigurationError(
      'The API client is not configured: call configureApiClient() or pass a client created with createApiClient().',
    );
  }
  return defaultClient;
}
