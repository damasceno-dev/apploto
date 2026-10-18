// Compile-time gate over the `@loto/api/queries` entry (Shared M2 4.3): financial headers stay
// required in mutation variables, and hook errors are the `ApiError` the transport throws.
import type { ApiError, PostTransactionHeaders, PostTransactionInstallmentHeaders } from '../index';
import {
  getPostTransactionMutationOptions,
  type PostTransactionInstallmentMutationVariables,
  type PostTransactionMutationVariables,
  type PutTransactionTransactionIdMutationVariables,
} from '../queries';
import type { Equal, Expect, IsOptional } from './assert';

/** Never called: lets TypeScript apply the generated `TError` default, as an app's call would. */
function defaultMutationOptions() {
  return getPostTransactionMutationOptions();
}

/** Never called: the hooks' per-request options are the raw functions' options. */
export function queryRequestOptionsGate(): void {
  getPostTransactionMutationOptions({
    request: { cache: 'no-store', headers: { 'X-Trace': 't' } },
  });
  // @ts-expect-error Request options never carry a body in the query output either.
  getPostTransactionMutationOptions({ request: { body: '{}' } });
}
type PostTransactionMutationError = Parameters<
  NonNullable<ReturnType<typeof defaultMutationOptions>['onError']>
>[0];

export type QueryHeaderChecks = [
  Expect<Equal<PostTransactionMutationVariables['headers'], PostTransactionHeaders>>,
  Expect<Equal<IsOptional<PostTransactionMutationVariables, 'headers'>, false>>,
  Expect<Equal<IsOptional<PostTransactionHeaders, 'Idempotency-Key'>, false>>,
  Expect<
    Equal<PostTransactionInstallmentMutationVariables['headers'], PostTransactionInstallmentHeaders>
  >,
  Expect<Equal<IsOptional<PostTransactionInstallmentMutationVariables, 'headers'>, false>>,
  Expect<Equal<IsOptional<PutTransactionTransactionIdMutationVariables, 'headers'>, false>>,
  Expect<Equal<PostTransactionMutationError, ApiError>>,
];

export { defaultMutationOptions as queryFidelityProbe };
