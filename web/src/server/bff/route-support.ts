import 'server-only';
import { randomUUID } from 'node:crypto';
import { NextResponse, type NextRequest } from 'next/server';
import { serverEnv } from '@/config/server-env';
import { scopeHeader } from '@/lib/bff-protocol';
import { createAuthBackend, type AuthBackend } from '../session/backend';
import {
  deviceCookieName,
  readSessionCookies,
  type CookieWrite,
  type SessionCookies,
} from '../session/cookies';
import { serializeDevice, sessionRegistry } from '../session/registry';
import {
  fenceWrites,
  scopeAfter,
  type SessionCommandResult,
  type SessionContext,
} from '../session/service';
import { allowedOriginFor, checkBffRequest } from './policy';
import { applyCookieWrites, bffErrorResponse, jsonResponse } from './responses';

let backend: AuthBackend | undefined;

/** Device cookies minted for a request, written with its response whatever the fence decides. */
const mintedDevices = new WeakMap<SessionContext, CookieWrite>();

/**
 * A fresh context per request: shared registry and stateless backend, a unique owner, and this
 * browser's device id (minted, and set on the response, when the browser has none yet).
 */
export function requestContext(cookies: SessionCookies): SessionContext {
  backend ??= createAuthBackend({ baseUrl: serverEnv.apiBaseUrl });
  const device = cookies.device ?? randomUUID();
  const context: SessionContext = {
    registry: sessionRegistry(),
    backend,
    owner: {},
    device,
    fixtures: serverEnv.e2eFixtures,
  };
  if (cookies.device === null)
    mintedDevices.set(context, { name: deviceCookieName, value: device });
  return context;
}

/** The device-cookie write this request owes the browser, if any. */
export function deviceWrites(context: SessionContext): CookieWrite[] {
  const write = mintedDevices.get(context);
  return write ? [write] : [];
}

export interface BffRequest {
  readonly context: SessionContext;
  readonly cookies: SessionCookies;
  readonly allowedOrigin: string;
  /** The scope the browser acted on (`x-loto-scope`; null when it held none). */
  readonly declaredScope: string | null;
}

/** Applies the shared request policy; returns the refusal response, or the request's inputs. */
export function admit(request: NextRequest, stateChanging: boolean): BffRequest | Response {
  const allowedOrigin = allowedOriginFor(request, serverEnv.webOrigin);
  const refusal = checkBffRequest(request, allowedOrigin, stateChanging);
  if (refusal !== null) return bffErrorResponse(refusal);
  if (stateChanging && !isJson(request) && request.method !== 'DELETE') {
    return bffErrorResponse('unsupported-media-type');
  }
  const cookies = readSessionCookies(request.cookies);
  return {
    context: requestContext(cookies),
    cookies,
    allowedOrigin,
    declaredScope: request.headers.get(scopeHeader),
  };
}

/**
 * Runs one session command for this browser, serialized with its other session commands and
 * applied only to the authority it was issued against: the scope the browser declared must be the
 * scope its last committed command left (or, if this process has not seen it, the scope its
 * cookies hold). Otherwise the command is `superseded` and changes nothing — a late login cannot
 * restore a session a later logout ended, and a late logout cannot end a newer login.
 */
export function runCommand(
  admitted: BffRequest,
  command: () => Promise<SessionCommandResult>,
  options: { readonly compareScope: boolean } = { compareScope: true },
): Promise<SessionCommandResult | 'superseded'> {
  const { context, cookies, declaredScope } = admitted;
  const cookieScope = cookies.meta?.scope ?? null;
  return serializeDevice<SessionCommandResult | 'superseded'>(
    context.registry,
    context.device ?? randomUUID(),
    async (current) => {
      const authoritative = current === undefined ? cookieScope : current;
      if (
        options.compareScope &&
        (declaredScope !== authoritative || cookieScope !== authoritative)
      ) {
        return { value: 'superseded' as const, scope: undefined };
      }
      // A read is serialized but not compared: cookies of an ended generation never authorize.
      const result = await command();
      return { value: result, scope: scopeAfter(result.writes) };
    },
  );
}

/** Converts a core response and applies fenced cookie writes (`base` null: a new generation). */
export function respond(
  context: SessionContext,
  base: SessionCookies['meta'],
  response: Response,
  writes: readonly CookieWrite[],
): NextResponse {
  const next = new NextResponse(response.body, {
    status: response.status,
    headers: response.headers,
  });
  applyCookieWrites(next.cookies, [
    ...deviceWrites(context),
    ...fenceWrites(context, base, writes),
  ]);
  return next;
}

export function json(
  context: SessionContext,
  base: SessionCookies['meta'],
  body: unknown,
  writes: readonly CookieWrite[],
  status = 200,
  headers: HeadersInit = {},
): NextResponse {
  return respond(context, base, jsonResponse(body, status, headers), writes);
}

/** Reads a small JSON object body; anything else yields an empty object for the API to validate. */
export async function readJsonObject(request: NextRequest): Promise<Record<string, unknown>> {
  try {
    const value: unknown = await request.json();
    return typeof value === 'object' && value !== null && !Array.isArray(value)
      ? (value as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

function isJson(request: NextRequest): boolean {
  return (request.headers.get('content-type') ?? '').toLowerCase().startsWith('application/json');
}
