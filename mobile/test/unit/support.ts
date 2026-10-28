import type { CredentialEnvelope, CredentialStore } from '../../src/session/types';
import { SessionEngine } from '../../src/session/engine';
export const userId = '10000000-0000-4000-8000-000000000001';
export const branchId = '20000000-0000-4000-8000-000000000001';
export const otherBranch = '20000000-0000-4000-8000-000000000002';
export const profile = {
  userId,
  name: 'Pessoa de teste',
  email: 'fixture@example.invalid',
  hasPassword: true,
  isPlatformOperator: false,
};
export const branch = { id: branchId, name: 'Filial de teste', role: 'Admin' as const };
export function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (value: unknown) => void;
  const promise = new Promise<T>((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
}
export const copy = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;
export class MemoryCredentials implements CredentialStore {
  value: CredentialEnvelope | null = null;
  failRead = false;
  failWrite = false;
  failClear = false;
  async read() {
    if (this.failRead) throw new Error('secure read failed');
    return copy(this.value);
  }
  async write(value: CredentialEnvelope) {
    if (this.failWrite) throw new Error('secure write failed');
    this.value = copy(value);
  }
  async clear() {
    if (this.failClear) throw new Error('secure clear failed');
    this.value = null;
  }
}
export function response(body: unknown, status = 200, headers: Record<string, string> = {}) {
  return new Response(status === 204 ? null : JSON.stringify(body), {
    status,
    headers: {
      'content-type': 'application/json',
      date: 'Tue, 27 Oct 2026 12:00:00 GMT',
      ...headers,
    },
  });
}
export function rig() {
  const credentials = new MemoryCredentials();
  const calls: {
    path: string;
    method: string;
    authorization: string | null;
    body: unknown;
    headers: Record<string, string>;
  }[] = [];
  let identity = 'identity-1';
  let refresh = 'refresh-1';
  let branchToken = 'branch-1';
  let selected = branchId;
  let role = 'Admin';
  let date = '2026-10-27';
  let time = 0;
  let interceptor: ((path: string, init: RequestInit) => Promise<Response | undefined>) | null =
    null;
  const transport: typeof fetch = jest.fn(
    async (input: RequestInfo | URL, init: RequestInit = {}) => {
      const path = new URL(String(input)).pathname.toLowerCase();
      const method = init.method ?? 'GET';
      const authorization = new Headers(init.headers).get('authorization');
      const body = typeof init.body === 'string' ? JSON.parse(init.body) : undefined;
      calls.push({
        path,
        method,
        authorization,
        body,
        headers: Object.fromEntries(new Headers(init.headers).entries()),
      });
      const special = await interceptor?.(path, init);
      if (special) return special;
      if (path === '/user/login')
        return response({ ...profile, responseToken: { token: identity, refreshToken: refresh } });
      if (path === '/user/renew-token') {
        if (body.value !== refresh)
          return response({ errorMessages: ['Renovação recusada.'] }, 401);
        identity += 'r';
        refresh += 'r';
        return response({ token: identity, refreshToken: refresh });
      }
      if (path === '/user/logout') return response(null, 204);
      if (path === '/user/profile')
        return authorization === `Bearer ${identity}`
          ? response(profile)
          : response({ errorMessages: ['Sessão inválida.'] }, 401);
      if (path === '/branch/session') {
        selected = body.branchId;
        branchToken += 'b';
        return response({ token: branchToken, branch: { ...branch, id: selected, role } });
      }
      if (path === '/branch/current')
        return authorization === `Bearer ${branchToken}`
          ? response({
              branch: { ...branch, id: selected, role },
              branchLocalDate: date,
              branchLocalDateTime: `${date}T09:00:00`,
            })
          : response({ errorMessages: ['Sessão inválida.'] }, 401);
      if (path === '/transaction')
        return response({ transaction: { id: '30000000-0000-4000-8000-000000000001' } }, 201, {
          etag: '"123"',
        });
      return response({ errorMessages: ['Não autorizado.'] }, 403);
    },
  );
  const options = {
    baseUrl: 'http://fixture.invalid',
    credentials,
    fetch: transport,
    monotonicNow: () => time,
    clearSelections: jest.fn(async () => {}),
  };
  const engine = new SessionEngine(options);
  return {
    engine,
    credentials,
    calls,
    transport,
    options,
    intercept: (value: typeof interceptor) => {
      interceptor = value;
    },
    expire: () => {
      identity += 'expired';
      branchToken += 'expired';
    },
    role: (value: string) => {
      role = value;
    },
    date: (value: string) => {
      date = value;
    },
    advance: (milliseconds: number) => {
      time += milliseconds;
    },
    login: async () => {
      await engine.login({ email: profile.email, password: 'test-only-password' });
    },
    ready: async () => {
      await engine.login({ email: profile.email, password: 'test-only-password' });
      await engine.selectBranch(branchId);
    },
  };
}
