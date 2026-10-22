import type { NextRequest } from 'next/server';
import { admit, readJsonObject, runCommand } from '@/server/bff/route-support';
import { commandResponse } from '@/server/bff/session-responses';
import { leaveBranch, selectBranch } from '@/server/session/service';

/** Select or switch to a branch: `POST /branch/session` with the identity token. */
export async function POST(request: NextRequest) {
  const admitted = admit(request, true);
  if (admitted instanceof Response) return admitted;
  const { context, cookies } = admitted;
  const body = await readJsonObject(request);
  const branchId = typeof body.branchId === 'string' ? body.branchId : '';
  const result = await runCommand(admitted, () => selectBranch(context, cookies, branchId));
  return commandResponse(context, cookies, result);
}

/** Leave the current branch (first half of Switch branch): back to the choice, branch state dropped. */
export async function DELETE(request: NextRequest) {
  const admitted = admit(request, true);
  if (admitted instanceof Response) return admitted;
  const { context, cookies } = admitted;
  const result = await runCommand(admitted, () => leaveBranch(context, cookies));
  return commandResponse(context, cookies, result);
}
