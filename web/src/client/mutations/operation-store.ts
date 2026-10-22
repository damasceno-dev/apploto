import { partitionKey, type StoragePartition } from '../storage/partition';
import { assertDeviceSafe } from '../storage/privacy';

export const operationNamespace = 'loto:op:v1';

/** Replay window of `Idempotency-Key` creates (§6.1: 24 h), less a one-hour safety margin. */
export const replayWindowMs = 23 * 60 * 60 * 1000;

export type OperationStatus =
  'pending' | 'not-sent' | 'auth-refused' | 'rate-limited' | 'conflict' | 'rejected' | 'uncertain';

/**
 * One frozen user intent: endpoint, exact payload bytes and its `Idempotency-Key`, persisted in the
 * identity+branch partition before dispatch so retry and reload send the same key and bytes. It is
 * deleted only after a confirmed success or a deliberate discard — never by a TTL.
 */
export interface OperationRecord {
  readonly v: 1;
  readonly operation: string;
  readonly key: string;
  readonly payloadJson: string;
  readonly status: OperationStatus;
  /**
   * Lower bound of the first dispatch on the **server** clock: the newest BFF `Date` seen before it.
   * Null when none was seen; then the replay window cannot be proved.
   */
  readonly firstDispatchAfterMs: number | null;
  readonly dispatches: number;
  /** Set once any dispatch ended uncertain: every later resend needs the window proof. */
  readonly everUncertain: boolean;
  readonly messages: readonly string[];
}

/** `crypto.randomUUID()`: 36 visible ASCII characters, within the API's 1–128 limit. */
export function mintIdempotencyKey(): string {
  return crypto.randomUUID();
}

export function newOperation(operation: string, payload: unknown): OperationRecord {
  assertDeviceSafe(payload);
  return {
    v: 1,
    operation,
    key: mintIdempotencyKey(),
    payloadJson: JSON.stringify(payload),
    status: 'pending',
    firstDispatchAfterMs: null,
    dispatches: 0,
    everUncertain: false,
    messages: [],
  };
}

export type StoredOperation =
  | { readonly status: 'none' }
  | { readonly status: 'found'; readonly record: OperationRecord }
  | { readonly status: 'corrupt' }
  | { readonly status: 'unavailable' };

export function loadOperation(
  storage: Storage | null,
  partition: StoragePartition,
  context: string,
): StoredOperation {
  const key = partitionKey(operationNamespace, partition, context);
  if (storage === null) return { status: 'unavailable' };
  let raw: string | null;
  try {
    raw = storage.getItem(key);
  } catch {
    return { status: 'unavailable' };
  }
  if (raw === null) return { status: 'none' };
  try {
    const record: unknown = JSON.parse(raw);
    return isOperationRecord(record) ? { status: 'found', record } : { status: 'corrupt' };
  } catch {
    return { status: 'corrupt' };
  }
}

const statuses: ReadonlySet<string> = new Set<OperationStatus>([
  'pending',
  'not-sent',
  'auth-refused',
  'rate-limited',
  'conflict',
  'rejected',
  'uncertain',
]);
const visibleAscii = /^[\x21-\x7e]{1,128}$/;

/**
 * Accepts a stored record only when every field recovery depends on is present, typed and
 * consistent. Anything less is `corrupt` and quarantined: a record missing `status`, `dispatches`
 * or `everUncertain` could otherwise skip the replay-window proof and resend an expired key.
 */
export function isOperationRecord(value: unknown): value is OperationRecord {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false;
  const record = value as Record<string, unknown>;
  const { firstDispatchAfterMs, dispatches, status, everUncertain, messages } = record;
  if (
    record.v !== 1 ||
    typeof record.operation !== 'string' ||
    record.operation.length === 0 ||
    typeof record.key !== 'string' ||
    !visibleAscii.test(record.key) ||
    typeof record.payloadJson !== 'string' ||
    typeof status !== 'string' ||
    !statuses.has(status) ||
    !(
      firstDispatchAfterMs === null ||
      (typeof firstDispatchAfterMs === 'number' && Number.isFinite(firstDispatchAfterMs))
    ) ||
    typeof dispatches !== 'number' ||
    !Number.isSafeInteger(dispatches) ||
    dispatches < 0 ||
    typeof everUncertain !== 'boolean' ||
    !Array.isArray(messages) ||
    !messages.every((message) => typeof message === 'string')
  ) {
    return false;
  }
  try {
    JSON.parse(record.payloadJson);
  } catch {
    return false;
  }
  // Consistency: every stored status follows at least one dispatch, and an uncertain outcome is
  // always remembered as such.
  if (dispatches === 0) return false;
  if (status === 'uncertain' && !everUncertain) return false;
  return true;
}

/** Returns false when the record could not be made durable: it stays in memory and the screen says so. */
export function storeOperation(
  storage: Storage | null,
  partition: StoragePartition,
  context: string,
  record: OperationRecord,
): boolean {
  const key = partitionKey(operationNamespace, partition, context);
  if (storage === null) return false;
  try {
    storage.setItem(key, JSON.stringify(record));
    return true;
  } catch {
    return false;
  }
}

export function removeOperation(
  storage: Storage | null,
  partition: StoragePartition,
  context: string,
): void {
  const key = partitionKey(operationNamespace, partition, context);
  try {
    storage?.removeItem(key);
  } catch {
    // Unavailable storage keeps the record; the screen still shows its unresolved state.
  }
}

/**
 * Whether the same key may be resent: provable only from server time — the newest BFF `Date` now
 * minus the recorded lower bound of the first dispatch — never from the device clock.
 */
export function withinReplayWindow(record: OperationRecord, serverNowMs: number | null): boolean {
  if (record.firstDispatchAfterMs === null || serverNowMs === null) return false;
  return serverNowMs - record.firstDispatchAfterMs < replayWindowMs;
}
