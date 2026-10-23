'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { usePathname } from 'next/navigation';
import { parseScope, type SessionSnapshot } from '@/lib/bff-protocol';
import { routeTier } from '@/lib/route-policy';
import { sessionSignalEvent } from '../bff-fetch';
import * as sessionClient from './session-client';
import type { LogoutResult, SessionCommandFailure, SessionCommandResult } from './session-client';

/** Fired before the gate navigates away, so device-input helpers flush pending saves. */
export const beforeGateEvent = 'loto:before-gate';
const identityChannelName = 'loto-session';
const branchChannelName = 'loto-branch';

export interface SessionApi {
  /**
   * The view of the scope this component renders in: identity level outside a branch route, the
   * tab's own branch inside `/f/{branchId}`. Never a token.
   */
  readonly snapshot: SessionSnapshot;
  /** True while the session is being re-read after a session signal: writes must wait. */
  readonly recovering: boolean;
  /** True unless this scope's authority is settled. Financial writes check it before dispatch. */
  readonly writesPaused: boolean;
  readonly refresh: () => Promise<void>;
  /** Login; the result's snapshot (with `resume`) tells the tab where to land (`landingFor`). */
  readonly login: (email: string, password: string) => Promise<SessionCommandResult>;
  /**
   * Opens a branch for this identity (Branch picker, a role change). It does not move other tabs:
   * the caller navigates **this** tab to `landingFor(result.snapshot)`.
   */
  readonly selectBranch: (branchId: string) => Promise<SessionCommandResult>;
  /** Branch picker: the identity's branches and the membership rule (one branch → opened). */
  readonly readBranchChoice: () => Promise<SessionCommandResult>;
  readonly logout: () => Promise<LogoutResult | SessionCommandFailure>;
}

const SessionContext = createContext<SessionApi | null>(null);

/** The identity-level session of this tab, for branch scopes that nest inside it. */
interface IdentityInternals {
  readonly api: SessionApi;
  /** Moves whenever an identity command starts or settles: reads in flight at that point are void. */
  readonly authority: number;
}
const IdentityContext = createContext<IdentityInternals | null>(null);

export function useSession(): SessionApi {
  const api = useContext(SessionContext);
  if (api === null) throw new Error('useSession must be used inside SessionProvider.');
  return api;
}

interface SessionProviderProps {
  readonly initial: SessionSnapshot;
  readonly fixtures: boolean;
  readonly children: ReactNode;
}

/**
 * Identity level: who is signed in, never a branch. A command result that opened a branch is kept
 * here only as its identity part — each tab's branch lives in its `/f/{branchId}` route and its
 * `BranchSessionProvider`.
 */
function identityView(snapshot: SessionSnapshot): SessionSnapshot {
  const identity = parseScope(snapshot.scope)?.identity ?? null;
  return snapshot.state === 'branch'
    ? { ...snapshot, state: 'choose-branch', scope: identity, branch: null }
    : { ...snapshot, scope: identity };
}

/**
 * Holds the browser's view of the identity — never a token. Seeded by the server render from the
 * cookies, re-read from `GET /bff/session` when a BFF response signals a session change, and kept
 * consistent across tabs through `BroadcastChannel` (all tabs share one cookie jar for the identity;
 * each tab's branch is its own).
 */
export function SessionProvider({ initial, fixtures, children }: SessionProviderProps) {
  const [snapshot, setSnapshot] = useState(() => identityView(initial));
  const [recovering, setRecovering] = useState(false);
  const pathname = usePathname();
  const channel = useRef<BroadcastChannel | null>(null);
  const pendingRefresh = useRef<Promise<void> | null>(null);
  const currentScope = useRef(identityView(initial).scope);
  // `authority` changes when a session command starts **and** when it settles: a read is applied
  // only if neither happened while it was in flight (a read started before or during a login or
  // Sair can never overwrite that command's result). `latestCommand` lets only the newest command
  // apply its own result.
  const authority = useRef(0);
  const [authorityVersion, setAuthorityVersion] = useState(0);
  const latestCommand = useRef(0);
  const pendingRead = useRef<AbortController | null>(null);

  // An identity change is announced to the other tabs, except when another tab caused it.
  const apply = useCallback((next: SessionSnapshot, announce: boolean) => {
    const view = identityView(next);
    const changed = currentScope.current !== view.scope;
    currentScope.current = view.scope;
    setSnapshot(view);
    if (announce && changed) channel.current?.postMessage({ type: 'session-changed' });
  }, []);

  const reread = useCallback(
    (announce: boolean) => {
      // One re-read at a time; a burst of signals shares it.
      pendingRefresh.current ??= (async () => {
        const started = authority.current;
        const abort = new AbortController();
        pendingRead.current = abort;
        setRecovering(true);
        try {
          const result = await sessionClient.readSession({ signal: abort.signal });
          if (result.ok && authority.current === started) apply(result.snapshot, announce);
        } catch (error) {
          if (!abort.signal.aborted) throw error;
        } finally {
          if (pendingRead.current === abort) {
            pendingRead.current = null;
            pendingRefresh.current = null;
            setRecovering(false);
          }
        }
      })();
      return pendingRefresh.current;
    },
    [apply],
  );
  const refresh = useCallback(() => reread(true), [reread]);

  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') return;
    const opened = new BroadcastChannel(identityChannelName);
    channel.current = opened;
    opened.onmessage = () => void reread(false);
    return () => {
      opened.close();
      channel.current = null;
    };
  }, [reread]);

  useEffect(() => {
    const onSignal = () => void refresh();
    window.addEventListener(sessionSignalEvent, onSignal);
    return () => window.removeEventListener(sessionSignalEvent, onSignal);
  }, [refresh]);

  // The gate owns navigation when the identity this route needs is gone: flush device input, then
  // reload the current address so the proxy redirects (and keeps the intent) before any render.
  // Branch authority is the branch scope's concern (`BranchSessionProvider`).
  useEffect(() => {
    const tier = routeTier(pathname, { fixtures });
    if ((tier === 'identity' || tier === 'branch') && snapshot.state === 'anonymous') {
      window.dispatchEvent(new Event(beforeGateEvent));
      window.location.assign(window.location.href);
    }
  }, [pathname, fixtures, snapshot]);

  /** Invalidates every read in flight (a new authority boundary). */
  const invalidateReads = useCallback(() => {
    authority.current += 1;
    setAuthorityVersion(authority.current);
    pendingRead.current?.abort();
    pendingRead.current = null;
    pendingRefresh.current = null;
    setRecovering(false);
  }, []);

  /** Starts a session command: reads in flight are cancelled and older results ignored. */
  const begin = useCallback(() => {
    invalidateReads();
    latestCommand.current += 1;
    return { mine: latestCommand.current, declared: currentScope.current };
  }, [invalidateReads]);

  /**
   * Settles a command: reads that started while it ran are invalidated too. When its result was
   * not applied (refused, superseded, overtaken), the session is read afresh after the boundary.
   */
  const settle = useCallback(
    (mine: number, snapshotOf: SessionSnapshot | null) => {
      invalidateReads();
      if (snapshotOf !== null && latestCommand.current === mine) {
        apply(snapshotOf, true);
      } else if (latestCommand.current === mine) {
        void reread(true);
      }
    },
    [apply, invalidateReads, reread],
  );

  const command = useCallback(
    async (run: (declared: string | null) => Promise<SessionCommandResult>) => {
      const { mine, declared } = begin();
      try {
        const result = await run(declared);
        settle(mine, result.ok ? result.snapshot : null);
        return result;
      } catch (error) {
        settle(mine, null);
        throw error;
      }
    },
    [begin, settle],
  );

  // Opening a branch changes no identity authority: it is not an identity command. Only a refusal
  // because the identity moved on (another login or Sair elsewhere) re-reads the identity.
  const selectBranch = useCallback(
    async (branchId: string) => {
      const result = await sessionClient.selectBranch(branchId, currentScope.current);
      if (!result.ok && result.bffError !== null) void reread(true);
      return result;
    },
    [reread],
  );

  const readBranchChoice = useCallback(async () => {
    const started = authority.current;
    const result = await sessionClient.readSession({ branches: true });
    if (result.ok && authority.current === started) apply(result.snapshot, true);
    return result;
  }, [apply]);

  const api = useMemo<SessionApi>(
    () => ({
      snapshot,
      recovering,
      writesPaused:
        recovering || snapshot.state === 'anonymous' || snapshot.state === 'renewal-unconfirmed',
      refresh,
      login: (email, password) =>
        command((declared) => sessionClient.loginWithPassword(email, password, declared)),
      selectBranch,
      readBranchChoice,
      logout: async () => {
        window.dispatchEvent(new Event(beforeGateEvent));
        const { mine, declared } = begin();
        try {
          const result = await sessionClient.logout(declared);
          settle(mine, 'snapshot' in result ? result.snapshot : null);
          return result;
        } catch (error) {
          settle(mine, null);
          throw error;
        }
      },
    }),
    [snapshot, recovering, refresh, command, selectBranch, readBranchChoice, begin, settle],
  );
  const internals = useMemo(() => ({ api, authority: authorityVersion }), [api, authorityVersion]);

  return (
    <IdentityContext.Provider value={internals}>
      <SessionContext.Provider value={api}>{children}</SessionContext.Provider>
    </IdentityContext.Provider>
  );
}

interface BranchSessionProviderProps {
  readonly branchId: string;
  /** The server render's view of this branch, from this request's cookies (no token). */
  readonly initial: SessionSnapshot;
  readonly children: ReactNode;
}

/**
 * One tab's branch context (D-M2-1): the branch named by its `/f/{branchId}` route. It re-reads
 * only this branch's session (`GET /bff/session` with `x-loto-branch`), so a switch, renewal or
 * role change in another tab never moves this one; an identity change (Sair, another login) does.
 * Its snapshot's scope — identity generation plus this branch's generation — keys the query scope
 * below it, so a new branch generation remounts the subtree (caches, forms, selections reset).
 */
export function BranchSessionProvider({ branchId, initial, children }: BranchSessionProviderProps) {
  const identity = useContext(IdentityContext);
  if (identity === null) throw new Error('BranchSessionProvider must be inside SessionProvider.');
  const root = identity.api;
  const [snapshot, setSnapshot] = useState(initial);
  const [recovering, setRecovering] = useState(false);
  const pendingRefresh = useRef<Promise<void> | null>(null);
  const pendingRead = useRef<AbortController | null>(null);
  const authority = useRef(identity.authority);
  const channel = useRef<BroadcastChannel | null>(null);
  const currentScope = useRef(initial.scope);

  const apply = useCallback(
    (next: SessionSnapshot, announce: boolean) => {
      const changed = currentScope.current !== next.scope;
      currentScope.current = next.scope;
      setSnapshot(next);
      // Another tab in the same branch learns of a new branch generation (a changed role).
      if (announce && changed) channel.current?.postMessage({ type: 'branch-changed', branchId });
    },
    [branchId],
  );

  const reread = useCallback(
    (announce: boolean) => {
      pendingRefresh.current ??= (async () => {
        const started = authority.current;
        const abort = new AbortController();
        pendingRead.current = abort;
        setRecovering(true);
        try {
          const result = await sessionClient.readSession({ signal: abort.signal, branchId });
          if (result.ok && authority.current === started) apply(result.snapshot, announce);
        } catch (error) {
          if (!abort.signal.aborted) throw error;
        } finally {
          if (pendingRead.current === abort) {
            pendingRead.current = null;
            pendingRefresh.current = null;
            setRecovering(false);
          }
        }
      })();
      return pendingRefresh.current;
    },
    [apply, branchId],
  );
  const refresh = useCallback(() => reread(true), [reread]);

  // An identity command started or settled: this tab's branch reads in flight are void, and the
  // branch is read again under whatever identity now holds the cookies.
  const identityAuthority = identity.authority;
  useEffect(() => {
    if (authority.current === identityAuthority) return;
    authority.current = identityAuthority;
    pendingRead.current?.abort();
    pendingRead.current = null;
    pendingRefresh.current = null;
    setRecovering(false);
  }, [identityAuthority]);

  // The identity changed under this tab (another tab signed in or out): re-read this branch for
  // the identity the cookies now hold — the new identity's own authority, never the old one's.
  const identityScope = root.snapshot.scope;
  const heldIdentity = parseScope(snapshot.scope)?.identity ?? null;
  const identityMoved = identityScope !== heldIdentity;
  useEffect(() => {
    if (identityMoved) void reread(false);
  }, [identityMoved, identityScope, reread]);

  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') return;
    const opened = new BroadcastChannel(branchChannelName);
    channel.current = opened;
    opened.onmessage = (event: MessageEvent<{ type?: string; branchId?: string }>) => {
      if (event.data?.branchId === branchId) void reread(false);
    };
    return () => {
      opened.close();
      channel.current = null;
    };
  }, [branchId, reread]);

  useEffect(() => {
    const onSignal = () => void refresh();
    window.addEventListener(sessionSignalEvent, onSignal);
    return () => window.removeEventListener(sessionSignalEvent, onSignal);
  }, [refresh]);

  // This branch is no longer open to the identity (or no identity remains): the gate takes over —
  // the proxy answers 403 for a refused branch, Login for a lost identity.
  useEffect(() => {
    if (snapshot.state !== 'branch' && snapshot.state !== 'renewal-unconfirmed') {
      window.dispatchEvent(new Event(beforeGateEvent));
      window.location.assign(window.location.href);
    }
  }, [snapshot]);

  // Re-opening this tab's own branch (a live role change) applies its result here; any other
  // branch leaves this tab alone — the caller navigates.
  const selectBranch = useCallback(
    async (id: string) => {
      const result = await root.selectBranch(id);
      if (result.ok && id === branchId && result.snapshot.state === 'branch') {
        apply(result.snapshot, true);
      }
      return result;
    },
    [root, branchId, apply],
  );

  const api = useMemo<SessionApi>(() => {
    const busy = recovering || root.recovering || identityMoved;
    return {
      snapshot,
      recovering: busy,
      writesPaused: busy || snapshot.state !== 'branch',
      refresh,
      login: root.login,
      selectBranch,
      readBranchChoice: root.readBranchChoice,
      logout: root.logout,
    };
  }, [snapshot, recovering, root, identityMoved, refresh, selectBranch]);

  return <SessionContext.Provider value={api}>{children}</SessionContext.Provider>;
}
