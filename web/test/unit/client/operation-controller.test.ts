import { ApiError, createApiClient, type ApiRequestOptions } from '@loto/api';
import { describe, expect, it } from 'vitest';
import {
  OperationController,
  type OperationControllerConfig,
} from '@/client/mutations/operation-controller';
import { replayWindowMs } from '@/client/mutations/operation-store';
import { MemoryStorage } from '../support/memory-storage';

const partition = { userKey: 'hash-ana', branchId: 'b1' };
const hour = 60 * 60 * 1000;

function fixture(overrides: Partial<OperationControllerConfig<{ value: number }, string>> = {}) {
  const storage = new MemoryStorage();
  const keys: string[] = [];
  const payloads: string[] = [];
  const options: ApiRequestOptions[] = [];
  const client = createApiClient({ baseUrl: '/bff/api', getAccessToken: () => null });
  const server = { now: 1_000_000 as number | null };
  let behavior: () => Promise<string> = async () => 'created';
  const config: OperationControllerConfig<{ value: number }, string> = {
    storage,
    partition,
    context: 'transaction-create',
    operation: 'POST /transaction',
    client,
    send: async (payload, key, sendOptions) => {
      options.push(sendOptions);
      keys.push(key);
      payloads.push(JSON.stringify(payload));
      return behavior();
    },
    writesPaused: () => false,
    serverNow: () => server.now,
    refreshServerTime: async () => undefined,
    ...overrides,
  };
  return {
    storage,
    keys,
    payloads,
    options,
    client,
    server,
    config,
    respond: (next: () => Promise<string>) => (behavior = next),
    controller: () => new OperationController(config),
  };
}

const uncertain = () =>
  Promise.reject(
    new ApiError({
      kind: 'http',
      method: 'POST',
      path: '/transaction',
      dispatched: true,
      status: 504,
    }),
  );
const rejected = (status: number, messages: string[]) =>
  Promise.reject(
    new ApiError({
      kind: 'http',
      method: 'POST',
      path: '/transaction',
      dispatched: true,
      status,
      errorMessages: messages,
      body: 'error-json',
    }),
  );

describe('Idempotency-Key lifetime', () => {
  it('mints one cryptographic key per intent and a new one for the next intent', async () => {
    const f = fixture();
    const controller = f.controller();
    expect(await controller.submit({ value: 10 })).toEqual({
      kind: 'confirmed',
      result: 'created',
    });
    expect(await controller.submit({ value: 10 })).toEqual({
      kind: 'confirmed',
      result: 'created',
    });
    expect(f.keys).toHaveLength(2);
    expect(f.keys[0]).not.toBe(f.keys[1]);
    expect(f.keys[0]).toMatch(/^[0-9a-f-]{36}$/);
    expect(f.storage.keys()).toEqual([]);
  });

  it('keeps key and exact bytes through an uncertain outcome, a retry and a page reload', async () => {
    const f = fixture();
    f.respond(uncertain);
    const first = await f.controller().submit({ value: 10 });
    expect(first).toMatchObject({ kind: 'failed', failure: { kind: 'uncertain' } });
    // Reload: a new controller reads the persisted record.
    const reloaded = f.controller();
    expect(reloaded.current()?.status).toBe('uncertain');
    f.respond(async () => 'replayed');
    expect(await reloaded.verifyAndRetry()).toEqual({ kind: 'confirmed', result: 'replayed' });
    expect(f.keys[1]).toBe(f.keys[0]);
    expect(f.payloads[1]).toBe(f.payloads[0]);
    expect(reloaded.current()).toBeNull();
  });

  it('treats a record left pending by a reload as uncertain', async () => {
    const f = fixture();
    let release!: (value: string) => void;
    f.respond(() => new Promise((resolve) => (release = resolve)));
    void f.controller().submit({ value: 5 });
    await Promise.resolve();
    const afterReload = f.controller();
    expect(afterReload.current()).toMatchObject({ status: 'uncertain', everUncertain: true });
    release('late');
  });

  it('refuses a changed payload while the original outcome is unresolved', async () => {
    const f = fixture();
    f.respond(uncertain);
    const controller = f.controller();
    await controller.submit({ value: 10 });
    const second = await controller.submit({ value: 11 });
    expect(second.kind).toBe('unresolved');
    expect(f.keys).toHaveLength(1);
  });

  it('after a known rejection: identical retry keeps the key, a corrected payload gets a new one', async () => {
    const f = fixture();
    f.respond(() => rejected(400, ['Valor inválido']));
    const controller = f.controller();
    const refused = await controller.submit({ value: 0 });
    expect(refused).toMatchObject({
      kind: 'failed',
      failure: { kind: 'rejected', messages: ['Valor inválido'] },
    });
    await controller.submit({ value: 0 });
    expect(f.keys[1]).toBe(f.keys[0]);
    f.respond(async () => 'ok');
    await controller.submit({ value: 12 });
    expect(f.keys[2]).not.toBe(f.keys[0]);
  });

  it('a 409 keeps the original operation and key for an explicit decision', async () => {
    const f = fixture();
    f.respond(() => rejected(409, ['Chave já usada com outro conteúdo']));
    const controller = f.controller();
    await controller.submit({ value: 3 });
    expect(controller.current()).toMatchObject({ status: 'conflict', key: f.keys[0] });
    expect((await controller.submit({ value: 4 })).kind).toBe('unresolved');
  });

  it('resends only while the 24 h window is provable from server time', async () => {
    const f = fixture();
    f.respond(uncertain);
    const controller = f.controller();
    await controller.submit({ value: 10 });
    f.server.now = 1_000_000 + replayWindowMs - 1;
    f.respond(async () => 'replayed');
    expect((await controller.verifyAndRetry()).kind).toBe('confirmed');

    const late = fixture();
    late.respond(uncertain);
    const lateController = late.controller();
    await lateController.submit({ value: 10 });
    late.server.now = 1_000_000 + 23 * hour;
    expect((await lateController.verifyAndRetry()).kind).toBe('reconcile');
    expect(late.keys).toHaveLength(1);

    const unknown = fixture();
    unknown.respond(uncertain);
    const unknownController = unknown.controller();
    await unknownController.submit({ value: 10 });
    unknown.server.now = null;
    expect((await unknownController.verifyAndRetry()).kind).toBe('reconcile');
  });

  it('sends and stores nothing while writes are paused', async () => {
    const f = fixture({ writesPaused: () => true });
    expect(await f.controller().submit({ value: 1 })).toEqual({ kind: 'paused' });
    expect(f.keys).toEqual([]);
    expect(f.storage.keys()).toEqual([]);
  });

  it('reports unavailable device storage instead of claiming durability', async () => {
    const f = fixture();
    f.storage.failWrites = true;
    f.respond(uncertain);
    const controller = f.controller();
    await controller.submit({ value: 1 });
    expect(controller.durable).toBe(false);
    expect(controller.current()?.status).toBe('uncertain');
  });

  it('refuses to persist a credential-like payload', async () => {
    const f = fixture();
    await expect(f.controller().submit({ value: 1, password: 'x' } as never)).rejects.toThrow(
      /credential/,
    );
    expect(f.keys).toEqual([]);
  });
});

describe('scope binding and cancellation', () => {
  it('sends with the scope-bound client and an abort signal of its own', async () => {
    const f = fixture();
    await f.controller().submit({ value: 1 });
    expect(f.options[0]?.client).toBe(f.client);
    expect(f.options[0]?.signal).toBeInstanceOf(AbortSignal);
  });

  it('a scope that ends while the server time is awaited sends nothing and stores nothing', async () => {
    let release!: () => void;
    const f = fixture({ refreshServerTime: () => new Promise<void>((done) => (release = done)) });
    f.server.now = null;
    const controller = f.controller();
    const pending = controller.submit({ value: 1 });
    controller.dispose();
    release();
    expect(await pending).toEqual({ kind: 'cancelled' });
    expect(f.keys).toEqual([]);
    expect(f.storage.keys()).toEqual([]);
    expect(await controller.submit({ value: 1 })).toEqual({ kind: 'cancelled' });
  });

  it('a session that pauses while a verification awaits server time does not resend', async () => {
    let paused = false;
    let release!: () => void;
    const f = fixture({
      writesPaused: () => paused,
      refreshServerTime: () => new Promise<void>((done) => (release = done)),
    });
    f.respond(uncertain);
    const controller = f.controller();
    await controller.submit({ value: 1 });
    const verify = controller.verifyAndRetry();
    paused = true;
    release();
    expect(await verify).toEqual({ kind: 'paused' });
    expect(f.keys).toHaveLength(1);
  });

  it('disposal aborts a request already in flight; its record stays uncertain in its partition', async () => {
    const f = fixture();
    f.respond(
      () =>
        new Promise<string>((_, reject) =>
          f.options[0]?.signal?.addEventListener('abort', () =>
            reject(
              new ApiError({
                kind: 'aborted',
                method: 'POST',
                path: '/transaction',
                dispatched: true,
              }),
            ),
          ),
        ),
    );
    const controller = f.controller();
    const pending = controller.submit({ value: 1 });
    await new Promise((resolve) => setTimeout(resolve, 0));
    controller.dispose();
    expect(await pending).toMatchObject({ kind: 'failed', failure: { kind: 'uncertain' } });
    expect(f.controller().current()).toMatchObject({ status: 'uncertain', key: f.keys[0] });
  });
});

describe('unreadable device storage', () => {
  it('never mints a new key while an unresolved record cannot be read', async () => {
    const f = fixture();
    f.respond(uncertain);
    await f.controller().submit({ value: 10 });
    const original = f.keys[0];
    f.storage.failReads = true;
    const blind = f.controller();
    expect(blind.storageProblem()).toBe('unavailable');
    expect(await blind.submit({ value: 10 })).toEqual({
      kind: 'storage-unreadable',
      storage: 'unavailable',
    });
    expect(await blind.verifyAndRetry()).toEqual({
      kind: 'storage-unreadable',
      storage: 'unavailable',
    });
    expect(f.keys).toHaveLength(1);

    f.storage.failReads = false;
    blind.retryStorage();
    f.respond(async () => 'replayed');
    expect(await blind.verifyAndRetry()).toEqual({ kind: 'confirmed', result: 'replayed' });
    expect(f.keys[1]).toBe(original);
  });

  it('a corrupt record blocks until deliberately discarded', async () => {
    const f = fixture();
    f.storage.setItem('loto:op:v1:hash-ana:b1:transaction-create', '{broken');
    const controller = f.controller();
    expect(await controller.submit({ value: 1 })).toEqual({
      kind: 'storage-unreadable',
      storage: 'corrupt',
    });
    controller.discardCorrupt();
    expect(controller.storageProblem()).toBeNull();
    expect((await controller.submit({ value: 1 })).kind).toBe('confirmed');
  });

  it('continuing without a device record is explicit and reported as not durable', async () => {
    const f = fixture();
    f.storage.failReads = true;
    f.storage.failWrites = true;
    const controller = f.controller();
    expect((await controller.submit({ value: 1 })).kind).toBe('storage-unreadable');
    controller.continueWithoutDeviceRecord();
    f.respond(uncertain);
    expect((await controller.submit({ value: 1 })).kind).toBe('failed');
    expect(controller.durable).toBe(false);
    expect(controller.current()?.status).toBe('uncertain');
  });
});

describe('stored record validation (review round 2)', () => {
  const key = 'loto:op:v1:hash-ana:b1:transaction-create';
  const complete = {
    v: 1,
    operation: 'POST /transaction',
    key: '6c1f7f0e-3a0b-4c1e-9d55-0f2d1c4b8a11',
    payloadJson: '{"value":10}',
    status: 'uncertain',
    firstDispatchAfterMs: 1_000_000,
    dispatches: 1,
    everUncertain: true,
    messages: [],
  };

  it('quarantines a parseable record without its recovery metadata instead of resending it', async () => {
    const f = fixture();
    // The reviewer's probe: 30 hours old, `status` and `everUncertain` missing.
    const { status: _status, everUncertain: _uncertain, ...incomplete } = complete;
    void _status;
    void _uncertain;
    f.storage.setItem(key, JSON.stringify(incomplete));
    f.server.now = 1_000_000 + 30 * hour;
    const controller = f.controller();
    expect(controller.storageProblem()).toBe('corrupt');
    expect(await controller.verifyAndRetry()).toEqual({
      kind: 'storage-unreadable',
      storage: 'corrupt',
    });
    expect(await controller.submit({ value: 10 })).toEqual({
      kind: 'storage-unreadable',
      storage: 'corrupt',
    });
    expect(f.keys).toEqual([]);
  });

  it('rejects every malformed or inconsistent variant and accepts the complete record', () => {
    const variants: Record<string, unknown>[] = [
      { ...complete, status: 'done' },
      { ...complete, dispatches: 0 },
      { ...complete, dispatches: -1 },
      { ...complete, dispatches: 1.5 },
      { ...complete, everUncertain: false },
      { ...complete, everUncertain: 'yes' },
      { ...complete, firstDispatchAfterMs: '1000' },
      { ...complete, key: '' },
      { ...complete, key: 'chave com espaço' },
      { ...complete, payloadJson: '{broken' },
      { ...complete, messages: [1] },
      { ...complete, operation: '' },
      { ...complete, v: 2 },
    ];
    for (const variant of variants) {
      const f = fixture();
      f.storage.setItem(key, JSON.stringify(variant));
      expect(f.controller().storageProblem(), JSON.stringify(variant)).toBe('corrupt');
    }
    const f = fixture();
    f.storage.setItem(key, JSON.stringify(complete));
    const controller = f.controller();
    expect(controller.storageProblem()).toBeNull();
    expect(controller.current()).toMatchObject({ status: 'uncertain', key: complete.key });
  });

  it('a record frozen for another endpoint is quarantined, never resent through this one', async () => {
    const f = fixture();
    f.storage.setItem(
      key,
      JSON.stringify({ ...complete, operation: 'POST /transaction/fiado-payment' }),
    );
    const controller = f.controller();
    expect(controller.storageProblem()).toBe('corrupt');
    expect(await controller.verifyAndRetry()).toEqual({
      kind: 'storage-unreadable',
      storage: 'corrupt',
    });
    expect(f.keys).toEqual([]);
    // Recovery stays deliberate: after the discard, a new intent starts cleanly.
    controller.discardCorrupt();
    expect(controller.storageProblem()).toBeNull();
    expect((await controller.submit({ value: 10 })).kind).toBe('confirmed');
  });

  it('a complete record older than the window is reconciled, never resent', async () => {
    const f = fixture();
    f.storage.setItem(key, JSON.stringify(complete));
    f.server.now = 1_000_000 + 30 * hour;
    expect((await f.controller().verifyAndRetry()).kind).toBe('reconcile');
    expect(f.keys).toEqual([]);
  });
});
