import type { NextRequest } from 'next/server';
import { admit, json, runCommand } from '@/server/bff/route-support';
import { resolveSession } from '@/server/session/service';

/**
 * The current authority state (after renewal when needed). A branch tab sends its route's branch
 * (`x-loto-branch`) and receives that branch's state; `?branches=1` (Branch picker) also lists the
 * identity's branches and applies the membership rule.
 */
export async function GET(request: NextRequest) {
  const admitted = admit(request, false);
  if (admitted instanceof Response) return admitted;
  const { context, cookies, declaredBranch } = admitted;
  const needBranches = request.nextUrl.searchParams.get('branches') === '1';
  // Serialized after any pending session command of this browser; a read never compares scope.
  const result = await runCommand(
    admitted,
    async () => {
      const { snapshot, writes } = await resolveSession(context, cookies, {
        branchId: declaredBranch,
        needBranches,
      });
      return { snapshot, writes };
    },
    { compareScope: false },
  );
  if (result === 'superseded') throw new Error('A session read is never superseded.');
  return json(context, cookies.meta, result.snapshot, result.writes);
}
