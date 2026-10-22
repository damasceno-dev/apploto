import { isApiError, parseETag, toIfMatch } from '@loto/api';
import { bffErrorHeader } from '@/lib/bff-protocol';
import { classifyMutationError, type MutationFailure } from './classify';

/**
 * The version a response proves (§6.11): its strong `ETag` first, else the root's `Version` from the
 * body when the envelope carries it (for example `DailyClose.Version` in the item-save response).
 * Versions are session-lived: they live in memory and are never put in device drafts.
 */
export function versionOf(
  result: { readonly headers: Headers },
  bodyVersion?: number | null,
): number | null {
  return parseETag(result.headers.get('ETag')) ?? bodyVersion ?? null;
}

export type GuardedWriteResult<R> =
  | { readonly kind: 'saved'; readonly result: R; readonly version: number | null }
  /**
   * 409 on a guarded write — stale version, locked period or busy: the response cannot tell which
   * without reading prose, so the recovery is the generic **Recarregar e conferir**. The caller
   * keeps the user's candidate and reloads the current resource for a deliberate reconciliation.
   */
  | {
      readonly kind: 'conflict';
      readonly messages: readonly string[];
      readonly recovery: 'reload-and-review';
    }
  | { readonly kind: 'failed'; readonly failure: MutationFailure }
  /** No loaded version: nothing is sent and no version is invented. */
  | { readonly kind: 'missing-version' };

/**
 * Sends one guarded mutation with `If-Match` built from the loaded version. It never retries, never
 * puts a newer `ETag` on an old payload and never fabricates a version.
 */
export async function guardedWrite<R extends { readonly headers: Headers }>(
  version: number | null,
  write: (ifMatch: string) => Promise<R>,
  bodyVersion: (result: R) => number | null = () => null,
): Promise<GuardedWriteResult<R>> {
  if (version === null) return { kind: 'missing-version' };
  try {
    const result = await write(toIfMatch(version));
    return { kind: 'saved', result, version: versionOf(result, bodyVersion(result)) };
  } catch (error) {
    // A BFF refusal (scope changed in another tab) is not a server conflict: nothing was sent.
    if (
      isApiError(error) &&
      error.kind === 'http' &&
      error.status === 409 &&
      !error.headers?.has(bffErrorHeader)
    ) {
      return { kind: 'conflict', messages: error.errorMessages, recovery: 'reload-and-review' };
    }
    return { kind: 'failed', failure: classifyMutationError(error) };
  }
}
