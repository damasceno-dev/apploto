import type { ApiClient, ApiRequestOptions } from '@loto/api';
import { classifyMutationError, type MutationFailure } from './classify';
import {
  loadOperation,
  newOperation,
  removeOperation,
  storeOperation,
  withinReplayWindow,
  type OperationRecord,
  type OperationStatus,
} from './operation-store';
import type { StoragePartition } from '../storage/partition';

export type OperationResult<R> =
  | { readonly kind: 'confirmed'; readonly result: R }
  | { readonly kind: 'failed'; readonly failure: MutationFailure; readonly record: OperationRecord }
  /** No authorized, settled session: nothing was sent or stored. */
  | { readonly kind: 'paused' }
  | { readonly kind: 'in-flight' }
  /** A different payload while an earlier operation is unresolved: settle that one first. */
  | { readonly kind: 'unresolved'; readonly record: OperationRecord }
  /** The replay window cannot be proved: no resend; reconcile with the ledger instead. */
  | { readonly kind: 'reconcile'; readonly record: OperationRecord }
  | { readonly kind: 'nothing-to-retry' }
  /** The controller's scope ended (logout, user or branch change) before dispatch: nothing sent. */
  | { readonly kind: 'cancelled' }
  /**
   * The device record could not be read (`unavailable`) or parsed (`corrupt`). An unresolved key may
   * be hidden there, so no new key is minted until the user deliberately recovers.
   */
  | { readonly kind: 'storage-unreadable'; readonly storage: 'unavailable' | 'corrupt' };

export interface OperationControllerConfig<P, R> {
  readonly storage: Storage | null;
  readonly partition: StoragePartition;
  /** Form/operation + resource, e.g. `transaction-create` or `installment-create:2026-10-21`. */
  readonly context: string;
  /** The contract operation, e.g. `POST /transaction`. */
  readonly operation: string;
  /** The session scope this controller belongs to; its client sends that scope and no other. */
  readonly client: ApiClient;
  /**
   * Sends the frozen operation. `options` carries the controller's scope-bound client and abort
   * signal and must be passed to the generated function (`postTransaction(body, headers, options)`).
   */
  readonly send: (payload: P, idempotencyKey: string, options: ApiRequestOptions) => Promise<R>;
  readonly writesPaused: () => boolean;
  /** Newest server time seen (BFF `Date`), or null. */
  readonly serverNow: () => number | null;
  /** Fetches a fresh server time (a BFF read); resolves when `serverNow` is updated. */
  readonly refreshServerTime: () => Promise<unknown>;
}

/**
 * One user intent behind an `Idempotency-Key` create. Never retries by itself: every resend is an
 * explicit call that sends the same key and the same bytes, and only while the replay window is
 * provable when an earlier attempt may have committed.
 */
export class OperationController<P, R> {
  private memory: OperationRecord | null = null;
  private unreadable: 'unavailable' | 'corrupt' | null = null;
  private inFlight = false;
  private disposed = false;
  private readonly abort = new AbortController();
  /** False when the record could not be written to the device: show the storage warning. */
  durable = true;

  constructor(private readonly config: OperationControllerConfig<P, R>) {
    this.load();
  }

  current(): OperationRecord | null {
    return this.memory;
  }

  /** Why no operation can start: device storage could not be read. Null when readable. */
  storageProblem(): 'unavailable' | 'corrupt' | null {
    return this.unreadable;
  }

  /**
   * Ends this controller with its scope: nothing new is dispatched, and an in-flight request is
   * aborted (if it was already sent, its record stays uncertain in its own partition).
   */
  dispose(): void {
    this.disposed = true;
    this.abort.abort();
  }

  async submit(payload: P): Promise<OperationResult<R>> {
    const blocked = this.blocked();
    if (blocked) return blocked;
    const json = JSON.stringify(payload);
    const existing = this.memory;
    if (existing !== null) {
      if (existing.payloadJson === json) return this.resend(existing);
      const settledWithoutCommit =
        !existing.everUncertain &&
        (existing.status === 'rejected' || existing.status === 'not-sent');
      if (!settledWithoutCommit) return { kind: 'unresolved', record: existing };
    }
    // A new intent (or a corrected payload after a known rejection): a new key.
    return this.dispatch(newOperation(this.config.operation, payload));
  }

  /** **Verificar e tentar novamente**: the identical operation again, same key and bytes. */
  async verifyAndRetry(): Promise<OperationResult<R>> {
    const blocked = this.blocked();
    if (blocked) return blocked;
    if (this.memory === null) return { kind: 'nothing-to-retry' };
    return this.resend(this.memory);
  }

  /** Deliberate discard by the user (after reconciling); never automatic. */
  discard(): void {
    removeOperation(this.config.storage, this.config.partition, this.config.context);
    this.memory = null;
  }

  /** Reads the device record again (for example after storage became available). */
  retryStorage(): void {
    this.load();
  }

  /** Deliberate discard of an unparseable record, after the user reconciled the ledger. */
  discardCorrupt(): void {
    if (this.unreadable !== 'corrupt') return;
    removeOperation(this.config.storage, this.config.partition, this.config.context);
    this.load();
  }

  /**
   * Deliberate choice to continue while device storage cannot be read: the operation lives in
   * memory only (`durable` false) and a reload loses its key. The screen must say so first.
   */
  continueWithoutDeviceRecord(): void {
    if (this.unreadable !== 'unavailable') return;
    this.unreadable = null;
    this.durable = false;
  }

  private load(): void {
    const loaded = loadOperation(this.config.storage, this.config.partition, this.config.context);
    // A record frozen for another endpoint (the context reused, a tampered entry) is never resent
    // here: its bytes were prepared for that operation. It is quarantined like a corrupt record.
    const stored =
      loaded.status === 'found' && loaded.record.operation !== this.config.operation
        ? ({ status: 'corrupt' } as const)
        : loaded;
    this.unreadable =
      stored.status === 'unavailable' || stored.status === 'corrupt' ? stored.status : null;
    // A record left `pending` (reload or crash mid-dispatch) has an unknown outcome.
    this.memory =
      stored.status !== 'found'
        ? null
        : stored.record.status === 'pending'
          ? { ...stored.record, status: 'uncertain', everUncertain: true }
          : stored.record;
  }

  private blocked(): OperationResult<R> | null {
    if (this.disposed) return { kind: 'cancelled' };
    if (this.inFlight) return { kind: 'in-flight' };
    if (this.unreadable !== null) return { kind: 'storage-unreadable', storage: this.unreadable };
    if (this.config.writesPaused()) return { kind: 'paused' };
    return null;
  }

  /** Re-checked after every await: a scope that ended or paused meanwhile sends nothing. */
  private stopped(): OperationResult<R> | null {
    if (this.disposed) return { kind: 'cancelled' };
    if (this.config.writesPaused()) return { kind: 'paused' };
    return null;
  }

  private async resend(record: OperationRecord): Promise<OperationResult<R>> {
    this.inFlight = true;
    try {
      if (record.everUncertain || record.status === 'uncertain') {
        await this.config.refreshServerTime();
        const stopped = this.stopped();
        if (stopped) return stopped;
        if (!withinReplayWindow(record, this.config.serverNow())) {
          return { kind: 'reconcile', record };
        }
      }
      return await this.send(record);
    } finally {
      this.inFlight = false;
    }
  }

  private async dispatch(initial: OperationRecord): Promise<OperationResult<R>> {
    this.inFlight = true;
    try {
      return await this.send(initial);
    } finally {
      this.inFlight = false;
    }
  }

  private async send(initial: OperationRecord): Promise<OperationResult<R>> {
    let firstDispatchAfterMs = initial.firstDispatchAfterMs;
    if (initial.dispatches === 0) {
      if (this.config.serverNow() === null) await this.config.refreshServerTime();
      firstDispatchAfterMs = this.config.serverNow();
    }
    const stopped = this.stopped();
    if (stopped) return stopped;
    // Persisted before dispatch: a reload during the request finds it and treats it as uncertain.
    const pending: OperationRecord = {
      ...initial,
      status: 'pending',
      firstDispatchAfterMs,
      dispatches: initial.dispatches + 1,
    };
    this.remember(pending);
    try {
      // Synchronous from the checks above to the call: the request carries this controller's scope.
      const result = await this.config.send(JSON.parse(pending.payloadJson) as P, pending.key, {
        client: this.config.client,
        signal: this.abort.signal,
      });
      removeOperation(this.config.storage, this.config.partition, this.config.context);
      this.memory = null;
      return { kind: 'confirmed', result };
    } catch (error) {
      const failure = classifyMutationError(error);
      const record: OperationRecord = {
        ...pending,
        status: statusFor(failure),
        everUncertain: pending.everUncertain || failure.kind === 'uncertain',
        messages: 'messages' in failure ? failure.messages : [],
      };
      this.remember(record);
      return { kind: 'failed', failure, record };
    }
  }

  private remember(record: OperationRecord): void {
    this.memory = record;
    this.durable = storeOperation(
      this.config.storage,
      this.config.partition,
      this.config.context,
      record,
    );
  }
}

function statusFor(failure: MutationFailure): OperationStatus {
  return failure.kind;
}
