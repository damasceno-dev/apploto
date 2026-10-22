import type { SessionSnapshot } from '@/lib/bff-protocol';

/**
 * The device-storage partition for durable input and operation records (catalog: Durable scoped
 * input): the exact e-mail returned by a successful login — hashed, so the address itself is not a
 * storage key — plus the branch id. It exists only for an authorized branch session, so input is
 * never readable before authentication succeeds or under another account or branch.
 */
export interface StoragePartition {
  readonly userKey: string;
  readonly branchId: string;
}

export async function partitionFor(snapshot: SessionSnapshot): Promise<StoragePartition | null> {
  if (snapshot.state !== 'branch' || snapshot.user === null || snapshot.branch === null)
    return null;
  return { userKey: await sha256Hex(snapshot.user.email), branchId: snapshot.branch.id };
}

export async function sha256Hex(value: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value));
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, '0')).join('');
}

/** Durable device storage, or null when the browser refuses it (private mode, policy, quota). */
export function deviceStorage(): Storage | null {
  try {
    const storage = window.localStorage;
    const probe = 'loto:probe';
    storage.setItem(probe, '1');
    storage.removeItem(probe);
    return storage;
  } catch {
    return null;
  }
}

const contextPattern = /^[A-Za-z0-9][A-Za-z0-9:/._-]{0,199}$/;

/** A storage key inside one partition; `context` names the form/operation and its resource. */
export function partitionKey(
  namespace: string,
  partition: StoragePartition,
  context: string,
): string {
  if (!contextPattern.test(context) || context.includes('..')) {
    throw new RangeError(`Invalid device-storage context: ${JSON.stringify(context)}.`);
  }
  return `${partitionPrefix(namespace, partition)}${context}`;
}

export function partitionPrefix(namespace: string, partition: StoragePartition): string {
  return `${namespace}:${partition.userKey}:${partition.branchId}:`;
}
