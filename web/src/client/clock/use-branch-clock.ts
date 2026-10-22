'use client';

import { useCallback, useMemo, useState } from 'react';
import { useGetBranchCurrent } from '@loto/api/queries';
import { sameDate, toBranchClock, type BranchClock, type CalendarDate } from '@/lib/branch-clock';
import { useSession } from '../session/session-provider';

/**
 * Freshness: read on scope start, on focus and reconnect when older than `staleMs`, and every
 * `staleMs` while visible. A device timer only *requests* a read; it never advances business time.
 */
export const branchClockStaleMs = 60_000;

export type BranchClockState =
  | { readonly status: 'idle' | 'loading' }
  | { readonly status: 'error'; readonly error: unknown }
  /** The response broke the calendar contract: show an error, never fall back to the device. */
  | { readonly status: 'invalid' }
  | {
      readonly status: 'ready';
      readonly clock: BranchClock;
      /** The branch day differs from the first one seen in this scope until acknowledged. */
      readonly dayChanged: boolean;
      /** Live membership role differs from the session's role: re-resolve permissions and home. */
      readonly roleChanged: boolean;
      readonly updating: boolean;
    };

export interface BranchClockApi {
  readonly state: BranchClockState;
  readonly refresh: () => Promise<unknown>;
  readonly acknowledgeDayChange: () => void;
}

export function useBranchClock(): BranchClockApi {
  const { snapshot } = useSession();
  const enabled = snapshot.state === 'branch';
  const query = useGetBranchCurrent({
    query: {
      enabled,
      staleTime: branchClockStaleMs,
      refetchInterval: branchClockStaleMs,
      refetchOnWindowFocus: true,
      refetchOnReconnect: true,
    },
  });
  const clock = useMemo(() => (query.data ? toBranchClock(query.data.data) : null), [query.data]);
  // The first branch day seen in this scope; the scope boundary remounts this hook on change.
  const [baseline, setBaseline] = useState<CalendarDate | null>(null);
  if (clock !== null && baseline === null) setBaseline(clock.date);

  const acknowledgeDayChange = useCallback(() => {
    if (clock !== null) setBaseline(clock.date);
  }, [clock]);

  let state: BranchClockState;
  if (!enabled) state = { status: 'idle' };
  else if (query.isError) state = { status: 'error', error: query.error };
  else if (query.data === undefined) state = { status: 'loading' };
  else if (clock === null) state = { status: 'invalid' };
  else {
    state = {
      status: 'ready',
      clock,
      dayChanged: baseline !== null && !sameDate(baseline, clock.date),
      roleChanged: snapshot.branch !== null && snapshot.branch.role !== clock.liveRole,
      updating: query.isFetching,
    };
  }
  return { state, refresh: query.refetch, acknowledgeDayChange };
}
