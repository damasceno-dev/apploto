import { useState } from 'react';
import { Button, Overlay } from '@/ui';
import { DevPanel } from './dev-panel';
import { ComponentGallery } from './gallery';
/** Deliberate DEV-only entry; the root's folded require excludes this entire graph in release. */
export function DevelopmentTools() {
  const [open, setOpen] = useState(false);
  const [gallery, setGallery] = useState(false);
  return (
    <>
      <Button label="Verificação técnica (DEV)" onPress={() => setOpen(true)} variant="link" />
      <Overlay
        visible={open}
        title="Verificação técnica"
        variant="drawer"
        onDismiss={() => {
          setOpen(false);
          setGallery(false);
        }}
      >
        {gallery ? (
          <ComponentGallery onClose={() => setGallery(false)} />
        ) : (
          <DevPanel onOpenGallery={() => setGallery(true)} />
        )}
      </Overlay>
    </>
  );
}
