import { useEffect, useState } from 'react';
import { getTimeentry, getTimeentryTimeEntryId, type ResponseTimeEntryJson } from '@loto/api';
import { parseCalendarDate, type CalendarDate } from '@loto/core';
import { useScopedApiClient, useSession } from '@/session/provider';
import { isServerId } from '@/session/credentials';
import type { DeviceDraft, StorageScope } from '@/storage/scoped';

interface ClockAttempt {
  operatorId: string;
  date: CalendarDate;
  action: 'Open' | 'Close';
}
export function isClockAttempt(value: unknown): value is ClockAttempt {
  if (!value || typeof value !== 'object') return false;
  const record = value as ClockAttempt;
  return (
    Object.keys(record).sort().join(',') === 'action,date,operatorId' &&
    isServerId(record.operatorId) &&
    typeof record.date === 'string' &&
    parseCalendarDate(record.date) !== null &&
    (record.action === 'Open' || record.action === 'Close')
  );
}
interface HeldAttempt {
  draft: DeviceDraft<ClockAttempt>;
  scope: StorageScope;
}
/** Nonfinancial context only. Restoration and acknowledgement never dispatch/replay a tap. */
export function useClockAttempt(operatorId: string | null | undefined) {
  const { engine, store, drafts } = useSession();
  const client = useScopedApiClient();
  const resource = `time-tap/${operatorId ?? 'unlinked'}`;
  const [status, setStatus] = useState<'checking' | 'ready' | 'failed'>('checking');
  const [held, setHeld] = useState<HeldAttempt | null>(null);
  const [review, setReview] = useState<ResponseTimeEntryJson[] | null>(null);
  const [reading, setReading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [check, setCheck] = useState(0);
  useEffect(() => {
    let active = true;
    void (async () => {
      if (!operatorId) {
        await Promise.resolve();
        if (active) setStatus('ready');
        return;
      }
      const scope = await store.scope();
      const found = await drafts.restore(scope, resource, isClockAttempt, async () => {
        await engine.verifyEntry({ selfContext: true });
        const current = engine.getSnapshot();
        return (
          current.branch?.role === 'Member' &&
          current.selfContext?.operatorId === operatorId &&
          current.selfStatus === 'ready'
        );
      });
      if (!active) return;
      if (
        found.status === 'corrupt' ||
        (found.status === 'found' && found.value.input.operatorId !== operatorId)
      )
        throw new Error('O contexto do toque guardado não pôde ser validado.');
      setHeld(found.status === 'found' ? { scope, draft: found.value } : null);
      setStatus('ready');
    })().catch((failure) => {
      if (active) {
        setError(failure);
        setStatus('failed');
      }
    });
    return () => {
      active = false;
    };
  }, [operatorId, resource, store, drafts, engine, check]);
  return {
    status,
    held,
    review,
    reading,
    error,
    blocked: status !== 'ready' || held !== null,
    retryCheck: () => {
      setStatus('checking');
      setError(null);
      setCheck((value) => value + 1);
    },
    begin: async (date: CalendarDate, action: 'Open' | 'Close') => {
      if (!operatorId || status !== 'ready' || held)
        throw new Error('Confira o toque anterior antes de continuar.');
      const scope = await store.scope();
      const draft = await drafts.save(scope, resource, { operatorId, date, action }, null);
      const holding = { scope, draft };
      store.assert(scope);
      setHeld(holding);
      setReview(null);
      setError(null);
      return holding;
    },
    confirmed: async (holding: HeldAttempt) => {
      await drafts.acknowledge(holding.scope, resource, holding.draft.revision);
      store.assert(holding.scope);
      setHeld(null);
      setReview(null);
    },
    reread: async () => {
      if (!held || reading) return;
      setReading(true);
      setReview(null);
      setError(null);
      try {
        store.assert(held.scope);
        const date = held.draft.input.date;
        const rows = (
          await getTimeentry(
            { Mine: true, DateFrom: date, DateTo: date, Page: 1, PageSize: 10 },
            { client },
          )
        ).data.items;
        const details = await Promise.all(
          rows.map(async (row) => (await getTimeentryTimeEntryId(row.id, { client })).data),
        );
        store.assert(held.scope);
        setReview(details);
      } catch (failure) {
        if (engine.getSnapshot().scope === held.scope.generation) setError(failure);
      } finally {
        if (engine.getSnapshot().scope === held.scope.generation) setReading(false);
      }
    },
    reviewed: async () => {
      if (!held || review === null) return false;
      try {
        await drafts.acknowledge(held.scope, resource, held.draft.revision);
        store.assert(held.scope);
        setHeld(null);
        setReview(null);
        setError(null);
        return true;
      } catch (failure) {
        if (engine.getSnapshot().scope === held.scope.generation) setError(failure);
        return false;
      }
    },
  };
}
