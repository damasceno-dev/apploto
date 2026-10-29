import { NextResponse, type NextRequest } from 'next/server';
import { matchDestination } from '@/lib/destinations';
import { parseBranchPath } from '@/lib/branch-route';
import { serverEnv } from '@/config/server-env';
import { decideGate, routeBranch, routeTier, validateIntentPath } from '@/lib/route-policy';
import { gateCopy } from '@/lib/session-copy';
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
 * credentials (the only place a page request can set cookies), resolves the authority state — for a
 * `/f/{branchId}` route, the signed-in identity's own session for **that** branch — and either lets
 * the render see the refreshed cookies, redirects to Login / Branch picker, or answers 403 when the
 * route's branch is not open to this identity. A gated deep link is kept as an unexecuted intent.
 */
export async function proxy(request: NextRequest) {
  const fixtures = serverEnv.e2eFixtures;
  const pathname = request.nextUrl.pathname;
  const route = parseBranchPath(pathname);
  if (
    pathname.startsWith('/f/') &&
    (!route ||
      (route.rest !== '/' &&
        matchDestination(route.rest) === null &&
        !(fixtures && ['/e2e/read', '/e2e/writes'].includes(route.rest))))
  ) {
    return new NextResponse(
      '<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Página não encontrada · Lotero</title></head><body><main><h1>Página não encontrada</h1><p>Confira o endereço ou volte para escolher uma filial.</p><a href="/filiais">Escolher filial</a></main></body></html>',
      { status: 404, headers: { ...noStoreHeaders, 'content-type': 'text/html; charset=utf-8' } },
    );
  }
  const tier = routeTier(pathname, { fixtures });
  const cookies = readSessionCookies(request.cookies);
  // Every page gives the browser its device id before any session command needs it.
  const context = requestContext(cookies);
  const device = deviceWrites(context);
  if (tier === 'public' || tier === 'anonymous') {
    const response = NextResponse.next();
    applyCookieWrites(response.cookies, device);
    return response;
  }

  const branchId = tier === 'branch' ? routeBranch(pathname) : null;
  // Serialized after this browser's pending session commands, like `GET /bff/session`.
  const { snapshot, writes } = await serializeDevice(
    context.registry,
    context.device ?? '',
    async () => {
      const resolved = await resolveSession(context, cookies, { branchId });
      return { value: resolved, scope: scopeAfter(resolved.writes) };
    },
  );
  const fenced = [...device, ...fenceWrites(context, cookies.meta, writes)];
  const decision = decideGate(tier, snapshot, { branchId });

  if (decision.kind === 'forbidden') {
    // Not an existence oracle: a branch that does not exist and one without membership read alike.
    const response = new NextResponse(forbiddenPage(decision.messages), {
      status: 403,
      headers: { ...noStoreHeaders, 'content-type': 'text/html; charset=utf-8' },
    });
    applyCookieWrites(response.cookies, fenced);
    return response;
  }

  if (decision.kind === 'redirect') {
    const response = NextResponse.redirect(new URL(decision.to, request.url));
    const intentPath =
      decision.holdIntent && request.method === 'GET' && !isPrefetch(request)
        ? validateIntentPath(pathname + request.nextUrl.search, { fixtures })
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
                branchId,
                email: snapshot.user?.email ?? cookies.meta?.user.email ?? null,
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
  if (branchId !== null) {
    for (const [name, value] of Object.entries(noStoreHeaders)) response.headers.set(name, value);
  }
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

/**
 * A minimal, self-contained 403 document (the gate answers before any app render): the backend's
 * messages verbatim, in order, and the way back to Branch picker. No branch data, no script.
 */
function forbiddenPage(messages: readonly string[]): string {
  const escape = (text: string) =>
    text.replace(/[&<>"']/g, (character) => `&#${character.charCodeAt(0)};`);
  const list =
    messages.length === 0
      ? ''
      : `<ul>${messages.map((message) => `<li>${escape(message)}</li>`).join('')}</ul>`;
  return [
    '<!doctype html><html lang="pt-BR"><head><meta charset="utf-8">',
    '<meta name="viewport" content="width=device-width, initial-scale=1">',
    `<meta name="color-scheme" content="light dark"><title>${escape(gateCopy.forbiddenTitle)} · Lotero</title>`,
    '</head><body><main>',
    `<h1>${escape(gateCopy.forbiddenTitle)}</h1>`,
    `<p>${escape(gateCopy.forbiddenBody)}</p>`,
    list,
    `<p><a href="/filiais">${escape(gateCopy.chooseBranch)}</a></p>`,
    '</main></body></html>',
  ].join('');
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|bff/).*)'],
};
