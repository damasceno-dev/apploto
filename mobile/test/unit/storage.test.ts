import { ScopedDeviceStore, DeviceDraftStore } from '../../src/storage/scoped';
import { assertDeviceSafe } from '../../src/storage/privacy';
import { NavigationIntentStore, safeDestination } from '../../src/data/navigation';
import { branchId, otherBranch, rig } from './support';
import { MemoryDevice } from './device';
const stringInput = (value: unknown): value is { notes: string } =>
  typeof value === 'object' &&
  value !== null &&
  typeof (value as { notes?: unknown }).notes === 'string';
it.each([
  { password: 'x' },
  { nested: { refreshToken: 'x' } },
  { url: '/invitation#token=secret' },
  { base: { version: 123 } },
  { value: Infinity },
])('refuses credential/version-like or non-JSON draft payloads', (value) => {
  expect(() => assertDeviceSafe(value)).toThrow();
});
it('device cleanup exposes counts only and explicitly removes quarantined inputs across owners, preserving credentials', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const scope = await store.scope();
  await store.write('draft', scope, 'form', { notes: 'private' });
  device.values.set('loto.operation.v1:other:email:branch:form', '{unreadable unresolved record');
  device.values.set('loto.navigation.intent.v1', '{}');
  device.values.set('loto.install.v1', '1');
  device.values.set('unrelated', 'keep');
  const review = await store.inspectRetainedData();
  expect(review).toEqual({ drafts: 1, operations: 1, intents: 1 });
  expect(JSON.stringify(review)).not.toContain('private');
  expect(await store.discardRetainedData(review, false)).toBe(false);
  expect(await store.discardRetainedData(review, true)).toBe(true);
  expect([...device.values.keys()]).toEqual(['loto.install.v1', 'unrelated']);
  expect(await store.discardRetainedData(review, true)).toBe(false);
});
it('cleanup refuses stale review and active financial work, and surfaces removal failures', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const scope = await store.scope();
  await store.write('draft', scope, 'form', { notes: 'first' });
  const old = await store.inspectRetainedData();
  await store.write('draft', scope, 'form', { notes: 'new edit' });
  await expect(store.discardRetainedData(old, true)).rejects.toThrow('mudaram');
  const review = await store.inspectRetainedData();
  const release = store.claimOperation('financial')!;
  await expect(store.discardRetainedData(review, true)).rejects.toThrow('andamento');
  release();
  jest.spyOn(device, 'removeItem').mockRejectedValueOnce(new Error('unavailable'));
  await expect(store.discardRetainedData(review, true)).rejects.toThrow('unavailable');
  expect(device.values.size).toBe(1);
});
it('anonymous navigation expires in memory, never survives restart, and rejects old null-time persisted records', async () => {
  const r = rig();
  const device = new MemoryDevice();
  let now = 0;
  const nav = new NavigationIntentStore(device, r.engine, () => now);
  await nav.hold('/transacoes');
  expect(device.values.size).toBe(0);
  await r.ready();
  expect(
    await new NavigationIntentStore(device, r.engine, () => now).resolve(async () => true),
  ).toBeNull();
  now = 1800000;
  expect(await nav.resolve(async () => true)).toBeNull();
  device.values.set(
    'loto.navigation.intent.v1',
    JSON.stringify({
      v: 1,
      userId: null,
      branchId: null,
      destination: '/transacoes',
      origin: [],
      serverCapturedAt: null,
    }),
  );
  expect(await nav.resolve(async () => true)).toBeNull();
  expect(device.values.size).toBe(0);
});
it('acknowledges only the saved draft snapshot and requires fresh resource access before restore', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  let revision = 0;
  const drafts = new DeviceDraftStore(store, () => `revision-${++revision}`);
  const scope = await store.scope();
  const first = await drafts.save(scope, 'close:one', { notes: 'primeiro' }, { notes: 'servidor' });
  await drafts.save(scope, 'close:one', { notes: 'novo' }, { notes: 'servidor' });
  await drafts.acknowledge(scope, 'close:one', first.revision);
  const denied = jest.fn(async () => false);
  await expect(drafts.restore(scope, 'close:one', stringInput, denied)).rejects.toThrow();
  const found = await drafts.restore(scope, 'close:one', stringInput, async () => true);
  expect(found).toMatchObject({
    status: 'found',
    value: { input: { notes: 'novo' }, base: { notes: 'servidor' } },
  });
});
it('keeps branch partitions separate and fences writes from the switched-away scope', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const drafts = new DeviceDraftStore(store, () => 'revision');
  const a = await store.scope();
  await drafts.save(a, 'form', { notes: 'Alfa' }, {});
  await r.engine.selectBranch(otherBranch);
  const b = await store.scope();
  expect(await drafts.restore(b, 'form', stringInput, async () => true)).toEqual({
    status: 'none',
  });
  await expect(drafts.save(a, 'form', { notes: 'late' }, {})).rejects.toThrow();
  await r.engine.selectBranch(branchId);
  const returned = await store.scope();
  expect(await drafts.restore(returned, 'form', stringInput, async () => true)).toMatchObject({
    status: 'found',
    value: { input: { notes: 'Alfa' } },
  });
});
it('retains non-secret local edits while connectivity is down without authorizing financial writes', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const scope = await store.scope();
  await r.engine.connectivity(false);
  await new DeviceDraftStore(store, () => 'r').save(scope, 'form', { notes: 'sem rede' }, {});
  expect(r.engine.getSnapshot().writesPaused).toBe(true);
  expect(device.values.size).toBe(1);
});
it('reports unreadable/corrupt/foreign records rather than adopting their contents', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const store = new ScopedDeviceStore(device, r.engine, async () => 'email-hash');
  const scope = await store.scope();
  const key = store.key('draft', scope, 'form');
  device.values.set(key, '{broken');
  expect(await store.read('draft', scope, 'form', stringInput)).toEqual({ status: 'corrupt' });
  device.values.set(
    key,
    JSON.stringify({
      v: 1,
      userId: 'foreign',
      emailKey: scope.emailKey,
      branchId: scope.branchId,
      resource: 'form',
      value: { notes: 'private' },
    }),
  );
  expect(await store.read('draft', scope, 'form', stringInput)).toEqual({ status: 'corrupt' });
  device.fail = true;
  await expect(store.read('draft', scope, 'form', stringInput)).rejects.toThrow('unavailable');
});
it.each([
  'https://evil.invalid/path',
  '//evil.invalid/path',
  '/invitation#token=secret',
  '/reset-password',
  '/conta/reset/secret',
  '/transacoes/eyJabcdef.abc.abc',
  '/conta/token/secret',
  '/transacoes?token=x',
  '/transacoes?focus=eyJabcdef.abc.abc',
  '/transacoes%0aevil',
  '/api/user',
  '/transacoes?focus=x#secret',
])('rejects unsafe or capability navigation destinations: %s', (path) => {
  expect(safeDestination(path)).toBeNull();
});
it('holds a validated link through the gate and checks every return origin before resuming', async () => {
  const r = rig();
  const device = new MemoryDevice();
  const nav = new NavigationIntentStore(device, r.engine);
  await nav.hold('/transacoes?date=2026-10-27', branchId, ['/turno/hoje']);
  expect(await nav.resolve(async () => true)).toBeNull();
  await r.ready();
  const checks = jest.fn(async () => true);
  expect(await nav.resolve(checks)).toMatchObject({
    branchId,
    destination: '/transacoes?date=2026-10-27',
    origin: ['/turno/hoje'],
  });
  expect(checks.mock.calls).toEqual([['/turno/hoje'], ['/transacoes?date=2026-10-27']]);
  await r.engine.selectBranch(otherBranch);
  expect(await nav.resolve(async () => true)).toBeNull();
});
