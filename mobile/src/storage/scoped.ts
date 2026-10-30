import type { SessionEngine } from '../session/engine';
import { SessionGateError, SessionSupersededError } from '../session/types';
import { assertDeviceSafe } from './privacy';
import type { DeviceStore } from './ports';

export interface StorageScope {
  readonly userId: string;
  readonly emailKey: string;
  readonly branchId: string;
  readonly generation: number;
}
export type Stored<T> = { status: 'none' } | { status: 'found'; value: T } | { status: 'corrupt' };
export interface RetainedDataReview {
  readonly drafts: number;
  readonly operations: number;
  readonly intents: number;
}
const retainedKind = (key: string) =>
  key === 'loto.navigation.intent.v1'
    ? 'intent'
    : /^loto\.(draft|operation|intent)\.v1:/.exec(key)?.[1];
export class ScopedDeviceStore {
  private tail: Promise<unknown> = Promise.resolve();
  private operationFlights = new Set<string>();
  private maintenance = false;
  private reviews = new WeakMap<
    RetainedDataReview,
    readonly (readonly [string, string | null])[]
  >();
  claimOperation(key: string): (() => void) | null {
    if (this.maintenance || this.operationFlights.has(key)) return null;
    this.operationFlights.add(key);
    return () => this.operationFlights.delete(key);
  }
  constructor(
    readonly storage: DeviceStore,
    private readonly engine: SessionEngine,
    private readonly hash: (email: string) => Promise<string>,
  ) {}
  /** Counts only for the currently verified identity; never lets another employee's input drive shell/logout UI. */
  inspectOwnWork(): Promise<{ readonly drafts: number; readonly operations: number }> {
    return this.exclusive(async () => {
      const user = this.engine.getSnapshot().user;
      if (!user) return { drafts: 0, operations: 0 };
      const emailKey = await this.hash(user.email);
      const keys = await this.storage.getAllKeys();
      const current = this.engine.getSnapshot().user;
      if (current?.userId !== user.userId || current.email !== user.email)
        throw new SessionSupersededError();
      return {
        drafts: keys.filter((key) => key.startsWith(`loto.draft.v1:${user.userId}:${emailKey}:`))
          .length,
        operations: keys.filter((key) =>
          key.startsWith(`loto.operation.v1:${user.userId}:${emailKey}:`),
        ).length,
      };
    });
  }
  /** Counts only, including quarantined owners; never exposes another person's input. */
  inspectRetainedData(): Promise<RetainedDataReview> {
    return this.exclusive(async () => {
      const keys = (await this.storage.getAllKeys()).filter((key) => retainedKind(key)).sort();
      const entries = await Promise.all(
        keys.map(async (key) => [key, await this.storage.getItem(key)] as const),
      );
      const review = Object.freeze({
        drafts: keys.filter((key) => retainedKind(key) === 'draft').length,
        operations: keys.filter((key) => retainedKind(key) === 'operation').length,
        intents: keys.filter((key) => retainedKind(key) === 'intent').length,
      });
      this.reviews.set(review, entries);
      return review;
    });
  }
  /** Device-owner action: explicitly abandon retained input for ALL users/branches.
   * Operation outcomes are not cancelled/reconciled by erasure. Warn before confirmation;
   * use FinancialOperation.discardAfterReconciliation for ordinary form recovery instead.
   * No TTL silently destroys unresolved input. Credentials/installation markers are excluded.
   */
  discardRetainedData(review: RetainedDataReview, confirmLoss: boolean): Promise<boolean> {
    return this.exclusive(async () => {
      const entries = this.reviews.get(review);
      if (!confirmLoss || !entries) return false;
      if (this.operationFlights.size) throw new Error('Uma operação ainda está em andamento.');
      this.maintenance = true;
      try {
        const keys = (await this.storage.getAllKeys()).filter((key) => retainedKind(key)).sort();
        if (JSON.stringify(keys) !== JSON.stringify(entries.map(([key]) => key)))
          throw new Error('Os dados guardados mudaram. Confira novamente antes de apagar.');
        for (const [key, raw] of entries)
          if ((await this.storage.getItem(key)) !== raw)
            throw new Error('Os dados guardados mudaram. Confira novamente antes de apagar.');
        this.reviews.delete(review);
        for (const [key] of entries) await this.storage.removeItem(key);
        return true;
      } finally {
        this.maintenance = false;
      }
    });
  }
  async scope(): Promise<StorageScope> {
    const snapshot = this.engine.getSnapshot();
    if (
      !['branch', 'offline', 'recovering', 'renewal-unconfirmed', 'storage-error'].includes(
        snapshot.status,
      ) ||
      !snapshot.user ||
      !snapshot.branch
    )
      throw new SessionGateError();
    const emailKey = await this.hash(snapshot.user.email);
    if (this.engine.getSnapshot().scope !== snapshot.scope) throw new SessionSupersededError();
    return {
      userId: snapshot.user.userId,
      emailKey,
      branchId: snapshot.branch.id,
      generation: snapshot.scope,
    };
  }
  assert(scope: StorageScope) {
    const now = this.engine.getSnapshot();
    if (
      now.scope !== scope.generation ||
      !['branch', 'offline', 'recovering', 'renewal-unconfirmed', 'storage-error'].includes(
        now.status,
      ) ||
      now.user?.userId !== scope.userId ||
      now.branch?.id !== scope.branchId
    )
      throw new SessionSupersededError();
  }
  key(kind: 'draft' | 'operation' | 'intent', scope: StorageScope, resource: string) {
    if (!/^[A-Za-z0-9][A-Za-z0-9:/._-]{0,199}$/.test(resource) || resource.includes('..'))
      throw new RangeError('Invalid storage resource');
    return `loto.${kind}.v1:${scope.userId}:${scope.emailKey}:${scope.branchId}:${resource}`;
  }
  /** Serialize read/modify/write and acknowledges; always fail before dispatch on storage error. */
  exclusive<T>(action: () => Promise<T>): Promise<T> {
    const job = this.tail.catch(() => undefined).then(action);
    this.tail = job;
    return job;
  }
  async read<T>(
    kind: 'draft' | 'operation' | 'intent',
    scope: StorageScope,
    resource: string,
    validate: (value: unknown) => value is T,
  ): Promise<Stored<T>> {
    this.assert(scope);
    const raw = await this.storage.getItem(this.key(kind, scope, resource));
    this.assert(scope);
    if (raw === null) return { status: 'none' };
    try {
      const record = JSON.parse(raw) as {
        v?: unknown;
        userId?: unknown;
        emailKey?: unknown;
        branchId?: unknown;
        resource?: unknown;
        value?: unknown;
      };
      if (
        record.v !== 1 ||
        record.userId !== scope.userId ||
        record.emailKey !== scope.emailKey ||
        record.branchId !== scope.branchId ||
        record.resource !== resource ||
        !validate(record.value)
      )
        return { status: 'corrupt' };
      assertDeviceSafe(record.value);
      return { status: 'found', value: record.value };
    } catch {
      return { status: 'corrupt' };
    }
  }
  async write(
    kind: 'draft' | 'operation' | 'intent',
    scope: StorageScope,
    resource: string,
    value: unknown,
  ) {
    this.assert(scope);
    assertDeviceSafe(value);
    await this.storage.setItem(
      this.key(kind, scope, resource),
      JSON.stringify({
        v: 1,
        userId: scope.userId,
        emailKey: scope.emailKey,
        branchId: scope.branchId,
        resource,
        value,
      }),
    );
    this.assert(scope);
  }
}
export interface DeviceDraft<T> {
  readonly revision: string;
  readonly input: T;
  readonly base: unknown;
}
export class DeviceDraftStore {
  constructor(
    private readonly store: ScopedDeviceStore,
    private readonly uuid: () => string,
  ) {}
  async save<T>(
    scope: StorageScope,
    resource: string,
    input: T,
    base: unknown,
  ): Promise<DeviceDraft<T>> {
    const draft = { revision: this.uuid(), input: JSON.parse(JSON.stringify(input)) as T, base };
    assertDeviceSafe(input);
    assertDeviceSafe(base);
    await this.store.exclusive(() => this.store.write('draft', scope, resource, draft));
    return draft;
  }
  async restore<T>(
    scope: StorageScope,
    resource: string,
    validate: (value: unknown) => value is T,
    recheck: () => Promise<boolean>,
  ): Promise<Stored<DeviceDraft<T>>> {
    this.store.assert(scope);
    if (!(await recheck()))
      throw new SessionGateError('Seu acesso a este registro precisa ser verificado novamente.');
    this.store.assert(scope);
    return this.store.read('draft', scope, resource, (value): value is DeviceDraft<T> => {
      if (typeof value !== 'object' || value === null) return false;
      const record = value as Partial<DeviceDraft<T>>;
      return typeof record.revision === 'string' && validate(record.input) && 'base' in record;
    });
  }
  /** A saved older snapshot cannot delete input edited while that save was in flight. */
  acknowledge(scope: StorageScope, resource: string, revision: string) {
    return this.store.exclusive(async () => {
      const found = await this.store.read(
        'draft',
        scope,
        resource,
        (value): value is DeviceDraft<unknown> =>
          typeof value === 'object' &&
          value !== null &&
          typeof (value as DeviceDraft<unknown>).revision === 'string',
      );
      if (found.status === 'found' && found.value.revision === revision) {
        this.store.assert(scope);
        await this.store.storage.removeItem(this.store.key('draft', scope, resource));
        this.store.assert(scope);
      }
    });
  }
}
