import { describe, expect, it } from 'vitest';
import { cookieNames, readSessionCookies, type CookieWrite } from '@/server/session/cookies';
import {
  ensureCredentials,
  fenceWrites,
  leaveBranch,
  login,
  logout,
  resolveSession,
  selectBranch,
  snapshotFromCookies,
} from '@/server/session/service';
import {
  applyWrites,
  branch,
  fail,
  httpError,
  jwt,
  meta,
  networkError,
  ok,
  sessionCookies,
  setup,
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
      meta: meta({ branchId: null, branch: null }),
    });
    expect((await ensureCredentials(context(), cookies, 'identity')).ok).toBe(true);
    expect(backend.count('renew')).toBe(0);
    clock.advance(2_000);
    const renewed = await ensureCredentials(context(), cookies, 'identity');
    expect(renewed.ok && renewed.renewed).toBe(true);
    expect(backend.count('renew')).toBe(1);
  });

  it('recreates only the expired branch session for the same branch, keeping the scope', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      branch: jwt('br', clock.ms - 1),
      meta: meta(),
    });
    const result = await ensureCredentials(context(), cookies, 'branch');
    expect(result.ok).toBe(true);
    expect(backend.count('renew')).toBe(0);
    expect(backend.calls.find((call) => call.method === 'createBranchSession')?.args[1]).toBe('b1');
    const next = applyWrites(cookies, result.writes);
    expect(next.meta?.scope).toBe('scope-1');
    expect(next.branch).not.toBe(cookies.branch);
  });

  it('turns a 404 on recreation into the branch gate under a new scope with the verbatim message', async () => {
    const { clock, backend, registry, context } = setup();
    backend.branchHandler = () =>
      fail(httpError('POST', '/branch/session', 404, ['Filial não encontrada']));
    backend.branchesHandler = () => ok([branch('b2'), branch('b3')]);
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      branch: null,
      meta: meta(),
    });
    const resolved = await resolveSession(context(), cookies);
    expect(resolved.snapshot.state).toBe('branch-unavailable');
    expect(resolved.snapshot.messages).toEqual(['Filial não encontrada']);
    expect(resolved.snapshot.branches?.map((item) => item.id)).toEqual(['b2', 'b3']);
    const next = applyWrites(cookies, resolved.writes);
    expect(next.branch).toBeNull();
    expect(next.meta?.branchId).toBeNull();
    expect(next.meta?.scope).not.toBe('scope-1');
    // Any late write for the old scope is now fenced.
    expect(registry.endedScopes.has('scope-1')).toBe(true);
  });

  it('reports a lost session after a definitive renewal refusal and clears every cookie', async () => {
    const { clock, backend, context } = setup();
    backend.renewHandler = () =>
      fail(httpError('POST', '/user/renew-token', 401, ['Refresh token inválido']));
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    const resolved = await resolveSession(context(), cookies);
    expect(resolved.snapshot.state).toBe('anonymous');
    expect(resolved.snapshot.messages).toEqual(['Refresh token inválido']);
    expect(written(resolved.writes, cookieNames.refresh)).toBeNull();
    expect(written(resolved.writes, cookieNames.identity)).toBeNull();
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

describe('membership rule at login', () => {
  const credentials = { email: 'ana@example.invalid', password: 'never-stored' };

  it('zero branches: identity with no memberships', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([]);
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.snapshot?.state).toBe('no-memberships');
    expect(backend.count('createBranchSession')).toBe(0);
  });

  it('one branch: opens it automatically', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch('b9', 'Member')]);
    backend.branchHandler = (_identity, id) =>
      ok({ token: backend.token('br'), branch: branch(id, 'Member') });
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.snapshot?.state).toBe('branch');
    expect(result.snapshot?.branch).toEqual(branch('b9', 'Member'));
    expect(written(result.writes, cookieNames.branch)).toMatch(/^eyJ/);
  });

  it('several branches: awaits a choice and fetches nothing branch-scoped', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch('b1'), branch('b2')]);
    const result = await login(context(), sessionCookies({}), credentials);
    expect(result.snapshot?.state).toBe('choose-branch');
    expect(result.snapshot?.branches).toHaveLength(2);
    expect(backend.count('createBranchSession')).toBe(0);
    expect(written(result.writes, cookieNames.branch)).toBeNull();
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

  it('resumes a held intent only for the matching user and branch', async () => {
    const { backend, context } = setup();
    backend.branchesHandler = () => ok([branch('b1')]);
    const intent = {
      v: 1 as const,
      path: '/transacoes?de=2026-10-01',
      branchId: 'b1',
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
      sessionCookies({ intent: { ...intent, branchId: 'b7' } }),
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
      branch: jwt('br', env.clock.ms + 3_600_000),
      meta: meta(),
    });
    const renewalContext = env.context();
    const pending = ensureCredentials(renewalContext, cookies, 'branch');
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

  it('a branch switch while a renewal is in flight keeps only the identity rotation', async () => {
    const env = await inFlightRenewal();
    env.backend.branchesHandler = () => ok([branch('b1'), branch('b2')]);
    const leaving = leaveBranch(env.context(), env.cookies);
    env.gate.resolve();
    await leaving;
    const late = await env.pending;
    const fenced = fenceWrites(env.renewalContext, env.cookies.meta, late.writes);
    expect(fenced.map((write) => write.name).sort()).toEqual(
      [cookieNames.identity, cookieNames.refresh].sort(),
    );
  });

  it('a request arriving with an ended generation is refused as stale, never renewed', async () => {
    const env = setup();
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    await logout(env.context(), cookies);
    const late = await ensureCredentials(env.context(), cookies, 'branch');
    expect(late).toEqual({ ok: false, failure: 'stale', messages: [], writes: [] });
    expect(env.backend.count('renew')).toBe(1);
  });

  it('a request’s own generation change does not fence its own writes', async () => {
    const env = setup();
    env.backend.branchesHandler = () => ok([branch('b1'), branch('b2')]);
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'r',
      meta: meta(),
    });
    const own = env.context();
    const switched = await selectBranch(own, cookies, 'b2');
    const fenced = fenceWrites(own, cookies.meta, switched.writes);
    expect(written(fenced, cookieNames.branch)).toMatch(/^eyJ/);
    expect(readSessionCookies({ get: () => undefined }).meta).toBeNull();
  });
});

describe('logout revocation', () => {
  it('revokes with the stored refresh token and clears cookies', async () => {
    const { clock, backend, context } = setup();
    const cookies = sessionCookies({
      identity: jwt('id', clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    const result = await logout(context(), cookies);
    expect(result.serverConfirmed).toBe(true);
    expect(backend.calls.find((call) => call.method === 'logout')?.args[1]).toBe('refresh-ana');
    expect(result.writes.every((write) => write.value === null)).toBe(true);
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
  it('describes cookies without network and without tokens', () => {
    const snapshot = snapshotFromCookies(
      sessionCookies({
        identity: jwt('id', 0),
        refresh: 'secret-refresh',
        branch: jwt('br', 0),
        meta: meta(),
      }),
    );
    expect(snapshot.state).toBe('branch');
    expect(JSON.stringify(snapshot)).not.toMatch(/eyJ|secret-refresh/);
  });
});

describe('ended generations never authorize (review P2-3)', () => {
  it('after Sair, the old cookies resolve to signed out and the page gate redirects', async () => {
    const { decideGate } = await import('@/lib/route-policy');
    const env = setup();
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      branch: jwt('br', env.clock.ms + 3_600_000),
      meta: meta(),
    });
    await logout(env.context(), cookies);
    const late = await resolveSession(env.context(), cookies);
    expect(late.snapshot.state).toBe('anonymous');
    expect(late.writes).toEqual([]);
    expect(decideGate('branch', late.snapshot)).toMatchObject({ kind: 'redirect', to: '/entrar' });
  });

  it('cookies of a scope another request ended carry no branch authority', async () => {
    const env = setup();
    env.backend.branchesHandler = () => ok([branch('b1'), branch('b2')]);
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms + 3_600_000),
      refresh: 'refresh-ana',
      branch: jwt('br', env.clock.ms + 3_600_000),
      meta: meta(),
    });
    await leaveBranch(env.context(), cookies);
    const late = await resolveSession(env.context(), cookies);
    expect(late.snapshot).toMatchObject({ state: 'choose-branch', scope: null, branch: null });
  });

  it('cookies older than a rotation this process made read as an unconfirmed renewal', async () => {
    const env = setup();
    const cookies = sessionCookies({
      identity: jwt('id', env.clock.ms - 1),
      refresh: 'refresh-ana',
      meta: meta(),
    });
    await ensureCredentials(env.context(), cookies, 'branch');
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
      branch: jwt('br', env.clock.ms + 3_600_000),
      meta: meta(),
    });
    expect((await ensureCredentials(env.context(), cookies, 'identity')).ok).toBe(true);
    expect(
      await ensureCredentials(env.context(), cookies, 'identity', { write: true }),
    ).toMatchObject({
      ok: false,
      failure: 'renewal-unconfirmed',
    });
  });
});
