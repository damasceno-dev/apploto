import { Button } from 'react-native';
import { IntlProbe } from './intl-probe';
import { SessionHarness } from './session-harness';
import { QueryProof } from './query-proof';

export function DevPanel({ onOpenGallery }: { readonly onOpenGallery: () => void }) {
  return (
    <>
      <Button
        testID="open-component-gallery"
        title="Abrir galeria técnica de componentes"
        onPress={onOpenGallery}
      />
      <QueryProof />
      <SessionHarness />
      <IntlProbe />
    </>
  );
}
