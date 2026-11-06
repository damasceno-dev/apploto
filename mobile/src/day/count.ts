import { useCallback, useEffect, useRef, useState } from 'react';
import {
  isApiError,
  getSessionSessionIdReview,
  parseETag,
  putSessionSessionIdItems,
  toIfMatch,
  type ResponseAffectedDailyCloseSuccessorJson,
  type ResponseDailyCloseJson,
  type ResponseDailyCloseReviewJson,
} from '@loto/api';
import { useSession } from '@/session/provider';
import { SessionGateError } from '@/session/types';
import { actionFailure, type ActionFailure } from '@/data/failures';
import { useAdminDraft } from '@/admin/common';
import type { SaveState } from '@/ui';
import {
  autosaveEligible,
  countRequest,
  isCountInput,
  sameCount,
  serverInput,
  sessionInput,
  type CountInput,
} from './contracts';

/** Debounce after the last real edit; tests shorten it, product code never changes it. */
export const autosaveTiming = { delayMs: 1200 };

/** A fresh review read with its strong ETag verified against the body version (never persisted). */
export async function readReview(
  engine: ReturnType<typeof useSession>['engine'],
  sessionId: string,
) {
  const result = await getSessionSessionIdReview(sessionId, { client: engine.client() });
  if (parseETag(result.headers.get('etag')) !== result.data.version)
    throw new SessionGateError('A versão do fechamento não pôde ser verificada.');
  return result.data;
}

export interface Reconciliation {
  readonly server: CountInput;
  readonly mine: CountInput;
}
type Phase = 'idle' | 'saving' | 'failed' | 'uncertain' | 'stale';

/**
 * The close-day count: the user's candidate, the last server-acknowledged snapshot and the
 * session-lived version. Autosave runs only on today's plain Draft after a real edit; one save is
 * in flight at a time, later edits coalesce into the next complete candidate, and an older
 * acknowledgement never clears a newer edit. Stale, unknown and unsupported-row refusals stop
 * autosave and keep the buffer until an explicit, fresh reconciliation. Nothing here confers or
 * submits.
 */
/**
 * Gap AA protection for the whole count, not one body: `retired` when a stored row is a product the
 * active product list no longer has, `unverified` while that cannot be read, `refused` after the
 * server answered a save with 404. While locked nothing is edited or saved (a row is never trimmed
 * to pass); the lock is not lifted by later edits or rereads in this view.
 */
export type CountLock = 'retired' | 'unverified' | 'refused' | null;

export function useCountForm(
  sessionId: string,
  review: ResponseDailyCloseReviewJson,
  /** The active product ids, or null while they could not be read. */
  activeProducts: readonly string[] | null,
) {
  const { engine, snapshot } = useSession();
  const today = snapshot.clock?.branchLocalDate ?? '';
  const [current, setCurrent] = useState(review);
  const [input, setInputState] = useState<CountInput>(() => serverInput(review));
  const [acknowledged, setAcknowledged] = useState<CountInput>(() => serverInput(review));
  const [savedAt, setSavedAt] = useState<string | null>(review.updatedAt ?? null);
  const [phase, setPhase] = useState<Phase>('idle');
  const [failure, setFailure] = useState<ActionFailure | null>(null);
  const [successor, setSuccessor] = useState<ResponseAffectedDailyCloseSuccessorJson | null>(null);
  const [reconciliation, setReconciliation] = useState<Reconciliation | null>(null);
  /** The exact candidate a 404 refused (gap AA): never resent unchanged, never trimmed to pass. */
  const [unsupported, setUnsupported] = useState<string | null>(null);
  /** The exact body of a definite refusal: autosave does not retry it; an edit makes a new body. */
  const [refused, setRefused] = useState<string | null>(null);
  const [edited, setEdited] = useState(false);
  const [queuedTick, setQueuedTick] = useState(0);
  // Mirrors for async work only (never read during render).
  const live = useRef({ input, acknowledged, current, version: review.version });
  const lastSent = useRef<CountInput | null>(null);
  const flight = useRef(false);
  const queued = useRef(false);
  const alive = useRef(true);
  const draft = useAdminDraft(`day/count/${sessionId}`, isCountInput, async () => {
    const fresh = await readReview(engine, sessionId);
    return fresh.id === sessionId && fresh.branchId === engine.getSnapshot().branch?.id;
  });
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const autosave = autosaveEligible(current, today);
  const dirty = !sameCount(input, acknowledged);
  const neverRecorded = !current.itemsFirstRecordedAt;
  const fingerprint = JSON.stringify(countRequest(current, input, acknowledged));
  const openCalculations = Object.keys(input.buffers).length > 0;
  const lock: CountLock =
    unsupported !== null
      ? 'refused'
      : !current.allowedActions.canSaveCount
        ? null
        : activeProducts === null
          ? 'unverified'
          : current.items.some(
                (r) =>
                  !r.isCashVarianceProduct &&
                  r.closingValue != null &&
                  !activeProducts.includes(r.productId),
              )
            ? 'retired'
            : null;
  const lockRef = useRef(lock);
  useEffect(() => {
    lockRef.current = lock;
  }, [lock]);

  const acknowledge = (next: CountInput) => {
    live.current.acknowledged = next;
    setAcknowledged(next);
  };
  const adoptSession = (session: ResponseDailyCloseJson) => {
    live.current.version = session.version;
    // A save/Conferir response is the session shape: the review rows and count it lacks are kept.
    const merge = (before: ResponseDailyCloseReviewJson): ResponseDailyCloseReviewJson => ({
      ...before,
      ...session,
      items: before.items,
      count: session.count ?? before.count,
      allowedActions: session.allowedActions ?? before.allowedActions,
      closeFields: session.closeFields ?? before.closeFields,
    });
    live.current.current = merge(live.current.current);
    setCurrent(merge);
  };
  const adoptReview = (fresh: ResponseDailyCloseReviewJson) => {
    live.current.version = fresh.version;
    live.current.current = fresh;
    setCurrent(fresh);
    setSavedAt(fresh.updatedAt ?? null);
    acknowledge(serverInput(fresh));
  };

  const save = useCallback(
    async (explicit = false) => {
      if (flight.current) {
        queued.current = true;
        return;
      }
      const state = live.current;
      if (
        engine.getSnapshot().writesPaused ||
        !state.current.allowedActions.canSaveCount ||
        lockRef.current !== null
      )
        return;
      const sent = state.input;
      const request = countRequest(state.current, sent, state.acknowledged);
      const body = JSON.stringify(request);
      if (!explicit && body === refused) return;
      flight.current = true;
      queued.current = false;
      setPhase('saving');
      setFailure(null);
      lastSent.current = sent;
      const kept = await draft.keep(sent, state.acknowledged);
      try {
        const response = await putSessionSessionIdItems(
          sessionId,
          request,
          { 'If-Match': toIfMatch(state.version) },
          { client: engine.client() },
        );
        if (!alive.current) return;
        const session = response.data.dailyClose;
        if (parseETag(response.headers.get('etag')) !== session.version)
          throw new SessionGateError('A versão do fechamento não pôde ser verificada.');
        acknowledge(
          sessionInput(
            session,
            state.current.items.filter((r) => !r.isCashVarianceProduct).map((r) => r.productId),
          ),
        );
        adoptSession(session);
        setSavedAt(session.updatedAt ?? null);
        setSuccessor(response.data.affectedSuccessor ?? null);
        setRefused(null);
        setPhase('idle');
        // Removes the device copy only if no newer edit replaced it meanwhile.
        await draft.acknowledge(kept);
      } catch (error) {
        if (!alive.current) return;
        const known = actionFailure(error);
        setFailure(known);
        if (known.kind === 'uncertain') setPhase('uncertain');
        else if (known.kind === 'conflict') setPhase('stale');
        else {
          // A 404 on this guarded save: a row of this exact candidate is not accepted by the product
          // contract (gap AA) or the session is gone. It is never resent unchanged.
          if (isApiError(error) && error.status === 404) setUnsupported(body);
          setRefused(body);
          setPhase('failed');
        }
      } finally {
        flight.current = false;
        if (alive.current && queued.current) setQueuedTick((n) => n + 1);
      }
    },
    [engine, sessionId, refused, draft],
  );

  // Autosave: today's plain Draft, after a real edit, debounced, coalesced, never after a refusal.
  useEffect(() => {
    if (
      !autosave ||
      !edited ||
      !dirty ||
      phase === 'saving' ||
      phase === 'stale' ||
      phase === 'uncertain' ||
      lock !== null ||
      reconciliation ||
      snapshot.writesPaused ||
      openCalculations ||
      fingerprint === refused
    )
      return;
    const timer = setTimeout(() => void save(), autosaveTiming.delayMs);
    return () => clearTimeout(timer);
  }, [
    autosave,
    edited,
    dirty,
    fingerprint,
    phase,
    lock,
    reconciliation,
    snapshot.writesPaused,
    openCalculations,
    refused,
    queuedTick,
    save,
  ]);

  /** Functional updates compose: two edits in one gesture never drop each other. */
  const setInput = (update: CountInput | ((previous: CountInput) => CountInput), manual = true) => {
    if (lockRef.current !== null) return;
    const next = typeof update === 'function' ? update(live.current.input) : update;
    live.current.input = next;
    setInputState(next);
    if (manual) setEdited(true);
    void draft.keep(next, live.current.acknowledged);
  };

  /**
   * Recarregar e conferir: a fresh review. An unknown save whose values are now on the server is
   * confirmed without resending; otherwise current and local values are shown for a deliberate
   * choice. Local edits are never overwritten blindly.
   */
  const reload = useCallback(async () => {
    const fresh = await readReview(engine, sessionId);
    const server = serverInput(fresh);
    const previous = live.current.acknowledged;
    adoptReview(fresh);
    setFailure(null);
    setPhase('idle');
    setRefused(null);
    const mine = live.current.input;
    const sent = lastSent.current;
    if (sameCount(server, mine)) return fresh;
    if (sent && sameCount(server, sent)) return fresh; // the unknown save landed; later edits stay dirty
    if (!sameCount(server, previous)) setReconciliation({ server, mine });
    return fresh;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [engine, sessionId]);

  const saveState: SaveState = draft.problem
    ? 'storage-failed'
    : phase === 'saving'
      ? 'saving'
      : phase === 'uncertain'
        ? 'uncertain'
        : phase === 'stale'
          ? 'stale'
          : phase === 'failed' && dirty
            ? 'failed'
            : dirty
              ? autosave
                ? 'dirty'
                : 'unsaved-server'
              : neverRecorded
                ? 'never'
                : 'saved';
  return {
    current,
    input,
    setInput,
    acknowledged,
    version: () => live.current.version,
    dirty,
    neverRecorded,
    autosave,
    saveState,
    savedAt,
    failure,
    successor,
    dismissSuccessor: () => setSuccessor(null),
    unsupported: unsupported !== null,
    lock,
    openCalculations,
    saving: phase === 'saving',
    canSave:
      current.allowedActions.canSaveCount &&
      phase !== 'saving' &&
      phase !== 'stale' &&
      phase !== 'uncertain' &&
      !reconciliation &&
      (dirty || neverRecorded) &&
      lock === null &&
      !openCalculations,
    save,
    reload,
    reconciliation,
    useServer: () => {
      if (!reconciliation) return;
      live.current.input = reconciliation.server;
      setInputState(reconciliation.server);
      setReconciliation(null);
    },
    keepMine: () => setReconciliation(null),
    adopt: adoptSession,
    adoptReview,
    draft,
    /** Restoration fills the form for review only: no autosave until a real edit, nothing sent. */
    restore: (retained: CountInput) => {
      live.current.input = retained;
      setInputState(retained);
      setEdited(false);
      return sameCount(acknowledged, retained);
    },
    /** After a confirmed submission: remove the device copy, keeping retention for later edits. */
    clearDraft: async () => {
      const kept = await draft.keep(live.current.input, live.current.acknowledged);
      await draft.acknowledge(kept);
    },
  };
}
export type CountForm = ReturnType<typeof useCountForm>;
