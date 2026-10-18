// Orval configuration (Shared M2 Phase 2.2 and 4.2). The only input is the committed server export;
// generated code under `src/generated/` is never hand-edited. Run through `pnpm generate`, which
// runs the two projects in order and then writes the operation auth-tier map.
import { defineConfig, type OutputOptions } from 'orval';
import { withHeaderBoundary } from './scripts/header-boundary.ts';

const input = { target: './openapi/server-api.openapi.json' };

const shared = {
  mode: 'tags-split',
  schemas: './src/generated/model',
  httpClient: 'fetch',
  // Required: Orval defaults this to false, which would drop the declared `Idempotency-Key` and
  // `If-Match` header parameters from every generated signature.
  headers: true,
  clean: true,
  // Orval 8.39.0 formats through `formatter` (its `prettier` flag is not read); Prettier resolves the
  // pinned root `.prettierrc` for each generated file.
  formatter: 'prettier',
  override: {
    mutator: { path: './src/client/fetch.ts', name: 'apiFetch' },
    // Dates stay strings: `date`, `date-time` and the offset-free branch wall clock are never instants.
    useDates: false,
    // Generated functions return only the 2xx variants as `{ data, status, headers }`; the mutator
    // rejects every other status with a typed `ApiError`.
    fetch: { includeHttpResponseReturnType: true, forceSuccessResponse: true },
  },
} satisfies Partial<OutputOptions>;

export default defineConfig({
  raw: {
    input,
    output: {
      ...shared,
      // Orval's `fetch` client, with the caller's headers kept apart from the generated ones.
      client: withHeaderBoundary('fetch'),
      target: './src/generated/endpoints/index.ts',
    },
  },
  queries: {
    input,
    output: {
      ...shared,
      // Orval's `react-query` client, with the same header boundary as the raw functions.
      client: withHeaderBoundary('react-query'),
      target: './src/generated/queries/index.ts',
      override: {
        ...shared.override,
        // Orval's defaults: GET operations become queries (with the request's AbortSignal) and every
        // other verb a mutation, so a write never runs as an automatic query. Retries are app-owned:
        // no generated hook or option may carry a `retry` policy (checked by check-boundary.ts).
        query: { signal: true },
      },
    },
  },
});
