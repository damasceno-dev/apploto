import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { Text } from 'react-native';

// Technical context proof only: permanently disabled, with no backend configuration.
const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false }, mutations: { retry: 0 } },
});

function DisabledQueryProof() {
  const result = useGetBranchMyBranches({ query: { enabled: false, retry: false } });
  return (
    <Text>
      Contexto de consultas: {result.fetchStatus === 'idle' ? 'sem requisição' : 'inesperado'}.
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
