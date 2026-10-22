import type { NextRequest } from 'next/server';
import { admit, readJsonObject, runCommand } from '@/server/bff/route-support';
import { commandResponse } from '@/server/bff/session-responses';
import { login } from '@/server/session/service';

/** `POST /user/login` through the BFF. The password is passed on once and never kept or logged. */
export async function POST(request: NextRequest) {
  const admitted = admit(request, true);
  if (admitted instanceof Response) return admitted;
  const { context, cookies } = admitted;
  const body = await readJsonObject(request);
  const email = typeof body.email === 'string' ? body.email : '';
  const password = typeof body.password === 'string' ? body.password : '';
  const result = await runCommand(admitted, () => login(context, cookies, { email, password }));
  // A login starts a new generation: its writes are not fenced by the one it replaced.
  return commandResponse(context, cookies, result, { newGeneration: true });
}
