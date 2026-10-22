import 'server-only';
import type { AuthTier } from '@loto/api';
import { forwardedOperations } from './operations.generated';

export interface ForwardedOperation {
  readonly method: string;
  readonly template: string;
  readonly authTier: AuthTier;
  /** Declared header parameters, lower-cased (`idempotency-key`, `if-match`). */
  readonly headers: readonly string[];
}

interface Compiled extends ForwardedOperation {
  readonly segments: readonly (string | null)[];
  readonly literals: number;
}

const compiled: readonly Compiled[] = forwardedOperations.map((operation) => {
  const segments = operation.path
    .split('/')
    .slice(1)
    .map((segment) => (segment.startsWith('{') && segment.endsWith('}') ? null : segment));
  return {
    method: operation.method,
    template: operation.path,
    authTier: operation.authTier,
    headers: operation.headers.map((name) => name.toLowerCase()),
    segments,
    literals: segments.filter((segment) => segment !== null).length,
  };
});

/** One path segment as the API publishes them: ids, dates, numbers and literal names. */
const safeSegment = /^[A-Za-z0-9_~-][A-Za-z0-9._~-]*$/;

/**
 * Splits a raw request path (still percent-encoded) into API segments, or null when any segment is
 * empty, a dot segment, percent-encoded (an encoded slash or backslash could change the target) or
 * outside the published character set.
 */
export function apiSegments(rawPath: string): string[] | null {
  if (!rawPath.startsWith('/') || rawPath.length > 2048) return null;
  const segments = rawPath.slice(1).split('/');
  return segments.every((segment) => safeSegment.test(segment)) ? segments : null;
}

/**
 * The operation `method` + `segments` names, using Shared's precedence: the template with the most
 * literal segments wins (`/transaction/installment` over `/transaction/{transactionId}`).
 */
export function matchOperation(
  method: string,
  segments: readonly string[],
): ForwardedOperation | null {
  let best: Compiled | null = null;
  for (const operation of compiled) {
    if (operation.method !== method || operation.segments.length !== segments.length) continue;
    const matches = operation.segments.every(
      (segment, index) => segment === null || segment === segments[index],
    );
    if (matches && (best === null || operation.literals > best.literals)) best = operation;
  }
  return best;
}
