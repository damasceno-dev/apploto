// Shared M2 3.2–3.7: the client core driven through the real generated functions and mutator, with
// an injected fake `fetch` (no network).
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ApiConfigurationError,
  ApiError,
  clearApiClientConfiguration,
  configureApiClient,
  createApiClient,
  getBranchMyBranches,
  getTransactionTransactionId,
  isApiConfigurationError,
  isApiError,
  isOutcomeUncertain,
  parseETag,
  postTransaction,
  postTransactionInstallment,
  postUserLogin,
  putTransactionTransactionId,
  type AccessTokenRequest,
  type RequestCreateTransactionJson,
  type RequestUpdateTransactionJson,
} from '../src/index';
import { getPostTransactionMutationOptions } from '../src/queries';
import { deferred, fakeFetch, json, pendingUntilAborted } from './fake-fetch';

const transactionId = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
const createBody: RequestCreateTransactionJson = {
  date: '2026-10-17',
  value: '12.50',
  transactionTypeId: '0f8fad5b-d9cb-469f-a165-70867728950e',
  accountId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
};
const updateBody = {} as RequestUpdateTransactionJson;

afterEach(() => {
  clearApiClientConfiguration();
});

async function rejection(promise: Promise<unknown>): Promise<unknown> {
  try {
    await promise;
  } catch (error) {
    return error;
  }
  throw new Error('expected the call to reject');
}

describe('configuration and injection (3.2)', () => {
  it('fails with a typed configuration error before any configuration, without calling fetch', async () => {
    const error = await rejection(getBranchMyBranches());
    expect(isApiConfigurationError(error)).toBe(true);
    expect(isApiError(error)).toBe(false);
  });

  it('rejects an invalid configuration', () => {
    const getAccessToken = () => null;
    expect(() => createApiClient({ baseUrl: '', getAccessToken })).toThrow(ApiConfigurationError);
    expect(() =>
      createApiClient({ baseUrl: 'https://api.test', getAccessToken: undefined as never }),
    ).toThrow(ApiConfigurationError);
    expect(() =>
      createApiClient({ baseUrl: 'https://api.test', getAccessToken, fetch: 'nope' as never }),
    ).toThrow(ApiConfigurationError);
  });

  it('rejects a per-call client that was not created by createApiClient', async () => {
    const forged = { baseUrl: 'https://api.test', getAccessToken: () => null, fetch: undefined };
    expect(isApiConfigurationError(await rejection(getBranchMyBranches({ client: forged })))).toBe(
      true,
    );
  });

  it('asks the token provider on every request with method, path and the generated auth tier', async () => {
    const { fetch, calls } = fakeFetch(() => json(200, {}));
    const seen: AccessTokenRequest[] = [];
    configureApiClient({
      baseUrl: 'https://api.test/',
      fetch,
      getAccessToken: (request) => {
        seen.push(request);
        return `token-${seen.length}`;
      },
    });

    await postUserLogin({ email: 'a@b.test', password: 'secret' });
    await getBranchMyBranches();
    await getTransactionTransactionId(transactionId);

    expect(seen).toEqual([
      { method: 'POST', path: '/user/login', authTier: 'anonymous' },
      { method: 'GET', path: '/branch/my-branches', authTier: 'identity' },
      { method: 'GET', path: `/transaction/${transactionId}`, authTier: 'branch' },
    ]);
    expect(calls.map((call) => call.headers.get('Authorization'))).toEqual([
      'Bearer token-1',
      'Bearer token-2',
      'Bearer token-3',
    ]);
    expect(calls[2]?.url).toBe(`https://api.test/transaction/${transactionId}`);
  });

  it('prefers the more specific template when resolving a tier (installment over {transactionId})', async () => {
    const { fetch } = fakeFetch(() => json(201, []));
    const getAccessToken = vi.fn(() => 'branch-token');
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken });
    await postTransactionInstallment({} as never, { 'Idempotency-Key': 'k' });
    expect(getAccessToken).toHaveBeenCalledWith({
      method: 'POST',
      path: '/transaction/installment',
      authTier: 'branch',
    });
  });

  it('sends no Authorization header when the provider returns null', async () => {
    const { fetch, calls } = fakeFetch(() => json(200, { token: 't', refreshToken: 'r' }));
    configureApiClient({ baseUrl: '/api', fetch, getAccessToken: async () => null });
    await postUserLogin({ email: 'a@b.test', password: 'secret' });
    expect(calls[0]?.headers.has('Authorization')).toBe(false);
    expect(calls[0]?.url).toBe('/api/user/login');
  });

  it('isolates concurrent requests that use different per-call clients', async () => {
    const identityFetch = fakeFetch(() => json(200, []));
    const branchFetch = fakeFetch(() => json(200, {}));
    const identityHeld = deferred<string>();
    const identity = createApiClient({
      baseUrl: 'https://identity.test',
      fetch: identityFetch.fetch,
      getAccessToken: () => identityHeld.promise,
    });
    const branch = createApiClient({
      baseUrl: 'https://branch.test',
      fetch: branchFetch.fetch,
      getAccessToken: () => 'branch-token',
    });

    const first = getBranchMyBranches({ client: identity });
    const second = getTransactionTransactionId(transactionId, { client: branch });
    await second;
    identityHeld.resolve('identity-token');
    await first;

    expect(
      identityFetch.calls.map((call) => [call.url, call.headers.get('Authorization')]),
    ).toEqual([['https://identity.test/branch/my-branches', 'Bearer identity-token']]);
    expect(branchFetch.calls.map((call) => [call.url, call.headers.get('Authorization')])).toEqual([
      [`https://branch.test/transaction/${transactionId}`, 'Bearer branch-token'],
    ]);
  });

  it('keeps an in-flight request on the configuration it started with', async () => {
    const before = fakeFetch(() => json(200, []));
    const after = fakeFetch(() => json(200, []));
    const held = deferred<string>();
    configureApiClient({
      baseUrl: 'https://before.test',
      fetch: before.fetch,
      getAccessToken: () => held.promise,
    });

    const inFlight = getBranchMyBranches();
    configureApiClient({
      baseUrl: 'https://after.test',
      fetch: after.fetch,
      getAccessToken: () => 'after-token',
    });
    held.resolve('before-token');
    await inFlight;
    await getBranchMyBranches();

    expect(before.calls.map((call) => [call.url, call.headers.get('Authorization')])).toEqual([
      ['https://before.test/branch/my-branches', 'Bearer before-token'],
    ]);
    expect(after.calls.map((call) => [call.url, call.headers.get('Authorization')])).toEqual([
      ['https://after.test/branch/my-branches', 'Bearer after-token'],
    ]);
  });

  it('passes RequestInit options such as the AbortSignal through to fetch', async () => {
    const { fetch, calls } = fakeFetch(() => json(200, []));
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    const controller = new AbortController();
    await getBranchMyBranches({ signal: controller.signal, cache: 'no-store' });
    expect(calls[0]?.signal).toBe(controller.signal);
  });
});

describe('typed ApiError (3.4)', () => {
  function configure(answer: Parameters<typeof fakeFetch>[0]) {
    const fake = fakeFetch(answer);
    configureApiClient({
      baseUrl: 'https://api.test',
      fetch: fake.fetch,
      getAccessToken: () => 't',
    });
    return fake;
  }

  it('keeps a multi-message ResponseErrorJson verbatim, in order, duplicates included', async () => {
    configure(() => json(400, { errorMessages: ['B', 'A', 'B'] }));
    const error = (await rejection(
      postTransaction(createBody, { 'Idempotency-Key': 'k' }),
    )) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      kind: 'http',
      status: 400,
      method: 'POST',
      path: '/transaction',
      body: 'error-json',
      dispatched: true,
    });
    expect(error.errorMessages).toEqual(['B', 'A', 'B']);
  });

  it('turns a null message list into an empty list while recording that a body was present', async () => {
    configure(() => json(409, { errorMessages: null }));
    const error = (await rejection(getTransactionTransactionId(transactionId))) as ApiError;
    expect(error.errorMessages).toEqual([]);
    expect(error.body).toBe('error-json');
  });

  it('distinguishes a non-JSON body and an empty body', async () => {
    configure(() => new Response('<html>gateway</html>', { status: 502 }));
    const html = (await rejection(getTransactionTransactionId(transactionId))) as ApiError;
    expect(html).toMatchObject({
      kind: 'http',
      status: 502,
      body: 'unrecognized',
      rawBody: '<html>gateway</html>',
    });
    expect(html.errorMessages).toEqual([]);

    configure(() => new Response(null, { status: 401 }));
    const empty = (await rejection(getTransactionTransactionId(transactionId))) as ApiError;
    expect(empty).toMatchObject({ kind: 'http', status: 401, body: 'none' });
    expect(empty.errorMessages).toEqual([]);
  });

  it('reports a network failure as a dispatched failure with no status', async () => {
    configure(() => {
      throw new TypeError('fetch failed');
    });
    const error = (await rejection(getTransactionTransactionId(transactionId))) as ApiError;
    expect(error).toMatchObject({ kind: 'network', status: undefined, dispatched: true });
    expect(error.cause).toBeInstanceOf(TypeError);
  });

  it('reports a malformed 2xx body as invalid-response, keeping the status and headers', async () => {
    configure(() => new Response('{not json', { status: 200, headers: { ETag: '"5"' } }));
    const error = (await rejection(getTransactionTransactionId(transactionId))) as ApiError;
    expect(error).toMatchObject({ kind: 'invalid-response', status: 200, rawBody: '{not json' });
    expect(error.headers?.get('ETag')).toBe('"5"');
    expect(isOutcomeUncertain(error)).toBe(false);
  });

  it('treats a body lost after the status line as a dispatched network failure', async () => {
    configure(
      () =>
        new Response(
          new ReadableStream({
            start(controller) {
              controller.error(new TypeError('connection reset'));
            },
          }),
          { status: 201 },
        ),
    );
    const error = (await rejection(
      postTransaction(createBody, { 'Idempotency-Key': 'k' }),
    )) as ApiError;
    expect(error).toMatchObject({ kind: 'network', status: 201, dispatched: true });
    expect(isOutcomeUncertain(error)).toBe(true);
  });

  it('carries no pt-BR copy: messages are the server keys/strings or technical text', async () => {
    configure(() => json(400, { errorMessages: ['TRANSACTION_STALE_WRITE'] }));
    const error = (await rejection(getTransactionTransactionId(transactionId))) as ApiError;
    expect(error.message).toBe(`GET /transaction/${transactionId} failed: http 400`);
  });
});

describe('isOutcomeUncertain (3.4)', () => {
  it('is false for GET, true for a dispatched non-GET 5xx, false for a non-5xx answer', async () => {
    const statuses = [500, 409, 503, 401];
    let index = 0;
    const { fetch } = fakeFetch(() => json(statuses[index++] ?? 500, { errorMessages: ['x'] }));
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });

    expect(isOutcomeUncertain(await rejection(getTransactionTransactionId(transactionId)))).toBe(
      false,
    ); // GET 500
    expect(
      isOutcomeUncertain(await rejection(postTransaction(createBody, { 'Idempotency-Key': 'k' }))),
    ).toBe(false); // POST 409
    expect(
      isOutcomeUncertain(await rejection(postTransaction(createBody, { 'Idempotency-Key': 'k' }))),
    ).toBe(true); // POST 503
    expect(
      isOutcomeUncertain(await rejection(postTransaction(createBody, { 'Idempotency-Key': 'k' }))),
    ).toBe(false); // POST 401
  });

  it('is false for a network failure on GET and true for one on POST', async () => {
    const { fetch } = fakeFetch(() => Promise.reject(new TypeError('fetch failed')));
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    expect(isOutcomeUncertain(await rejection(getTransactionTransactionId(transactionId)))).toBe(
      false,
    );
    expect(
      isOutcomeUncertain(await rejection(postTransaction(createBody, { 'Idempotency-Key': 'k' }))),
    ).toBe(true);
  });

  it('sends nothing and is not uncertain when the signal is already aborted', async () => {
    const { fetch, calls } = fakeFetch();
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    const controller = new AbortController();
    controller.abort();
    const error = (await rejection(
      postTransaction(createBody, { 'Idempotency-Key': 'k' }, { signal: controller.signal }),
    )) as ApiError;
    expect(calls).toHaveLength(0);
    expect(error).toMatchObject({ kind: 'aborted', dispatched: false });
    expect(isOutcomeUncertain(error)).toBe(false);
  });

  it('settles at once, sends nothing and is not uncertain when aborted while the token provider is pending', async () => {
    const { fetch, calls } = fakeFetch();
    const held = deferred<string>();
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => held.promise });
    const controller = new AbortController();
    const call = postTransaction(
      createBody,
      { 'Idempotency-Key': 'k' },
      { signal: controller.signal },
    );
    controller.abort();
    const error = (await rejection(call)) as ApiError;
    held.resolve('late-token');
    await Promise.resolve();
    expect(calls).toHaveLength(0);
    expect(error).toMatchObject({ kind: 'aborted', dispatched: false });
    expect(isOutcomeUncertain(error)).toBe(false);
  });

  it('is uncertain when a POST is aborted after dispatch, while fetch is pending', async () => {
    const { fetch, calls } = fakeFetch(pendingUntilAborted);
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    const controller = new AbortController();
    const call = postTransaction(
      createBody,
      { 'Idempotency-Key': 'k' },
      { signal: controller.signal },
    );
    await vi.waitFor(() => expect(calls).toHaveLength(1));
    controller.abort();
    const error = (await rejection(call)) as ApiError;
    expect(error).toMatchObject({ kind: 'aborted', dispatched: true });
    expect(isOutcomeUncertain(error)).toBe(true);
  });

  it('classifies a timeout after dispatch as an uncertain timeout', async () => {
    const { fetch } = fakeFetch(pendingUntilAborted);
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    const error = (await rejection(
      postTransaction(createBody, { 'Idempotency-Key': 'k' }, { signal: AbortSignal.timeout(5) }),
    )) as ApiError;
    expect(error).toMatchObject({ kind: 'timeout', dispatched: true });
    expect(isOutcomeUncertain(error)).toBe(true);
  });
});

describe('transparent financial headers (3.5)', () => {
  it('sends Idempotency-Key and If-Match byte for byte and exposes the response ETag', async () => {
    const { fetch, calls } = fakeFetch((call) =>
      json(call.method === 'POST' ? 201 : 200, { id: transactionId }, { ETag: '"4294967295"' }),
    );
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });

    const key = 'Frozen-Key_2026-10-17:~ascii!';
    const created = await postTransaction(createBody, { 'Idempotency-Key': key });
    const updated = await putTransactionTransactionId(transactionId, updateBody, {
      'If-Match': '"123"',
    });

    expect(calls[0]?.headers.get('Idempotency-Key')).toBe(key);
    expect(calls[0]?.body).toBe(JSON.stringify(createBody));
    expect(calls[1]?.headers.get('If-Match')).toBe('"123"');
    expect(created.status).toBe(201);
    expect(parseETag(created.headers.get('ETag'))).toBe(4294967295);
    expect(updated.headers.get('ETag')).toBe('"4294967295"');
  });

  it('returns no data for an empty 2xx body', async () => {
    const { fetch } = fakeFetch(() => new Response(null, { status: 204 }));
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    const result = await getBranchMyBranches();
    expect(result).toMatchObject({ status: 204, data: undefined });
  });
});

describe('no retry (3.6)', () => {
  it.each([401, 409, 500])('calls fetch exactly once for a %i answer', async (status) => {
    const { fetch, calls } = fakeFetch(() => json(status, { errorMessages: ['x'] }));
    const getAccessToken = vi.fn(() => 't');
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken });
    await rejection(postTransaction(createBody, { 'Idempotency-Key': 'k' }));
    expect(calls).toHaveLength(1);
    expect(getAccessToken).toHaveBeenCalledTimes(1);
  });

  it('calls fetch exactly once for a network error', async () => {
    const { fetch, calls } = fakeFetch(() => Promise.reject(new TypeError('fetch failed')));
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => 't' });
    await rejection(postTransaction(createBody, { 'Idempotency-Key': 'k' }));
    expect(calls).toHaveLength(1);
  });
});

// Review round 1 (S06) regressions: each case below failed before the fixes.
describe('caller headers next to generated ones (review rounds 1–2, finding 1–2)', () => {
  type Cast = never;
  const forms = (name: string, value: string) => [
    { [name]: value },
    new Headers({ [name]: value }),
    [[name, value]] as [string, string][],
  ];

  function configureRecording(answer: Parameters<typeof fakeFetch>[0] = () => json(201, {})) {
    const fake = fakeFetch(answer);
    const getAccessToken = vi.fn(() => 'token');
    configureApiClient({ baseUrl: 'https://api.test', fetch: fake.fetch, getAccessToken });
    return { ...fake, getAccessToken };
  }

  it('refuses an exact-case or other-case caller value for a protected header (raw output), sending nothing', async () => {
    const { calls, getAccessToken } = configureRecording();
    for (const name of ['Idempotency-Key', 'idempotency-key', 'IDEMPOTENCY-KEY']) {
      for (const headers of forms(name, 'different-B')) {
        const error = (await rejection(
          postTransaction(createBody, { 'Idempotency-Key': 'immutable-A' }, { headers }),
        )) as ApiError;
        expect(error).toMatchObject({ kind: 'invalid-request', dispatched: false });
        expect(isOutcomeUncertain(error)).toBe(false);
      }
    }
    for (const name of ['If-Match', 'if-match']) {
      const error = (await rejection(
        putTransactionTransactionId(
          transactionId,
          updateBody,
          { 'If-Match': '"7"' },
          { headers: { [name]: '"99"' } },
        ),
      )) as ApiError;
      expect(error.kind).toBe('invalid-request');
    }
    const contentType = (await rejection(
      postTransaction(
        createBody,
        { 'Idempotency-Key': 'k' },
        { headers: { 'Content-Type': 'text/plain' } },
      ),
    )) as ApiError;
    expect(contentType.kind).toBe('invalid-request');
    expect(calls).toHaveLength(0);
    expect(getAccessToken).not.toHaveBeenCalled();
  });

  it('refuses the same conflicts through the query output', async () => {
    const { calls } = configureRecording();
    for (const name of ['Idempotency-Key', 'idempotency-key']) {
      const { mutationFn } = getPostTransactionMutationOptions({
        request: { headers: { [name]: 'different-B' } },
      });
      const error = (await rejection(
        Promise.resolve(
          mutationFn?.(
            { data: createBody, headers: { 'Idempotency-Key': 'immutable-A' } },
            {} as Cast,
          ),
        ),
      )) as ApiError;
      expect(error).toMatchObject({ kind: 'invalid-request', dispatched: false });
    }
    expect(calls).toHaveLength(0);
  });

  it('passes ordinary caller headers through, keeping every generated value', async () => {
    const { calls } = configureRecording();
    for (const headers of forms('X-Trace', 'trace-1')) {
      await postTransaction(createBody, { 'Idempotency-Key': 'immutable-A' }, { headers });
    }
    await putTransactionTransactionId(
      transactionId,
      updateBody,
      { 'If-Match': '"7"' },
      {
        headers: { 'If-Match': '"7"', 'X-Trace': 'same-value-is-not-a-conflict' },
      },
    );
    const { mutationFn } = getPostTransactionMutationOptions({
      request: { headers: new Headers({ 'x-trace': 'trace-q' }) },
    });
    await mutationFn?.(
      { data: createBody, headers: { 'Idempotency-Key': 'immutable-Q' } },
      {} as Cast,
    );
    await getBranchMyBranches({ headers: [['X-Trace', 'trace-get']] });

    expect(calls.map((call) => [...call.headers.entries()])).toEqual([
      ...forms('X-Trace', 'trace-1').map(() => [
        ['authorization', 'Bearer token'],
        ['content-type', 'application/json'],
        ['idempotency-key', 'immutable-A'],
        ['x-trace', 'trace-1'],
      ]),
      [
        ['authorization', 'Bearer token'],
        ['content-type', 'application/json'],
        ['if-match', '"7"'],
        ['x-trace', 'same-value-is-not-a-conflict'],
      ],
      [
        ['authorization', 'Bearer token'],
        ['content-type', 'application/json'],
        ['idempotency-key', 'immutable-Q'],
        ['x-trace', 'trace-q'],
      ],
      [
        ['authorization', 'Bearer token'],
        ['x-trace', 'trace-get'],
      ],
    ]);
  });

  it('refuses a caller Authorization and a name repeated in another casing', async () => {
    const { fetch, calls } = fakeFetch();
    const getAccessToken = vi.fn(() => null);
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken });
    for (const headers of forms('Authorization', 'Bearer stale-branch-token')) {
      const get = (await rejection(getBranchMyBranches({ headers }))) as ApiError;
      const post = (await rejection(
        postTransaction(createBody, { 'Idempotency-Key': 'k' }, { headers }),
      )) as ApiError;
      expect([get.kind, post.kind]).toEqual(['invalid-request', 'invalid-request']);
    }
    const repeated = (await rejection(
      getBranchMyBranches({ headers: { 'X-Trace': 'a', 'x-trace': 'b' } }),
    )) as ApiError;
    expect(repeated.kind).toBe('invalid-request');
    expect(calls).toHaveLength(0);
    expect(getAccessToken).not.toHaveBeenCalled();
  });

  it('sends exactly the generated header values when the caller adds none', async () => {
    const { fetch, calls } = fakeFetch(() => json(201, {}));
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => null });
    await postTransaction(createBody, { 'Idempotency-Key': 'immutable-A' });
    expect([...(calls[0]?.headers.entries() ?? [])]).toEqual([
      ['content-type', 'application/json'],
      ['idempotency-key', 'immutable-A'],
    ]);
  });
});

describe('pre-dispatch failures (review finding 4, 5)', () => {
  it('sends nothing when the abort lands between the provider resolving and fetch', async () => {
    const { fetch, calls } = fakeFetch();
    const held = deferred<string>();
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: () => held.promise });
    const controller = new AbortController();
    const call = postTransaction(
      createBody,
      { 'Idempotency-Key': 'k' },
      { signal: controller.signal },
    );
    held.resolve('token');
    queueMicrotask(() => controller.abort());
    const error = (await rejection(call)) as ApiError;
    expect(calls).toHaveLength(0);
    expect(error).toMatchObject({ kind: 'aborted', dispatched: false });
    expect(isOutcomeUncertain(error)).toBe(false);
  });

  it.each([
    [
      'throws',
      () => {
        throw new Error('secure-store unavailable');
      },
    ],
    ['rejects', () => Promise.reject(new Error('secure-store unavailable'))],
  ])('normalizes a provider that %s into a typed pre-dispatch ApiError', async (_, provider) => {
    const { fetch, calls } = fakeFetch();
    configureApiClient({ baseUrl: 'https://api.test', fetch, getAccessToken: provider });
    const error = (await rejection(
      postTransaction(createBody, { 'Idempotency-Key': 'k' }),
    )) as ApiError;
    expect(isApiError(error)).toBe(true);
    expect(error).toMatchObject({
      kind: 'token-provider',
      method: 'POST',
      path: '/transaction',
      dispatched: false,
    });
    expect((error.cause as Error).message).toBe('secure-store unavailable');
    expect(isOutcomeUncertain(error)).toBe(false);
    expect(calls).toHaveLength(0);
  });
});
