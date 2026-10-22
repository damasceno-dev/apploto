'use client';

import { useEffect, useRef, type ReactNode } from 'react';
import type { SessionSnapshot } from '@/lib/bff-protocol';
import { useBranchClock } from './clock/use-branch-clock';
import { ScopedQueryProvider } from './query/scoped-query-provider';
import { SessionProvider, useSession } from './session/session-provider';

/** App-wide client providers: session view, then one query scope per user + branch. */
export function AppProviders({
  session,
  fixtures,
  children,
}: {
  readonly session: SessionSnapshot;
  readonly fixtures: boolean;
  readonly children: ReactNode;
}) {
  return (
    <SessionProvider initial={session} fixtures={fixtures}>
      <ScopedSubtree>{children}</ScopedSubtree>
    </SessionProvider>
  );
}

function ScopedSubtree({ children }: { readonly children: ReactNode }) {
  const { snapshot } = useSession();
  return (
    <ScopedQueryProvider scope={snapshot.scope}>
      {snapshot.state === 'branch' ? <LiveRoleWatcher /> : null}
      {children}
    </ScopedQueryProvider>
  );
}

/**
 * Catalog: `GET /branch/current` carries the live role. When it differs from the session's, the
 * branch session is reopened once for this scope — a new scope clears permission-dependent caches,
 * selections and mode, and the home is resolved again from the new role.
 */
function LiveRoleWatcher() {
  const { state } = useBranchClock();
  const { snapshot, selectBranch } = useSession();
  const reopened = useRef(false);
  const roleChanged = state.status === 'ready' && state.roleChanged;
  useEffect(() => {
    if (!roleChanged || reopened.current || snapshot.branch === null) return;
    reopened.current = true;
    void selectBranch(snapshot.branch.id);
  }, [roleChanged, selectBranch, snapshot.branch]);
  return null;
}
