import type { SessionSnapshot } from '@/lib/bff-protocol';
import { deviceStorage, partitionFor, partitionPrefix } from './partition';
import { draftNamespace } from '../drafts/draft-store';
import { operationNamespace } from '../mutations/operation-store';
/** Presence only; never deserialize or restore financial input from the shell. */
export async function hasScopedRetainedInput(snapshot: SessionSnapshot): Promise<boolean> {
  const partition = await partitionFor(snapshot);
  const storage = deviceStorage();
  if (!partition || !storage) return false;
  const prefixes = [
    partitionPrefix(draftNamespace, partition),
    partitionPrefix(operationNamespace, partition),
  ];
  return Array.from({ length: storage.length }, (_, index) => storage.key(index)).some(
    (key) => key !== null && prefixes.some((prefix) => key.startsWith(prefix)),
  );
}
