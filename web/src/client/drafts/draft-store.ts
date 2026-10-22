import { partitionKey, partitionPrefix, type StoragePartition } from '../storage/partition';
import { assertDeviceSafe, DevicePrivacyError } from '../storage/privacy';

export const draftNamespace = 'loto:draft:v1';

export type DraftSaveResult =
  | { readonly ok: true }
  /** Show `deviceStorageFailed` copy and keep the form in memory; never claim it was kept. */
  | { readonly ok: false; readonly reason: 'unavailable' | 'privacy'; readonly path?: string };

export type DraftLoadResult<T> =
  | { readonly status: 'none' }
  | { readonly status: 'found'; readonly data: T }
  /** Unreadable bytes: kept until the user deliberately discards them. */
  | { readonly status: 'corrupt' }
  | { readonly status: 'unavailable' };

interface StoredDraft {
  readonly v: 1;
  readonly data: unknown;
}

/** Saves `data` for one form/resource context. Throws nothing: storage failure is a result. */
export function saveDraft(
  storage: Storage | null,
  partition: StoragePartition,
  context: string,
  data: unknown,
): DraftSaveResult {
  try {
    assertDeviceSafe(data);
  } catch (error) {
    if (error instanceof DevicePrivacyError)
      return { ok: false, reason: 'privacy', path: error.path };
    throw error;
  }
  const key = partitionKey(draftNamespace, partition, context);
  if (storage === null) return { ok: false, reason: 'unavailable' };
  try {
    const record: StoredDraft = { v: 1, data };
    storage.setItem(key, JSON.stringify(record));
    return { ok: true };
  } catch {
    return { ok: false, reason: 'unavailable' };
  }
}

export function loadDraft<T>(
  storage: Storage | null,
  partition: StoragePartition,
  context: string,
): DraftLoadResult<T> {
  const key = partitionKey(draftNamespace, partition, context);
  if (storage === null) return { status: 'unavailable' };
  let raw: string | null;
  try {
    raw = storage.getItem(key);
  } catch {
    return { status: 'unavailable' };
  }
  if (raw === null) return { status: 'none' };
  try {
    const parsed = JSON.parse(raw) as Partial<StoredDraft>;
    if (parsed.v !== 1 || !('data' in parsed)) return { status: 'corrupt' };
    return { status: 'found', data: parsed.data as T };
  } catch {
    return { status: 'corrupt' };
  }
}

/** Clears one context: after a confirmed save, or when the user deliberately discards it. */
export function clearDraft(
  storage: Storage | null,
  partition: StoragePartition,
  context: string,
): boolean {
  const key = partitionKey(draftNamespace, partition, context);
  if (storage === null) return false;
  try {
    storage.removeItem(key);
    return true;
  } catch {
    return false;
  }
}

/** Removes only this identity+branch partition's drafts; never a broad storage wipe. */
export function clearPartitionDrafts(storage: Storage | null, partition: StoragePartition): number {
  if (storage === null) return 0;
  const prefix = partitionPrefix(draftNamespace, partition);
  const keys: string[] = [];
  try {
    for (let index = 0; index < storage.length; index += 1) {
      const key = storage.key(index);
      if (key?.startsWith(prefix)) keys.push(key);
    }
    for (const key of keys) storage.removeItem(key);
  } catch {
    return 0;
  }
  return keys.length;
}
