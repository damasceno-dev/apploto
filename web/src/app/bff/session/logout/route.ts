import type { NextRequest } from 'next/server';
import { admit, runCommand } from '@/server/bff/route-support';
import { commandResponse } from '@/server/bff/session-responses';
import { logout } from '@/server/session/service';

/**
 * Sair: revoke this device's refresh session, then clear every session cookie regardless — but only
 * for the session the browser declared; a newer login is never ended by an older Sair.
 */
export async function POST(request: NextRequest) {
  const admitted = admit(request, true);
  if (admitted instanceof Response) return admitted;
  const { context, cookies } = admitted;
  const result = await runCommand(admitted, () => logout(context, cookies));
  return commandResponse(context, cookies, result, { newGeneration: true });
}
