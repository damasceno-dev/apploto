import { randomUUID } from 'node:crypto';
import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { B1, B2, jwt } from '../support/session-fixtures';

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
/** Memberships by e-mail; `POST /branch/session` answers 404 outside them, as the API does. */
let memberships: Record<string, readonly string[]> = {};

beforeEach(() => {
  vi.stubEnv('LOTO_API_BASE_URL', api);
  vi.resetModules();
  logoutCalls = 0;
  loginStarted = 0;
  loginGate = null;
  memberships = {};
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
    const bearer = new Headers(init.headers).get('authorization') ?? '';
    const caller = /id-([^.#]+@example\.invalid)/.exec(
      Buffer.from(bearer.split('.')[1] ?? '', 'base64url').toString(),
    )?.[1];
    const mine = memberships[caller ?? ''] ?? [];
    if (path === '/branch/my-branches') {
      return json({
        branches: mine.map((id) => ({ id, name: `Filial ${id.slice(0, 2)}`, role: 'Admin' })),
      });
    }
    if (path === '/branch/session') {
      const { branchId } = JSON.parse(String(init.body)) as { branchId: string };
      if (!mine.includes(branchId)) return json({ errorMessages: ['Filial não encontrada.'] }, 404);
      return json({
        token: jwt(`br-${caller}-${branchId}`, Date.now() + 3_600_000),
        branch: { id: branchId, name: `Filial ${branchId.slice(0, 2)}`, role: 'Admin' },
      });
    }
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
  const select = (await import('@/app/bff/session/branch/route')).POST;
  const read = (await import('@/app/bff/session/route')).GET;
  return { login, logout, select, read };
}

/** A tab's `GET /bff/session` for its route branch. */
function readBranch(jar: Jar, branchId: string) {
  return new NextRequest(`${origin}/bff/session`, {
    headers: { 'x-loto-bff': '1', cookie: jar.header(), 'x-loto-branch': branchId },
  });
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

describe('two tabs of one browser keep two branches (D-M2-1)', () => {
  it('opening a branch in one tab never replaces the other tab’s branch session', async () => {
    const { login, logout, select, read } = await routes();
    memberships = { 'ana@example.invalid': [B1, B2] };
    const jar = new Jar();
    const signedIn = await login(
      post('/bff/session/login', jar, null, { email: 'ana@example.invalid', password: 'x' }),
    );
    jar.take(signedIn);
    expect(await signedIn.json()).toMatchObject({ state: 'choose-branch' });
    const identityScope = jar.scope();

    // Tab A opens Alfa, tab B opens Beta: each answer is that branch; the identity scope stays.
    const tabA = await select(post('/bff/session/branch', jar, identityScope, { branchId: B1 }));
    jar.take(tabA);
    const tabB = await select(post('/bff/session/branch', jar, identityScope, { branchId: B2 }));
    jar.take(tabB);
    const a = (await tabA.json()) as { state: string; scope: string; branch: { id: string } };
    const b = (await tabB.json()) as { state: string; scope: string; branch: { id: string } };
    expect([a.branch.id, b.branch.id]).toEqual([B1, B2]);
    expect(jar.scope()).toBe(identityScope);
    expect(jar.has(`__Host-loto-b-${B1}`) && jar.has(`__Host-loto-b-${B2}`)).toBe(true);

    // Each tab re-reads its own branch and finds it unchanged.
    const readA = (await (await read(readBranch(jar, B1))).json()) as typeof a;
    const readB = (await (await read(readBranch(jar, B2))).json()) as typeof b;
    expect(readA).toMatchObject({ state: 'branch', scope: a.scope, branch: { id: B1 } });
    expect(readB).toMatchObject({ state: 'branch', scope: b.scope, branch: { id: B2 } });

    // A command issued from a branch tab declares its whole scope; the identity part decides.
    const reopened = await select(post('/bff/session/branch', jar, a.scope, { branchId: B1 }));
    expect(reopened.status).toBe(200);
    jar.take(reopened);

    // Sair in either tab ends the identity and every branch session it held.
    const before = jar.header();
    const sair = await logout(post('/bff/session/logout', jar, b.scope));
    expect(sair.status).toBe(200);
    jar.take(sair);
    expect(jar.has(`__Host-loto-b-${B1}`) || jar.has(`__Host-loto-b-${B2}`)).toBe(false);
    const replayed = await read(
      new NextRequest(`${origin}/bff/session`, {
        headers: { 'x-loto-bff': '1', cookie: before, 'x-loto-branch': B1 },
      }),
    );
    expect(await replayed.json()).toMatchObject({ state: 'anonymous' });
  });

  it('another person signed in on the same browser cannot use the old address’s branch', async () => {
    const { login, read } = await routes();
    memberships = { 'ana@example.invalid': [B1], 'bia@example.invalid': [B2] };
    const jar = new Jar();
    jar.take(
      await login(
        post('/bff/session/login', jar, null, { email: 'ana@example.invalid', password: 'x' }),
      ),
    );
    expect(jar.has(`__Host-loto-b-${B1}`)).toBe(true);
    const anaScope = jar.scope();
    const bia = await login(
      post('/bff/session/login', jar, anaScope, { email: 'bia@example.invalid', password: 'x' }),
    );
    jar.take(bia);
    // Bia's login dropped Ana's branch session; Bia's own session for Alfa's address is refused.
    expect(jar.has(`__Host-loto-b-${B1}`)).toBe(false);
    const oldAddress = await read(readBranch(jar, B1));
    expect(await oldAddress.json()).toMatchObject({
      state: 'branch-unavailable',
      branch: null,
      messages: ['Filial não encontrada.'],
    });
  });
});
