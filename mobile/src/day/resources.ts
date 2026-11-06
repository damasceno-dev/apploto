import { getSessionSessionId } from '@loto/api';
import type { SessionEngine } from '@/session/engine';
import { matchDestination } from '@/shell/policy';
import { validateTimeResource } from '@/time/resources';
/**
 * Fresh validation of an M5 resource origin/intent: the exact session must still be readable in
 * this branch by the current caller (server scope/role), and only then may navigation restore it.
 * A stored id, URL or mode never grants access.
 */
export async function validateDayResource(engine: SessionEngine, path: string): Promise<boolean> {
  const snapshot = engine.getSnapshot();
  if (snapshot.status !== 'branch' || !snapshot.branch || !snapshot.clock) return false;
  const url = new URL(path, 'https://navigation.invalid');
  const destination = matchDestination(url.pathname);
  if (destination?.key !== 'close' && destination?.key !== 'correction') return false;
  try {
    const session = await getSessionSessionId(url.pathname.split('/')[2]!, {
      client: engine.client(),
    });
    return (
      session.data.branchId === snapshot.branch.id && engine.getSnapshot().scope === snapshot.scope
    );
  } catch {
    return false;
  }
}
/** Every shipped feature-owned resource validator (M9 time, M5 sessions). Others stay refused. */
export async function validateResource(engine: SessionEngine, path: string): Promise<boolean> {
  return (await validateTimeResource(engine, path)) || (await validateDayResource(engine, path));
}
