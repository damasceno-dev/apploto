// A recording `fetch` double: every call is captured, and each test decides the answer.
export interface RecordedCall {
  readonly url: string;
  readonly method: string;
  readonly headers: Headers;
  readonly body: string | undefined;
  readonly signal: AbortSignal | undefined;
}

export type Answer = (call: RecordedCall) => Response | Promise<Response>;

export interface FakeFetch {
  readonly fetch: typeof globalThis.fetch;
  readonly calls: RecordedCall[];
}

export function fakeFetch(answer: Answer = () => json(200, {})): FakeFetch {
  const calls: RecordedCall[] = [];
  const fetch = ((input: RequestInfo | URL, init?: RequestInit) => {
    const call: RecordedCall = {
      url: String(input),
      method: init?.method ?? 'GET',
      headers: new Headers(init?.headers),
      body: typeof init?.body === 'string' ? init.body : undefined,
      signal: init?.signal ?? undefined,
    };
    calls.push(call);
    return Promise.resolve(answer(call));
  }) as typeof globalThis.fetch;
  return { fetch, calls };
}

export function json(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  });
}

/** A `fetch` answer that stays pending until the request's signal aborts (as real fetch does). */
export function pendingUntilAborted(call: RecordedCall): Promise<Response> {
  return new Promise((_, reject) => {
    call.signal?.addEventListener('abort', () => reject(call.signal?.reason), { once: true });
  });
}

/** A promise plus its resolver, for holding a token provider open. */
export function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void } {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}
