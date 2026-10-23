import { describe, expect, it } from 'vitest';
import {
  cookieNames,
  maxBranchSlots,
  readSessionCookies,
  slotCookieName,
  type CookieWrite,
} from '@/server/session/cookies';
import {
  branchTokenFromCookies,
  ensureCredentials,
  fenceWrites,
  login,
  logout,
  resolveSession,
  selectBranch,
  snapshotFromCookies,
} from '@/server/session/service';
import {
  B1,
  B2,
  B3,
  B9,
  applyWrites,
  branch,
  cookieReader,
  fail,
  httpError,
  jwt,
  meta,
  networkError,
  ok,
  sessionCookies,
  setup,
  slot,
} from '../support/session-fixtures';

function written(writes: readonly CookieWrite[], name: string) {
  return writes.findLast((write) => write.name === name)?.value;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => (resolve = done));
  return { promise, resolve };
}

describe('credentials and renewal', () => {
  it('renews proactively inside the 30 s margin and not before', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 31_000),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    expect((await ensureCredentials(context(), cookies, 'identity')).ok).toBe(true);
    expect(backend.count('renew')).toBe(0);
    clock.advance(2_000);
    const renewed = await ensureCredentials(context(), cookies, 'identity');
    expect(renewed.ok && renewed.renewed).toBe(true);
    expect(backend.count('renew')).toBe(1);
  });

  it('recreates only the expired session of the requested branch, keeping its generation', async () => {
    const { clock, backend, context } = setup();
    const other = slot(B2, jwt('b2', clock.ms + 3_600_000));
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('b1', clock.ms - 1)), other],
    });
    const result = await ensureCredentials(context(), cookies, 'branch', {
      branchId: B1,
      declaredGeneration: 'gen-11',
    });
    expect(result.ok).toBe(true);
    expect(backend.count('renew')).toBe(0);
    expect(backend.calls.find((call) => call.method === 'createBranchSession')?.args[1]).toBe(B1);
    const next = applyWrites(cookies, result.writes);
    expect(next.meta?.scope).toBe('scope-1');
    expect(next.slots.get(B1)?.gen).toBe('gen-11');
    expect(next.slots.get(B1)?.token).not.toBe(cookies.slots.get(B1)?.token);
    // The other tab's branch is untouched.
    expect(next.slots.get(B2)).toEqual(other);
    expect(result.writes.map((write) => write.name)).toEqual([slotCookieName(B1)]);
  });

  it('a 404 on (re)opening closes only that branch, with the verbatim message', async () => {
    const { clock, backend, registry, context } = setup();
    backend.branchHandler = () =>
      fail(httpError('POST', '/branch/session', 404, ['Filial não encontrada']));
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('b1', clock.ms - 1)), slot(B2, jwt('b2', clock.ms + 3_600_000))],
    });
    const resolved = await resolveSession(context(), cookies, { branchId: B1 });
    expect(resolved.snapshot.state).toBe('branch-unavailable');
    expect(resolved.snapshot.messages).toEqual(['Filial não encontrada']);
    expect(resolved.snapshot.branch).toBeNull();
    const next = applyWrites(cookies, resolved.writes);
    expect(next.slots.has(B1)).toBe(false);
    expect(next.slots.has(B2)).toBe(true);
    expect(next.meta?.scope).toBe('scope-1');
    // Any late write for that branch generation is now fenced; the identity is not.
    expect(registry.endedScopes.has('gen-11')).toBe(true);
    expect(registry.endedScopes.has('scope-1')).toBe(false);
  });

  it('reports a lost session after a definitive renewal refusal and clears every cookie', async () => {
    const { clock, backend, context } = setup();
    backend.renewHandler = () =>
      fail(httpError('POST', '/user/renew-token', 401, ['Refresh token inválido']));
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('b1', clock.ms + 3_600_000)), slot(B2, jwt('b2', clock.ms))],
    });
    const resolved = await resolveSession(context(), cookies);
    expect(resolved.snapshot.state).toBe('anonymous');
    expect(resolved.snapshot.messages).toEqual(['Refresh token inválido']);
    expect(written(resolved.writes, cookieNames.refresh)).toBeNull();
    expect(written(resolved.writes, cookieNames.identity)).toBeNull();
    expect(written(resolved.writes, slotCookieName(B1))).toBeNull();
    expect(written(resolved.writes, slotCookieName(B2))).toBeNull();
  });

  it('keeps an unknown renewal outcome paused instead of logging out', async () => {
    const { clock, backend, context } = setup();
    backend.renewHandler = () => fail(networkError('POST', '/user/renew-token'));
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    const resolved = await resolveSession(context(), cookies);
    expect(resolved.snapshot.state).toBe('renewal-unconfirmed');
    expect(resolved.writes).toEqual([]);
  });
});

describe('per-tab branch context (D-M2-1)', () => {
  const live = (clock: { ms: number }) => ({
    identity: jwt('id', clock.ms + 3_600_000),
    refresh: 'refresh-ana',
    meta: meta(),
  });

  it('two branches are held at once; each request uses only its own branch’s session', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      ...live(clock),
      slots: [slot(B1, 'token-b1'), slot(B2, 'token-b2')].map((held) => ({
        ...held,
        token: jwt(held.token, clock.ms + 3_600_000),
      })),
    });
    const first = await ensureCredentials(context(), cookies, 'branch', { branchId: B1 });
    const second = await ensureCredentials(context(), cookies, 'branch', { branchId: B2 });
    expect(first.ok && first.token).toBe(cookies.slots.get(B1)?.token);
    expect(second.ok && second.token).toBe(cookies.slots.get(B2)?.token);
    expect(backend.count('createBranchSession')).toBe(0);
    expect(first.ok && first.writes).toEqual([]);
  });

  it('opening a third branch writes its own slot and leaves the others in place', async () => {
    const { clock, context } = setup();
    const cookies = sessionCookies({
      ...live(clock),
      slots: [slot(B1, jwt('b1', clock.ms + 3_600_000)), slot(B2, jwt('b2', clock.ms + 3_600_000))],
    });
    const opened = await ensureCredentials(context(), cookies, 'branch', { branchId: B3 });
    expect(opened.ok).toBe(true);
    const next = applyWrites(cookies, opened.writes);
    expect([...next.slots.keys()].sort()).toEqual([B1, B2, B3].sort());
    expect(next.slots.get(B3)?.sid).toBe('sid-1');
  });

  it('a branch session held under another login is never used for this identity', async () => {
    const { clock, backend, context } = setup();
    const foreign = slot(B1, jwt('bia-b1', clock.ms + 3_600_000), { sid: 'sid-of-bia' });
    const cookies = sessionCookies({ ...live(clock), slots: [foreign] });
    const result = await ensureCredentials(context(), cookies, 'branch', { branchId: B1 });
    expect(result.ok && result.token).not.toBe(foreign.token);
    expect(backend.calls.find((call) => call.method === 'createBranchSession')?.args[0]).toBe(
      cookies.identity,
    );
    expect(snapshotFromCookies(cookies, B1).state).toBe('choose-branch');
    expect(branchTokenFromCookies(cookies, B1)).toBeNull();
  });

  it('a tab declaring another or an ended branch generation is stale; nothing is opened', async () => {
    const { clock, backend, registry, context } = setup();
    const cookies = sessionCookies({
      ...live(clock),
      slots: [slot(B1, jwt('b1', clock.ms + 3_600_000))],
    });
    expect(
      await ensureCredentials(context(), cookies, 'branch', {
        branchId: B1,
        declaredGeneration: 'gen-older',
      }),
    ).toMatchObject({ ok: false, failure: 'stale' });
    registry.endedScopes.set('gen-gone', { until: clock.ms + 60_000, by: {} });
    expect(
      await ensureCredentials(context(), cookies, 'branch', {
        branchId: B2,
        declaredGeneration: 'gen-gone',
      }),
    ).toMatchObject({ ok: false, failure: 'stale' });
    expect(backend.count('createBranchSession')).toBe(0);
  });

  it('a slot evicted while its tab still works is reopened under that tab’s generation', async () => {
    const { clock, context } = setup();
    const cookies = sessionCookies(live(clock));
    const reopened = await ensureCredentials(context(), cookies, 'branch', {
      branchId: B1,
      declaredGeneration: 'gen-of-tab',
    });
    expect(reopened.ok && reopened.slot?.gen).toBe('gen-of-tab');
  });

  it('a request without a branch never gets branch authority', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      ...live(clock),
      slots: [slot(B1, jwt('b1', clock.ms + 3_600_000))],
    });
    expect(await ensureCredentials(context(), cookies, 'branch')).toMatchObject({
      ok: false,
      failure: 'no-branch-session',
    });
    expect(
      await ensureCredentials(context(), cookies, 'branch', { branchId: 'not-a-branch' }),
    ).toMatchObject({ ok: false, failure: 'no-branch-session' });
    expect(backend.count('createBranchSession')).toBe(0);
  });

  it('selecting a branch re-verifies it; the generation survives an unchanged role', async () => {
    const { clock, backend, context } = setup();
    const held = slot(B1, jwt('b1', clock.ms + 3_600_000));
    const cookies = sessionCookies({ ...live(clock), slots: [held] });
    const same = await selectBranch(context(), cookies, B1);
    expect(backend.count('createBranchSession')).toBe(1);
    expect(same.snapshot?.scope).toBe(`scope-1.${held.gen}`);
  });

  it('a changed role starts a new branch generation and ends the old one for every tab', async () => {
    const { clock, backend, registry, context } = setup();
    backend.branchHandler = (_identity, id) =>
      ok({ token: backend.token('br'), branch: branch(id, 'Member') });
    const held = slot(B1, jwt('b1', clock.ms + 3_600_000));
    const cookies = sessionCookies({ ...live(clock), slots: [held] });
    const changed = await selectBranch(context(), cookies, B1);
    expect(changed.snapshot?.branch?.role).toBe('Member');
    expect(changed.snapshot?.scope).not.toBe(`scope-1.${held.gen}`);
    expect(registry.endedScopes.has(held.gen)).toBe(true);
    // Another tab still holding the old cookie and declaring the old generation is refused.
    expect(
      await ensureCredentials(context(), cookies, 'branch', {
        branchId: B1,
        declaredGeneration: held.gen,
      }),
    ).toMatchObject({ ok: false, failure: 'stale' });
  });

  it(`holds at most ${maxBranchSlots} branch sessions, evicting the oldest`, async () => {
    const { clock, context } = setup();
    const ids = [B1, B2, B3, B9];
    const cookies = sessionCookies({
      ...live(clock),
      slots: ids.map((id, index) => slot(id, jwt(id, clock.ms + 3_600_000), { at: index })),
    });
    const fifth = '55555555-5555-4555-8555-555555555555';
    const opened = await ensureCredentials(context(), cookies, 'branch', { branchId: fifth });
    const next = applyWrites(cookies, opened.ok ? opened.writes : []);
    expect(next.slots.size).toBe(maxBranchSlots);
    expect(next.slots.has(B1)).toBe(false);
    expect(next.slots.has(fifth)).toBe(true);
  });
});

describe('membership rule at login', () => {
  const credentials = { email: 'ana@example.invalid', password: 'never-stored' };

  it('zero branches: identity with no memberships', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([]);
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.snapshot?.state).toBe('no-memberships');
    expect(backend.count('createBranchSession')).toBe(0);
  });

  it('one branch: opens it automatically, as this identity’s branch session', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch(B9, 'Member')]);
    backend.branchHandler = (_identity, id) =>
      ok({ token: backend.token('br'), branch: branch(id, 'Member') });
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.snapshot?.state).toBe('branch');
    expect(result.snapshot?.branch).toEqual(branch(B9, 'Member'));
    const next = applyWrites(sessionCookies({}), result.writes);
    expect(next.slots.get(B9)?.token).toMatch(/^eyJ/);
    expect(next.slots.get(B9)?.sid).toBe(next.meta?.sid);
    expect(result.snapshot?.scope).toBe(`${next.meta?.scope}.${next.slots.get(B9)?.gen}`);
  });

  it('several branches: awaits a choice and opens nothing', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch(B1), branch(B2)]);
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.snapshot?.state).toBe('choose-branch');
    expect(result.snapshot?.branches).toHaveLength(2);
    expect(backend.count('createBranchSession')).toBe(0);
    expect(result.writes.some((write) => write.name.startsWith('__Host-loto-b-'))).toBe(false);
  });

  it('a new login drops every branch session the previous identity held', async () => {
    const { clock, registry, context } = setup();
    const before = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-old',
      meta: meta(),
      slots: [slot(B1, jwt('b1', clock.ms + 3_600_000)), slot(B2, jwt('b2', clock.ms))],
    });
    const result = await login(context(), before, credentials);
    const next = applyWrites(before, result.writes);
    expect(next.slots.has(B2)).toBe(false);
    expect(next.slots.get(B1)?.sid ?? next.meta?.sid).toBe(next.meta?.sid);
    expect(registry.endedScopes.has('gen-11')).toBe(true);
    expect(registry.endedIdentities.has('sid-1')).toBe(true);
  });

  it('passes a refusal through verbatim with Retry-After and changes no cookie', async () => {
    const { backend, context } = setup();
    backend.loginHandler = () =>
      fail(
        httpError('POST', '/user/login', 429, ['Muitas tentativas', 'Muitas tentativas'], {
          'Retry-After': '900',
        }),
      );
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.error).toEqual({
      kind: 'refused',
      status: 429,
      messages: ['Muitas tentativas', 'Muitas tentativas'],
      retryAfter: '900',
    });
    expect(result.writes).toEqual([]);
  });

  it('never puts a token or the password in the snapshot', async () => {
    const { context } = setup();
    const result = await login(context(), sessionCookies({}), credentials);
    const json = JSON.stringify(result.snapshot);
    expect(json).not.toContain('never-stored');
    expect(json).not.toContain('eyJ');
    expect(json).not.toContain('refresh-');
  });

  it('resumes an unscoped intent only for the matching user and branch', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch(B1)]);
    const intent = {
      v: 1 as const,
      path: '/transacoes?de=2026-10-01',
      branchId: B1,
      email: 'ana@example.invalid',
    };
    const resumed = await login(context(), sessionCookies({ intent }), credentials);
    expect(resumed.snapshot?.resume).toBe('/transacoes?de=2026-10-01');
    expect(written(resumed.writes, cookieNames.intent)).toBeNull();

    const otherUser = await login(
      context(),
      sessionCookies({ intent: { ...intent, email: 'bia@example.invalid' } }),
      credentials,
    );
    expect(otherUser.snapshot?.resume).toBeNull();
    const otherBranch = await login(
      context(),
      sessionCookies({ intent: { ...intent, branchId: B9 } }),
      credentials,
    );
    expect(otherBranch.snapshot?.resume).toBeNull();
    const external = await login(
      context(),
      sessionCookies({ intent: { ...intent, path: '//evil.example/x' } }),
      credentials,
    );
    expect(external.snapshot?.resume).toBeNull();
  });

  it('an intent that names its branch in the address resumes there, for the same person only', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch(B1), branch(B2)]);
    const path = `/f/${B2}/transacoes?de=2026-10-01`;
    const intent = { v: 1 as const, path, branchId: B2, email: 'ana@example.invalid' };
    const resumed = await login(context(), sessionCookies({ intent }), credentials);
    expect(resumed.snapshot).toMatchObject({ state: 'choose-branch', resume: path });
    const otherUser = await login(
      context(),
      sessionCookies({ intent: { ...intent, email: 'bia@example.invalid' } }),
      credentials,
    );
    expect(otherUser.snapshot?.resume).toBeNull();
    // Opening another branch never resumes a link that names a different one.
    const elsewhere = await selectBranch(
      context(),
      applyWrites(
        sessionCookies({ intent }),
        (await login(context(), sessionCookies({}), credentials)).writes,
      ),
      B1,
    );
    expect(elsewhere.snapshot?.resume).toBeNull();
  });
});

describe('generation fence: late results never resurrect a session', () => {
  async function inFlightRenewal() {
    const env = setup();
    const gate = deferred<void>();
    const original = env.backend.renewHandler;
    env.backend.renewHandler = async (refresh) => {
      await gate.promise;
      return original(refresh);
    };
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('br', env.clock.ms + 3_600_000))],
    });
    const renewalContext = env.context();
    const pending = ensureCredentials(renewalContext, cookies, 'branch', { branchId: B1 });
    return { ...env, gate, cookies, renewalContext, pending };
  }

  it('logout while a renewal is in flight: it shares the rotation, revokes it, and the late renewal writes nothing', async () => {
    const env = await inFlightRenewal();
    const loggingOut = logout(env.context(), env.cookies);
    env.gate.resolve();
    const loggedOut = await loggingOut;
    expect(env.backend.count('renew')).toBe(1);
    expect(env.backend.calls.find((call) => call.method === 'logout')?.args[1]).toMatch(
      /^refresh-renewed/,
    );
    expect(written(loggedOut.writes, cookieNames.refresh)).toBeNull();
    expect(written(loggedOut.writes, slotCookieName(B1))).toBeNull();
    const late = await env.pending;
    expect(late.ok).toBe(true);
    expect(fenceWrites(env.renewalContext, env.cookies.meta, late.writes)).toEqual([]);
  });

  it('another login while a renewal is in flight: the late renewal writes nothing', async () => {
    const env = await inFlightRenewal();
    const other = await login(env.context(), env.cookies, {
      email: 'bia@example.invalid',
      password: 'x',
    });
    expect(other.snapshot?.user?.email).toBe('bia@example.invalid');
    env.gate.resolve();
    const late = await env.pending;
    expect(fenceWrites(env.renewalContext, env.cookies.meta, late.writes)).toEqual([]);
  });

  it('another tab opening another branch never fences this tab’s renewal', async () => {
    const env = await inFlightRenewal();
    env.gate.resolve();
    const opening = await selectBranch(env.context(), env.cookies, B2);
    expect(opening.snapshot?.branch?.id).toBe(B2);
    const late = await env.pending;
    const fenced = fenceWrites(env.renewalContext, env.cookies.meta, late.writes);
    expect(fenced.map((write) => write.name)).toEqual([cookieNames.identity, cookieNames.refresh]);
  });

  it('a late slot write of a branch generation another request ended is dropped', async () => {
    const env = setup();
    const gate = deferred<void>();
    env.backend.branchHandler = async (identity, id) => {
      await gate.promise;
      return ok({ token: env.backend.token(`b-${identity.slice(-4)}`), branch: branch(id) });
    };
    const held = slot(B1, jwt('b1', env.clock.ms - 1));
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'r',
      meta: meta(),
      slots: [held],
    });
    const lateContext = env.context();
    const pending = ensureCredentials(lateContext, cookies, 'branch', { branchId: B1 });
    env.registry.endedScopes.set(held.gen, { until: env.clock.ms + 60_000, by: {} });
    gate.resolve();
    const late = await pending;
    // Its slot reused the held generation, which another request ended meanwhile.
    expect(late.ok && late.slot?.gen).toBe(held.gen);
    expect(fenceWrites(lateContext, cookies.meta, late.ok ? late.writes : [])).toEqual([]);
  });

  it('a request arriving with an ended generation is refused as stale, never renewed', async () => {
    const env = setup();
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    await logout(env.context(), cookies);
    const late = await ensureCredentials(env.context(), cookies, 'branch', { branchId: B1 });
    expect(late).toEqual({ ok: false, failure: 'stale', messages: [], writes: [] });
    expect(env.backend.count('renew')).toBe(1);
  });

  it('a request’s own generation change does not fence its own writes', async () => {
    const env = setup();
    env.backend.branchHandler = (_identity, id) =>
      ok({ token: env.backend.token('br'), branch: branch(id, 'Manager') });
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'r',
      meta: meta(),
      slots: [slot(B1, jwt('b1', env.clock.ms + 3_600_000))],
    });
    const own = env.context();
    const changed = await selectBranch(own, cookies, B1);
    const fenced = fenceWrites(own, cookies.meta, changed.writes);
    expect(written(fenced, slotCookieName(B1))).toBeTruthy();
    expect(readSessionCookies(cookieReader(new Map())).meta).toBeNull();
  });
});

describe('logout revocation', () => {
  it('revokes with the stored refresh token and clears every cookie, every branch included', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('b1', clock.ms)), slot(B2, jwt('b2', clock.ms))],
    });
    const result = await logout(context(), cookies);
    expect(result.serverConfirmed).toBe(true);
    expect(backend.calls.find((call) => call.method === 'logout')?.args[1]).toBe('refresh-ana');
    expect(result.writes.every((write) => write.value === null)).toBe(true);
    expect(applyWrites(cookies, result.writes).slots.size).toBe(0);
  });

  it('renews an expired identity first and revokes with the rotated refresh token', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    await logout(context(), cookies);
    const revoked = backend.calls.find((call) => call.method === 'logout')?.args[1];
    expect(revoked).toMatch(/^refresh-renewed-from-refresh-ana/);
  });

  it('clears local credentials even when the revocation is not confirmed', async () => {
    const { clock, backend, context } = setup();
    backend.logoutHandler = () => fail(networkError('POST', '/user/logout'));
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    const result = await logout(context(), cookies);
    expect(result.serverConfirmed).toBe(false);
    expect(result.snapshot?.state).toBe('anonymous');
    expect(written(result.writes, cookieNames.refresh)).toBeNull();
  });
});

describe('server-side snapshot', () => {
  it('describes cookies without network and without tokens, per route branch', () => {
    const cookies = sessionCookies({
      identity: jwt('id', 0),
      refresh: 'secret-refresh',
      meta: meta(),
      slots: [slot(B1, jwt('br', 0))],
    });
    const identity = snapshotFromCookies(cookies);
    expect(identity).toMatchObject({ state: 'choose-branch', scope: 'scope-1', branch: null });
    const inBranch = snapshotFromCookies(cookies, B1);
    expect(inBranch).toMatchObject({
      state: 'branch',
      scope: 'scope-1.gen-11',
      branch: { id: B1 },
    });
    expect(snapshotFromCookies(cookies, B2).state).toBe('choose-branch');
    for (const snapshot of [identity, inBranch]) {
      expect(JSON.stringify(snapshot)).not.toMatch(/eyJ|secret-refresh/);
    }
  });

  it('an earlier one-branch cookie model never authorizes and is cleared', async () => {
    const { context } = setup();
    const cookies = readSessionCookies(
      cookieReader(
        new Map([
          ['__Host-loto-br', jwt('legacy', Date.now() + 3_600_000)],
          ['__Host-loto-meta', Buffer.from(JSON.stringify({ v: 1 })).toString('base64url')],
        ]),
      ),
    );
    const resolved = await resolveSession(context(), cookies);
    expect(resolved.snapshot.state).toBe('anonymous');
    expect(written(resolved.writes, '__Host-loto-br')).toBeNull();
  });
});

describe('ended generations never authorize (review P2-3)', () => {
  it('after Sair, the old cookies resolve to signed out and the page gate redirects', async () => {
    const { decideGate } = await import('@/lib/route-policy');
    const env = setup();
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('br', env.clock.ms + 3_600_000))],
    });
    await logout(env.context(), cookies);
    const late = await resolveSession(env.context(), cookies, { branchId: B1 });
    expect(late.snapshot.state).toBe('anonymous');
    expect(late.writes).toEqual([]);
    expect(decideGate('branch', late.snapshot, { branchId: B1 })).toMatchObject({
      kind: 'redirect',
      to: '/entrar',
    });
  });

  it('a held slot whose generation another request ended is not used: the branch is reopened', async () => {
    const env = setup();
    const held = slot(B1, jwt('br', env.clock.ms + 3_600_000));
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [held],
    });
    env.registry.endedScopes.set(held.gen, { until: env.clock.ms + 60_000, by: {} });
    const resolved = await resolveSession(env.context(), cookies, { branchId: B1 });
    expect(env.backend.count('createBranchSession')).toBe(1);
    expect(resolved.snapshot.state).toBe('branch');
    expect(resolved.snapshot.scope).not.toBe(`scope-1.${held.gen}`);
  });

  it('cookies older than a rotation this process made read as an unconfirmed renewal', async () => {
    const env = setup();
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    await ensureCredentials(env.context(), cookies, 'identity');
    env.backend.renewHandler = () =>
      fail(httpError('POST', '/user/renew-token', 401, ['Refresh token inválido']));
    env.clock.advance(20_000);
    const late = await resolveSession(env.context(), cookies);
    expect(late.snapshot).toMatchObject({ state: 'renewal-unconfirmed', scope: null });
    expect(late.writes).toEqual([]);
  });
});

describe('unconfirmed renewal pauses writes (review P2-5)', () => {
  it('a still-valid bearer serves reads but not writes after a lost renewal response', async () => {
    const env = setup();
    env.backend.renewHandler = () => fail(networkError('POST', '/user/renew-token'));
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 10_000),
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, jwt('br', env.clock.ms + 3_600_000))],
    });
    expect((await ensureCredentials(env.context(), cookies, 'identity')).ok).toBe(true);
    expect(
      await ensureCredentials(env.context(), cookies, 'identity', { write: true }),
    ).toMatchObject({
      ok: false,
      failure: 'renewal-unconfirmed',
    });
    expect(
      await ensureCredentials(env.context(), cookies, 'branch', { branchId: B1, write: true }),
    ).toMatchObject({ ok: false, failure: 'renewal-unconfirmed' });
  });
});
