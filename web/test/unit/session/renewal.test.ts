import { describe, expect, it, vi } from 'vitest';
import { peerResultMs } from '@/server/session/registry';
import { openBranchSession, renewIdentity } from '@/server/session/renewal';
import { fail, httpError, networkError, ok, setup } from '../support/session-fixtures';

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => (resolve = done));
  return { promise, resolve };
}

describe('renewal single flight', () => {
  it('coalesces a burst of renewals with one refresh token into one rotation', async () => {
    const { registry, backend } = setup();
    const gate = deferred<void>();
    const original = backend.renewHandler;
    backend.renewHandler = async (refresh) => {
      await gate.promise;
      return original(refresh);
    };
    const burst = Array.from({ length: 25 }, () => renewIdentity(registry, backend, 'refresh-ana'));
    gate.resolve();
    const outcomes = await Promise.all(burst);
    expect(backend.count('renew')).toBe(1);
    const first = outcomes[0];
    expect(first?.kind).toBe('renewed');
    expect(new Set(outcomes.map((outcome) => JSON.stringify(outcome))).size).toBe(1);
  });

  it('never shares a rotation between two users renewing at the same time', async () => {
    const { registry, backend } = setup();
    const [ana, bia] = await Promise.all([
      renewIdentity(registry, backend, 'refresh-ana'),
      renewIdentity(registry, backend, 'refresh-bia'),
    ]);
    expect(backend.count('renew')).toBe(2);
    expect(ana.kind === 'renewed' && ana.refresh).toContain('refresh-ana');
    expect(bia.kind === 'renewed' && bia.refresh).toContain('refresh-bia');
    expect(ana).not.toEqual(bia);
  });

  it('hands a late holder of the rotated token the peer result, then reports staleness, not loss', async () => {
    const { registry, backend, clock } = setup();
    const first = await renewIdentity(registry, backend, 'refresh-ana');
    // The API deactivated the old token on rotation.
    backend.renewHandler = () =>
      fail(httpError('POST', '/user/renew-token', 401, ['Refresh token inválido']));

    clock.advance(peerResultMs - 1);
    expect(await renewIdentity(registry, backend, 'refresh-ana')).toEqual(first);
    expect(backend.count('renew')).toBe(1);

    clock.advance(2);
    expect(await renewIdentity(registry, backend, 'refresh-ana')).toEqual({ kind: 'stale' });
    expect(backend.count('renew')).toBe(2);
  });

  it('treats a definitive 401 for a never-rotated token as rejected with verbatim messages', async () => {
    const { registry, backend } = setup();
    backend.renewHandler = () =>
      fail(
        httpError('POST', '/user/renew-token', 401, [
          'Refresh token expirado',
          'Refresh token expirado',
        ]),
      );
    expect(await renewIdentity(registry, backend, 'refresh-old')).toEqual({
      kind: 'rejected',
      status: 401,
      messages: ['Refresh token expirado', 'Refresh token expirado'],
    });
  });

  it('allows one verification retry after an unknown outcome, then pauses without calling', async () => {
    const { registry, backend, clock } = setup();
    backend.renewHandler = () => fail(networkError('POST', '/user/renew-token'));
    expect((await renewIdentity(registry, backend, 'refresh-ana')).kind).toBe('unknown');
    expect((await renewIdentity(registry, backend, 'refresh-ana')).kind).toBe('unknown');
    expect((await renewIdentity(registry, backend, 'refresh-ana')).kind).toBe('unknown');
    expect(backend.count('renew')).toBe(2);
    // After the pause a deliberate attempt may verify again.
    clock.advance(61_000);
    backend.renewHandler = (refresh) => ok(backend.tokenPair(refresh));
    expect((await renewIdentity(registry, backend, 'refresh-ana')).kind).toBe('renewed');
  });

  it('retries a coordination-busy 409 once and never reports it as a lost session', async () => {
    vi.useFakeTimers();
    try {
      const { registry, backend } = setup();
      backend.renewHandler = () =>
        fail(httpError('POST', '/user/renew-token', 409, ['Tente novamente em instantes']));
      const pending = renewIdentity(registry, backend, 'refresh-ana');
      await vi.advanceTimersByTimeAsync(300);
      expect(await pending).toEqual({ kind: 'unknown' });
      expect(backend.count('renew')).toBe(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('opens one branch session per identity and branch at a time', async () => {
    const { registry, backend } = setup();
    const results = await Promise.all([
      openBranchSession(registry, backend, 'identity-a', 'b1'),
      openBranchSession(registry, backend, 'identity-a', 'b1'),
      openBranchSession(registry, backend, 'identity-a', 'b2'),
    ]);
    expect(backend.count('createBranchSession')).toBe(2);
    expect(results[0]).toEqual(results[1]);
    backend.branchHandler = () =>
      fail(httpError('POST', '/branch/session', 404, ['Filial não encontrada']));
    expect(await openBranchSession(registry, backend, 'identity-b', 'b1')).toEqual({
      kind: 'unavailable',
      messages: ['Filial não encontrada'],
    });
  });

  it('shares one registry per process across separately loaded bundles', async () => {
    const first = await import('@/server/session/registry');
    vi.resetModules();
    const second = await import('@/server/session/registry');
    expect(first.sessionRegistry()).toBe(second.sessionRegistry());
  });
});
