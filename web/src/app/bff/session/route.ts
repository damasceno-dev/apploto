import type { NextRequest } from 'next/server';
import { admit, json, runCommand } from '@/server/bff/route-support';
import { resolveSession } from '@/server/session/service';

/** The current authority state (after renewal / branch-session recreation when needed). */
export async function GET(request: NextRequest) {
  const admitted = admit(request, false);
  if (admitted instanceof Response) return admitted;
  const { context, cookies } = admitted;
  // Serialized after any pending session command of this browser; a read never compares scope.
  const result = await runCommand(
    admitted,
    async () => {
      const { snapshot, writes } = await resolveSession(context, cookies);
      return { snapshot, writes };
    },
    { compareScope: false },
  );
  if (result === 'superseded') throw new Error('A session read is never superseded.');
  return json(context, cookies.meta, result.snapshot, result.writes);
}
