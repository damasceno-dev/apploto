'use client';

import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { getGetBranchMyBranchesQueryKey, useGetBranchMyBranches } from '@loto/api/queries';

function DisabledQueryProof() {
  const query = useGetBranchMyBranches({ query: { enabled: false, retry: false } });
  return (
    <p>
      Entrada de consultas: {typeof useGetBranchMyBranches} ·{' '}
      {getGetBranchMyBranchesQueryKey().length} chave ·{' '}
      {query.fetchStatus === 'idle' ? 'sem requisição' : 'inesperado'}.
    </p>
  );
}

export function QueryProof() {
  // Context identity proof only; the generated query is permanently disabled.
  const [client] = useState(() => new QueryClient({ defaultOptions: { mutations: { retry: 0 } } }));
  return (
    <QueryClientProvider client={client}>
      <DisabledQueryProof />
    </QueryClientProvider>
  );
}
