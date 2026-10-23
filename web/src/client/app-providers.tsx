'use client';

import { useEffect, useRef, type ReactNode } from 'react';
import type { SessionSnapshot } from '@/lib/bff-protocol';
import { useBranchClock } from './clock/use-branch-clock';
import { ScopedQueryProvider } from './query/scoped-query-provider';
import { BranchSessionProvider, SessionProvider, useSession } from './session/session-provider';

/** App-wide client providers: the identity session, then its query scope. */
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
  return <ScopedQueryProvider scope={snapshot.scope}>{children}</ScopedQueryProvider>;
}

/**
 * The providers of one tab's branch (`/f/{branchId}` layout): its branch session and a query scope
 * keyed by that branch's scope, so moving this tab to another branch — or a new generation of the
 * same branch — remounts everything below with fresh caches, forms, filters and selections.
 */
export function BranchProviders({
  branchId,
  session,
  children,
}: {
  readonly branchId: string;
  readonly session: SessionSnapshot;
  readonly children: ReactNode;
}) {
  return (
    <BranchSessionProvider key={branchId} branchId={branchId} initial={session}>
      <BranchScopedSubtree branchId={branchId}>{children}</BranchScopedSubtree>
    </BranchSessionProvider>
  );
}

function BranchScopedSubtree({
  branchId,
  children,
}: {
  readonly branchId: string;
  readonly children: ReactNode;
}) {
  const { snapshot } = useSession();
  return (
    <ScopedQueryProvider scope={snapshot.scope} branchId={branchId}>
      {snapshot.state === 'branch' ? <LiveRoleWatcher /> : null}
      {children}
    </ScopedQueryProvider>
  );
}

/**
 * Catalog: `GET /branch/current` carries the live role. When it differs from the session's, this
 * tab's branch session is reopened once for this scope — a new branch generation clears
 * permission-dependent caches, selections and mode, and the home is resolved again from the new role.
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
