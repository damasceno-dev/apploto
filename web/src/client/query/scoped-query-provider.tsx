'use client';

import {
  createContext,
  useContext,
  useEffect,
  useLayoutEffect,
  useState,
  type ReactNode,
} from 'react';
import { scopeExitEvent } from '../session/session-provider';
import type { ApiClient } from '@loto/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createBrowserApiClient,
  installBrowserApiClient,
  mountBrowserApiClient,
  shouldRetryRead,
} from './browser-api';

export function createScopedQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: shouldRetryRead, staleTime: 30_000, refetchOnWindowFocus: true },
      // Writes are never retried automatically (an idempotent replay is an explicit user action).
      mutations: { retry: 0 },
    },
  });
}

const ScopedClientContext = createContext<ApiClient | null>(null);
/** How deeply scopes nest here: identity scope 0, a branch tab's scope 1. */
const ScopeDepthContext = createContext(-1);

/**
 * The client of the scope this component renders in. Pass it as `{ client }` to raw mutation calls:
 * a write prepared in one scope then always carries that scope (and, in a branch tab, that branch),
 * and the forwarder refuses it if the user or the branch authority changed meanwhile.
 */
export function useScopedApiClient(): ApiClient {
  const client = useContext(ScopedClientContext);
  if (client === null)
    throw new Error('useScopedApiClient must be used inside ScopedQueryProvider.');
  return client;
}

interface ScopeProps {
  readonly scope: string | null;
  /** The tab's route branch, for a branch subtree; null at identity level. */
  readonly branchId?: string | null;
  readonly children: ReactNode;
}

/**
 * One `QueryClient` per session scope (user + the tab's branch). A new scope remounts the subtree
 * with a new client, so forms, loaded versions, filters and selections reset; the old client's
 * queries are cancelled and its cache cleared, so a late response can only land in a cache nothing
 * reads any more. A branch subtree nests inside the identity scope; while it is mounted its client
 * is the tab's default for generated hooks, and the enclosing scope's client returns when it leaves.
 */
export function ScopedQueryProvider({ scope, branchId = null, children }: ScopeProps) {
  return (
    <ScopeBoundary
      key={`${branchId ?? '-'}|${scope ?? 'anonymous'}`}
      scope={scope}
      branchId={branchId}
    >
      {children}
    </ScopeBoundary>
  );
}

function ScopeBoundary({ scope, branchId = null, children }: ScopeProps) {
  const depth = useContext(ScopeDepthContext) + 1;
  const [queryClient] = useState(createScopedQueryClient);
  const [apiClient] = useState(() => {
    const client = createBrowserApiClient(scope, branchId);
    // First render only: a query that fetches during render (Suspense) already uses this scope.
    // Later re-renders must not reinstall: a nested scope below may own the default by then.
    installBrowserApiClient(client);
    return client;
  });
  // The settled default: the deepest mounted scope, decided before passive effects run.
  useLayoutEffect(() => mountBrowserApiClient(apiClient, depth), [apiClient, depth]);
  useEffect(
    () => () => {
      void queryClient.cancelQueries();
      queryClient.clear();
    },
    [queryClient],
  );

  useEffect(() => {
    const stop = (event: Event) => {
      const exiting = (event as CustomEvent<{ branchId: string | null }>).detail?.branchId;
      if (exiting === null || exiting === branchId) {
        void queryClient.cancelQueries();
        queryClient.clear();
      }
    };
    window.addEventListener(scopeExitEvent, stop);
    return () => window.removeEventListener(scopeExitEvent, stop);
  }, [branchId, queryClient]);
  return (
    <ScopeDepthContext.Provider value={depth}>
      <ScopedClientContext.Provider value={apiClient}>
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </ScopedClientContext.Provider>
    </ScopeDepthContext.Provider>
  );
}
