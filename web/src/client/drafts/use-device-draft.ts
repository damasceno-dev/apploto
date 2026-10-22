'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSession } from '../session/session-provider';
import { deviceStorage, partitionFor, type StoragePartition } from '../storage/partition';
import {
  clearDraft,
  loadDraft,
  saveDraft,
  type DraftLoadResult,
  type DraftSaveResult,
} from './draft-store';

export interface DeviceDraftOptions {
  /**
   * Set by the screen once it has re-read the resource and current permission in this session
   * (catalog: restore only after branch, resource and grants are rechecked). Until then nothing is
   * read back, even for the right partition.
   */
  readonly authorityVerified: boolean;
}

export type DeviceDraftState<T> =
  /** No authorized branch session yet (or it changed): input stays isolated and unread. */
  | { readonly status: 'locked' }
  | { readonly status: 'awaiting-authority' }
  | { readonly status: 'loaded'; readonly result: DraftLoadResult<T> };

export interface DeviceDraft<T> {
  readonly state: DeviceDraftState<T>;
  readonly save: (data: T) => DraftSaveResult;
  /** After a confirmed server success, or a deliberate discard. */
  readonly clear: () => void;
}

/**
 * Durable device input for one form/resource `context`, partitioned by the returned login e-mail and
 * branch. A different account or branch derives a different partition, so it can neither see nor
 * overwrite this input; nothing here clears another partition.
 */
export function useDeviceDraft<T>(context: string, options: DeviceDraftOptions): DeviceDraft<T> {
  const { snapshot } = useSession();
  const [derived, setDerived] = useState<{
    readonly scope: string | null;
    readonly partition: StoragePartition | null;
  } | null>(null);

  useEffect(() => {
    let current = true;
    void partitionFor(snapshot).then((partition) => {
      if (current) setDerived({ scope: snapshot.scope, partition });
    });
    return () => {
      current = false;
    };
  }, [snapshot]);
  // A partition derived for another scope is never used, even for the render before it updates.
  const partition = derived?.scope === snapshot.scope ? derived.partition : null;

  // Bumped by save/clear so the next read reflects this tab's own writes.
  const [revision, setRevision] = useState(0);
  const verified = options.authorityVerified;
  const loaded = useMemo<DraftLoadResult<T> | null>(() => {
    void revision;
    return partition !== null && verified
      ? loadDraft<T>(deviceStorage(), partition, context)
      : null;
  }, [partition, verified, context, revision]);

  const save = useCallback(
    (data: T): DraftSaveResult => {
      if (partition === null) return { ok: false, reason: 'unavailable' };
      const result = saveDraft(deviceStorage(), partition, context, data);
      setRevision((value) => value + 1);
      return result;
    },
    [partition, context],
  );
  const clear = useCallback(() => {
    if (partition === null) return;
    clearDraft(deviceStorage(), partition, context);
    setRevision((value) => value + 1);
  }, [partition, context]);

  let state: DeviceDraftState<T>;
  if (partition === null) state = { status: 'locked' };
  else if (!verified || loaded === null) state = { status: 'awaiting-authority' };
  else state = { status: 'loaded', result: loaded };
  return { state, save, clear };
}
