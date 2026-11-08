import { FinancialOperation, isOperationRecord } from '../../src/data/operations';
import { ScopedDeviceStore } from '../../src/storage/scoped';
import { MemoryDevice } from './device';
import { rig, response } from './support';

const id = (n: number) => `81000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
async function operationRig() {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const scope = await store.scope();
  let keys = 0;
  const make = (resource = 'ledger/cheque/fixture') =>
    new FinancialOperation(
      r.engine,
      store,
      scope,
      resource,
      () => `40000000-0000-4000-8000-${String(++keys).padStart(12, '0')}`,
    );
  return { ...r, device, store, scope, make };
}
const returnPayload = {
  depositEventId: id(5),
  expectedVersion: 3,
  occurredOn: '2026-11-06',
  clientId: id(6),
  nextExpectedDepositDate: '2026-11-20',
  returnReason: '11',
};

it('a targeted cheque return persists its path id; the unknown outcome is verified with the same key, path and bytes', async () => {
  const r = await operationRig();
  const applied = new Map<string, unknown>();
  let lose = true;
  r.intercept(async (path, init) => {
    if (!path.startsWith('/cheque/')) return undefined;
    const key = new Headers(init.headers).get('idempotency-key')!;
    if (!applied.has(key)) applied.set(key, { receivables: [], bankReceipt: null });
    if (lose) {
      lose = false;
      throw new TypeError('connection reset after commit');
    }
    return response(applied.get(key));
  });
  const first = await r
    .make()
    .submit({ operation: 'cheque-return', target: id(1), payload: returnPayload });
  expect(first).toMatchObject({ kind: 'failed', failure: { kind: 'uncertain' } });
  // A fresh controller (screen remount / app restart) inspects without dispatching.
  const inspected = await r.make().inspect();
  expect(inspected).toMatchObject({
    status: 'found',
    unknown: true,
    record: { operation: 'cheque-return', target: id(1) },
  });
  expect(r.calls.filter((c) => c.path.startsWith('/cheque/'))).toHaveLength(1);
  // A different cheque or changed body while unknown is blocked, never sent.
  expect(
    await r.make().submit({ operation: 'cheque-return', target: id(2), payload: returnPayload }),
  ).toEqual({ kind: 'unresolved' });
  expect(
    await r.make().submit({
      operation: 'cheque-deposit',
      payload: {
        bankAccountId: id(9),
        occurredOn: '2026-11-06',
        items: [{ transactionId: id(1), expectedVersion: 3 }],
      },
    }),
  ).toEqual({
    kind: 'unresolved',
  });
  const retried = await r.make().verifyAndRetry();
  expect(retried.kind).toBe('confirmed');
  const sent = r.calls.filter((c) => c.path.startsWith('/cheque/'));
  expect(sent.map((c) => c.path)).toEqual([`/cheque/${id(1)}/return`, `/cheque/${id(1)}/return`]);
  expect(sent[0]!.headers['idempotency-key']).toBe(sent[1]!.headers['idempotency-key']);
  expect(sent[0]!.body).toEqual(sent[1]!.body);
  expect(applied.size).toBe(1);
  expect(await r.make().inspect()).toEqual({ status: 'none' });
});

it('targeted commands require a uuid target; untargeted ones reject a target in the stored record', async () => {
  const r = await operationRig();
  await expect(
    r.make().submit({
      operation: 'cheque-represent',
      target: 'not-an-id',
      payload: { expectedVersion: 1, occurredOn: '2026-11-06' },
    }),
  ).rejects.toThrow(RangeError);
  expect(r.calls.filter((c) => c.path.startsWith('/cheque/'))).toHaveLength(0);
  const base = {
    operation: 'safe-deposit',
    key: '40000000-0000-4000-8000-000000000009',
    payloadJson: JSON.stringify({
      date: '2026-11-06',
      value: 10,
      accountId: id(1),
      safeAccountId: id(2),
    }),
    status: 'uncertain',
    dispatches: 1,
    everUncertain: true,
    firstServerMs: 1,
  };
  expect(isOperationRecord(base)).toBe(true);
  expect(isOperationRecord({ ...base, target: id(3) })).toBe(false);
  expect(isOperationRecord({ ...base, operation: 'cheque-convert' })).toBe(false);
  // A pre-M6 stored record (no target field) stays valid for its untargeted operation.
  expect(
    isOperationRecord({
      ...base,
      operation: 'transaction',
      payloadJson: JSON.stringify({
        date: '2026-11-06',
        value: 1,
        accountId: id(1),
        transactionTypeId: id(2),
      }),
    }),
  ).toBe(true);
});

it('Depósito Dinheiro: a definite refusal releases the intent and a corrected request gets a fresh key', async () => {
  const r = await operationRig();
  let first = true;
  r.intercept(async (path) => {
    if (path !== '/transaction/safe-deposit') return undefined;
    if (first) {
      first = false;
      return response({ errorMessages: ['Cofre não encontrado.'] }, 404);
    }
    return response({ id: id(7), generatedTransactionId: id(8), custodyTransferId: id(9) }, 201);
  });
  const payload = { date: '2026-11-06', value: 50, accountId: id(1), safeAccountId: id(2) };
  const refused = await r
    .make('ledger/safe-deposit')
    .submit({ operation: 'safe-deposit', payload });
  expect(refused).toMatchObject({
    kind: 'failed',
    failure: { kind: 'rejected', messages: ['Cofre não encontrado.'] },
  });
  const corrected = await r
    .make('ledger/safe-deposit')
    .submit({ operation: 'safe-deposit', payload: { ...payload, safeAccountId: id(3) } });
  expect(corrected.kind).toBe('confirmed');
  const sent = r.calls.filter((c) => c.path === '/transaction/safe-deposit');
  expect(sent).toHaveLength(2);
  expect(sent[0]!.headers['idempotency-key']).not.toBe(sent[1]!.headers['idempotency-key']);
});
