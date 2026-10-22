import 'server-only';
import type { NextResponse } from 'next/server';
import type { BffErrorCode } from '@/lib/bff-protocol';
import type { SessionCookies } from '../session/cookies';
import type { CredentialFailure, SessionCommandResult, SessionContext } from '../session/service';
import { bffErrorResponse, noStoreHeaders } from './responses';
import { json, respond } from './route-support';

/**
 * Maps a session command to HTTP: the snapshot on success (with `serverConfirmed` for Sair); a
 * backend refusal with its status, verbatim `ErrorMessages` and `Retry-After`; an unconfirmed
 * dispatch as 502; a credential failure or a superseded command as a not-forwarded BFF error.
 * `newGeneration` (login, logout): the success writes are not fenced by the generation replaced.
 */
export function commandResponse(
  context: SessionContext,
  cookies: SessionCookies,
  result: SessionCommandResult | 'superseded',
  options: { readonly newGeneration?: boolean } = {},
): NextResponse {
  if (result === 'superseded') {
    return respond(context, cookies.meta, bffErrorResponse('session-scope-changed'), []);
  }
  const { error } = result;
  if (error === undefined) {
    const body =
      result.serverConfirmed === undefined
        ? result.snapshot
        : { ...result.snapshot, serverConfirmed: result.serverConfirmed };
    return json(context, options.newGeneration ? null : cookies.meta, body, result.writes);
  }
  switch (error.kind) {
    case 'refused':
      return json(
        context,
        cookies.meta,
        { errorMessages: error.messages },
        result.writes,
        error.status,
        error.retryAfter === null ? {} : { 'retry-after': error.retryAfter },
      );
    case 'unconfirmed':
      return respond(
        context,
        cookies.meta,
        new Response(null, { status: 502, headers: noStoreHeaders }),
        result.writes,
      );
    case 'credentials':
      return respond(
        context,
        cookies.meta,
        bffErrorResponse(codeFor(error.failure)),
        result.writes,
      );
  }
}

function codeFor(failure: CredentialFailure): BffErrorCode {
  return failure === 'stale' ? 'session-scope-changed' : failure;
}
