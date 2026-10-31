import { getTimeentryTimeEntryId, getReportOperatorSummary } from '@loto/api';
import type { SessionEngine } from '@/session/engine';
import { matchDestination } from '@/shell/policy';
import { datePart, periodFor } from './contracts';
/** Fresh ownership validation for shipped M9 resources only. Other milestones remain refused. */
export async function validateTimeResource(engine: SessionEngine, path: string): Promise<boolean> {
  const snapshot = engine.getSnapshot();
  if (snapshot.status !== 'branch' || !snapshot.branch || !snapshot.clock) return false;
  const url = new URL(path, 'https://navigation.invalid');
  const destination = matchDestination(url.pathname);
  try {
    if (destination?.key === 'time-detail' && snapshot.branch.role !== 'Member') {
      const result = await getTimeentryTimeEntryId(url.pathname.split('/').at(-1)!, {
        client: engine.client(),
      });
      return (
        result.data.branchId === snapshot.branch.id && engine.getSnapshot().scope === snapshot.scope
      );
    }
    if (destination?.key === 'operator-transactions' && snapshot.branch.role !== 'Member') {
      const period = periodFor(
        datePart(snapshot.clock.branchLocalDate)!,
        url.searchParams.get('from') ?? undefined,
        url.searchParams.get('to') ?? undefined,
      );
      if (!period) return false;
      await getReportOperatorSummary(
        { OperatorId: url.pathname.split('/')[3]!, DateFrom: period.from, DateTo: period.to },
        { client: engine.client() },
      );
      return engine.getSnapshot().scope === snapshot.scope;
    }
  } catch {
    return false;
  }
  return false;
}
