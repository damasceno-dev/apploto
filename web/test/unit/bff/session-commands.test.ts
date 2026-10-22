import { randomUUID } from 'node:crypto';
import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { jwt } from '../support/session-fixtures';

const origin = 'http://localhost:3000';
const api = 'http://127.0.0.1:5180';

/** A browser's cookie jar, fed by real Set-Cookie headers. */
class Jar {
  // A fresh browser per test: the registry remembers each device's last committed scope.
  private readonly values = new Map<string, string>([['__Host-loto-device', randomUUID()]]);
  header() {
    return [...this.values].map(([name, value]) => `${name}=${value}`).join('; ');
  }
  take(response: Response) {
    for (const cookie of response.headers.getSetCookie()) {
      const [pair = ''] = cookie.split(';');
      const index = pair.indexOf('=');
      const name = pair.slice(0, index);
      const value = pair.slice(index + 1);
      if (/Max-Age=0/i.test(cookie)) this.values.delete(name);
      else this.values.set(name, value);
    }
  }
  scope(): string | null {
    const raw = this.values.get('__Host-loto-meta');
    if (!raw) return null;
    return (
      JSON.parse(Buffer.from(decodeURIComponent(raw), 'base64url').toString()) as { scope: string }
    ).scope;
  }
  has(name: string) {
    return this.values.has(name);
  }
}

function deferred() {
  let release!: () => void;
  const promise = new Promise<void>((done) => (release = done));
  return { promise, release };
}

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

let loginGate: Promise<void> | null = null;
let logoutCalls = 0;
let loginStarted = 0;

beforeEach(() => {
  vi.stubEnv('LOTO_API_BASE_URL', api);
  vi.resetModules();
  logoutCalls = 0;
  loginStarted = 0;
  loginGate = null;
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const path = new URL(String(input)).pathname;
    if (path === '/user/login') {
      const body = JSON.parse(String(init.body)) as { email: string };
      loginStarted += 1;
      if (loginGate) await loginGate;
      return json({
        userId: body.email,
        name: body.email,
        email: body.email,
        responseToken: {
          token: jwt(`id-${body.email}`, Date.now() + 3_600_000),
          refreshToken: `rt-${body.email}`,
        },
      });
    }
    if (path === '/branch/my-branches') return json({ branches: [] });
    if (path === '/user/logout') {
      logoutCalls += 1;
      return new Response(null, { status: 204 });
    }
    throw new Error(`Unexpected API call ${path}`);
  });
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

function post(path: string, jar: Jar, declared: string | null, body: unknown = {}) {
  return new NextRequest(`${origin}${path}`, {
    method: 'POST',
    body: JSON.stringify(body),
    headers: {
      'content-type': 'application/json',
      'x-loto-bff': '1',
      origin,
      cookie: jar.header(),
      ...(declared === null ? {} : { 'x-loto-scope': declared }),
    },
  });
}

async function routes() {
  const login = (await import('@/app/bff/session/login/route')).POST;
  const logout = (await import('@/app/bff/session/logout/route')).POST;
  return { login, logout };
}

describe('session commands apply only to the authority they were issued against', () => {
  it('a login still at the API when Sair is pressed: Sair waits, then cannot end the newer login', async () => {
    const { login, logout } = await routes();
    const jar = new Jar();
    jar.take(
      await login(
        post('/bff/session/login', jar, null, { email: 'ana@example.invalid', password: 'x' }),
      ),
    );
    const anaScope = jar.scope();
    expect(anaScope).not.toBeNull();

    const gate = deferred();
    loginGate = gate.promise;
    const sentWith = jar.header();
    const bia = login(
      post('/bff/session/login', jar, anaScope, { email: 'bia@example.invalid', password: 'x' }),
    );
    // Sair is pressed while the login is at the API (it holds this browser's command queue).
    await vi.waitFor(() => expect(loginStarted).toBeGreaterThan(1));
    const sair = logout(post('/bff/session/logout', jar, anaScope));
    gate.release();
    const biaResponse = await bia;
    const sairResponse = await sair;
    expect(biaResponse.status).toBe(200);
    expect(sairResponse.status).toBe(409);
    expect(sairResponse.headers.get('x-loto-bff-error')).toBe('session-scope-changed');
    expect(sairResponse.headers.getSetCookie()).toEqual([]);
    expect(logoutCalls).toBe(0);
    jar.take(biaResponse);
    jar.take(sairResponse);
    expect(jar.has('__Host-loto-rt')).toBe(true);
    expect(jar.header()).not.toBe(sentWith);
  });

  it('the same race in the other order: Sair first, then the older login is superseded', async () => {
    const { login, logout } = await routes();
    const jar = new Jar();
    jar.take(
      await login(
        post('/bff/session/login', jar, null, { email: 'ana@example.invalid', password: 'x' }),
      ),
    );
    const anaScope = jar.scope();
    const sair = logout(post('/bff/session/logout', jar, anaScope));
    const bia = login(
      post('/bff/session/login', jar, anaScope, { email: 'bia@example.invalid', password: 'x' }),
    );
    const [sairResponse, biaResponse] = await Promise.all([sair, bia]);
    expect(sairResponse.status).toBe(200);
    expect(biaResponse.status).toBe(409);
    expect(biaResponse.headers.getSetCookie()).toEqual([]);
    jar.take(sairResponse);
    jar.take(biaResponse);
    expect(jar.has('__Host-loto-rt')).toBe(false);
  });

  it('a late login issued before Sair cannot restore the session Sair ended', async () => {
    const { login, logout } = await routes();
    const jar = new Jar();
    jar.take(
      await login(
        post('/bff/session/login', jar, null, { email: 'ana@example.invalid', password: 'x' }),
      ),
    );
    const anaScope = jar.scope();
    const sair = await logout(post('/bff/session/logout', jar, anaScope));
    expect((await sair.json()) as { serverConfirmed: boolean }).toMatchObject({
      serverConfirmed: true,
    });
    const lateCookies = jar.header();
    jar.take(sair);
    // The login request carries the pre-Sair cookies and scope: it is superseded, writes nothing.
    const late = await login(
      new NextRequest(`${origin}/bff/session/login`, {
        method: 'POST',
        body: JSON.stringify({ email: 'bia@example.invalid', password: 'x' }),
        headers: {
          'content-type': 'application/json',
          'x-loto-bff': '1',
          origin,
          cookie: lateCookies,
          'x-loto-scope': anaScope ?? '',
        },
      }),
    );
    expect(late.status).toBe(409);
    expect(late.headers.getSetCookie()).toEqual([]);
    jar.take(late);
    expect(jar.has('__Host-loto-rt')).toBe(false);
  });

  it('a browser without a device id receives one with its first BFF response', async () => {
    const { login } = await routes();
    const response = await login(
      new NextRequest(`${origin}/bff/session/login`, {
        method: 'POST',
        body: JSON.stringify({ email: 'ana@example.invalid', password: 'x' }),
        headers: { 'content-type': 'application/json', 'x-loto-bff': '1', origin },
      }),
    );
    const deviceCookie = response.headers
      .getSetCookie()
      .find((cookie) => cookie.startsWith('__Host-loto-device='));
    expect(deviceCookie).toMatch(/HttpOnly/i);
    expect(deviceCookie).toMatch(/Secure/i);
  });
});
