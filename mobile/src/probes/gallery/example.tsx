import type { ReactNode } from 'react';
import { View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';
import { AppText } from '@/ui';

/** One labelled technical example: what it shows and the decisive behavior to check. */
export function Example({
  title,
  behavior,
  children,
  testID,
}: {
  readonly title: string;
  readonly behavior?: string;
  readonly children: ReactNode;
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  return (
    <View
      testID={testID}
      style={{
        gap: spacing.stack.sm,
        paddingTop: spacing.stack.md,
        borderTopWidth: spacing.size['border-width'],
        borderTopColor: colors.sem.border.subtle,
      }}
    >
      <AppText variant="overline" tone="muted" accessibilityRole="header">
        {title}
      </AppText>
      {behavior ? (
        <AppText variant="helper" tone="secondary">
          {behavior}
        </AppText>
      ) : null}
      <View style={{ gap: spacing.stack.md }}>{children}</View>
    </View>
  );
}

/** A visible event log so callbacks (or their absence) can be checked by eye and by automation. */
export function EventLog({
  events,
  testID,
}: {
  readonly events: readonly string[];
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  return (
    <View
      style={{
        backgroundColor: colors.sem.surface.subtle,
        borderRadius: radius.control,
        padding: spacing.inline.md,
      }}
    >
      <AppText testID={testID} variant="code" tone="secondary">
        {events.length === 0 ? 'Eventos: nenhum' : `Eventos: ${events.join(' · ')}`}
      </AppText>
    </View>
  );
}
