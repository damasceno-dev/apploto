import {
  destinations,
  canEnter,
  defaultMode,
  destinationMode,
  matchDestination,
  menuDestinations,
  fallbackPath,
} from '../../src/shell/policy';
import {
  ShellNavigationStore,
  validateShellNavigation,
  navigationAddress,
} from '../../src/shell/navigation';
import { MemoryDevice } from './device';
import { ScopedDeviceStore, DeviceDraftStore } from '../../src/storage/scoped';
import { rig, branchId, otherBranch, profile } from './support';

it.each(['Member', 'Manager', 'Admin'] as const)(
  '%s has an exhaustive route/mode matrix',
  (role) => {
    for (const destination of destinations) {
      const sample = destination.path
        .replace(':id', branchId)
        .replace(':year', '2026')
        .replace(':month', '10');
      expect(matchDestination(sample)?.key).toBe(destination.key);
      expect(canEnter(destination, role)).toBe(
        !(destination.owner === 'management' && role === 'Member') &&
          !(destination.memberOnly && role !== 'Member'),
      );
      const mode = destinationMode(destination, role);
      expect(['shift', 'management']).toContain(mode);
      if (destination.owner === 'shared') expect(mode).toBe(defaultMode(role));
      expect(matchDestination(fallbackPath(destination, mode))).not.toBeNull();
    }
  },
);
it('keeps nine reports distinct, Ponto Member-only and shared counter work in both modes', () => {
  expect(destinations.filter((d) => d.group === 'Relatórios')).toHaveLength(9);
  expect(new Set(destinations.map((d) => d.path)).size).toBe(destinations.length);
  expect(menuDestinations('Member', 'shift').some((d) => d.owner === 'management')).toBe(false);
  expect(menuDestinations('Manager', 'shift').map((d) => d.key)).not.toContain('clock');
  expect(menuDestinations('Member', 'shift').map((d) => d.key)).toEqual(
    expect.arrayContaining(['clients', 'create', 'transactions', 'today']),
  );
  for (const key of ['open', 'close', 'correction'])
    expect(
      canEnter(
        destinations.find((d) => d.key === key)!,
        'Manager',
      ),
    ).toBe(true);
});
it.each([
  '/gestao/fila/foreign',
  '/transacoes/nope',
  '/relatorios/financeiro-mensal/0000/10',
  '/relatorios/financeiro-mensal/2026/13',
  '//gestao/fila',
  '/f/' + branchId + '/turno/hoje',
])('rejects invalid route %s', (path) => expect(matchDestination(path)).toBeNull());
const candidate = {
  v: 1 as const,
  path: '/transacoes',
  mode: 'shift' as const,
  origins: ['/turno/hoje'],
};
it('requires current role, link and resource checks for each restored hop', async () => {
  expect(await validateShellNavigation(candidate, 'Manager', true)).toEqual(candidate);
  expect(await validateShellNavigation(candidate, 'Manager', false)).toBeNull();
  expect(
    await validateShellNavigation({ ...candidate, mode: 'management' }, 'Member', true),
  ).toBeNull();
  const resource = { ...candidate, origins: [`/fechamentos/${branchId}`] };
  expect(await validateShellNavigation(resource, 'Member', true)).toBeNull();
  const validate = jest.fn(async () => true);
  expect(await validateShellNavigation(resource, 'Member', true, validate)).not.toBeNull();
  expect(validate).toHaveBeenCalledWith(`/fechamentos/${branchId}`);
  for (const path of [
    '/conta/senha',
    '/invitation?token=secret',
    '/transacoes?origin=/gestao/fila',
    'https://elsewhere.invalid/transacoes',
  ])
    expect(await validateShellNavigation({ ...candidate, path }, 'Member', true)).toBeNull();
  expect(await validateShellNavigation({ ...candidate, etag: '"123"' }, 'Member', true)).toBeNull();
  expect(
    await validateShellNavigation({ ...candidate, origins: ['/gestao/fila'] }, 'Member', true),
  ).toBeNull();
});
it('isolates durable non-secret navigation by verified UserId, canonical email and branch', async () => {
  const device = new MemoryDevice();
  const store = new ShellNavigationStore(device, async (value) => 'hash:' + value);
  await store.write(profile, branchId, candidate);
  expect(await store.read(profile, branchId)).toEqual(candidate);
  expect(await store.read({ ...profile, userId: otherBranch }, branchId)).toBeNull();
  expect(await store.read({ ...profile, email: 'other@example.invalid' }, branchId)).toBeNull();
  expect(await store.read(profile, otherBranch)).toBeNull();
  expect([...device.values.keys()][0]).toContain(profile.userId);
  await store.clear(profile, branchId);
  expect(await store.read(profile, branchId)).toBeNull();
});
it('retains a blocker round-trip candidate without replaying a count, ETag or financial write', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const scoped = new ScopedDeviceStore(device, r.engine, async () => 'canonical-email-hash');
  const drafts = new DeviceDraftStore(scoped, () => 'revision');
  const scope = await scoped.scope();
  const input = { count: '', notes: 'Entrada retida', businessDate: '2026-10-27' };
  await drafts.save(scope, 'count:' + branchId, input, { count: '0' });
  const journey = {
    v: 1 as const,
    path: '/fechamentos/abrir',
    mode: 'shift' as const,
    origins: [`/fechamentos/${branchId}`],
  };
  const resource = jest.fn(async () => true);
  expect(await validateShellNavigation(journey, 'Member', true, resource)).toEqual(journey);
  await r.engine.selectBranch(otherBranch);
  expect(
    (
      await drafts.restore(
        await scoped.scope(),
        'count:' + branchId,
        (v): v is typeof input => !!v,
        async () => true,
      )
    ).status,
  ).toBe('none');
  await r.engine.selectBranch(branchId);
  const restored = await drafts.restore(
    await scoped.scope(),
    'count:' + branchId,
    (v): v is typeof input => !!v,
    async () => true,
  );
  expect(restored).toMatchObject({ status: 'found', value: { input } });
  expect(
    r.calls.some(
      (call) => call.method !== 'GET' && !['/user/login', '/branch/session'].includes(call.path),
    ),
  ).toBe(false);
  expect([...device.values.values()].join('')).not.toMatch(/etag|idempotency/i);
});
it('keeps navigation groups contiguous and the catalog shortcut order on compact layouts', () => {
  const groups = menuDestinations('Admin', 'management').map((d) => d.group);
  expect(groups.filter((group, index) => group !== groups[index - 1])).toEqual([
    'Gestão',
    'Relatórios',
    'Ponto',
    'Cadastros',
  ]);
  expect(
    menuDestinations('Member', 'shift')
      .slice(0, 4)
      .map((d) => d.key),
  ).toEqual(['today', 'create', 'transactions', 'clients']);
});
it('rejects secrets before writing navigation and keeps allowlisted filter context', async () => {
  const device = new MemoryDevice();
  const store = new ShellNavigationStore(device, async () => 'hash');
  await expect(
    store.write(profile, branchId, {
      v: 1,
      path: '/transacoes?token=private',
      mode: 'shift',
      origins: [],
    }),
  ).rejects.toThrow();
  expect(device.values.size).toBe(0);
  expect(
    navigationAddress('/transacoes', {
      status: 'Draft',
      page: '2',
      branchId,
      origin: '/gestao/fila',
      mode: 'management',
    }),
  ).toBe('/transacoes?status=Draft&page=2');
});
