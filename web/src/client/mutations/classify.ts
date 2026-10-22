import { isApiError, isOutcomeUncertain } from '@loto/api';
import {
  bffErrorHeader,
  sessionOutcomeHeader,
  type BffErrorCode,
  type SessionOutcome,
} from '@/lib/bff-protocol';

/**
 * What a failed write means for the user's operation (catalog: Financial create retry and uncertain
 * outcomes; Guarded edits). Messages are the backend `ErrorMessages`, verbatim and in order.
 */
export type MutationFailure =
  /** Proven not executed: refused before dispatch, or by the BFF before forwarding. */
  | { readonly kind: 'not-sent'; readonly bffError: BffErrorCode | null }
  /**
   * A definite 401: authenticated use cases throw before committing, so nothing was written. The
   * BFF already ran one recovery cycle; writes are never replayed automatically — continuing is an
   * explicit user action with the same operation.
   */
  | {
      readonly kind: 'auth-refused';
      readonly messages: readonly string[];
      readonly session: SessionOutcome | null;
    }
  | {
      readonly kind: 'rate-limited';
      readonly messages: readonly string[];
      /** Advisory only: no countdown, no automatic retry. */
      readonly retryAfterSeconds: number | null;
    }
  /** 409: stale version, lock, frozen close, payload conflict or busy — resolved by its context. */
  | { readonly kind: 'conflict'; readonly messages: readonly string[] }
  /** Another definite rejection (400/403/404/…): show it, keep input, stop. */
  | { readonly kind: 'rejected'; readonly status: number; readonly messages: readonly string[] }
  /** **Resultado não confirmado**: dispatched, and the outcome is unknown. Keep payload and key. */
  | {
      readonly kind: 'uncertain';
      readonly messages: readonly string[];
      readonly reason: 'server-error' | 'no-response' | 'unreadable-success';
    };

export function classifyMutationError(error: unknown): MutationFailure {
  if (!isApiError(error)) return { kind: 'not-sent', bffError: null };
  const bffError = error.headers?.get(bffErrorHeader) as BffErrorCode | null | undefined;
  if (bffError) return { kind: 'not-sent', bffError };
  if (!error.dispatched) return { kind: 'not-sent', bffError: null };
  const messages = error.errorMessages;
  if (isOutcomeUncertain(error)) {
    return {
      kind: 'uncertain',
      messages,
      reason: error.kind === 'http' ? 'server-error' : 'no-response',
    };
  }
  if (error.kind === 'invalid-response') {
    // A 2xx whose body could not be read: committed, but the result is unknown here.
    return { kind: 'uncertain', messages, reason: 'unreadable-success' };
  }
  const status = error.status ?? 0;
  if (status === 401) {
    const session = error.headers?.get(sessionOutcomeHeader) as SessionOutcome | null | undefined;
    return { kind: 'auth-refused', messages, session: session ?? null };
  }
  if (status === 429) {
    const seconds = Number(error.headers?.get('Retry-After'));
    return {
      kind: 'rate-limited',
      messages,
      retryAfterSeconds: Number.isFinite(seconds) && seconds > 0 ? seconds : null,
    };
  }
  if (status === 409) return { kind: 'conflict', messages };
  return { kind: 'rejected', status, messages };
}
