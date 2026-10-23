'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import type { ApiRequestOptions } from '@loto/api';
import { anonymousSnapshot, bffSessionPath } from '@/lib/bff-protocol';
import { bffFetch, latestServerTimeMs } from '../bff-fetch';
import { createBrowserApiClient } from '../query/browser-api';
import { useSession } from '../session/session-provider';
import { deviceStorage, partitionFor } from '../storage/partition';
import { OperationController, type OperationResult } from './operation-controller';
import type { OperationRecord } from './operation-store';

export interface IdempotentOperation<P, R> {
  /** False until the authorized partition is known (no write is possible before that). */
  readonly ready: boolean;
  readonly record: OperationRecord | null;
  readonly durable: boolean;
  /** Device storage could not be read: no operation starts until the user recovers deliberately. */
  readonly storageProblem: 'unavailable' | 'corrupt' | null;
  readonly submit: (payload: P) => Promise<OperationResult<R>>;
  readonly verifyAndRetry: () => Promise<OperationResult<R>>;
  readonly discard: () => void;
  readonly retryStorage: () => void;
  readonly discardCorrupt: () => void;
  readonly continueWithoutDeviceRecord: () => void;
}

/**
 * React binding of `OperationController` for one `Idempotency-Key` create in the current session
 * scope. Each controller sends through a client bound to the scope it was created in, so after a
 * logout or a user/branch change its request is refused by the forwarder; the controller is also
 * disposed when the scope ends or the component unmounts. Its record stays in its own partition.
 */
export function useIdempotentOperation<P, R>(config: {
  readonly context: string;
  readonly operation: string;
  readonly send: (payload: P, idempotencyKey: string, options: ApiRequestOptions) => Promise<R>;
}): IdempotentOperation<P, R> {
  const { writesPaused, snapshot } = useSession();
  const [controller, setController] = useState<OperationController<P, R> | null>(null);
  const [view, setView] = useState<{
    record: OperationRecord | null;
    durable: boolean;
    storageProblem: 'unavailable' | 'corrupt' | null;
  }>({ record: null, durable: true, storageProblem: null });
  const { context, operation, send } = config;
  const pausedRef = useRef(writesPaused);
  const sendRef = useRef(send);
  useEffect(() => {
    pausedRef.current = writesPaused;
    sendRef.current = send;
  }, [writesPaused, send]);
  // Only the partition's inputs recreate the controller (never a re-render or a new `send`).
  const { state, scope } = snapshot;
  const user = useStableValue(snapshot.user);
  const branch = useStableValue(snapshot.branch);

  useEffect(() => {
    let current = true;
    let created: OperationController<P, R> | null = null;
    void partitionFor({ ...anonymousSnapshot, state, scope, user, branch }).then((partition) => {
      if (!current) return;
      if (partition === null || scope === null) {
        setController(null);
        return;
      }
      created = new OperationController<P, R>({
        storage: deviceStorage(),
        partition,
        context,
        operation,
        // Bound to this scope and this tab's branch for the controller's whole life: its writes
        // are never sent under another branch, even after the tab moves on.
        client: createBrowserApiClient(scope, branch?.id ?? null),
        send: (payload, key, options) => sendRef.current(payload, key, options),
        writesPaused: () => pausedRef.current,
        serverNow: latestServerTimeMs,
        refreshServerTime: () =>
          bffFetch(bffSessionPath, { method: 'GET' }, scope, branch?.id ?? null),
      });
      setController(created);
      setView(describe(created));
    });
    return () => {
      current = false;
      created?.dispose();
    };
  }, [state, scope, user, branch, context, operation]);

  const run = useCallback(
    async (action: (target: OperationController<P, R>) => Promise<OperationResult<R>>) => {
      if (controller === null) return { kind: 'paused' } as const;
      const result = await action(controller);
      setView(describe(controller));
      return result;
    },
    [controller],
  );
  const act = (action: (target: OperationController<P, R>) => void) => {
    if (controller === null) return;
    action(controller);
    setView(describe(controller));
  };

  return {
    ready: controller !== null,
    ...view,
    submit: (payload) => run((target) => target.submit(payload)),
    verifyAndRetry: () => run((target) => target.verifyAndRetry()),
    discard: () => act((target) => target.discard()),
    retryStorage: () => act((target) => target.retryStorage()),
    discardCorrupt: () => act((target) => target.discardCorrupt()),
    continueWithoutDeviceRecord: () => act((target) => target.continueWithoutDeviceRecord()),
  };
}

function describe<P, R>(controller: OperationController<P, R>) {
  return {
    record: controller.current(),
    durable: controller.durable,
    storageProblem: controller.storageProblem(),
  };
}

/** Keeps an object's identity while its JSON value is unchanged (snapshot re-reads build new ones). */
function useStableValue<T>(value: T): T {
  const [stable, setStable] = useState(value);
  if (JSON.stringify(stable) !== JSON.stringify(value)) setStable(value);
  return JSON.stringify(stable) === JSON.stringify(value) ? stable : value;
}
