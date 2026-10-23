import { describe, expect, it } from 'vitest';
import { forward, targetUrl, type ForwarderConfig } from '@/server/bff/forward';
import { cookieNames, slotCookieName, type CookieWrite } from '@/server/session/cookies';
import {
  B1,
  B2,
  fail,
  httpError,
  jwt,
  meta,
  networkError,
  sessionCookies,
  setup,
  slot,
} from '../support/session-fixtures';

const origin = 'http://localhost:3000';
const api = 'http://127.0.0.1:5180';

interface Sent {
  readonly url: string;
  readonly method: string;
  readonly headers: Headers;
  readonly body: string | null;
  readonly signal: AbortSignal | null;
}

function harness(responder: (sent: Sent, index: number) => Response | Promise<Response>) {
  const env = setup();
  const sent: Sent[] = [];
  const fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const record: Sent = {
      url: String(input),
      method: init?.method ?? 'GET',
      headers: new Headers(init?.headers),
      body: init?.body ? new TextDecoder().decode(init.body as ArrayBuffer) : null,
      signal: init?.signal ?? null,
    };
    sent.push(record);
    return responder(record, sent.length - 1);
  }) as typeof globalThis.fetch;
  const identity = jwt('identity-ana', env.clock.ms + 3_600_000);
  const branchToken = jwt('branch-ana', env.clock.ms + 3_600_000);
  const cookies = sessionCookies({
    identity,
    refresh: 'refresh-ana',
    meta: meta(),
    slots: [slot(B1, branchToken)],
  });
  const config = (): ForwarderConfig => ({
    context: env.context(),
    apiBaseUrl: api,
    allowedOrigin: origin,
    fetch,
  });
  return { ...env, sent, cookies, identity, branchToken, config };
}

/** A request from a tab working in branch B1 (its scope: identity generation + B1's generation). */
function request(
  path: string,
  init: RequestInit & { scope?: string | null; branch?: string | null } = {},
): Request {
  const headers = new Headers(init.headers);
  if (!headers.has('x-loto-bff')) headers.set('x-loto-bff', '1');
  const scope = init.scope === undefined ? 'scope-1.gen-11' : init.scope;
  if (scope !== null) headers.set('x-loto-scope', scope);
  const branch = init.branch === undefined ? B1 : init.branch;
  if (branch !== null) headers.set('x-loto-branch', branch);
  const method = init.method ?? 'GET';
  if (method !== 'GET' && !headers.has('origin')) headers.set('origin', origin);
  return new Request(`${origin}/bff/api${path}`, { ...init, method, headers });
}

const json = (body: unknown, status = 200, headers: HeadersInit = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', ...headers },
  });

describe('forwarder target constraints', () => {
  it('refuses unknown operations, methods and malformed paths without dispatching', async () => {
    const h = harness(() => json({}));
    const cases: [string, RequestInit][] = [
      ['/user/login', { method: 'POST', body: '{}' }],
      ['/user/renew-token', { method: 'POST', body: '{}' }],
      ['/branch/session', { method: 'POST', body: '{}' }],
      ['/invitation/register', { method: 'POST', body: '{}' }],
      ['/branch/current', { method: 'DELETE' }],
      ['/not-an-operation', {}],
      ['/../../etc/passwd', {}],
      ['/transaction/a%2Fb', {}],
      ['/transaction/a%5Cb', {}],
      ['//evil.example/branch/current', {}],
      ['/transaction//x', {}],
    ];
    for (const [path, init] of cases) {
      const { response } = await forward(request(path, init), h.cookies, h.config());
      expect(response.headers.get('x-loto-bff-error'), path).toMatch(
        /unknown-operation|invalid-path/,
      );
    }
    expect(h.sent).toHaveLength(0);
  });

  it('resolves dot segments before matching, so they can only name another allowlisted operation', async () => {
    const h = harness(() => json({}));
    await forward(request('/branch/../user/profile'), h.cookies, h.config());
    expect(h.sent[0]?.url).toBe(`${api}/user/profile`);
  });

  it('never builds a URL outside the configured API', () => {
    expect(targetUrl('http://api.internal:8080/base/', '/branch/current?x=1').href).toBe(
      'http://api.internal:8080/base/branch/current?x=1',
    );
    expect(targetUrl(api, '/transaction?accountId=7&mine=true').origin).toBe(api);
  });

  it('refuses state changes without the BFF header, from another origin or a cross-site context', async () => {
    const h = harness(() => json({}));
    const post = (headers: HeadersInit) =>
      forward(
        request('/client', {
          method: 'POST',
          body: '{}',
          headers: { 'content-type': 'application/json', ...headers },
        }),
        h.cookies,
        h.config(),
      );
    const missingHeader = new Request(`${origin}/bff/api/client`, {
      method: 'POST',
      headers: { origin, 'x-loto-scope': 'scope-1' },
      body: '{}',
    });
    expect((await forward(missingHeader, h.cookies, h.config())).response.status).toBe(400);
    expect(
      (await post({ origin: 'https://evil.example' })).response.headers.get('x-loto-bff-error'),
    ).toBe('forbidden-origin');
    expect(
      (await post({ 'sec-fetch-site': 'cross-site' })).response.headers.get('x-loto-bff-error'),
    ).toBe('forbidden-origin');
    expect(h.sent).toHaveLength(0);
  });

  it('refuses browser-supplied authority and a scope that is no longer current', async () => {
    const h = harness(() => json({}));
    const withBearer = await forward(
      request('/branch/current', { headers: { authorization: 'Bearer stolen' } }),
      h.cookies,
      h.config(),
    );
    expect(withBearer.response.headers.get('x-loto-bff-error')).toBe('authorization-not-allowed');
    const otherScope = await forward(
      request('/branch/current', { scope: 'scope-old' }),
      h.cookies,
      h.config(),
    );
    expect(otherScope.response.status).toBe(409);
    expect(otherScope.response.headers.get('x-loto-bff-error')).toBe('session-scope-changed');
    expect(h.sent).toHaveLength(0);
  });

  it('rejects a body above 1 MiB before dispatch', async () => {
    const h = harness(() => json({}));
    const big = 'x'.repeat(1024 * 1024 + 1);
    const { response } = await forward(
      request('/client', {
        method: 'POST',
        body: big,
        headers: { 'content-type': 'application/json' },
      }),
      h.cookies,
      h.config(),
    );
    expect(response.status).toBe(413);
    expect(h.sent).toHaveLength(0);
  });
});

describe('forwarder fidelity', () => {
  it('sends the tier’s bearer and only contract headers; returns status, bytes and contract headers', async () => {
    const h = harness(() =>
      json(
        { errorMessages: ['Versão desatualizada', 'Versão desatualizada', 'Outra mensagem'] },
        409,
        {
          etag: '"42"',
          'retry-after': '3',
          'set-cookie': 'backend=1',
          connection: 'close',
          server: 'Kestrel',
        },
      ),
    );
    const { response } = await forward(
      request('/transaction/7c9e/cancel', {
        method: 'POST',
        body: '{"reason":"x"}',
        headers: {
          'content-type': 'application/json',
          'if-match': '"41"',
          'idempotency-key': 'not-declared-here',
          cookie: `${cookieNames.identity}=leak`,
          'x-forwarded-for': '10.0.0.1',
          'keep-alive': 'timeout=5',
        },
      }),
      h.cookies,
      h.config(),
    );
    const [sent] = h.sent;
    expect(sent?.url).toBe(`${api}/transaction/7c9e/cancel`);
    expect(sent?.headers.get('authorization')).toBe(`Bearer ${h.branchToken}`);
    expect(sent?.headers.get('if-match')).toBe('"41"');
    expect(sent?.headers.has('idempotency-key')).toBe(false);
    expect(sent?.headers.has('cookie')).toBe(false);
    expect(sent?.headers.has('x-forwarded-for')).toBe(false);
    expect(sent?.headers.has('keep-alive')).toBe(false);
    expect(sent?.body).toBe('{"reason":"x"}');

    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({
      errorMessages: ['Versão desatualizada', 'Versão desatualizada', 'Outra mensagem'],
    });
    expect(response.headers.get('etag')).toBe('"42"');
    expect(response.headers.get('retry-after')).toBe('3');
    expect(response.headers.get('cache-control')).toBe('no-store, private');
    expect(response.headers.has('set-cookie')).toBe(false);
    expect(response.headers.has('server')).toBe(false);
    expect(response.headers.has('x-loto-bff-error')).toBe(false);
  });

  it('forwards Idempotency-Key byte for byte where the operation declares it, with the query string', async () => {
    const h = harness(() => json({ id: 't1' }, 201));
    await forward(
      request('/transaction', {
        method: 'POST',
        body: '{}',
        headers: { 'content-type': 'application/json', 'idempotency-key': ' 3f2c-Key ' },
      }),
      h.cookies,
      h.config(),
    );
    expect(h.sent[0]?.headers.get('idempotency-key')).toBe('3f2c-Key');
    await forward(request('/transaction?mine=true&page=2'), h.cookies, h.config());
    expect(h.sent[1]?.url).toBe(`${api}/transaction?mine=true&page=2`);
  });

  it('uses the identity token for identity work and none for anonymous work', async () => {
    const h = harness(() => json({ branches: [] }));
    await forward(request('/branch/my-branches'), h.cookies, h.config());
    expect(h.sent[0]?.headers.get('authorization')).toBe(`Bearer ${h.identity}`);
    await forward(
      request('/user/forgot-password', {
        method: 'POST',
        body: '{}',
        scope: null,
        headers: { 'content-type': 'application/json' },
      }),
      sessionCookies({}),
      h.config(),
    );
    expect(h.sent[1]?.headers.has('authorization')).toBe(false);
  });

  it('never exposes a token in the response it relays', async () => {
    const h = harness(() => json({ ok: true }));
    const { response, writes } = await forward(request('/branch/current'), h.cookies, h.config());
    const text = await response.text();
    const headerText = JSON.stringify([...response.headers]);
    for (const secret of [h.identity, h.branchToken, 'refresh-ana']) {
      expect(text).not.toContain(secret);
      expect(headerText).not.toContain(secret);
    }
    expect(writes).toEqual([]);
  });

  it('reports a lost upstream answer to a write as 502 without the not-forwarded marker', async () => {
    const h = harness(() => {
      throw new TypeError('socket hang up');
    });
    const { response } = await forward(
      request('/transaction', {
        method: 'POST',
        body: '{}',
        headers: { 'content-type': 'application/json', 'idempotency-key': 'k' },
      }),
      h.cookies,
      h.config(),
    );
    expect(response.status).toBe(502);
    expect(response.headers.has('x-loto-bff-error')).toBe(false);
  });

  it('propagates browser cancellation to the API request', async () => {
    const h = harness(() => json({}));
    const controller = new AbortController();
    await forward(request('/branch/current', { signal: controller.signal }), h.cookies, h.config());
    controller.abort();
    expect(h.sent[0]?.signal?.aborted).toBe(true);
  });
});

describe('review fixes', () => {
  it('relays the API Date header (the replay-window clock) unchanged', async () => {
    const date = 'Wed, 21 Oct 2026 12:00:00 GMT';
    const h = harness(() => json({}, 200, { date }));
    const { response } = await forward(request('/branch/current'), h.cookies, h.config());
    expect(response.headers.get('date')).toBe(date);
  });

  it('pauses a write — but not a read — while a renewal outcome is unknown', async () => {
    const h = harness(() => json({}));
    h.backend.renewHandler = () => fail(networkError('POST', '/user/renew-token'));
    const expiring = { ...h.cookies, identity: jwt('old', h.clock.ms + 10_000) };
    const write = await forward(
      request('/branch/my-branches', { method: 'GET' }),
      expiring,
      h.config(),
    );
    expect(write.response.status).toBe(200);
    const refused = await forward(
      request('/client', {
        method: 'POST',
        body: '{}',
        headers: { 'content-type': 'application/json' },
      }),
      { ...expiring, slots: new Map([[B1, slot(B1, jwt('br', h.clock.ms + 10_000))]]) },
      h.config(),
    );
    expect(refused.response.headers.get('x-loto-bff-error')).toBe('renewal-unconfirmed');
    expect(h.sent.map((sent) => sent.method)).toEqual(['GET']);
  });
});

describe('forwarder renewal policy', () => {
  const unauthorized = () => json({ errorMessages: ['Token expirado'] }, 401);

  it('renews proactively before dispatch so an expiring token is never sent', async () => {
    const h = harness(() => json({}));
    const expiring = { ...h.cookies, identity: jwt('old', h.clock.ms + 10_000) };
    const { writes } = await forward(request('/branch/my-branches'), expiring, h.config());
    expect(h.backend.count('renew')).toBe(1);
    expect(h.sent[0]?.headers.get('authorization')).not.toBe(`Bearer ${expiring.identity}`);
    expect(writes.map((write) => write.name)).toEqual([cookieNames.identity, cookieNames.refresh]);
  });

  it('recovers a GET once after a 401 and returns the second answer', async () => {
    const h = harness((_sent, index) => (index === 0 ? unauthorized() : json({ branch: 'ok' })));
    const { response, writes } = await forward(request('/branch/current'), h.cookies, h.config());
    expect(h.sent).toHaveLength(2);
    expect(h.sent[1]?.headers.get('authorization')).not.toBe(
      h.sent[0]?.headers.get('authorization'),
    );
    expect(response.status).toBe(200);
    expect(response.headers.get('x-loto-session')).toBe('renewed');
    expect(writes.some((write: CookieWrite) => write.name === cookieNames.refresh)).toBe(true);
  });

  it('never replays a write after a 401: one dispatch, the 401 verbatim, session renewed for later', async () => {
    const h = harness(() => unauthorized());
    const { response, writes } = await forward(
      request('/transaction', {
        method: 'POST',
        body: '{"value":10}',
        headers: { 'content-type': 'application/json', 'idempotency-key': 'k-1' },
      }),
      h.cookies,
      h.config(),
    );
    expect(h.sent).toHaveLength(1);
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ errorMessages: ['Token expirado'] });
    expect(response.headers.get('x-loto-session')).toBe('renewed');
    expect(response.headers.has('x-loto-bff-error')).toBe(false);
    expect(writes.some((write) => write.name === cookieNames.identity)).toBe(true);
  });

  it('stops after a second 401: the branch gate takes over and nothing loops', async () => {
    const h = harness(() => unauthorized());
    const { response, writes } = await forward(request('/branch/current'), h.cookies, h.config());
    expect(h.sent).toHaveLength(2);
    expect(h.backend.count('renew')).toBe(1);
    expect(response.headers.get('x-loto-session')).toBe('branch-unavailable');
    expect(writes.findLast((write) => write.name === slotCookieName(B1))?.value).toBeNull();
    // Only this tab's branch is closed; the identity stays.
    expect(writes.some((write) => write.name === cookieNames.refresh && write.value === null)).toBe(
      false,
    );
  });

  it('coalesces a burst of concurrent 401s into one renewal', async () => {
    const h = harness((sent) =>
      sent.headers.get('authorization') === `Bearer ${h.branchToken}` ? unauthorized() : json({}),
    );
    const responses = await Promise.all(
      Array.from({ length: 12 }, () => forward(request('/branch/current'), h.cookies, h.config())),
    );
    expect(h.backend.count('renew')).toBe(1);
    expect(h.backend.count('createBranchSession')).toBe(1);
    expect(responses.every(({ response }) => response.status === 200)).toBe(true);
  });

  it('refuses before forwarding when renewal is definitively refused', async () => {
    const h = harness(() => json({}));
    h.backend.renewHandler = () =>
      fail(httpError('POST', '/user/renew-token', 401, ['Refresh token inválido']));
    const expired = { ...h.cookies, identity: jwt('old', h.clock.ms - 1) };
    const { response, writes } = await forward(
      request('/client', {
        method: 'POST',
        body: '{}',
        headers: { 'content-type': 'application/json' },
      }),
      expired,
      h.config(),
    );
    expect(response.headers.get('x-loto-bff-error')).toBe('session-lost');
    expect(h.sent).toHaveLength(0);
    expect(writes.every((write) => write.value === null)).toBe(true);
  });
});

describe('the tab’s branch selects the credential (D-M2-1)', () => {
  it('each tab’s request carries its own branch’s bearer, never another tab’s', async () => {
    const h = harness(() => json({}));
    const other = jwt('branch-b2', h.clock.ms + 3_600_000);
    const cookies = sessionCookies({
      identity: h.identity,
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, h.branchToken), slot(B2, other)],
    });
    await forward(request('/branch/current'), cookies, h.config());
    await forward(
      request('/branch/current', { branch: B2, scope: 'scope-1.gen-22' }),
      cookies,
      h.config(),
    );
    expect(h.sent.map((sent) => sent.headers.get('authorization'))).toEqual([
      `Bearer ${h.branchToken}`,
      `Bearer ${other}`,
    ]);
    expect(h.backend.count('createBranchSession')).toBe(0);
  });

  it('branch work without a branch, or with a malformed one, is refused before dispatch', async () => {
    const h = harness(() => json({}));
    const none = await forward(request('/branch/current', { branch: null }), h.cookies, h.config());
    expect(none.response.headers.get('x-loto-bff-error')).toBe('no-branch-session');
    const identityScopeOnly = await forward(
      request('/branch/current', { scope: 'scope-1' }),
      h.cookies,
      h.config(),
    );
    expect(identityScopeOnly.response.headers.get('x-loto-bff-error')).toBe('no-branch-session');
    const malformed = await forward(
      request('/branch/current', { branch: '../b1' }),
      h.cookies,
      h.config(),
    );
    expect(malformed.response.status).toBe(400);
    expect(malformed.response.headers.get('x-loto-bff-error')).toBe('invalid-branch');
    expect(h.sent).toHaveLength(0);
  });

  it('a branch this identity cannot open answers 403 and nothing is forwarded', async () => {
    const h = harness(() => json({}));
    h.backend.branchHandler = () =>
      fail(httpError('POST', '/branch/session', 404, ['Filial não encontrada']));
    const { response, writes } = await forward(
      request('/branch/current', { branch: B2, scope: 'scope-1.gen-22' }),
      h.cookies,
      h.config(),
    );
    expect(response.status).toBe(403);
    expect(response.headers.get('x-loto-bff-error')).toBe('branch-unavailable');
    expect(h.sent).toHaveLength(0);
    // B1, the other tab's branch, is untouched.
    expect(writes.some((write) => write.name === slotCookieName(B1))).toBe(false);
  });

  it('a tab still on an older branch generation is refused; its identity work is not', async () => {
    const h = harness(() => json({ branches: [] }));
    const branchWork = await forward(
      request('/branch/current', { scope: 'scope-1.gen-old' }),
      h.cookies,
      h.config(),
    );
    expect(branchWork.response.status).toBe(409);
    expect(branchWork.response.headers.get('x-loto-bff-error')).toBe('session-scope-changed');
    const identityWork = await forward(
      request('/branch/my-branches', { scope: 'scope-1.gen-old' }),
      h.cookies,
      h.config(),
    );
    expect(identityWork.response.status).toBe(200);
    expect(h.sent.map((sent) => new URL(sent.url).pathname)).toEqual(['/branch/my-branches']);
  });

  it('a write prepared in one branch keeps its branch: it never goes out under another', async () => {
    const h = harness(() => json({}, 201));
    const other = jwt('branch-b2', h.clock.ms + 3_600_000);
    const cookies = sessionCookies({
      identity: h.identity,
      refresh: 'refresh-ana',
      meta: meta(),
      slots: [slot(B1, h.branchToken), slot(B2, other)],
    });
    await forward(
      request('/transaction', {
        method: 'POST',
        body: '{"value":"1.00"}',
        headers: { 'content-type': 'application/json', 'idempotency-key': 'k-b1' },
      }),
      cookies,
      h.config(),
    );
    expect(h.sent[0]?.headers.get('authorization')).toBe(`Bearer ${h.branchToken}`);
    expect(h.sent[0]?.headers.get('idempotency-key')).toBe('k-b1');
  });
});
