import { NextResponse, type NextRequest } from 'next/server';
import { serverEnv } from '@/config/server-env';
import { decideGate, routeTier, validateIntentPath } from '@/lib/route-policy';
import { deviceWrites, requestContext } from '@/server/bff/route-support';
import { applyCookieWrites, noStoreHeaders } from '@/server/bff/responses';
import {
  cookieNames,
  encodeIntent,
  readSessionCookies,
  type CookieWrite,
} from '@/server/session/cookies';
import { serializeDevice } from '@/server/session/registry';
import { fenceWrites, resolveSession, scopeAfter } from '@/server/session/service';

/**
 * The page-level session gate (Web M2 Phase 5). Before any gated route renders, it renews expiring
 * credentials (the only place a page request can set cookies), resolves the authority state and
 * either lets the render see the refreshed cookies or redirects to Login / Branch picker. A gated
 * branch deep link is kept as an unexecuted navigation intent.
 */
export async function proxy(request: NextRequest) {
  const fixtures = serverEnv.e2eFixtures;
  const tier = routeTier(request.nextUrl.pathname, { fixtures });
  const cookies = readSessionCookies(request.cookies);
  // Every page gives the browser its device id before any session command needs it.
  const context = requestContext(cookies);
  const device = deviceWrites(context);
  if (tier === 'public' || tier === 'anonymous') {
    const response = NextResponse.next();
    applyCookieWrites(response.cookies, device);
    return response;
  }

  // Serialized after this browser's pending session commands, like `GET /bff/session`.
  const { snapshot, writes } = await serializeDevice(
    context.registry,
    context.device ?? '',
    async () => {
      const resolved = await resolveSession(context, cookies, { needBranches: tier === 'branch' });
      return { value: resolved, scope: scopeAfter(resolved.writes) };
    },
  );
  const fenced = [...device, ...fenceWrites(context, cookies.meta, writes)];
  const decision = decideGate(tier, snapshot);

  if (decision.kind === 'redirect') {
    const response = NextResponse.redirect(new URL(decision.to, request.url));
    const intentPath =
      decision.holdIntent && request.method === 'GET' && !isPrefetch(request)
        ? validateIntentPath(request.nextUrl.pathname + request.nextUrl.search, { fixtures })
        : null;
    const intent: CookieWrite[] =
      intentPath === null
        ? []
        : [
            {
              name: cookieNames.intent,
              value: encodeIntent({
                v: 1,
                path: intentPath,
                branchId: cookies.meta?.branchId ?? null,
                email: snapshot.user?.email ?? null,
              }),
            },
          ];
    applyCookieWrites(response.cookies, [...fenced, ...intent]);
    for (const [name, value] of Object.entries(noStoreHeaders)) response.headers.set(name, value);
    return response;
  }

  const headers = new Headers(request.headers);
  if (fenced.length > 0) headers.set('cookie', cookieHeaderAfter(request, fenced));
  const response = NextResponse.next({ request: { headers } });
  applyCookieWrites(response.cookies, fenced);
  return response;
}

/** The request's `Cookie` header with this request's renewals applied, for the render. */
function cookieHeaderAfter(request: NextRequest, writes: readonly CookieWrite[]): string {
  const values = new Map(request.cookies.getAll().map((cookie) => [cookie.name, cookie.value]));
  for (const write of writes) {
    if (write.value === null) values.delete(write.name);
    else values.set(write.name, write.value);
  }
  return [...values].map(([name, value]) => `${name}=${encodeURIComponent(value)}`).join('; ');
}

function isPrefetch(request: NextRequest): boolean {
  return (
    request.headers.get('next-router-prefetch') === '1' ||
    request.headers.get('purpose') === 'prefetch' ||
    request.headers.get('sec-purpose')?.includes('prefetch') === true
  );
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|bff/).*)'],
};
