'use client';

import { useBranchClock } from '@/client/clock/use-branch-clock';
import { useSession } from '@/client/session/session-provider';

export function ClientClock() {
  const { state, refresh } = useBranchClock();
  const { snapshot } = useSession();
  return (
    <div>
      <p data-testid="client-clock">
        {state.status === 'ready'
          ? `${state.clock.branchId}|${state.clock.branchLocalDate}|${state.clock.liveRole}`
          : state.status}
      </p>
      <p data-testid="client-session">{`${snapshot.user?.email ?? '-'}|${snapshot.branch?.name ?? '-'}`}</p>
      <p data-testid="client-scope">{snapshot.scope ?? '-'}</p>
      <button type="button" onClick={() => void refresh()}>
        Atualizar
      </button>
    </div>
  );
}
