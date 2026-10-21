import { Role, createApiClient } from '@loto/api';
import { getGetBranchMyBranchesQueryKey, useGetBranchMyBranches } from '@loto/api/queries';
// eslint-disable-next-line import/namespace -- Shared M2 intentionally exports an empty runtime namespace until M3.
import * as core from '@loto/core';
import { ScrollView, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { IntlProbe } from '@/probes/intl-probe';
import { QueryProof } from '@/probes/query-proof';

export default function HomeScreen() {
  return (
    <SafeAreaView className="flex-1">
      <ScrollView contentContainerClassName="gap-4 p-6">
        <Text accessibilityRole="header" className="text-2xl font-semibold">
          Lotero
        </Text>
        <Text>Estrutura inicial do aplicativo.</Text>
        <Text>
          API: {typeof createApiClient} · {Object.keys(Role).length} tipos de perfil.
        </Text>
        <Text>
          Consultas: {typeof useGetBranchMyBranches} · {getGetBranchMyBranchesQueryKey().length}{' '}
          chave.
        </Text>
        <Text>Core: {Object.keys(core).length} exportações.</Text>
        <QueryProof />
        {__DEV__ ? <IntlProbe /> : null}
      </ScrollView>
    </SafeAreaView>
  );
}
