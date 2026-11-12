import { getSessionSessionIdReview } from '@loto/api';
import type { SessionEngine } from '@/session/engine';
import { matchDestination } from '@/shell/policy';
import { isId, managerRole } from './contracts';

/**
 * Fresh validation of an M7 resource origin: the exact session review must be readable now, in this
 * branch, by a current Manager/Admin. A stored id, `?from` or retained origin grants nothing.
 */
export async function validateManagerResource(
  engine: SessionEngine,
  path: string,
): Promise<boolean> {
  const snapshot = engine.getSnapshot();
  if (snapshot.status !== 'branch' || !snapshot.branch || !snapshot.clock) return false;
  if (!managerRole(snapshot.branch.role)) return false;
  const url = new URL(path, 'https://navigation.invalid');
  if (matchDestination(url.pathname)?.key !== 'approval') return false;
  const id = url.pathname.split('/')[3];
  if (!isId(id)) return false;
  try {
    const review = await getSessionSessionIdReview(id, { client: engine.client() });
    return (
      review.data.branchId === snapshot.branch.id && engine.getSnapshot().scope === snapshot.scope
    );
  } catch {
    return false;
  }
}
