import type { NextRequest } from 'next/server';
import { serverEnv } from '@/config/server-env';
import { forward } from '@/server/bff/forward';
import { allowedOriginFor } from '@/server/bff/policy';
import { requestContext, respond } from '@/server/bff/route-support';
import { readSessionCookies } from '@/server/session/cookies';

// The cookie-to-bearer forwarder for `@loto/api/queries` hooks (Web M2 Phase 4).
async function handle(request: NextRequest) {
  const cookies = readSessionCookies(request.cookies);
  const context = requestContext(cookies);
  const result = await forward(request, cookies, {
    context,
    apiBaseUrl: serverEnv.apiBaseUrl,
    allowedOrigin: allowedOriginFor(request, serverEnv.webOrigin),
  });
  return respond(context, cookies.meta, result.response, result.writes);
}

export { handle as GET, handle as POST, handle as PUT, handle as PATCH, handle as DELETE };
