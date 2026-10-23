import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { B1, jwt } from '../support/session-fixtures';

const origin = 'http://localhost:3000';
const api = 'http://127.0.0.1:5180';

/** A fake API behind the real route handlers (they call the generated raw functions). */
function stubApi(routes: Record<string, (init: RequestInit) => Response>) {
  const calls: string[] = [];
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = new URL(String(input));
    const key = `${init.method ?? 'GET'} ${url.pathname}`;
    calls.push(key);
    const route = routes[key];
    if (!route) throw new Error(`Unexpected API call ${key}`);
    return route(init);
  });
  return calls;
}

const json = (body: unknown, status = 200, headers: HeadersInit = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });

function post(path: string, body: unknown, headers: HeadersInit = {}) {
  return new NextRequest(`${origin}${path}`, {
    method: 'POST',
    body: JSON.stringify(body),
    headers: { 'content-type': 'application/json', 'x-loto-bff': '1', origin, ...headers },
  });
}

function setCookies(response: Response): string[] {
  return response.headers.getSetCookie();
}

beforeEach(() => {
  vi.stubEnv('LOTO_API_BASE_URL', api);
  vi.resetModules();
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

describe('login route', () => {
  it('sets host-only HttpOnly Secure SameSite=Lax cookies and returns no token or password', async () => {
    const identity = jwt('identity', Date.now() + 3_600_000);
    const branchToken = jwt('branch', Date.now() + 3_600_000);
    stubApi({
      'POST /user/login': () =>
        json({
          userId: 'u1',
          name: 'Ana',
          email: 'ana@example.invalid',
          responseToken: { token: identity, refreshToken: 'refresh+/=value' },
        }),
      'GET /branch/my-branches': () =>
        json({ branches: [{ id: B1, name: 'Centro', role: 'Member' }] }),
      'POST /branch/session': () =>
        json({ token: branchToken, branch: { id: B1, name: 'Centro', role: 'Member' } }),
    });
    const { POST } = await import('@/app/bff/session/login/route');
    const response = await POST(
      post('/bff/session/login', { email: 'ana@example.invalid', password: 'p@ss-secret' }),
    );
    expect(response.status).toBe(200);
    expect(response.headers.get('cache-control')).toBe('no-store, private');
    const body = await response.text();
    for (const secret of [identity, branchToken, 'refresh+/=value', 'p@ss-secret']) {
      expect(body).not.toContain(secret);
    }
    expect(JSON.parse(body)).toMatchObject({
      state: 'branch',
      user: { email: 'ana@example.invalid' },
    });
    const cookies = setCookies(response);
    expect(cookies.length).toBeGreaterThanOrEqual(4);
    // The opened branch lives in its own cookie, named after the branch.
    expect(cookies.some((cookie) => cookie.startsWith(`__Host-loto-b-${B1}=`))).toBe(true);
    for (const cookie of cookies) {
      expect(cookie).toMatch(/^__Host-loto-/);
      expect(cookie).toMatch(/; HttpOnly/i);
      expect(cookie).toMatch(/; Secure/i);
      expect(cookie).toMatch(/; SameSite=lax/i);
      expect(cookie).toMatch(/; Path=\//i);
      expect(cookie).not.toMatch(/Domain=/i);
    }
    // The refresh token round-trips through cookie encoding unchanged.
    const refresh = cookies.find((cookie) => cookie.startsWith('__Host-loto-rt='));
    expect(decodeURIComponent(refresh?.split(';')[0]?.split('=').slice(1).join('=') ?? '')).toBe(
      'refresh+/=value',
    );
  });

  it('passes a 429 through with verbatim messages and Retry-After, setting no cookie', async () => {
    stubApi({
      'POST /user/login': () =>
        json({ errorMessages: ['Muitas tentativas. Tente mais tarde.'] }, 429, {
          'retry-after': '900',
        }),
    });
    const { POST } = await import('@/app/bff/session/login/route');
    const response = await POST(
      post('/bff/session/login', { email: 'a@example.invalid', password: 'x' }),
    );
    expect(response.status).toBe(429);
    expect(response.headers.get('retry-after')).toBe('900');
    expect(await response.json()).toEqual({
      errorMessages: ['Muitas tentativas. Tente mais tarde.'],
    });
    // Only the (non-secret) device id a first-time browser receives; no session cookie changes.
    expect(setCookies(response).map((cookie) => cookie.split('=')[0])).toEqual([
      '__Host-loto-device',
    ]);
  });

  it('refuses a cross-origin or header-less login before calling the API', async () => {
    const calls = stubApi({});
    const { POST } = await import('@/app/bff/session/login/route');
    const crossOrigin = await POST(
      post('/bff/session/login', {}, { origin: 'https://evil.example' }),
    );
    expect(crossOrigin.status).toBe(403);
    const noHeader = new NextRequest(`${origin}/bff/session/login`, {
      method: 'POST',
      body: '{}',
      headers: { 'content-type': 'application/json', origin },
    });
    expect((await POST(noHeader)).status).toBe(400);
    const form = await POST(
      post('/bff/session/login', {}, { 'content-type': 'application/x-www-form-urlencoded' }),
    );
    expect(form.status).toBe(415);
    expect(calls).toEqual([]);
  });
});

describe('logout route', () => {
  it('revokes, then deletes every session cookie with the attributes __Host- deletion needs', async () => {
    const identity = jwt('identity', Date.now() + 3_600_000);
    let revoked: unknown = null;
    stubApi({
      'POST /user/logout': (init) => {
        revoked = JSON.parse(String(init.body));
        return new Response(null, { status: 204 });
      },
    });
    const meta = Buffer.from(
      JSON.stringify({
        v: 2,
        sid: 's',
        scope: 'c',
        user: { userId: 'u', name: 'Ana', email: 'ana@example.invalid' },
      }),
    ).toString('base64url');
    const slot = Buffer.from(
      JSON.stringify({
        v: 1,
        sid: 's',
        gen: 'g',
        token: jwt('branch', Date.now() + 3_600_000),
        branch: { id: B1, name: 'Centro', role: 'Admin' },
        at: 0,
      }),
    ).toString('base64url');
    const request = post(
      '/bff/session/logout',
      {},
      {
        cookie: `__Host-loto-device=00000000-0000-4000-8000-0000000000aa; __Host-loto-id=${identity}; __Host-loto-rt=refresh-1; __Host-loto-meta=${meta}; __Host-loto-b-${B1}=${slot}`,
        'x-loto-scope': 'c',
      },
    );
    const { POST } = await import('@/app/bff/session/logout/route');
    const response = await POST(request);
    expect(revoked).toEqual({ refreshToken: 'refresh-1' });
    expect(await response.json()).toMatchObject({ state: 'anonymous', serverConfirmed: true });
    const cookies = setCookies(response);
    // identity, refresh, meta, intent and every branch cookie the browser holds.
    expect(cookies).toHaveLength(5);
    expect(cookies.some((cookie) => cookie.startsWith(`__Host-loto-b-${B1}=;`))).toBe(true);
    for (const cookie of cookies) {
      expect(cookie).toMatch(/^__Host-loto-[a-z0-9-]+=;/);
      expect(cookie).toMatch(/Max-Age=0/i);
      expect(cookie).toMatch(/; Secure/i);
      expect(cookie).toMatch(/; Path=\//i);
    }
  });
});

describe('forwarder route', () => {
  it('serves only the generated allowlist and relays the backend answer', async () => {
    const calls = stubApi({ 'GET /invitation/preview': () => json({}) });
    const { GET } = await import('@/app/bff/api/[...path]/route');
    const response = await GET(
      new NextRequest(`${origin}/bff/api/user/login`, { headers: { 'x-loto-bff': '1' } }),
    );
    expect(response.status).toBe(404);
    expect(response.headers.get('x-loto-bff-error')).toBe('unknown-operation');
    expect(calls).toEqual([]);
  });

  it('keeps the API Date header through the Next response', async () => {
    const date = 'Wed, 21 Oct 2026 12:00:00 GMT';
    stubApi({ 'POST /invitation/preview': () => json({}, 200, { date }) });
    const { POST } = await import('@/app/bff/api/[...path]/route');
    const response = await POST(
      new NextRequest(`${origin}/bff/api/invitation/preview`, {
        method: 'POST',
        body: '{}',
        headers: { 'x-loto-bff': '1', origin, 'content-type': 'application/json' },
      }),
    );
    expect(response.status).toBe(200);
    expect(response.headers.get('date')).toBe(date);
  });
});
