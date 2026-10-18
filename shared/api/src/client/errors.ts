import type { ResponseErrorJson } from '../generated/model';

/**
 * How a request failed.
 * - `invalid-request`: refused before dispatch — a caller header conflicted with a generated or
 *   declared header (`Idempotency-Key`, `If-Match`, `Content-Type`), named `Authorization` (the token
 *   provider owns it), or repeated a header name in another casing.
 * - `token-provider`: the injected token provider threw or rejected; nothing was sent (`cause` keeps
 *   the provider's error).
 * - `http`: the server answered with a non-2xx status.
 * - `invalid-response`: the server answered 2xx but the body could not be read as the contract says.
 * - `network`: no usable response — the connection failed, or the response was lost while reading it.
 * - `timeout`: the caller's signal timed out (`AbortSignal.timeout`).
 * - `aborted`: the caller's signal aborted the request.
 */
export type ApiErrorKind =
  | 'invalid-request'
  | 'token-provider'
  | 'http'
  | 'invalid-response'
  | 'network'
  | 'timeout'
  | 'aborted';

/**
 * Whether the failed response carried a body, and whether it was the serialized
 * `ResponseErrorJson` contract (`error-json`), something else (`unrecognized`), or nothing (`none`).
 */
export type ApiErrorBody = 'error-json' | 'unrecognized' | 'none';

export interface ApiErrorDetails {
  readonly kind: ApiErrorKind;
  readonly method: string;
  readonly path: string;
  /** True once the transport invoked `fetch`: the request may have reached the server. */
  readonly dispatched: boolean;
  readonly status?: number;
  readonly headers?: Headers;
  readonly errorMessages?: readonly string[];
  readonly body?: ApiErrorBody;
  readonly rawBody?: string;
  readonly cause?: unknown;
}

/**
 * One typed error for every failed API call. `errorMessages` is the server's `ResponseErrorJson`
 * list verbatim — same order, duplicates kept — or empty when the list is `null`, missing or the
 * body is not the error contract. Transport messages are technical; screens render pt-BR copy.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly method: string;
  readonly path: string;
  readonly dispatched: boolean;
  readonly status: number | undefined;
  readonly headers: Headers | undefined;
  readonly errorMessages: readonly string[];
  readonly body: ApiErrorBody;
  readonly rawBody: string | undefined;

  constructor(details: ApiErrorDetails) {
    super(
      `${details.method} ${details.path} failed: ${details.kind}${details.status === undefined ? '' : ` ${details.status}`}`,
      { cause: details.cause },
    );
    this.name = 'ApiError';
    this.kind = details.kind;
    this.method = details.method;
    this.path = details.path;
    this.dispatched = details.dispatched;
    this.status = details.status;
    this.headers = details.headers;
    this.errorMessages = Object.freeze([...(details.errorMessages ?? [])]);
    this.body = details.body ?? 'none';
    this.rawBody = details.rawBody;
  }
}

/** Thrown when the client is used without a configuration, or with an invalid one. */
export class ApiConfigurationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ApiConfigurationError';
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function isApiConfigurationError(error: unknown): error is ApiConfigurationError {
  return error instanceof ApiConfigurationError;
}

/**
 * True when a write may have reached the server but its result is unknown: a dispatched non-GET
 * that ended in a 5xx, a timeout, a lost or failed connection, or a cancellation after dispatch.
 * False for any GET, any non-5xx HTTP answer, and every failure before dispatch — an abort, a
 * refused header or a token-provider failure (nothing was sent).
 * The caller keeps its frozen candidate and `Idempotency-Key` either way; recovery is app-owned.
 */
export function isOutcomeUncertain(error: unknown): boolean {
  if (!isApiError(error) || error.method === 'GET' || !error.dispatched) return false;
  switch (error.kind) {
    case 'http':
      return error.status !== undefined && error.status >= 500;
    case 'network':
    case 'timeout':
    case 'aborted':
      return true;
    case 'invalid-request':
    case 'token-provider':
    case 'invalid-response':
      return false;
  }
}

/** Reads a non-2xx body as `ResponseErrorJson` when it is one; anything else is kept raw. */
export function readErrorBody(text: string): {
  body: ApiErrorBody;
  errorMessages: readonly string[];
} {
  if (text.length === 0) return { body: 'none', errorMessages: [] };
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return { body: 'unrecognized', errorMessages: [] };
  }
  if (!isResponseErrorJson(parsed)) return { body: 'unrecognized', errorMessages: [] };
  return { body: 'error-json', errorMessages: parsed.errorMessages ?? [] };
}

function isResponseErrorJson(value: unknown): value is ResponseErrorJson {
  if (typeof value !== 'object' || value === null || !('errorMessages' in value)) return false;
  const messages = (value as { errorMessages: unknown }).errorMessages;
  return (
    messages === null ||
    (Array.isArray(messages) && messages.every((message) => typeof message === 'string'))
  );
}
