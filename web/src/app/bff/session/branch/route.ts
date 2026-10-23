import type { NextRequest } from 'next/server';
import { admit, readJsonObject, runCommand } from '@/server/bff/route-support';
import { commandResponse } from '@/server/bff/session-responses';
import { selectBranch } from '@/server/session/service';

/**
 * Opens a branch for this identity (`POST /branch/session` with the identity token). Only that
 * branch's session changes: other tabs keep theirs. The tab then navigates into `/f/{branchId}`.
 */
export async function POST(request: NextRequest) {
  const admitted = admit(request, true);
  if (admitted instanceof Response) return admitted;
  const { context, cookies } = admitted;
  const body = await readJsonObject(request);
  const branchId = typeof body.branchId === 'string' ? body.branchId : '';
  const result = await runCommand(admitted, () => selectBranch(context, cookies, branchId));
  return commandResponse(context, cookies, result);
}
