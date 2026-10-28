import { Role, createApiClient } from '@loto/api';
import { getGetBranchMyBranchesQueryKey, useGetBranchMyBranches } from '@loto/api/queries';
import * as core from '@loto/core';
import { ScrollView, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
// Metro removes this require dependency from production when it folds __DEV__.
// eslint-disable-next-line @typescript-eslint/no-require-imports
const DevPanel = __DEV__ ? require('../probes/dev-panel').DevPanel : () => null;

export default function HomeScreen() {
  return (
    <SafeAreaView className="flex-1">
      <ScrollView
        keyboardDismissMode="on-drag"
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-4 p-6"
      >
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
        <DevPanel />
      </ScrollView>
    </SafeAreaView>
  );
}
