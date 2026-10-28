import { FinancialOperation, isOperationRecord } from '../../src/data/operations';
import { ScopedDeviceStore } from '../../src/storage/scoped';
import { GuardedResource } from '../../src/data/guarded';
import { putTransactionTransactionId } from '@loto/api';
import { MemoryDevice } from './device';
import { branchId, otherBranch, rig, response, deferred } from './support';
const key = '40000000-0000-4000-8000-000000000001';
const payload = {
  date: '2026-10-27',
  value: 12.34,
  accountId: branchId,
  transactionTypeId: branchId,
};
async function operationRig() {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const scope = await store.scope();
  let keys = 0;
  const make = () =>
    new FinancialOperation(r.engine, store, scope, 'create:fixture', () =>
      keys++ === 0 ? key : '40000000-0000-4000-8000-000000000002',
    );
  return { ...r, device, store, scope, make };
}
it('persists the exact payload and secure key before dispatch; restart replays one durable result', async () => {
  const r = await operationRig();
  let durableResults = 0;
  const server = new Map<string, unknown>();
  let lose = true;
  r.intercept(async (path, init) => {
    if (path !== '/transaction') return undefined;
    const id = new Headers(init.headers).get('idempotency-key')!;
    const stored = JSON.parse([...r.device.values.values()][0]!);
    expect(stored.value).toMatchObject({
      key: id,
      payloadJson: JSON.stringify(payload),
      status: 'pending',
    });
    if (!server.has(id)) {
      server.set(id, { id: 'durable-one' });
      durableResults++;
    }
    if (lose) {
      lose = false;
      throw new TypeError('response lost after commit');
    }
    return response(server.get(id), 201);
  });
  expect(await r.make().submit({ operation: 'transaction', payload })).toMatchObject({
    kind: 'failed',
    failure: { kind: 'uncertain' },
  });
  const result = await r.make().verifyAndRetry();
  expect(result.kind).toBe('confirmed');
  expect(durableResults).toBe(1);
  expect(r.calls.filter((c) => c.path === '/transaction').map((c) => c.body)).toEqual([
    payload,
    payload,
  ]);
});
it.each([401, 403, 429, 409])(
  'changed input after definite %i refusal gets a fresh key',
  async (status) => {
    const r = await operationRig();
    let first = true;
    r.intercept(async (path) => {
      if (path !== '/transaction') return undefined;
      if (first) {
        first = false;
        return response({ errorMessages: ['Recusado.'] }, status);
      }
      return undefined;
    });
    await r.make().submit({ operation: 'transaction', payload });
    await r.engine.refresh(); // 403 pauses writes until fresh role/clock verification.
    expect(
      await r.make().submit({ operation: 'transaction', payload: { ...payload, value: 25 } }),
    ).toMatchObject({ kind: 'confirmed' });
    const calls = r.calls.filter((c) => c.path === '/transaction');
    expect(calls).toHaveLength(2);
    expect(calls[0]!.headers['idempotency-key']).not.toBe(calls[1]!.headers['idempotency-key']);
  },
);
it('uncertainty followed by an auth refusal still requires reconciliation before changed input', async () => {
  const r = await operationRig();
  let first = true;
  r.intercept(async (path) => {
    if (path !== '/transaction') return undefined;
    if (first) {
      first = false;
      throw new TypeError('lost');
    }
    return response({ errorMessages: ['Recusado.'] }, 403);
  });
  await r.make().submit({ operation: 'transaction', payload });
  await r.make().verifyAndRetry();
  await r.engine.refresh();
  expect(
    await r.make().submit({ operation: 'transaction', payload: { ...payload, value: 25 } }),
  ).toEqual({ kind: 'unresolved' });
  expect(await r.make().discardAfterReconciliation(async () => 'unresolved', true)).toBe(
    'unresolved',
  );
  expect(await r.make().discardAfterReconciliation(async () => 'confirmed', false)).toBe(
    'unresolved',
  );
  expect(await r.make().discardAfterReconciliation(async () => 'confirmed', true)).toBe(
    'discarded',
  );
  expect(r.device.values.size).toBe(0);
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(2);
});
it('expired unknown intent can be checked then discarded without replay, and changed input can start', async () => {
  const r = await operationRig();
  r.intercept(async (path) => {
    if (path === '/transaction') throw new TypeError('lost');
    return undefined;
  });
  await r.make().submit({ operation: 'transaction', payload });
  const [storageKey, raw] = [...r.device.values.entries()][0]!;
  const record = JSON.parse(raw);
  record.value.firstServerMs -= 24 * 3600000;
  r.device.values.set(storageKey, JSON.stringify(record));
  expect(await r.make().verifyAndRetry()).toEqual({ kind: 'reconcile' });
  expect(
    await r.make().discardAfterReconciliation(async (saved) => {
      expect(saved.key).toBe(key);
      return 'not-executed';
    }, true),
  ).toBe('discarded');
  r.intercept(null);
  expect(
    await r.make().submit({ operation: 'transaction', payload: { ...payload, value: 25 } }),
  ).toMatchObject({ kind: 'confirmed' });
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(2);
});
it('reconciliation cannot clear a switched scope or overlap a financial dispatch', async () => {
  const r = await operationRig();
  r.intercept(async (path) => {
    if (path === '/transaction') throw new TypeError('lost');
    return undefined;
  });
  await r.make().submit({ operation: 'transaction', payload });
  const check = deferred<'confirmed'>();
  const checking = r.make().discardAfterReconciliation(() => check.promise, true);
  while (r.calls.filter((c) => c.path === '/branch/current').length < 3) await Promise.resolve();
  expect(await r.make().verifyAndRetry()).toEqual({ kind: 'in-flight' });
  await r.engine.selectBranch(otherBranch);
  check.resolve('confirmed');
  await expect(checking).rejects.toThrow();
  expect(r.device.values.size).toBe(1);
});
it('confirmed success with failed device acknowledgement can be discarded after checking without resending', async () => {
  const r = await operationRig();
  jest.spyOn(r.device, 'removeItem').mockRejectedValueOnce(new Error('unavailable'));
  expect(await r.make().submit({ operation: 'transaction', payload })).toMatchObject({
    kind: 'confirmed',
    deviceRecordCleared: false,
  });
  expect(await r.make().discardAfterReconciliation(async () => 'confirmed', true)).toBe(
    'discarded',
  );
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(1);
});
it('does not dispatch when durable storage fails', async () => {
  const r = await operationRig();
  r.device.fail = true;
  await expect(r.make().submit({ operation: 'transaction', payload })).rejects.toThrow();
  expect(r.calls.some((c) => c.path === '/transaction')).toBe(false);
});
it('blocks a changed candidate while an earlier result is unknown', async () => {
  const r = await operationRig();
  r.intercept(async (path) => {
    if (path === '/transaction') throw new TypeError('lost');
    return undefined;
  });
  await r.make().submit({ operation: 'transaction', payload });
  expect(
    await r.make().submit({ operation: 'transaction', payload: { ...payload, value: 99 } }),
  ).toEqual({ kind: 'unresolved' });
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(1);
});
it('two controllers for the same logical resource cannot dispatch concurrently', async () => {
  const r = await operationRig();
  const gate = deferred<Response>();
  let started = false;
  r.intercept(async (path) => {
    if (path === '/transaction') {
      started = true;
      return gate.promise;
    }
    return undefined;
  });
  const first = r.make().submit({ operation: 'transaction', payload });
  while (!started) await Promise.resolve();
  expect(await r.make().submit({ operation: 'transaction', payload })).toEqual({
    kind: 'in-flight',
  });
  gate.resolve(response({ id: 'one' }, 201));
  await first;
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(1);
});
it('expired or reversed server-time replay windows require reconciliation', async () => {
  const r = await operationRig();
  r.intercept(async (path) => {
    if (path === '/transaction') throw new TypeError('lost');
    return undefined;
  });
  await r.make().submit({ operation: 'transaction', payload });
  const [storageKey, raw] = [...r.device.values.entries()][0]!;
  const record = JSON.parse(raw);
  record.value.firstServerMs -= 24 * 3600000;
  r.device.values.set(storageKey, JSON.stringify(record));
  expect(await r.make().verifyAndRetry()).toEqual({ kind: 'reconcile' });
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(1);
});
it('invalid retained bodies and incomplete operation records cannot be replayed', () => {
  expect(
    isOperationRecord({
      operation: 'transaction',
      key,
      payloadJson: '{}',
      status: 'pending',
      dispatches: 1,
      everUncertain: false,
      firstServerMs: null,
    }),
  ).toBe(false);
  expect(
    isOperationRecord({
      operation: 'transaction',
      key,
      payloadJson: JSON.stringify(payload),
      status: 'uncertain',
      dispatches: 1,
      everUncertain: false,
      firstServerMs: null,
    }),
  ).toBe(false);
});
it('keeps 429 header and backend messages verbatim without automatic retry', async () => {
  const r = await operationRig();
  r.intercept(async (path) =>
    path === '/transaction'
      ? response({ errorMessages: ['Limite.', 'Limite.'] }, 429, { 'retry-after': '60' })
      : undefined,
  );
  expect(await r.make().submit({ operation: 'transaction', payload })).toMatchObject({
    kind: 'failed',
    failure: { kind: 'rate-limited', messages: ['Limite.', 'Limite.'], retryAfter: '60' },
  });
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(1);
});
it('guarded conflicts and uncertain non-idempotent writes never acquire deduplication or auto-retry', async () => {
  const r = await operationRig();
  r.intercept(async (path, init) =>
    path.startsWith('/transaction/') && init.method === 'PUT'
      ? response({ errorMessages: ['Versão alterada.'] }, 409)
      : undefined,
  );
  const guarded = new GuardedResource(r.engine);
  guarded.acceptFresh(new Headers({ etag: '"123"' }), 123);
  let sends = 0;
  const result = await guarded.execute(async (etag, options) => {
    sends++;
    expect(etag).toBe('"123"');
    return putTransactionTransactionId(
      branchId,
      { dueDate: '2026-10-27' },
      { 'If-Match': etag },
      options,
    );
  });
  expect(result).toMatchObject({
    kind: 'failed',
    failure: { kind: 'conflict', messages: ['Versão alterada.'] },
  });
  expect(
    await guarded.execute(async () => {
      sends++;
      return null;
    }),
  ).toEqual({ kind: 'paused' });
  expect(r.calls.find((c) => c.method === 'PUT')?.headers['if-match']).toBe('"123"');
  expect(sends).toBe(1);
});
it('a device acknowledgement failure preserves known server success and blocks a changed create', async () => {
  const r = await operationRig();
  jest.spyOn(r.device, 'removeItem').mockRejectedValueOnce(new Error('device removal failed'));
  expect(await r.make().submit({ operation: 'transaction', payload })).toMatchObject({
    kind: 'confirmed',
    deviceRecordCleared: false,
  });
  expect(
    await r.make().submit({ operation: 'transaction', payload: { ...payload, value: 99 } }),
  ).toEqual({ kind: 'unresolved' });
  expect(r.calls.filter((call) => call.path === '/transaction')).toHaveLength(1);
  expect(await r.make().verifyAndRetry()).toMatchObject({
    kind: 'confirmed',
    deviceRecordCleared: true,
  });
  const sends = r.calls.filter((call) => call.path === '/transaction');
  expect(sends.map((call) => call.headers['idempotency-key'])).toEqual([key, key]);
});
