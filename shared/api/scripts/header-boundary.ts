// Generation-time header boundary (Shared M2 2.3/3.5, S06 review rounds 1–2). Orval 8.39.0 builds every
// request's headers as `{ …generated, ...getHeaders(options?.headers) }`: the caller's options are
// spread last, so an options header with the same name silently replaces a required argument such
// as `Idempotency-Key` or `If-Match`, and nothing downstream can recover which value was the
// generated one. This wrapper keeps both sources apart in the generated code itself — the generated
// headers stay in `headers`, the caller's options headers travel unmerged as `callerHeaders` — so
// the mutator (`src/client/fetch.ts`) can pass ordinary caller headers through and refuse any that
// conflict. It wraps Orval's own client builders through the documented function-valued `client`
// option; no generated file is edited afterwards, and `generate:check` pins the result.
import type { OutputOptions } from 'orval';

type OutputClientFunction = Extract<
  NonNullable<OutputOptions['client']>,
  (...args: never[]) => unknown
>;
type GeneratorClients = Parameters<OutputClientFunction>[0];
type ClientName = keyof GeneratorClients;

/** Orval's merge: generated entries first, then the caller's options headers. */
const lossyMerge = /headers: \{ ?([^{}]*?),\s*\.\.\.getHeaders\(options\?\.headers\) ?\}/g;

/**
 * Rewrites every lossy merge in one generated implementation and fails if any merge it does not
 * recognize remains, so an Orval upgrade that changes the emitted shape stops generation instead of
 * silently reintroducing the override.
 */
export function separateCallerHeaders(implementation: string, operation: string): string {
  const rewritten = implementation.replace(
    lossyMerge,
    (_, generated: string) => `headers: { ${generated.trim()} }, callerHeaders: options?.headers`,
  );
  if (rewritten.includes('getHeaders(options?.headers)')) {
    throw new Error(
      `${operation}: Orval emitted a header merge the S06 header boundary does not recognize.`,
    );
  }
  return rewritten;
}

/**
 * Wraps one of Orval's built-in client generators with {@link separateCallerHeaders}. Orval hands
 * the configured `client` value back to the builders, which branch on its name (the query module only
 * emits React hooks for `'react-query'`), so the wrapper passes the built-in name, not itself.
 */
export function withHeaderBoundary(name: ClientName): OutputClientFunction {
  return (clients) => {
    const builtIn = clients[name];
    return {
      ...builtIn,
      client: async (verbOptions, options, _outputClient, output) => {
        const generated = await builtIn.client(verbOptions, options, name, output);
        return {
          ...generated,
          implementation: separateCallerHeaders(
            generated.implementation,
            verbOptions.operationName,
          ),
        };
      },
    };
  };
}
