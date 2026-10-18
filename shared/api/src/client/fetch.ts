import { resolveApiClient, type ApiClient } from './config';
import { ApiError, isApiError, readErrorBody, type ApiErrorKind } from './errors';
import { resolveOperation } from './operations';

/**
 * Options every generated function accepts as its last argument: standard `RequestInit` fields —
 * `signal`, `cache`, ordinary `headers` such as tracing headers — plus an optional per-call
 * `client` from `createApiClient()`. Caller headers may not replace a header the generated function
 * supplies (`Idempotency-Key`, `If-Match`, `Content-Type`) and may not carry `Authorization`, which
 * the token provider alone sets; such a request is refused before dispatch.
 */
export interface ApiRequestOptions extends Omit<RequestInit, 'method' | 'body'> {
  readonly client?: ApiClient;
}

/**
 * What generated functions pass to the mutator. Through the S06 header boundary
 * (`scripts/header-boundary.ts`), `headers` holds only the generated headers and `callerHeaders`
 * the caller's unmerged options headers; operations with no generated headers pass the caller's
 * headers as `headers` and no `callerHeaders`.
 */
export interface ApiFetchInit extends RequestInit {
  readonly client?: ApiClient;
  readonly callerHeaders?: HeadersInit;
}

/**
 * The error type Orval gives generated hooks (`TError`). Every failed call rejects with an `ApiError`
 * — whatever error body the operation declares — so hooks are typed with what is actually thrown.
 */
export type ErrorType<Body> = Body extends unknown ? ApiError : never;

/**
 * The Orval mutator: the one place a generated function reaches the network. For each call it
 * resolves the client once, merges the caller's ordinary headers with the generated ones (refusing
 * any conflict), asks the token provider for this request's token, and invokes `fetch` exactly once.
 * It never retries, never renews a token, and never mints or rewrites a header: `Idempotency-Key`
 * and `If-Match` are sent exactly as the generated arguments carry them. A 2xx answer resolves to
 * `{ data, status, headers }` (headers intact, so `ETag` is readable); anything else rejects with an
 * `ApiError` that records whether `fetch` had been invoked.
 *
 * The second overload is the caller-facing options type Orval copies into generated signatures
 * (`Parameters<typeof apiFetch>[1]`): ordinary `RequestInit` fields, without `method` or `body`.
 */
export function apiFetch<T>(url: string, init: ApiFetchInit): Promise<T>;
export function apiFetch<T>(url: string, options?: ApiRequestOptions): Promise<T>;
export async function apiFetch<T>(url: string, init: ApiFetchInit = {}): Promise<T> {
  const { client: requestedClient, callerHeaders, ...requestInit } = init;
  const separated = 'callerHeaders' in init;
  const client = resolveApiClient(requestedClient);
  const method = (requestInit.method ?? 'GET').toUpperCase();
  const path = url.split('?', 1)[0] ?? url;
  const operation = resolveOperation(method, path);
  const signal = requestInit.signal ?? undefined;
  const fail = (
    kind: ApiErrorKind,
    dispatched: boolean,
    extra: Partial<ConstructorParameters<typeof ApiError>[0]> = {},
  ) => new ApiError({ kind, method, path, dispatched, ...extra });
  const abortedBeforeDispatch = (reason: unknown) =>
    fail(abortKind(reason), false, { cause: reason });

  const headers = requestHeaders(
    separated ? requestInit.headers : undefined,
    separated ? callerHeaders : requestInit.headers,
    operation.headers,
    (message) => fail('invalid-request', false, { cause: new TypeError(message) }),
  );

  if (signal?.aborted) throw abortedBeforeDispatch(signal.reason);
  let pendingToken: Promise<string | null>;
  try {
    pendingToken = Promise.resolve(
      client.getAccessToken({ method, path, authTier: operation.authTier }),
    );
  } catch (error) {
    throw fail('token-provider', false, { cause: error });
  }
  let token: string | null;
  try {
    // An abort while the provider is pending settles the call at once; nothing has been sent.
    token = await untilAborted(pendingToken, signal, abortedBeforeDispatch);
  } catch (error) {
    throw isApiError(error) ? error : fail('token-provider', false, { cause: error });
  }
  // The last point before dispatch: an abort that landed while this function was resuming still
  // sends nothing. Everything from here to the `fetch` invocation is synchronous.
  if (signal?.aborted) throw abortedBeforeDispatch(signal.reason);

  if (token !== null) headers.set('Authorization', `Bearer ${token}`);
  const fetchFn = client.fetch ?? globalThis.fetch;

  // Dispatch is the `fetch` invocation itself: from here on the request may have reached the server,
  // so even a synchronous throw from `fetch` is reported as dispatched (the conservative reading).
  let response: Response;
  try {
    response = await fetchFn(joinUrl(client.baseUrl, url), { ...requestInit, method, headers });
  } catch (error) {
    throw fail(failureKind(error, signal), true, { cause: error });
  }

  let text: string;
  try {
    text = await response.text();
  } catch (error) {
    // The status line arrived but the body was lost: the outcome is not readable.
    throw fail(failureKind(error, signal), true, {
      status: response.status,
      headers: response.headers,
      cause: error,
    });
  }

  if (!response.ok) {
    const { body, errorMessages } = readErrorBody(text);
    throw fail('http', true, {
      status: response.status,
      headers: response.headers,
      body,
      errorMessages,
      rawBody: body === 'error-json' || text.length === 0 ? undefined : text,
    });
  }

  let data: unknown;
  if (text.length > 0) {
    try {
      data = JSON.parse(text);
    } catch (error) {
      throw fail('invalid-response', true, {
        status: response.status,
        headers: response.headers,
        body: 'unrecognized',
        rawBody: text,
        cause: error,
      });
    }
  }
  return { data, status: response.status, headers: response.headers } as T;
}

/**
 * Merges the request headers. Generated headers (`generated`: the declared header arguments plus
 * `Content-Type` for a body) are sent exactly as given. The caller's options headers (`caller`) pass
 * through when ordinary, but the request is refused before anything is sent when a caller header —
 * in any casing — names a generated or declared header with a different value (it could replace the
 * frozen `Idempotency-Key` or the `If-Match` precondition), names `Authorization` (the token provider
 * owns it, so a `null` token always means no `Authorization` header), or repeats a name in another
 * casing (which `Headers` would combine into one corrupt value such as `"7", "99"`).
 */
function requestHeaders(
  generated: HeadersInit | undefined,
  caller: HeadersInit | undefined,
  declared: ReadonlySet<string>,
  refuse: (message: string) => ApiError,
): Headers {
  const generatedEntries = entriesOf(generated);
  const generatedValues = new Map<string, string>();
  for (const [name, value] of generatedEntries) {
    const key = name.toLowerCase();
    if (generatedValues.has(key)) throw refuse(`Header ${name} is generated more than once.`);
    if (!declared.has(key) && key !== 'content-type') {
      throw refuse(`Header ${name} is not declared by this operation.`);
    }
    generatedValues.set(key, value);
  }

  const headers = new Headers();
  const callerNames = new Set<string>();
  for (const [name, value] of entriesOf(caller)) {
    const key = name.toLowerCase();
    if (callerNames.has(key)) throw refuse(`Header ${name} is given more than once.`);
    callerNames.add(key);
    if (key === 'authorization') throw refuse('Authorization is set by the token provider only.');
    const protectedValue = generatedValues.get(key);
    if (protectedValue !== undefined || declared.has(key)) {
      if (protectedValue !== value) {
        throw refuse(`Header ${name} conflicts with the value this operation sends.`);
      }
      continue;
    }
    headers.set(name, value);
  }
  for (const [name, value] of generatedEntries) headers.set(name, value);
  return headers;
}

function entriesOf(init: HeadersInit | undefined): [string, string][] {
  if (init === undefined) return [];
  if (init instanceof Headers) return [...init.entries()];
  if (Array.isArray(init)) return init.map(([name, value]) => [String(name), String(value)]);
  return Object.entries(init as Record<string, string>);
}

function untilAborted<T>(
  pending: Promise<T>,
  signal: AbortSignal | undefined,
  onAbort: (reason: unknown) => Error,
): Promise<T> {
  if (signal === undefined) return pending;
  return new Promise<T>((resolve, reject) => {
    const abort = () => reject(onAbort(signal.reason));
    signal.addEventListener('abort', abort, { once: true });
    pending.then(
      (value) => {
        signal.removeEventListener('abort', abort);
        if (signal.aborted) abort();
        else resolve(value);
      },
      (error: unknown) => {
        signal.removeEventListener('abort', abort);
        reject(error);
      },
    );
  });
}

function joinUrl(baseUrl: string, url: string): string {
  return `${baseUrl.replace(/\/+$/, '')}/${url.replace(/^\/+/, '')}`;
}

function abortKind(reason: unknown): ApiErrorKind {
  return isNamedError(reason, 'TimeoutError') ? 'timeout' : 'aborted';
}

function failureKind(error: unknown, signal: AbortSignal | undefined): ApiErrorKind {
  if (isNamedError(error, 'TimeoutError')) return 'timeout';
  if (isNamedError(error, 'AbortError') || signal?.aborted) {
    return abortKind(signal?.aborted ? signal.reason : error);
  }
  return 'network';
}

function isNamedError(value: unknown, name: string): boolean {
  return typeof value === 'object' && value !== null && (value as { name?: unknown }).name === name;
}
