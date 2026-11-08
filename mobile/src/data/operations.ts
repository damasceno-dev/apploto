import {
  postChequeCustody,
  postChequeDeposit,
  postChequeTransactionIdConvertToFiado,
  postChequeTransactionIdRepresent,
  postChequeTransactionIdReturn,
  postTransaction,
  postTransactionInstallment,
  postTransactionFiadoPayment,
  postTransactionSafeDeposit,
  type RequestChequeConvertToFiadoJson,
  type RequestChequeCustodyJson,
  type RequestChequeDepositJson,
  type RequestChequeRepresentJson,
  type RequestChequeReturnJson,
  type RequestCreateTransactionJson,
  type RequestCreateTransactionInstallmentJson,
  type RequestReceiveFiadoPaymentJson,
  type RequestSafeDepositJson,
  type ApiRequestOptions,
} from '@loto/api';
import type { SessionEngine } from '../session/engine';
import { SessionSupersededError } from '../session/types';
import type { ScopedDeviceStore, StorageScope } from '../storage/scoped';
import { validFinancialPayload } from './request-shape';
import { assertDeviceSafe } from '../storage/privacy';
import { actionFailure, type ActionFailure } from './failures';

/**
 * Every contract-idempotent financial command (`Idempotency-Key`, deterministic replay). Cheque
 * return/re-present/conversion address one cheque in the path: `target` is part of the intent.
 */
export type FinancialIntent =
  | { operation: 'transaction'; payload: RequestCreateTransactionJson; target?: null }
  | { operation: 'installment'; payload: RequestCreateTransactionInstallmentJson; target?: null }
  | { operation: 'fiado-payment'; payload: RequestReceiveFiadoPaymentJson; target?: null }
  | { operation: 'safe-deposit'; payload: RequestSafeDepositJson; target?: null }
  | { operation: 'cheque-custody'; payload: RequestChequeCustodyJson; target?: null }
  | { operation: 'cheque-deposit'; payload: RequestChequeDepositJson; target?: null }
  | { operation: 'cheque-return'; payload: RequestChequeReturnJson; target: string }
  | { operation: 'cheque-represent'; payload: RequestChequeRepresentJson; target: string }
  | { operation: 'cheque-convert'; payload: RequestChequeConvertToFiadoJson; target: string };
const targeted = new Set(['cheque-return', 'cheque-represent', 'cheque-convert']);
export interface OperationRecord {
  readonly operation: FinancialIntent['operation'];
  /** The path resource (cheque transaction id) for targeted commands; absent otherwise. */
  readonly target?: string | null;
  readonly key: string;
  readonly payloadJson: string;
  readonly status: 'pending' | ActionFailure['kind'];
  readonly dispatches: number;
  readonly everUncertain: boolean;
  readonly firstServerMs: number | null;
}
export type OperationOutcome =
  | { kind: 'confirmed'; data: unknown; deviceRecordCleared: boolean }
  | { kind: 'failed'; failure: ActionFailure; record: OperationRecord }
  | {
      kind:
        | 'paused'
        | 'in-flight'
        | 'nothing-to-retry'
        | 'storage-unreadable'
        | 'reconcile'
        | 'unresolved'
        | 'cancelled';
    };
export type Reconciliation = 'confirmed' | 'not-executed' | 'unresolved';
const operations = new Set([
  'transaction',
  'installment',
  'fiado-payment',
  'safe-deposit',
  'cheque-custody',
  'cheque-deposit',
  'cheque-return',
  'cheque-represent',
  'cheque-convert',
]);
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function isOperationRecord(value: unknown): value is OperationRecord {
  if (typeof value !== 'object' || value === null) return false;
  const record = value as OperationRecord;
  if (
    !operations.has(record.operation) ||
    !/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(record.key) ||
    typeof record.payloadJson !== 'string' ||
    ![
      'pending',
      'not-sent',
      'uncertain',
      'auth-refused',
      'rate-limited',
      'conflict',
      'rejected',
    ].includes(record.status) ||
    !Number.isSafeInteger(record.dispatches) ||
    record.dispatches < 1 ||
    typeof record.everUncertain !== 'boolean' ||
    (record.status === 'uncertain' && !record.everUncertain) ||
    !(
      record.firstServerMs === null ||
      (typeof record.firstServerMs === 'number' && Number.isFinite(record.firstServerMs))
    ) ||
    (targeted.has(record.operation)
      ? typeof record.target !== 'string' || !uuidPattern.test(record.target)
      : record.target !== undefined && record.target !== null)
  )
    return false;
  try {
    const payload = JSON.parse(record.payloadJson);
    if (typeof payload !== 'object' || payload === null || Array.isArray(payload)) return false;
    assertDeviceSafe(payload);
    return validFinancialPayload(record.operation, payload);
  } catch {
    return false;
  }
}
function send(record: OperationRecord, options: ApiRequestOptions) {
  const headers = { 'Idempotency-Key': record.key };
  switch (record.operation) {
    case 'transaction':
      return postTransaction(
        JSON.parse(record.payloadJson) as RequestCreateTransactionJson,
        headers,
        options,
      );
    case 'installment':
      return postTransactionInstallment(
        JSON.parse(record.payloadJson) as RequestCreateTransactionInstallmentJson,
        headers,
        options,
      );
    case 'fiado-payment':
      return postTransactionFiadoPayment(
        JSON.parse(record.payloadJson) as RequestReceiveFiadoPaymentJson,
        headers,
        options,
      );
    case 'safe-deposit':
      return postTransactionSafeDeposit(
        JSON.parse(record.payloadJson) as RequestSafeDepositJson,
        headers,
        options,
      );
    case 'cheque-custody':
      return postChequeCustody(
        JSON.parse(record.payloadJson) as RequestChequeCustodyJson,
        headers,
        options,
      );
    case 'cheque-deposit':
      return postChequeDeposit(
        JSON.parse(record.payloadJson) as RequestChequeDepositJson,
        headers,
        options,
      );
    case 'cheque-return':
      return postChequeTransactionIdReturn(
        record.target!,
        JSON.parse(record.payloadJson) as RequestChequeReturnJson,
        headers,
        options,
      );
    case 'cheque-represent':
      return postChequeTransactionIdRepresent(
        record.target!,
        JSON.parse(record.payloadJson) as RequestChequeRepresentJson,
        headers,
        options,
      );
    case 'cheque-convert':
      return postChequeTransactionIdConvertToFiado(
        record.target!,
        JSON.parse(record.payloadJson) as RequestChequeConvertToFiadoJson,
        headers,
        options,
      );
  }
}
/** Explicit user continuation only: there is no reconnect flush, automatic mutation retry or queue. */
export class FinancialOperation {
  private active = false;
  private readonly client;
  constructor(
    private readonly engine: SessionEngine,
    private readonly store: ScopedDeviceStore,
    private readonly scope: StorageScope,
    private readonly resource: string,
    private readonly uuid: () => string,
    private readonly dispatch: typeof send = send,
  ) {
    this.client = engine.client();
  }
  async submit(intent: FinancialIntent): Promise<OperationOutcome> {
    return this.execute(intent);
  }
  async verifyAndRetry(): Promise<OperationOutcome> {
    return this.execute(null);
  }
  /**
   * The retained record for this resource, read without dispatching, minting a key or changing it:
   * after a restart a `pending` record means its outcome is unknown.
   */
  async inspect(): Promise<
    | { status: 'none' }
    | { status: 'corrupt' }
    | { status: 'found'; record: OperationRecord; unknown: boolean }
  > {
    try {
      this.store.assert(this.scope);
    } catch {
      return { status: 'none' };
    }
    const loaded = await this.store.read('operation', this.scope, this.resource, isOperationRecord);
    if (loaded.status === 'found')
      return {
        status: 'found',
        record: loaded.value,
        unknown: loaded.value.status === 'pending' || loaded.value.everUncertain,
      };
    return loaded.status === 'corrupt' ? { status: 'corrupt' } : { status: 'none' };
  }
  /** Explicit loss acknowledgement after a fresh supported-resource/manual outcome check.
   * A missing listing row alone is not proof of non-execution. Never dispatches or mints a key.
   */
  async discardAfterReconciliation(
    checkOutcome: (record: Readonly<OperationRecord>) => Promise<Reconciliation>,
    confirmDiscard: boolean,
  ): Promise<'discarded' | 'unresolved' | 'in-flight' | 'nothing-to-discard' | 'paused'> {
    if (!confirmDiscard) return 'unresolved';
    const key = this.store.key('operation', this.scope, this.resource);
    const release = this.store.claimOperation(key);
    if (!release) return 'in-flight';
    try {
      await this.engine.refresh();
      this.store.assert(this.scope);
      if (this.engine.getSnapshot().writesPaused) return 'paused';
      const loaded = await this.store.read(
        'operation',
        this.scope,
        this.resource,
        isOperationRecord,
      );
      if (loaded.status === 'none') return 'nothing-to-discard';
      if (loaded.status !== 'found') return 'unresolved';
      const original = JSON.stringify(loaded.value);
      const outcome = await checkOutcome(Object.freeze({ ...loaded.value }));
      this.store.assert(this.scope);
      if (outcome !== 'confirmed' && outcome !== 'not-executed') return 'unresolved';
      await this.engine.refresh();
      this.store.assert(this.scope);
      if (this.engine.getSnapshot().writesPaused) return 'paused';
      return await this.store.exclusive(async () => {
        const current = await this.store.read(
          'operation',
          this.scope,
          this.resource,
          isOperationRecord,
        );
        if (current.status !== 'found' || JSON.stringify(current.value) !== original)
          return 'unresolved';
        await this.store.storage.removeItem(key);
        this.store.assert(this.scope);
        return 'discarded';
      });
    } finally {
      release();
    }
  }
  private async execute(intent: FinancialIntent | null): Promise<OperationOutcome> {
    if (this.active) return { kind: 'in-flight' };
    if (this.engine.getSnapshot().writesPaused) return { kind: 'paused' };
    try {
      this.store.assert(this.scope);
    } catch {
      return { kind: 'cancelled' };
    }
    const release = this.store.claimOperation(
      this.store.key('operation', this.scope, this.resource),
    );
    if (!release) return { kind: 'in-flight' };
    this.active = true;
    try {
      const prepared = await this.store.exclusive(
        async (): Promise<OperationRecord | OperationOutcome> => {
          const loaded = await this.store.read(
            'operation',
            this.scope,
            this.resource,
            isOperationRecord,
          );
          if (loaded.status === 'corrupt') return { kind: 'storage-unreadable' };
          let record = loaded.status === 'found' ? loaded.value : null;
          if (record?.status === 'pending')
            record = { ...record, status: 'uncertain', everUncertain: true };
          if (intent) {
            assertDeviceSafe(intent.payload);
            if (!validFinancialPayload(intent.operation, intent.payload))
              throw new RangeError('Invalid financial request shape');
            const payloadJson = JSON.stringify(intent.payload);
            const target = targeted.has(intent.operation) ? (intent.target ?? null) : null;
            if (targeted.has(intent.operation) && (!target || !uuidPattern.test(target)))
              throw new RangeError('Invalid financial command target');
            if (
              record &&
              (record.operation !== intent.operation ||
                record.payloadJson !== payloadJson ||
                (record.target ?? null) !== target)
            ) {
              if (
                record.everUncertain ||
                !['not-sent', 'rejected', 'auth-refused', 'rate-limited', 'conflict'].includes(
                  record.status,
                )
              )
                return { kind: 'unresolved' };
              record = null;
            }
            if (!record)
              record = {
                operation: intent.operation,
                ...(target ? { target } : {}),
                key: this.uuid(),
                payloadJson,
                status: 'pending',
                dispatches: 0,
                everUncertain: false,
                firstServerMs: null,
              };
          }
          return record ?? { kind: 'nothing-to-retry' };
        },
      );
      if ('kind' in prepared) return prepared;
      await this.engine.refresh();
      this.store.assert(this.scope);
      if (this.engine.getSnapshot().writesPaused) return { kind: 'paused' };
      const now = this.engine.serverNow();
      if (
        prepared.everUncertain &&
        (prepared.firstServerMs === null ||
          now === null ||
          now < prepared.firstServerMs ||
          now - prepared.firstServerMs >= 23 * 3600000)
      )
        return { kind: 'reconcile' };
      const pending: OperationRecord = {
        ...prepared,
        status: 'pending',
        dispatches: prepared.dispatches + 1,
        firstServerMs: prepared.dispatches === 0 ? now : prepared.firstServerMs,
      };
      if (!isOperationRecord(pending)) throw new RangeError('Invalid operation record');
      await this.store.exclusive(() =>
        this.store.write('operation', this.scope, this.resource, pending),
      );
      this.store.assert(this.scope);
      if (this.engine.getSnapshot().writesPaused) return { kind: 'paused' };
      let response: Awaited<ReturnType<typeof send>>;
      try {
        response = await this.dispatch(pending, { client: this.client });
      } catch (error) {
        const failure = actionFailure(error);
        const failed: OperationRecord = {
          ...pending,
          status: failure.kind,
          everUncertain: pending.everUncertain || failure.kind === 'uncertain',
        };
        try {
          this.store.assert(this.scope);
          await this.store.exclusive(() =>
            this.store.write('operation', this.scope, this.resource, failed),
          );
        } catch (storageError) {
          if (
            error instanceof SessionSupersededError ||
            this.engine.getSnapshot().scope !== this.scope.generation
          )
            return { kind: 'cancelled' };
          throw storageError;
        }
        return { kind: 'failed', failure, record: failed };
      }
      this.store.assert(this.scope);
      // A storage acknowledgement failure is NOT a server rejection. Keep the durable record
      // and report the known result separately; a second create remains blocked by that record.
      try {
        await this.store.exclusive(() =>
          this.store.storage.removeItem(this.store.key('operation', this.scope, this.resource)),
        );
      } catch {
        return { kind: 'confirmed', data: response, deviceRecordCleared: false };
      }
      this.store.assert(this.scope);
      return { kind: 'confirmed', data: response, deviceRecordCleared: true };
    } finally {
      this.active = false;
      release();
    }
  }
}
