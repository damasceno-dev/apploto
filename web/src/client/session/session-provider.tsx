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
import type { SessionSnapshot } from '@/lib/bff-protocol';
import { decideGate, routeTier } from '@/lib/route-policy';
import { sessionSignalEvent } from '../bff-fetch';
import * as sessionClient from './session-client';
import type { LogoutResult, SessionCommandFailure, SessionCommandResult } from './session-client';

/** Fired before the gate navigates away, so device-input helpers flush pending saves. */
export const beforeGateEvent = 'loto:before-gate';
const channelName = 'loto-session';

export interface SessionApi {
  readonly snapshot: SessionSnapshot;
  /** True while the session is being re-read after a session signal: writes must wait. */
  readonly recovering: boolean;
  /** True unless an authorized branch session is settled. Financial writes check it before dispatch. */
  readonly writesPaused: boolean;
  readonly refresh: () => Promise<void>;
  readonly login: (email: string, password: string) => Promise<SessionCommandResult>;
  readonly selectBranch: (branchId: string) => Promise<SessionCommandResult>;
  readonly leaveBranch: () => Promise<SessionCommandResult>;
  readonly logout: () => Promise<LogoutResult | SessionCommandFailure>;
}

const SessionContext = createContext<SessionApi | null>(null);

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
 * Holds the browser's view of the session — never a token. Seeded by the server render from the
 * cookies, re-read from `GET /bff/session` when a BFF response signals a session change, and kept
 * consistent across tabs through `BroadcastChannel` (all tabs share one cookie jar).
 */
export function SessionProvider({ initial, fixtures, children }: SessionProviderProps) {
  const [snapshot, setSnapshot] = useState(initial);
  const [recovering, setRecovering] = useState(false);
  const pathname = usePathname();
  const channel = useRef<BroadcastChannel | null>(null);
  const pendingRefresh = useRef<Promise<void> | null>(null);
  const currentScope = useRef(initial.scope);
  // `authority` changes when a session command starts **and** when it settles: a read is applied
  // only if neither happened while it was in flight (a read started before or during a login or
  // Sair can never overwrite that command's result). `latestCommand` lets only the newest command
  // apply its own result.
  const authority = useRef(0);
  const latestCommand = useRef(0);
  const pendingRead = useRef<AbortController | null>(null);

  // A scope change is announced to the other tabs, except when another tab caused it.
  const apply = useCallback((next: SessionSnapshot, announce: boolean) => {
    const changed = currentScope.current !== next.scope;
    currentScope.current = next.scope;
    setSnapshot(next);
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
          const result = await sessionClient.readSession(abort.signal);
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
    const opened = new BroadcastChannel(channelName);
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

  // The gate owns navigation when the authority this route needs is gone: flush device input, then
  // reload the current address so the proxy redirects (and keeps the intent) before any render.
  useEffect(() => {
    const decision = decideGate(routeTier(pathname, { fixtures }), snapshot);
    if (decision.kind === 'redirect' && snapshot.state !== 'renewal-unconfirmed') {
      window.dispatchEvent(new Event(beforeGateEvent));
      window.location.assign(window.location.href);
    }
  }, [pathname, fixtures, snapshot]);

  /** Invalidates every read in flight (a new authority boundary). */
  const invalidateReads = useCallback(() => {
    authority.current += 1;
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

  const api = useMemo<SessionApi>(
    () => ({
      snapshot,
      recovering,
      writesPaused: recovering || snapshot.state !== 'branch',
      refresh,
      login: (email, password) =>
        command((declared) => sessionClient.loginWithPassword(email, password, declared)),
      selectBranch: (branchId) =>
        command((declared) => sessionClient.selectBranch(branchId, declared)),
      leaveBranch: () => command((declared) => sessionClient.leaveBranch(declared)),
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
    [snapshot, recovering, refresh, command, begin, settle],
  );

  return <SessionContext.Provider value={api}>{children}</SessionContext.Provider>;
}
