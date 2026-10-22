import { ApiError } from '@loto/api';
import { describe, expect, it } from 'vitest';
import { classifyMutationError } from '@/client/mutations/classify';
import { guardedWrite, versionOf } from '@/client/mutations/guarded-write';

const http = (
  status: number,
  messages: string[] = [],
  headers: HeadersInit = {},
  method = 'POST',
) =>
  new ApiError({
    kind: 'http',
    method,
    path: '/transaction',
    dispatched: true,
    status,
    headers: new Headers(headers),
    body: messages.length ? 'error-json' : 'none',
    errorMessages: messages,
  });

describe('mutation outcome classification', () => {
  it('separates proven-not-executed, definite answers and uncertain outcomes', () => {
    expect(
      classifyMutationError(http(409, [], { 'x-loto-bff-error': 'session-scope-changed' })),
    ).toEqual({
      kind: 'not-sent',
      bffError: 'session-scope-changed',
    });
    expect(
      classifyMutationError(http(503, [], { 'x-loto-bff-error': 'renewal-unconfirmed' })).kind,
    ).toBe('not-sent');
    expect(
      classifyMutationError(
        new ApiError({ kind: 'invalid-request', method: 'POST', path: '/x', dispatched: false }),
      ),
    ).toEqual({ kind: 'not-sent', bffError: null });
    expect(classifyMutationError(http(500, ['Erro interno']))).toEqual({
      kind: 'uncertain',
      messages: ['Erro interno'],
      reason: 'server-error',
    });
    expect(classifyMutationError(http(502)).kind).toBe('uncertain');
    expect(
      classifyMutationError(
        new ApiError({ kind: 'network', method: 'POST', path: '/x', dispatched: true }),
      ),
    ).toMatchObject({ kind: 'uncertain', reason: 'no-response' });
    expect(
      classifyMutationError(
        new ApiError({ kind: 'aborted', method: 'POST', path: '/x', dispatched: true }),
      ).kind,
    ).toBe('uncertain');
    expect(
      classifyMutationError(
        new ApiError({
          kind: 'invalid-response',
          method: 'POST',
          path: '/x',
          dispatched: true,
          status: 201,
        }),
      ),
    ).toMatchObject({ kind: 'uncertain', reason: 'unreadable-success' });
  });

  it('keeps every backend message verbatim and reads advisory Retry-After', () => {
    expect(
      classifyMutationError(http(401, ['Token expirado'], { 'x-loto-session': 'renewed' })),
    ).toEqual({
      kind: 'auth-refused',
      messages: ['Token expirado'],
      session: 'renewed',
    });
    expect(classifyMutationError(http(429, ['Aguarde'], { 'Retry-After': '30' }))).toEqual({
      kind: 'rate-limited',
      messages: ['Aguarde'],
      retryAfterSeconds: 30,
    });
    expect(classifyMutationError(http(409, ['A', 'A']))).toEqual({
      kind: 'conflict',
      messages: ['A', 'A'],
    });
    expect(classifyMutationError(http(400, ['x', 'y']))).toEqual({
      kind: 'rejected',
      status: 400,
      messages: ['x', 'y'],
    });
  });
});

describe('guarded writes', () => {
  it('round-trips the ETag version into If-Match and reads the new version', async () => {
    let sent = '';
    const result = await guardedWrite(41, async (ifMatch) => {
      sent = ifMatch;
      return { headers: new Headers({ ETag: '"42"' }) };
    });
    expect(sent).toBe('"41"');
    expect(result).toMatchObject({ kind: 'saved', version: 42 });
    expect(versionOf({ headers: new Headers() }, 7)).toBe(7);
    expect(() => versionOf({ headers: new Headers({ ETag: 'W/"1"' }) })).toThrow();
  });

  it('turns a stale 409 into the reload path with verbatim messages, without retrying', async () => {
    let calls = 0;
    const result = await guardedWrite(41, async () => {
      calls += 1;
      throw http(409, ['Os dados foram alterados por outra pessoa.', 'Recarregue.']);
    });
    expect(calls).toBe(1);
    expect(result).toEqual({
      kind: 'conflict',
      messages: ['Os dados foram alterados por outra pessoa.', 'Recarregue.'],
      recovery: 'reload-and-review',
    });
  });

  it('never invents a version and never mistakes a BFF scope refusal for a stale version', async () => {
    let calls = 0;
    expect(
      await guardedWrite(null, async () => ((calls += 1), { headers: new Headers() })),
    ).toEqual({
      kind: 'missing-version',
    });
    expect(calls).toBe(0);
    const refused = await guardedWrite(3, async () => {
      throw http(409, [], { 'x-loto-bff-error': 'session-scope-changed' });
    });
    expect(refused).toEqual({
      kind: 'failed',
      failure: { kind: 'not-sent', bffError: 'session-scope-changed' },
    });
  });
});
