// DEV-only technical component gallery (Mobile M3 Phase 4). Reached only through the Metro-folded
// `__DEV__` require in the home route; production maps and Hermes bytecode must not contain it
// (tooling/check-bundles.mjs). Every value shown is a labelled technical example, not product data.
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { BackHandler, View, type ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useTheme } from '@/theme/provider';
import { PressableBox } from '@/ui/focus';
import { ScreenScroll } from '@/ui/screen';
import { spacing } from '@/theme/style';
import { AppText, Button, Glyph, PageHeader } from '@/ui';
import { gallerySections } from './sections';

export const GALLERY_MARKER = 'Galeria técnica de componentes';

export function ComponentGallery({ onClose }: { readonly onClose: () => void }) {
  const { colors } = useTheme();
  const [section, setSection] = useState<string | null>(null);
  const scroll = useRef<ScrollView>(null);
  // Each section (and the list on return) starts at the top, not at the previous offset.
  useEffect(() => {
    scroll.current?.scrollTo({ y: 0, animated: false });
  }, [section]);
  useEffect(() => {
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      if (section !== null) setSection(null);
      else onClose();
      return true;
    });
    return () => subscription.remove();
  }, [section, onClose]);
  const current = gallerySections.find((entry) => entry.key === section);
  return (
    <SafeAreaView style={{ flex: 1 }} edges={['top', 'left', 'right']}>
      <ScreenScroll
        ref={scroll}
        testID="gallery-scroll"
        contentContainerStyle={{
          padding: spacing.page['inline-compact'],
          paddingBottom: spacing.page['block-end'] * 3,
          gap: spacing.stack.lg,
        }}
      >
        {current ? (
          <Section
            key={current.key}
            title={current.title}
            contract={current.contract}
            onBack={() => setSection(null)}
          >
            <current.render />
          </Section>
        ) : (
          <>
            <PageHeader
              title={GALLERY_MARKER}
              context="DEV · exemplos técnicos rotulados; nenhuma requisição ou dado de cliente"
              variant="list"
              origin={{ label: 'o início', onPress: onClose }}
              testID="gallery-home"
            />
            <View accessibilityRole="list" style={{ gap: spacing.stack.xs }}>
              {gallerySections.map((entry) => (
                <PressableBox
                  key={entry.key}
                  testID={`gallery-section-${entry.key}`}
                  accessibilityRole="link"
                  accessibilityLabel={entry.title}
                  onPress={() => setSection(entry.key)}
                  style={(pressed) => ({
                    minHeight: spacing.size['touch-target-comfortable'],
                    flexDirection: 'row',
                    alignItems: 'center',
                    gap: spacing.inline.md,
                    paddingVertical: spacing.stack.sm,
                    paddingHorizontal: spacing.inline.sm,
                    borderBottomWidth: spacing.size['border-width'],
                    borderBottomColor: colors.sem.border.subtle,
                    backgroundColor: pressed ? colors.sem.surface.hover : undefined,
                  })}
                >
                  <View style={{ flex: 1, gap: spacing.stack.xs }}>
                    <AppText variant="body-strong" color={colors.sem.text.link}>
                      {entry.title}
                    </AppText>
                    <AppText variant="helper" tone="muted">
                      {entry.contract}
                    </AppText>
                  </View>
                  <Glyph name="chevronRight" color={colors.sem.text.muted} />
                </PressableBox>
              ))}
            </View>
            <Button label="Fechar galeria" variant="secondary" onPress={onClose} />
          </>
        )}
      </ScreenScroll>
    </SafeAreaView>
  );
}

function Section({
  title,
  contract,
  onBack,
  children,
}: {
  readonly title: string;
  readonly contract: string;
  readonly onBack: () => void;
  readonly children: ReactNode;
}) {
  return (
    <View style={{ gap: spacing.stack.md }}>
      <PageHeader
        title={title}
        context={contract}
        variant="list"
        origin={{ label: 'a galeria', onPress: onBack }}
      />
      {children}
    </View>
  );
}
