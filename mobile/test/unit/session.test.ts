import { getBranchCurrent, getUserProfile, postTransaction, isApiError } from '@loto/api';
import { SessionEngine } from '../../src/session/engine';
import { branchId, otherBranch, rig, response, deferred, profile } from './support';

it('fails closed when secure credential reads fail', async () => {
  const r = rig();
  r.credentials.failRead = true;
  await r.engine.start();
  expect(r.engine.getSnapshot().status).toBe('storage-error');
  expect(r.calls).toHaveLength(0);
});
it('identity alone cannot fetch branch data; authoritative branch clock opens the branch gate', async () => {
  const r = rig();
  await r.login();
  expect(r.engine.getSnapshot().status).toBe('identity');
  await expect(getBranchCurrent({ client: r.engine.client() })).rejects.toMatchObject({
    kind: 'token-provider',
    dispatched: false,
  });
  expect(r.calls.some((c) => c.path === '/branch/current')).toBe(false);
  await r.engine.selectBranch(branchId);
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'branch',
    branch: { id: branchId, role: 'Admin' },
    clock: { branchLocalDate: '2026-10-27' },
    writesPaused: false,
  });
  expect(JSON.stringify(r.engine.getSnapshot())).not.toContain('refresh-1');
});
it('cold restore verifies stored identity and membership rather than trusting retained metadata', async () => {
  const r = rig();
  await r.ready();
  const restarted = new SessionEngine(r.options);
  await restarted.start();
  expect(restarted.getSnapshot()).toMatchObject({
    status: 'branch',
    user: { userId: profile.userId },
  });
  r.credentials.value = {
    ...r.credentials.value!,
    user: { ...r.credentials.value!.user, userId: otherBranch },
  };
  const foreign = new SessionEngine(r.options);
  await foreign.start();
  expect(foreign.getSnapshot().status).toBe('anonymous');
  expect(r.credentials.value).toBeNull();
});
it('simultaneous authenticated 401 reads share one rotation and retry GET once', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  const client = r.engine.client();
  await Promise.all([getBranchCurrent({ client }), getBranchCurrent({ client })]);
  expect(r.calls.filter((c) => c.path === '/user/renew-token')).toHaveLength(1);
  expect(r.credentials.value!.tokens.refreshToken).toBe('refresh-1r');
  expect(r.engine.getSnapshot().status).toBe('branch');
});
it('logout waits for a rotation, revokes the new refresh token and fences the late read', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  const gate = deferred<Response>();
  let started = false;
  r.intercept(async (path) => {
    if (path === '/user/renew-token') {
      started = true;
      return gate.promise;
    }
    return undefined;
  });
  const read = getBranchCurrent({ client: r.engine.client() }).catch((error) => error);
  while (!started) await Promise.resolve();
  const logout = r.engine.logout();
  gate.resolve(response({ token: 'rotated-token', refreshToken: 'rotated-refresh' }));
  await expect(logout).resolves.toEqual({ serverConfirmed: true, localCleared: true });
  await read;
  expect(r.calls.find((c) => c.path === '/user/logout')?.body).toEqual({
    refreshToken: 'rotated-refresh',
  });
  expect(r.credentials.value).toBeNull();
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'anonymous', user: null, branch: null });
});
it('rotation persistence failure pauses writes; no plaintext or previous-token fallback', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  r.credentials.failWrite = true;
  await getBranchCurrent({ client: r.engine.client() }).catch(() => undefined);
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'storage-error', writesPaused: true });
  await expect(
    postTransaction(
      { date: '2026-10-27', value: 1, accountId: branchId, transactionTypeId: branchId },
      { 'Idempotency-Key': 'test' },
      { client: r.engine.client() },
    ),
  ).rejects.toMatchObject({ kind: 'token-provider', dispatched: false });
});
it('unknown rotation permits one explicit verification attempt and never loops', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  r.intercept(async (path) => {
    if (path === '/user/renew-token') throw new TypeError('lost response');
    return undefined;
  });
  await getBranchCurrent({ client: r.engine.client() }).catch(() => undefined);
  expect(r.engine.getSnapshot().status).toBe('renewal-unconfirmed');
  await r.engine.recover();
  await r.engine.recover();
  expect(r.calls.filter((c) => c.path === '/user/renew-token')).toHaveLength(2);
  expect(r.engine.getSnapshot().writesPaused).toBe(true);
});
it('definitive refresh refusal clears authority and keeps backend messages verbatim', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  r.intercept(async (path) =>
    path === '/user/renew-token'
      ? response({ errorMessages: ['Recusada.', 'Recusada.'] }, 401)
      : undefined,
  );
  await getBranchCurrent({ client: r.engine.client() }).catch(() => undefined);
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'anonymous',
    messages: ['Recusada.', 'Recusada.'],
    recovery: 'Não foi possível renovar sua sessão. Entre novamente.',
  });
});
it('does not replay an authenticated write after 401 recovery', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) =>
    path === '/transaction' ? response({ errorMessages: ['Sem sessão.'] }, 401) : undefined,
  );
  await expect(
    postTransaction(
      { date: '2026-10-27', value: 1, accountId: branchId, transactionTypeId: branchId },
      { 'Idempotency-Key': 'same-key' },
      { client: r.engine.client() },
    ),
  ).rejects.toMatchObject({ status: 401, errorMessages: ['Sem sessão.'] });
  expect(r.calls.filter((c) => c.path === '/transaction')).toHaveLength(1);
  expect(r.calls.filter((c) => c.path === '/user/renew-token')).toHaveLength(1);
});
it('branch switching invalidates old clients and clears only active selections', async () => {
  const r = rig();
  await r.ready();
  const old = r.engine.client();
  await r.engine.selectBranch(otherBranch);
  await expect(getBranchCurrent({ client: old })).rejects.toMatchObject({
    kind: 'token-provider',
    dispatched: false,
  });
  expect(r.options.clearSelections).toHaveBeenCalledWith(profile.userId, branchId);
  expect(r.engine.getSnapshot().branch?.id).toBe(otherBranch);
});
it('foreground rollover rechecks branch time and role; no device date supplies defaults', async () => {
  const r = rig();
  await r.ready();
  const oldScope = r.engine.getSnapshot().scope;
  await r.engine.foreground(false);
  expect(r.engine.getSnapshot().writesPaused).toBe(true);
  r.date('2026-10-28');
  r.role('Member');
  await r.engine.foreground(true);
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'branch',
    branch: { role: 'Member' },
    clock: { branchLocalDate: '2026-10-28' },
    dayChanged: true,
  });
  expect(r.engine.getSnapshot().scope).toBeGreaterThan(oldScope);
  expect(r.options.clearSelections).toHaveBeenCalledWith(profile.userId, branchId);
});
it('isolates a response body arriving after a branch switch', async () => {
  const r = rig();
  await r.ready();
  const text = deferred<string>();
  let delivered = false;
  r.intercept(async (path) => {
    if (path === '/user/profile' && !delivered) {
      delivered = true;
      const result = response(profile);
      result.text = () => text.promise;
      return result;
    }
    return undefined;
  });
  const pending = getUserProfile({ client: r.engine.client() }).catch((error) => error);
  while (!delivered) await Promise.resolve();
  await r.engine.selectBranch(otherBranch);
  text.resolve(JSON.stringify(profile));
  expect(isApiError(await pending)).toBe(true);
  expect(r.engine.getSnapshot().branch?.id).toBe(otherBranch);
});
it('keeps native cancellation attached until a delayed body finishes', async () => {
  const r = rig();
  await r.ready();
  const body = deferred<string>();
  let bodyStarted = false;
  let requestSignal: AbortSignal | null = null;
  r.intercept(async (path, init) => {
    if (path !== '/user/profile') return undefined;
    requestSignal = init.signal ?? null;
    const result = response(profile);
    result.text = () => {
      bodyStarted = true;
      return body.promise;
    };
    return result;
  });
  const pending = getUserProfile({ client: r.engine.client() }).catch((error) => error);
  while (!bodyStarted) await Promise.resolve();
  await r.engine.selectBranch(otherBranch);
  expect((requestSignal as AbortSignal | null)?.aborted).toBe(true);
  body.resolve(JSON.stringify(profile));
  expect(isApiError(await pending)).toBe(true);
});
it('membership refusal clears branch authority without rotating identity or replaying writes', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) =>
    path === '/branch/current'
      ? response({ errorMessages: ['Vínculo removido.', 'Vínculo removido.'] }, 403)
      : undefined,
  );
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'identity',
    branch: null,
    clock: null,
    messages: ['Vínculo removido.', 'Vínculo removido.'],
    writesPaused: true,
  });
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(0);
});
it('an offline logout clears local authority while reporting unconfirmed server revocation', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) => {
    if (path === '/user/logout') throw new TypeError('offline');
    return undefined;
  });
  await expect(r.engine.logout()).resolves.toEqual({ serverConfirmed: false, localCleared: true });
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'anonymous', user: null, branch: null });
  expect(r.credentials.value).toBeNull();
});
it('a stale branch clock prevents dispatch even before the next foreground event', async () => {
  const r = rig();
  await r.ready();
  r.advance(60001);
  await expect(
    postTransaction(
      { date: '2026-10-27', value: 1, accountId: branchId, transactionTypeId: branchId },
      { 'Idempotency-Key': 'test' },
      { client: r.engine.client() },
    ),
  ).rejects.toMatchObject({ dispatched: false });
  expect(r.calls.filter((call) => call.path === '/transaction')).toHaveLength(0);
});
it('a repeated branch 401 keeps the definitive server messages after one GET retry', async () => {
  const r = rig();
  await r.ready();
  let reads = 0;
  r.intercept(async (path) => {
    if (path === '/branch/current' && ++reads !== 2)
      return response({ errorMessages: ['Filial indisponível.', 'Filial indisponível.'] }, 401);
    return undefined;
  });
  // First refusal renews; the second read rebuilds branch authority; the final retry is refused.
  await expect(getBranchCurrent({ client: r.engine.client() })).rejects.toMatchObject({
    status: 401,
    errorMessages: ['Filial indisponível.', 'Filial indisponível.'],
  });
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'identity',
    branch: null,
    messages: ['Filial indisponível.', 'Filial indisponível.'],
  });
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('a slower older clock cannot undo a newer authoritative day without a role/scope change', async () => {
  const r = rig();
  await r.ready();
  const old = { ...r.engine.getSnapshot().clock! };
  const held = deferred<Response>();
  let started = false;
  r.intercept(async (path) => {
    if (path === '/branch/current' && !started) {
      started = true;
      return held.promise;
    }
    return undefined;
  });
  const slow = r.engine.refresh();
  while (!started) await Promise.resolve();
  r.date('2026-10-28');
  await r.engine.refresh();
  held.resolve(response(old));
  await slow;
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'branch',
    clock: { branchLocalDate: '2026-10-28' },
    dayChanged: true,
    writesPaused: true,
  });
});
it('explicit recovery retries an unavailable secure store before any restored authority is granted', async () => {
  const r = rig();
  r.credentials.failRead = true;
  await r.engine.start();
  expect(r.engine.getSnapshot().status).toBe('storage-error');
  r.credentials.failRead = false;
  await r.engine.recover();
  expect(r.engine.getSnapshot().status).toBe('anonymous');
  expect(r.calls).toHaveLength(0);
});
it('online logout with an expired identity renews only to revoke the rotated pair, without restoring a branch', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  let first = true;
  r.intercept(async (path) => {
    if (path === '/user/logout' && first) {
      first = false;
      return response({ errorMessages: ['Sessão expirada.'] }, 401);
    }
    return undefined;
  });
  await expect(r.engine.logout()).resolves.toEqual({ serverConfirmed: true, localCleared: true });
  const logouts = r.calls.filter((call) => call.path === '/user/logout');
  expect(logouts).toHaveLength(2);
  expect(logouts[1]?.body).toEqual({ refreshToken: 'refresh-1r' });
  expect(logouts[1]?.authorization).not.toBe(logouts[0]?.authorization);
  expect(r.calls.filter((call) => call.path === '/branch/session')).toHaveLength(1);
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'anonymous', branch: null, user: null });
});
it('a branch-clock 401 with still-valid identity performs one bounded renewal and rebuilds the clock', async () => {
  const r = rig();
  await r.ready();
  let refused = false;
  r.intercept(async (path) => {
    if (path === '/branch/current' && !refused) {
      refused = true;
      return response({ errorMessages: ['Sessão da filial expirada.'] }, 401);
    }
    return undefined;
  });
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'branch',
    writesPaused: false,
    branch: { id: branchId },
  });
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('a second branch-clock 401 after rebuilding stops recovery and preserves definitive backend messages', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) =>
    path === '/branch/current'
      ? response({ errorMessages: ['Vínculo recusado.', 'Vínculo recusado.'] }, 401)
      : undefined,
  );
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'identity',
    branch: null,
    clock: null,
    messages: ['Vínculo recusado.', 'Vínculo recusado.'],
    writesPaused: true,
  });
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('confirmed identity rotation followed by removed membership keeps the identity gate and the new refusal verbatim', async () => {
  const r = rig();
  await r.ready();
  let first = true;
  r.intercept(async (path) => {
    if (path === '/branch/current' && first) {
      first = false;
      return response({ errorMessages: ['Sessão da filial expirada.'] }, 401);
    }
    if (path === '/branch/session')
      return response({ errorMessages: ['Vínculo removido.', 'Vínculo removido.'] }, 403);
    return undefined;
  });
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'identity',
    user: { userId: profile.userId },
    branch: null,
    clock: null,
    messages: ['Vínculo removido.', 'Vínculo removido.'],
  });
  expect(r.credentials.value?.tokens.refreshToken).toBe('refresh-1r');
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('a rejected profile after rotation removes identity authority rather than treating it as only a branch failure', async () => {
  const r = rig();
  await r.ready();
  let reads = 0;
  r.intercept(async (path) =>
    path === '/user/profile'
      ? response(
          { errorMessages: [++reads === 1 ? 'Sessão expirada.' : 'Usuário indisponível.'] },
          401,
        )
      : undefined,
  );
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'anonymous',
    user: null,
    branch: null,
    messages: ['Usuário indisponível.'],
  });
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('a lost profile read after confirmed rotation recovers by reading the stored new identity, without another rotation or stale branch gate', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  let reads = 0;
  r.intercept(async (path) => {
    if (path === '/user/profile' && ++reads === 2) throw new TypeError('profile reply lost');
    return undefined;
  });
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'offline', writesPaused: true });
  expect(r.credentials.value?.tokens.refreshToken).toBe('refresh-1r');
  r.intercept(null);
  await r.engine.recover();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'identity',
    branch: null,
    clock: null,
    writesPaused: true,
  });
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('retrying a rotated credential write cannot reopen the old clock without newly verified branch credentials', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  r.credentials.failWrite = true;
  await r.engine.refresh();
  expect(r.engine.getSnapshot().status).toBe('storage-error');
  r.credentials.failWrite = false;
  await r.engine.recover();
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'identity',
    branch: null,
    clock: null,
    writesPaused: true,
  });
});
it('lifecycle reads cannot resume old valid access tokens after an unknown rotation', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) => {
    if (path === '/branch/current')
      return response({ errorMessages: ['Sessão da filial inválida.'] }, 401);
    if (path === '/user/renew-token') throw new TypeError('rotation reply lost');
    return undefined;
  });
  await getBranchCurrent({ client: r.engine.client() }).catch(() => undefined);
  expect(r.engine.getSnapshot().status).toBe('renewal-unconfirmed');
  const before = r.calls.length;
  await r.engine.refresh();
  await r.engine.foreground(false);
  await r.engine.foreground(true);
  await r.engine.connectivity(true);
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'renewal-unconfirmed',
    writesPaused: true,
  });
  expect(r.calls).toHaveLength(before);
});
it('background recovery cannot bypass failed rotated-token persistence', async () => {
  const r = rig();
  await r.ready();
  r.expire();
  r.credentials.failWrite = true;
  await r.engine.refresh();
  expect(r.engine.getSnapshot().status).toBe('storage-error');
  const before = r.calls.length;
  r.credentials.failWrite = false;
  await r.engine.foreground(false);
  await r.engine.foreground(true);
  await r.engine.refresh();
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'storage-error', writesPaused: true });
  expect(r.calls).toHaveLength(before);
  expect(r.credentials.value?.tokens.refreshToken).toBe('refresh-1');
  await r.engine.recover();
  expect(r.credentials.value?.tokens.refreshToken).toBe('refresh-1r');
});
it('logout and a new branch choice clear active selections while device input remains separately owned', async () => {
  const r = rig();
  await r.ready();
  r.options.clearSelections.mockClear();
  await r.engine.logout();
  expect(r.options.clearSelections).toHaveBeenCalledWith(profile.userId, branchId);
  await r.login();
  r.options.clearSelections.mockClear();
  await r.engine.selectBranch(branchId);
  expect(r.options.clearSelections).toHaveBeenCalledWith(profile.userId, branchId);
});
it('branch-session recovery cannot replace an unknown rotation with an identity-ready state', async () => {
  const r = rig();
  await r.login();
  r.intercept(async (path) => {
    if (path === '/branch/session') return response({ errorMessages: ['Sessão inválida.'] }, 401);
    if (path === '/user/renew-token') throw new TypeError('rotation reply lost');
    return undefined;
  });
  await r.engine.selectBranch(branchId).catch(() => undefined);
  expect(r.engine.getSnapshot()).toMatchObject({
    status: 'renewal-unconfirmed',
    writesPaused: true,
    branch: null,
  });
  await r.engine.refresh();
  expect(r.calls.filter((call) => call.path === '/user/renew-token')).toHaveLength(1);
});
it('API connectivity failure requires a fresh branch clock before writes can resume, even when the previous clock was recent', async () => {
  const r = rig();
  await r.ready();
  const current = { ...r.engine.getSnapshot().clock! };
  r.intercept(async (path) => {
    if (path === '/user/profile') throw new TypeError('API unavailable');
    return undefined;
  });
  await r.engine.refresh();
  expect(r.engine.getSnapshot().status).toBe('offline');
  const held = deferred<Response>();
  let waiting = false;
  r.intercept(async (path) => {
    if (path === '/branch/current') {
      waiting = true;
      return held.promise;
    }
    return undefined;
  });
  const recovery = r.engine.recover();
  while (!waiting) await Promise.resolve();
  expect(r.engine.getSnapshot().writesPaused).toBe(true);
  await expect(
    postTransaction(
      { date: '2026-10-27', value: 1, accountId: branchId, transactionTypeId: branchId },
      { 'Idempotency-Key': 'not-dispatched' },
      { client: r.engine.client() },
    ),
  ).rejects.toMatchObject({ dispatched: false });
  held.resolve(response(current));
  await recovery;
  expect(r.engine.getSnapshot()).toMatchObject({ status: 'branch', writesPaused: false });
});
