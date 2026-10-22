// @vitest-environment jsdom
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { postTransaction } from '@loto/api';
import { useGetBranchCurrent } from '@loto/api/queries';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useDeviceDraft } from '@/client/drafts/use-device-draft';
import { ScopedQueryProvider } from '@/client/query/scoped-query-provider';
import { SessionProvider, useSession } from '@/client/session/session-provider';
import { resetServerTime, sessionSignalEvent } from '@/client/bff-fetch';
import { useIdempotentOperation } from '@/client/mutations/use-idempotent-operation';
import { anonymousSnapshot, type SessionSnapshot } from '@/lib/bff-protocol';

vi.mock('next/navigation', () => ({ usePathname: () => '/' }));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

function branchSnapshot(email: string, branchId: string, scope: string): SessionSnapshot {
  return {
    ...anonymousSnapshot,
    state: 'branch',
    scope,
    user: { userId: `u-${email}`, name: email, email },
    branch: { id: branchId, name: `Filial ${branchId}`, role: 'Manager' },
  };
}

interface Pending {
  readonly url: string;
  readonly init: RequestInit;
  readonly resolve: (response: Response) => void;
}

function controlledFetch() {
  const pending: Pending[] = [];
  vi.stubGlobal(
    'fetch',
    (input: RequestInfo | URL, init: RequestInit = {}) =>
      new Promise<Response>((resolve, reject) => {
        init.signal?.addEventListener('abort', () =>
          reject(new DOMException('aborted', 'AbortError')),
        );
        pending.push({ url: String(input), init, resolve });
      }),
  );
  return pending;
}

const current = (branchName: string) =>
  new Response(
    JSON.stringify({
      branch: { id: branchName, name: branchName, role: 'Manager' },
      branchLocalDate: '2026-10-21',
      branchLocalDateTime: '2026-10-21T10:00:00',
    }),
    { status: 200, headers: { 'content-type': 'application/json' } },
  );

function BranchName() {
  const query = useGetBranchCurrent();
  return <p>{query.data ? `filial:${query.data.data.branch.name}` : 'carregando'}</p>;
}

describe('per-scope query isolation', () => {
  it('a late response from the previous user/branch never reaches the new scope', async () => {
    const pending = controlledFetch();
    const { rerender } = render(
      <ScopedQueryProvider scope="scope-ana-b1">
        <BranchName />
      </ScopedQueryProvider>,
    );
    await waitFor(() => expect(pending).toHaveLength(1));
    const first = pending[0]!;
    const firstHeaders = new Headers(first.init.headers);
    expect(first.url).toBe('/bff/api/branch/current');
    expect(firstHeaders.get('x-loto-scope')).toBe('scope-ana-b1');
    expect(firstHeaders.get('x-loto-bff')).toBe('1');
    expect(firstHeaders.has('authorization')).toBe(false);
    expect(first.init.credentials).toBe('same-origin');

    rerender(
      <ScopedQueryProvider scope="scope-bia-b2">
        <BranchName />
      </ScopedQueryProvider>,
    );
    await waitFor(() => expect(pending).toHaveLength(2));
    expect(first.init.signal?.aborted).toBe(true);
    expect(new Headers(pending[1]!.init.headers).get('x-loto-scope')).toBe('scope-bia-b2');

    await act(async () => {
      pending[1]!.resolve(current('Bia'));
      first.resolve(current('Ana'));
    });
    await screen.findByText('filial:Bia');
    expect(screen.queryByText('filial:Ana')).toBeNull();
  });
});

function SessionProbe() {
  const { snapshot } = useSession();
  return <p>estado:{snapshot.state}</p>;
}

describe('session provider', () => {
  it('coalesces a burst of session signals into one re-read and applies it', async () => {
    const calls: string[] = [];
    let release!: () => void;
    const gate = new Promise<void>((done) => (release = done));
    vi.stubGlobal('fetch', async (input: RequestInfo | URL) => {
      calls.push(String(input));
      await gate;
      return new Response(JSON.stringify(anonymousSnapshot), { status: 200 });
    });
    render(
      <SessionProvider initial={branchSnapshot('ana@x', 'b1', 's1')} fixtures={false}>
        <SessionProbe />
      </SessionProvider>,
    );
    act(() => {
      for (let index = 0; index < 5; index += 1) {
        window.dispatchEvent(
          new CustomEvent(sessionSignalEvent, { detail: { code: 'session-lost', outcome: null } }),
        );
      }
    });
    await act(async () => release());
    await screen.findByText('estado:anonymous');
    expect(calls).toEqual(['/bff/session']);
  });
});

function DraftProbe({ verified }: { readonly verified: boolean }) {
  const draft = useDeviceDraft<{ notes: string }>('close-day:acc-1', {
    authorityVerified: verified,
  });
  return (
    <div>
      <p>
        rascunho:{draft.state.status === 'loaded' ? draft.state.result.status : draft.state.status}
      </p>
      <button type="button" onClick={() => draft.save({ notes: 'contagem parcial' })}>
        salvar
      </button>
    </div>
  );
}

function Wrapped({
  snapshot,
  verified,
}: {
  readonly snapshot: SessionSnapshot;
  readonly verified: boolean;
}) {
  return (
    <SessionProvider key={snapshot.scope ?? 'anon'} initial={snapshot} fixtures={false}>
      <DraftProbe verified={verified} />
    </SessionProvider>
  );
}

describe('device draft restore rules', () => {
  it('stays locked without a branch session and unread until authority is rechecked', async () => {
    const ana = branchSnapshot('ana@example.invalid', 'b1', 's1');
    const { rerender } = render(<Wrapped snapshot={anonymousSnapshot} verified />);
    await screen.findByText('rascunho:locked');

    rerender(<Wrapped snapshot={ana} verified={false} />);
    await screen.findByText('rascunho:awaiting-authority');
    act(() => screen.getByText('salvar').click());

    rerender(<Wrapped snapshot={ana} verified />);
    await screen.findByText('rascunho:found');

    // A fresh login as another person on this device sees nothing of Ana's input.
    rerender(<Wrapped snapshot={branchSnapshot('bia@example.invalid', 'b1', 's2')} verified />);
    await screen.findByText('rascunho:none');
    // Ana again (re-login): her input is restored after the same checks.
    rerender(<Wrapped snapshot={branchSnapshot('ana@example.invalid', 'b1', 's3')} verified />);
    await screen.findByText('rascunho:found');
    // Ana in another branch: isolated.
    rerender(<Wrapped snapshot={branchSnapshot('ana@example.invalid', 'b2', 's4')} verified />);
    await screen.findByText('rascunho:none');
  });
});

function Operation({ onReady }: { readonly onReady: (submit: () => Promise<unknown>) => void }) {
  const operation = useIdempotentOperation<{ value: number }, unknown>({
    context: 'tx',
    operation: 'POST /transaction',
    send: (payload, key, options) =>
      postTransaction(
        { ...payload, date: '2026-10-21', transactionTypeId: 't', accountId: 'a' },
        { 'Idempotency-Key': key },
        options,
      ),
  });
  if (operation.ready) onReady(() => operation.submit({ value: 1 }));
  return <p>{operation.ready ? 'pronto' : 'aguardando'}</p>;
}

function Scoped({
  snapshot,
  children,
}: {
  readonly snapshot: SessionSnapshot;
  readonly children: ReactNode;
}) {
  return (
    <SessionProvider key={snapshot.scope ?? 'anon'} initial={snapshot} fixtures={false}>
      <ScopedQueryProvider scope={snapshot.scope}>{children}</ScopedQueryProvider>
    </SessionProvider>
  );
}

describe('financial intent and scope change', () => {
  it('an operation started for Ana/Alfa never dispatches after the scope becomes Bia/Beta', async () => {
    resetServerTime();
    const sent: { url: string; scope: string | null }[] = [];
    let releaseTime!: () => void;
    vi.stubGlobal('fetch', async (input: RequestInfo | URL, init: RequestInit = {}) => {
      const url = String(input);
      sent.push({ url, scope: new Headers(init.headers).get('x-loto-scope') });
      if (url === '/bff/session') {
        await new Promise<void>((done) => (releaseTime = done));
        return new Response('{}', { status: 200, headers: { date: new Date().toUTCString() } });
      }
      return new Response('{"id":"x"}', {
        status: 201,
        headers: { 'content-type': 'application/json' },
      });
    });
    let submit: (() => Promise<unknown>) | null = null;
    const ana = branchSnapshot('ana@example.invalid', 'alfa', 'scope-ana-alfa');
    const { rerender } = render(
      <Scoped snapshot={ana}>
        <Operation onReady={(next) => (submit = next)} />
      </Scoped>,
    );
    await screen.findByText('pronto');
    let outcome: unknown;
    act(() => {
      void submit?.().then((value) => (outcome = value));
    });
    await waitFor(() => expect(sent.map((entry) => entry.url)).toContain('/bff/session'));

    rerender(
      <Scoped snapshot={branchSnapshot('bia@example.invalid', 'beta', 'scope-bia-beta')}>
        <Operation onReady={() => undefined} />
      </Scoped>,
    );
    await act(async () => releaseTime());
    await waitFor(() => expect(outcome).toEqual({ kind: 'cancelled' }));
    expect(sent.filter((entry) => entry.url.includes('/bff/api/transaction'))).toEqual([]);
  });
});

describe('late session reads', () => {
  it('a read started before Sair never restores the signed-in state', async () => {
    let releaseRead!: (response: Response) => void;
    vi.stubGlobal('fetch', async (input: RequestInfo | URL, init: RequestInit = {}) => {
      const url = String(input);
      if (url === '/bff/session') {
        return new Promise<Response>((resolve, reject) => {
          releaseRead = resolve;
          init.signal?.addEventListener('abort', () =>
            reject(new DOMException('aborted', 'AbortError')),
          );
        });
      }
      if (url === '/bff/session/logout') {
        return new Response(JSON.stringify({ ...anonymousSnapshot, serverConfirmed: true }), {
          status: 200,
        });
      }
      throw new Error(url);
    });
    function Probe() {
      const session = useSession();
      return (
        <div>
          <p>estado:{session.snapshot.state}</p>
          <button type="button" onClick={() => void session.logout()}>
            Sair
          </button>
        </div>
      );
    }
    const ana = branchSnapshot('ana@example.invalid', 'alfa', 'scope-ana');
    render(
      <SessionProvider initial={ana} fixtures={false}>
        <Probe />
      </SessionProvider>,
    );
    act(() => {
      window.dispatchEvent(
        new CustomEvent(sessionSignalEvent, { detail: { code: 'session-lost', outcome: null } }),
      );
    });
    act(() => screen.getByText('Sair').click());
    await screen.findByText('estado:anonymous');
    await act(async () => releaseRead(new Response(JSON.stringify(ana), { status: 200 })));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(screen.getByText('estado:anonymous')).toBeTruthy();
  });
});

describe('reads during a session command (review round 2)', () => {
  function LoginProbe() {
    const session = useSession();
    return (
      <div>
        <p>estado:{session.snapshot.state}</p>
        <button type="button" onClick={() => void session.login('bia@example.invalid', 'x')}>
          Entrar
        </button>
      </div>
    );
  }

  it('a read started while a login runs cannot overwrite the login result', async () => {
    const bia = branchSnapshot('bia@example.invalid', 'beta', 'scope-bia');
    let releaseRead!: (response: Response) => void;
    let releaseLogin!: (response: Response) => void;
    const reads: string[] = [];
    vi.stubGlobal('fetch', (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/bff/session') {
        reads.push(url);
        // Only the first read is held (the one started during the login).
        if (reads.length === 1) return new Promise<Response>((resolve) => (releaseRead = resolve));
        return Promise.resolve(new Response(JSON.stringify(bia), { status: 200 }));
      }
      if (url === '/bff/session/login') {
        return new Promise<Response>((resolve) => (releaseLogin = resolve));
      }
      throw new Error(url);
    });
    render(
      <SessionProvider initial={anonymousSnapshot} fixtures={false}>
        <LoginProbe />
      </SessionProvider>,
    );
    act(() => screen.getByText('Entrar').click());
    // A session signal starts a read while the login is still at the API.
    act(() => {
      window.dispatchEvent(
        new CustomEvent(sessionSignalEvent, { detail: { code: 'session-lost', outcome: null } }),
      );
    });
    await waitFor(() => expect(reads).toHaveLength(1));
    await act(async () => releaseLogin(new Response(JSON.stringify(bia), { status: 200 })));
    await screen.findByText('estado:branch');
    // The older read answers "anonymous" after the login settled: it is ignored.
    await act(async () =>
      releaseRead(new Response(JSON.stringify(anonymousSnapshot), { status: 200 })),
    );
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(screen.getByText('estado:branch')).toBeTruthy();
  });

  it('a superseded command reads the session afresh after it settles', async () => {
    const bia = branchSnapshot('bia@example.invalid', 'beta', 'scope-bia');
    const calls: string[] = [];
    vi.stubGlobal('fetch', async (input: RequestInfo | URL) => {
      const url = String(input);
      calls.push(url);
      if (url === '/bff/session/login') {
        return new Response(null, {
          status: 409,
          headers: { 'x-loto-bff-error': 'session-scope-changed' },
        });
      }
      return new Response(JSON.stringify(bia), { status: 200 });
    });
    render(
      <SessionProvider initial={anonymousSnapshot} fixtures={false}>
        <LoginProbe />
      </SessionProvider>,
    );
    act(() => screen.getByText('Entrar').click());
    await screen.findByText('estado:branch');
    expect(calls[0]).toBe('/bff/session/login');
    expect(calls).toContain('/bff/session');
  });
});
