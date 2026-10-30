import { getOperatorSelfContext, postTransaction } from '@loto/api';
import { LoginForm } from '../../src/shell/login';
import { NavigationIntentStore } from '../../src/data/navigation';
import { MemoryDevice } from './device';
import { rig, branchId, otherBranch, deferred, response, userId, branch } from './support';

it('serializes Login across scope replacement, clears password and preserves verbatim failures and advisory retry', async () => {
  const r = rig();
  const form = new LoginForm(r.engine);
  const wait = deferred<Response>();
  r.intercept(async (path) => (path === '/user/login' ? wait.promise : undefined));
  form.email(' typed@example.invalid ');
  form.password('private-fixture');
  const first = form.submit();
  const duplicate = form.submit();
  expect(form.get()).toMatchObject({ pending: true, password: '' });
  await Promise.resolve();
  await Promise.resolve();
  wait.resolve(response({ errorMessages: ['Aguarde.', 'Aguarde.'] }, 429, { 'retry-after': '7' }));
  await Promise.all([first, duplicate]);
  expect(r.calls.filter((c) => c.path === '/user/login')).toHaveLength(1);
  expect(form.get()).toMatchObject({
    email: ' typed@example.invalid ',
    pending: false,
    password: '',
    failure: { kind: 'rate-limited', retryAfter: '7', messages: ['Aguarde.', 'Aguarde.'] },
  });
  r.intercept(async () => undefined);
  form.password('next-fixture');
  await form.submit();
  expect(r.engine.getSnapshot().status).toBe('identity');
});
it('keeps uncertain Login manual with no automatic retry or existence/provider inference', async () => {
  const r = rig();
  const form = new LoginForm(r.engine);
  r.intercept(async (path) => {
    if (path === '/user/login') throw new TypeError('lost response');
    return undefined;
  });
  form.email('fixture@example.invalid');
  form.password('private');
  await form.submit();
  expect(form.get()).toMatchObject({
    pending: false,
    password: '',
    failure: { kind: 'uncertain' },
  });
  expect(r.calls.filter((c) => c.path === '/user/login')).toHaveLength(1);
});
it('rereads self-context at branch creation and foreground; changed grants cancel old clients without replay', async () => {
  const r = rig();
  await r.ready();
  const scope = r.engine.getSnapshot().scope;
  const old = r.engine.client();
  expect(r.calls.filter((c) => c.path === '/operator/self-context')).toHaveLength(1);
  await r.engine.foreground(false);
  r.intercept(async (path) =>
    path === '/operator/self-context'
      ? response({ operatorId: null, availableAccounts: [] })
      : undefined,
  );
  await r.engine.foreground(true);
  expect(r.engine.getSnapshot()).toMatchObject({
    selfStatus: 'ready',
    selfContext: { operatorId: null },
    writesPaused: false,
  });
  expect(r.engine.getSnapshot().scope).toBeGreaterThan(scope);
  await expect(getOperatorSelfContext({ client: old })).rejects.toMatchObject({
    kind: 'token-provider',
  });
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(0);
});
it('pauses financial dispatch throughout revalidation and keeps fetch failure distinct from missing link', async () => {
  const r = rig();
  await r.ready();
  const wait = deferred<Response>();
  r.intercept(async (path) => (path === '/operator/self-context' ? wait.promise : undefined));
  const pending = r.engine.recheckSelfContext();
  expect(r.engine.getSnapshot().writesPaused).toBe(true);
  // Exercise the generated client boundary before a protected request can dispatch.
  await expect(
    postTransaction(
      {} as never,
      { 'Idempotency-Key': 'fixture-key' },
      { client: r.engine.client() },
    ),
  ).rejects.toBeDefined();
  wait.resolve(response({ errorMessages: ['Falha atual.'] }, 503));
  await pending;
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'branch',
    selfStatus: 'failed',
    selfContext: null,
    selfMessages: ['Falha atual.'],
    writesPaused: true,
  });
});
it('detaches delayed old-branch self-context and clears an X intent after explicit Y', async () => {
  const r = rig();
  await r.ready();
  const wait = deferred<Response>();
  r.intercept(async (path) => (path === '/operator/self-context' ? wait.promise : undefined));
  const pending = r.engine.recheckSelfContext();
  r.intercept(async () => undefined);
  await r.engine.selectBranch(otherBranch);
  wait.resolve(response({ operatorId: branchId, availableAccounts: [] }));
  await pending;
  expect(r.engine.getSnapshot().selfContext?.operatorId).toBe(userId);
  const device = new MemoryDevice();
  const intents = new NavigationIntentStore(device, r.engine);
  await intents.hold('/transacoes', branchId);
  await intents.discardForeignBranch(otherBranch);
  expect(await intents.resolve(async () => true)).toBeNull();
});
it('cannot rebind the logged-out screen as an anonymous intent for the next person', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const navigation = new NavigationIntentStore(device, r.engine);
  await r.engine.logout();
  await navigation.hold('/turno/hoje');
  await r.login();
  await r.engine.selectBranch(branchId);
  expect(await navigation.resolve(async () => true)).toBeNull();
  await r.engine.logout();
  r.engine.incomingNavigation();
  await navigation.hold('/transacoes', branchId);
  await r.login();
  await r.engine.selectBranch(branchId);
  expect((await navigation.resolve(async () => true))?.userId).toBe(userId);
});
it('never captures the switched-away screen as an unscoped intent for another branch', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const nav = new NavigationIntentStore(device, r.engine);
  await r.engine.selectBranch(null);
  await nav.hold('/transacoes');
  await r.engine.selectBranch(otherBranch);
  expect(await nav.resolve(async () => true)).toBeNull();
});
it('permission refusals refresh live role and self-context without resending the refused write', async () => {
  const r = rig();
  await r.ready();
  const before = r.calls.filter((call) => call.path === '/operator/self-context').length;
  r.intercept(async (path) =>
    path === '/transaction'
      ? response({ errorMessages: ['Acesso atual recusado.'] }, 403)
      : undefined,
  );
  await expect(
    postTransaction(
      {} as never,
      { 'Idempotency-Key': 'fixture-refusal' },
      { client: r.engine.client() },
    ),
  ).rejects.toMatchObject({ errorMessages: ['Acesso atual recusado.'] });
  for (let turn = 0; turn < 30; turn++) await Promise.resolve();
  expect(r.calls.filter((call) => call.path === '/transaction')).toHaveLength(1);
  expect(r.calls.filter((call) => call.path === '/operator/self-context').length).toBeGreaterThan(
    before,
  );
});
it('counts shell/logout work only for the verified caller and keeps other owners isolated', async () => {
  const r = rig();
  await r.ready();
  const device = new MemoryDevice();
  const { ScopedDeviceStore } = jest.requireActual(
    '../../src/storage/scoped',
  ) as typeof import('../../src/storage/scoped');
  const store = new ScopedDeviceStore(device, r.engine, async () => 'hash');
  await device.setItem('loto.draft.v1:other-user:hash:' + branchId + ':notes', 'other-owner');
  expect(await store.inspectOwnWork()).toEqual({ drafts: 0, operations: 0 });
  await device.setItem(`loto.draft.v1:${userId}:hash:${branchId}:notes`, 'own-input');
  expect(await store.inspectOwnWork()).toEqual({ drafts: 1, operations: 0 });
  expect(await store.inspectRetainedData()).toMatchObject({ drafts: 2 });
  await r.engine.logout();
  expect(await store.inspectOwnWork()).toEqual({ drafts: 0, operations: 0 });
  expect(device.values.size).toBe(2);
});

it.each(['failure', 'recreation'])(
  'retains the verified link baseline across %s before detecting removal',
  async (boundary) => {
    const r = rig();
    r.role('Manager');
    await r.ready();
    const old = r.engine.client();
    const scope = r.engine.getSnapshot().scope;
    r.options.clearSelections.mockClear();
    if (boundary === 'failure') {
      r.intercept(async (path) =>
        path === '/operator/self-context'
          ? response({ errorMessages: ['Indisponível.'] }, 503)
          : undefined,
      );
      await r.engine.recheckSelfContext();
      expect(r.engine.getSnapshot()).toMatchObject({ selfStatus: 'failed', selfContext: null });
      expect(r.options.clearSelections).not.toHaveBeenCalled();
    }
    r.intercept(async (path) =>
      path === '/operator/self-context'
        ? response({ operatorId: null, availableAccounts: [] })
        : undefined,
    );
    if (boundary === 'recreation') await r.engine.start();
    else await r.engine.recheckSelfContext();
    expect(r.engine.getSnapshot()).toMatchObject({
      selfStatus: 'ready',
      accessChanged: true,
      selfContext: { operatorId: null },
    });
    expect(r.engine.getSnapshot().scope).toBe(scope + (boundary === 'recreation' ? 2 : 1));
    expect(r.options.clearSelections).toHaveBeenCalledWith(userId, branchId);
    await expect(getOperatorSelfContext({ client: old })).rejects.toMatchObject({
      kind: 'token-provider',
    });
  },
);

it('keeps clock timer and ordinary route verification free of self-context reads and pauses', async () => {
  const r = rig();
  await r.ready();
  const states: { selfStatus: string; writesPaused: boolean }[] = [];
  const stop = r.engine.subscribe(() => states.push(r.engine.getSnapshot()));
  r.advance(60000);
  await r.engine.refresh();
  r.advance(60000);
  await r.engine.refresh();
  await r.engine.verifyEntry();
  stop();
  expect(r.calls.filter((call) => call.path === '/operator/self-context')).toHaveLength(1);
  expect(states.length).toBeGreaterThan(0);
  expect(states.every((state) => state.selfStatus === 'ready' && !state.writesPaused)).toBe(true);
});

it('keeps an unchanged background context read paused but quiet until its outcome is known', async () => {
  const r = rig();
  await r.ready();
  const wait = deferred<Response>();
  r.intercept(async (path) => (path === '/operator/self-context' ? wait.promise : undefined));
  const scope = r.engine.getSnapshot().scope;
  const pending = r.engine.recheckSelfContext({ background: true });
  expect(r.engine.getSnapshot()).toMatchObject({
    selfStatus: 'ready',
    writesPaused: true,
    accessChanged: false,
  });
  wait.resolve(response({ operatorId: userId, availableAccounts: [], primaryAccount: null }));
  await pending;
  expect(r.engine.getSnapshot()).toMatchObject({
    scope,
    selfStatus: 'ready',
    writesPaused: false,
    accessChanged: false,
  });
});

it('clears the previous caller email on logout while preserving typed email after a rejected login', async () => {
  const r = rig();
  const form = new LoginForm(r.engine);
  form.email('previous@example.invalid');
  form.password('fixture-only');
  await form.submit();
  await r.engine.selectBranch(branchId);
  await r.engine.logout();
  expect(form.get()).toMatchObject({ email: '', password: '', failure: null });
  r.intercept(async (path) =>
    path === '/user/login' ? response({ errorMessages: ['Recusado.'] }, 401) : undefined,
  );
  form.email('next@example.invalid');
  form.password('fixture-only');
  await form.submit();
  expect(form.get()).toMatchObject({ email: 'next@example.invalid', password: '' });
});

it.each(['foreground', 'reconnect'])(
  'keeps %s writes paused but quiet until the self-context response is verified',
  async (event) => {
    const r = rig();
    r.role('Manager');
    await r.ready();
    if (event === 'foreground') await r.engine.foreground(false);
    else await r.engine.connectivity(false);
    const entered = deferred<void>();
    const wait = deferred<Response>();
    r.intercept(async (path) => {
      if (path === '/operator/self-context') {
        entered.resolve(undefined);
        return wait.promise;
      }
      return undefined;
    });
    const states: { selfStatus: string; writesPaused: boolean }[] = [];
    const stop = r.engine.subscribe(() => states.push(r.engine.getSnapshot()));
    const pending =
      event === 'foreground' ? r.engine.foreground(true) : r.engine.connectivity(true);
    await entered.promise;
    expect(r.engine.getSnapshot()).toMatchObject({
      selfStatus: 'ready',
      writesPaused: true,
      recovery: null,
    });
    expect(states.every((state) => state.selfStatus === 'ready' && state.writesPaused)).toBe(true);
    await expect(
      postTransaction(
        {} as never,
        { 'Idempotency-Key': 'paused-fixture' },
        { client: r.engine.client() },
      ),
    ).rejects.toBeDefined();
    expect(r.calls.filter((call) => call.path === '/transaction')).toHaveLength(0);
    wait.resolve(response({ operatorId: null, availableAccounts: [] }));
    await pending;
    stop();
    expect(r.engine.getSnapshot()).toMatchObject({
      selfStatus: 'ready',
      writesPaused: false,
      accessChanged: true,
    });
    expect(r.calls.filter((call) => call.path === '/transaction')).toHaveLength(0);
  },
);

it('blocks a write at transport when foreground verification starts after token acquisition', async () => {
  const r = rig();
  await r.ready();
  const entered = deferred<void>();
  const wait = deferred<Response>();
  r.intercept(async (path) => {
    if (path === '/operator/self-context') {
      entered.resolve(undefined);
      return wait.promise;
    }
    return undefined;
  });
  const write = postTransaction(
    {} as never,
    { 'Idempotency-Key': 'racing-fixture' },
    { client: r.engine.client() },
  ).then(
    () => 'sent',
    () => 'blocked',
  );
  const pending = r.engine.foreground(true);
  await entered.promise;
  expect(await write).toBe('blocked');
  expect(r.calls.filter((call) => call.path === '/transaction')).toHaveLength(0);
  wait.resolve(response({ operatorId: userId, availableAccounts: [], primaryAccount: null }));
  await pending;
  expect(r.engine.getSnapshot().writesPaused).toBe(false);
});

it('does not let a superseded scope release a newer scope verification hold', async () => {
  const r = rig();
  await r.ready();
  const oldWait = deferred<Response>();
  r.intercept(async (path) => (path === '/operator/self-context' ? oldWait.promise : undefined));
  const oldCheck = r.engine.recheckSelfContext({ background: true });
  r.intercept(async () => undefined);
  await r.engine.selectBranch(otherBranch);
  expect(r.engine.getSnapshot().writesPaused).toBe(false);
  const newWait = deferred<Response>();
  r.intercept(async (path) => (path === '/operator/self-context' ? newWait.promise : undefined));
  const newCheck = r.engine.recheckSelfContext({ background: true });
  oldWait.resolve(response({ operatorId: null, availableAccounts: [] }));
  await oldCheck;
  expect(r.engine.getSnapshot()).toMatchObject({ selfStatus: 'ready', writesPaused: true });
  newWait.resolve(response({ operatorId: userId, availableAccounts: [], primaryAccount: null }));
  await newCheck;
  expect(r.engine.getSnapshot().writesPaused).toBe(false);
});

it('finishes mandatory foreground self verification when a clock-only poll supersedes its clock read', async () => {
  const r = rig();
  await r.ready();
  const clockEntered = deferred<void>();
  const oldClock = deferred<Response>();
  const selfEntered = deferred<void>();
  const selfReply = deferred<Response>();
  let clocks = 0;
  r.intercept(async (path) => {
    if (path === '/branch/current' && ++clocks === 1) {
      clockEntered.resolve(undefined);
      return oldClock.promise;
    }
    if (path === '/operator/self-context') {
      selfEntered.resolve(undefined);
      return selfReply.promise;
    }
    return undefined;
  });
  const foreground = r.engine.foreground(true);
  await clockEntered.promise;
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({ selfStatus: 'ready', writesPaused: true });
  oldClock.resolve(
    response({ branch, branchLocalDate: '2026-10-27', branchLocalDateTime: '2026-10-27T09:00:00' }),
  );
  expect(
    await Promise.race([
      selfEntered.promise.then(() => 'checking'),
      foreground.then(() => 'finished'),
    ]),
  ).toBe('checking');
  expect(r.engine.getSnapshot()).toMatchObject({ selfStatus: 'ready', writesPaused: true });
  selfReply.resolve(response({ operatorId: null, availableAccounts: [] }));
  await foreground;
  expect(r.engine.getSnapshot()).toMatchObject({
    selfStatus: 'ready',
    writesPaused: false,
    accessChanged: true,
  });
});

it('keeps the existing failure visible during a quiet retry instead of publishing checking', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) =>
    path === '/operator/self-context'
      ? response({ errorMessages: ['Falha atual.'] }, 503)
      : undefined,
  );
  await r.engine.recheckSelfContext();
  const wait = deferred<Response>();
  r.intercept(async (path) => (path === '/operator/self-context' ? wait.promise : undefined));
  const pending = r.engine.recheckSelfContext({ background: true });
  expect(r.engine.getSnapshot()).toMatchObject({
    selfStatus: 'failed',
    selfMessages: ['Falha atual.'],
    writesPaused: true,
  });
  wait.resolve(response({ operatorId: userId, availableAccounts: [], primaryAccount: null }));
  await pending;
  expect(r.engine.getSnapshot()).toMatchObject({
    selfStatus: 'ready',
    selfMessages: [],
    writesPaused: false,
  });
});

it('finishes one branch opening when a same-scope clock poll wins its initial clock read', async () => {
  const r = rig();
  await r.login();
  const entered = deferred<void>();
  const firstClock = deferred<Response>();
  let clocks = 0;
  r.intercept(async (path) => {
    if (path === '/branch/current' && ++clocks === 1) {
      entered.resolve(undefined);
      return firstClock.promise;
    }
    return undefined;
  });
  const opened = r.engine.selectBranch(branchId).then(
    () => 'opened',
    () => 'failed',
  );
  await entered.promise;
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'branch', selfStatus: 'ready' });
  firstClock.resolve(
    response({ branch, branchLocalDate: '2026-10-27', branchLocalDateTime: '2026-10-27T09:00:00' }),
  );
  expect(await opened).toBe('opened');
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'branch',
    branch: { id: branchId },
    selfStatus: 'ready',
    writesPaused: false,
  });
  expect(r.calls.filter((call) => call.path === '/branch/session')).toHaveLength(1);
});
