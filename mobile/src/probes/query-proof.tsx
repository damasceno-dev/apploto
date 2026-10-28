import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { Text } from 'react-native';
import { createApiClient } from '@loto/api';

let tokenReads = 0;
let dispatches = 0;
// An inert, injected trap: even an accidental activation cannot reach a backend.
const proofClient = createApiClient({
  baseUrl: 'http://query-proof.invalid',
  getAccessToken: () => {
    tokenReads += 1;
    return null;
  },
  fetch: async () => {
    dispatches += 1;
    throw new Error('Disabled query dispatched');
  },
});

// Technical context proof only: permanently disabled, with no backend configuration.
const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false }, mutations: { retry: 0 } },
});

function DisabledQueryProof() {
  const result = useGetBranchMyBranches({
    query: { enabled: false, retry: false },
    request: { client: proofClient },
  });
  return (
    <Text>
      Contexto de consultas: {result.fetchStatus === 'idle' ? 'sem requisição' : 'inesperado'}.{' '}
      Credenciais consultadas: {tokenReads}. Requisições: {dispatches}.
    </Text>
  );
}

export function QueryProof() {
  return (
    <QueryClientProvider client={queryClient}>
      <DisabledQueryProof />
    </QueryClientProvider>
  );
}
