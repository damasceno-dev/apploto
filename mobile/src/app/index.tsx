import { Role, createApiClient } from '@loto/api';
import { getGetBranchMyBranchesQueryKey, useGetBranchMyBranches } from '@loto/api/queries';
import * as core from '@loto/core';
import { useState } from 'react';
import { ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { spacing } from '@/theme/style';
import { AppText } from '@/ui/text';
// Metro removes these require dependencies from production when it folds __DEV__.
/* eslint-disable @typescript-eslint/no-require-imports */
const DevPanel = __DEV__ ? require('../probes/dev-panel').DevPanel : () => null;
const DevGallery = __DEV__ ? require('../probes/gallery').ComponentGallery : null;
/* eslint-enable @typescript-eslint/no-require-imports */

export default function HomeScreen() {
  const [gallery, setGallery] = useState(false);
  if (DevGallery !== null && gallery) return <DevGallery onClose={() => setGallery(false)} />;
  return (
    <SafeAreaView style={{ flex: 1 }}>
      <ScrollView
        keyboardDismissMode="on-drag"
        keyboardShouldPersistTaps="handled"
        contentContainerStyle={{ gap: spacing.stack.md, padding: spacing.page['block-start'] }}
      >
        <AppText variant="page-title" accessibilityRole="header">
          Lotero
        </AppText>
        <AppText>Estrutura inicial do aplicativo.</AppText>
        <AppText>
          API: {typeof createApiClient} · {Object.keys(Role).length} tipos de perfil.
        </AppText>
        <AppText>
          Consultas: {typeof useGetBranchMyBranches} · {getGetBranchMyBranchesQueryKey().length}{' '}
          chave.
        </AppText>
        <AppText>Core: {Object.keys(core).length} exportações.</AppText>
        <DevPanel onOpenGallery={() => setGallery(true)} />
      </ScrollView>
    </SafeAreaView>
  );
}
