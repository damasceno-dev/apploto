'use client';

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import type { ApiClient } from '@loto/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createBrowserApiClient, installBrowserApiClient, shouldRetryRead } from './browser-api';

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

/**
 * The client of the scope this component renders in. Pass it as `{ client }` to raw mutation calls:
 * a write prepared in one scope then always carries that scope, and the forwarder refuses it if
 * the user or branch changed meanwhile.
 */
export function useScopedApiClient(): ApiClient {
  const client = useContext(ScopedClientContext);
  if (client === null)
    throw new Error('useScopedApiClient must be used inside ScopedQueryProvider.');
  return client;
}

interface ScopeProps {
  readonly scope: string | null;
  readonly children: ReactNode;
}

/**
 * One `QueryClient` per session scope (user + branch). A new scope remounts the subtree with a new
 * client, so forms, loaded versions and selections reset; the old client's queries are cancelled
 * and its cache cleared, so a late response can only land in a cache nothing reads any more.
 */
export function ScopedQueryProvider({ scope, children }: ScopeProps) {
  return (
    <ScopeBoundary key={scope ?? 'anonymous'} scope={scope}>
      {children}
    </ScopeBoundary>
  );
}

function ScopeBoundary({ scope, children }: ScopeProps) {
  const [queryClient] = useState(createScopedQueryClient);
  const [apiClient] = useState(() => createBrowserApiClient(scope));
  // Before the children render and start queries in this scope; idempotent and browser-only.
  installBrowserApiClient(apiClient);

  useEffect(
    () => () => {
      void queryClient.cancelQueries();
      queryClient.clear();
    },
    [queryClient],
  );

  return (
    <ScopedClientContext.Provider value={apiClient}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </ScopedClientContext.Provider>
  );
}
