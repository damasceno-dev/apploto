'use client';

import { getGetBranchMyBranchesQueryKey, useGetBranchMyBranches } from '@loto/api/queries';

/**
 * Technical proof that the client entry runs under the app-owned, per-scope `QueryClient`
 * (`AppProviders`); the generated query stays permanently disabled, so no request is made.
 */
export function QueryProof() {
  const query = useGetBranchMyBranches({ query: { enabled: false, retry: false } });
  return (
    <p>
      Entrada de consultas: {typeof useGetBranchMyBranches} ·{' '}
      {getGetBranchMyBranchesQueryKey().length} chave ·{' '}
      {query.fetchStatus === 'idle' ? 'sem requisição' : 'inesperado'}.
    </p>
  );
}
