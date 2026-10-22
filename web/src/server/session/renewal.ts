import 'server-only';
import type { ResponseBranchSummaryJson } from '@loto/api';
import { isDefinitiveRefusal, type AuthBackend } from './backend';
import {
  hashKey,
  peerResultMs,
  rotationMemoryMs,
  singleFlight,
  unknownPauseMs,
  type SessionRegistry,
} from './registry';

const busyRetryDelayMs = 250;

export type RenewalOutcome =
  | { readonly kind: 'renewed'; readonly identity: string; readonly refresh: string }
  /** A definite refusal (`REFRESHTOKEN_*` 401, or another 4xx): final for that refresh token. */
  | { readonly kind: 'rejected'; readonly status: number; readonly messages: readonly string[] }
  /** This process already rotated that token: the caller holds stale credentials, not a lost session. */
  | { readonly kind: 'stale' }
  /** Lost response, timeout or 5xx: the server may have rotated. Writes stay paused. */
  | { readonly kind: 'unknown' };

/**
 * The one renewal path. Concurrent callers holding the same refresh token share one
 * `POST /user/renew-token`; a successful rotation is handed to late holders of the old token for
 * `peerResultMs` (the catalog's across-tab peer result) instead of spending it again. An unknown
 * outcome allows exactly one verification retry, then pauses for `unknownPauseMs` without calling.
 */
export function renewIdentity(
  registry: SessionRegistry,
  backend: AuthBackend,
  refreshToken: string,
): Promise<RenewalOutcome> {
  const tokenKey = hashKey('refresh', refreshToken);
  return singleFlight<RenewalOutcome>(
    registry,
    `renew:${tokenKey}`,
    async () => {
      const paused = registry.unknown.get(tokenKey);
      if (paused && paused.count >= 2 && paused.until > registry.now()) return { kind: 'unknown' };
      let result = await backend.renew(refreshToken);
      // 409 `SECURITY_COORDINATION_BUSY`: another login/renewal of this account holds the lock and
      // nothing rotated. Retry once; a second busy answer is treated as unconfirmed, never as loss.
      if (!result.ok && result.error.kind === 'http' && result.error.status === 409) {
        await new Promise((resolve) => setTimeout(resolve, busyRetryDelayMs));
        result = await backend.renew(refreshToken);
      }
      if (!result.ok && result.error.kind === 'http' && result.error.status === 409) {
        return { kind: 'unknown' };
      }
      if (result.ok) {
        registry.unknown.delete(tokenKey);
        registry.rotated.set(tokenKey, registry.now() + rotationMemoryMs);
        return {
          kind: 'renewed',
          identity: result.data.token,
          refresh: result.data.refreshToken,
        };
      }
      if (isDefinitiveRefusal(result.error)) {
        registry.unknown.delete(tokenKey);
        if ((registry.rotated.get(tokenKey) ?? 0) > registry.now()) return { kind: 'stale' };
        return {
          kind: 'rejected',
          status: result.error.status ?? 401,
          messages: result.error.errorMessages,
        };
      }
      const count = (paused?.count ?? 0) + 1;
      registry.unknown.set(tokenKey, { count, until: registry.now() + unknownPauseMs });
      return { kind: 'unknown' };
    },
    (outcome) => (outcome.kind === 'unknown' ? 0 : peerResultMs),
  );
}

export type BranchSessionOutcome =
  | {
      readonly kind: 'created';
      readonly token: string;
      readonly branch: ResponseBranchSummaryJson;
    }
  /** 404 `BRANCH_NOT_FOUND`: membership or branch gone. Back to Branch picker, verbatim. */
  | { readonly kind: 'unavailable'; readonly messages: readonly string[] }
  | { readonly kind: 'rejected'; readonly status: number; readonly messages: readonly string[] }
  | { readonly kind: 'unknown' };

/** `POST /branch/session`, single-flight per identity token and branch. */
export function openBranchSession(
  registry: SessionRegistry,
  backend: AuthBackend,
  identityToken: string,
  branchId: string,
): Promise<BranchSessionOutcome> {
  return singleFlight<BranchSessionOutcome>(
    registry,
    `branch:${hashKey('identity', identityToken, branchId)}`,
    async () => {
      const result = await backend.createBranchSession(identityToken, branchId);
      if (result.ok) {
        return { kind: 'created', token: result.data.token, branch: result.data.branch };
      }
      const { error } = result;
      if (!isDefinitiveRefusal(error)) return { kind: 'unknown' };
      if (error.status === 404) return { kind: 'unavailable', messages: error.errorMessages };
      return { kind: 'rejected', status: error.status ?? 400, messages: error.errorMessages };
    },
    (outcome) => (outcome.kind === 'created' ? peerResultMs : 0),
  );
}
