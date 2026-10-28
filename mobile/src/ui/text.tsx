import { forwardRef, type ReactNode } from 'react';
import { AccessibilityInfo, Text, type TextProps, type TextStyle } from 'react-native';
import { useTheme } from '@/theme/provider';
import { textStyle } from '@/theme/style';
import type { SemanticColors, TypographyRole } from '@/theme/theme';

type TextTone = keyof SemanticColors['text'];

/** Token typography and text color; the OS font scale always applies (`allowFontScaling`). */
export const AppText = forwardRef<
  Text,
  TextProps & {
    readonly variant?: TypographyRole;
    readonly tone?: TextTone;
    readonly color?: string;
    readonly align?: TextStyle['textAlign'];
  }
>(function AppText({ variant = 'body', tone = 'primary', color, align, style, ...props }, ref) {
  const { colors } = useTheme();
  return (
    <Text
      ref={ref}
      {...props}
      allowFontScaling
      style={[
        textStyle(variant),
        { color: color ?? colors.sem.text[tone] },
        align ? { textAlign: align } : null,
        style,
      ]}
    />
  );
});

/**
 * Small outline glyphs drawn with text so no icon font or extra native module is needed. They are
 * always decorative: the accessible name comes from the surrounding label.
 */
const glyphs = {
  check: '✓',
  chevronDown: '▾',
  chevronRight: '›',
  chevronLeft: '‹',
  close: '✕',
  search: '⌕',
  warning: '!',
  info: 'i',
  error: '×',
  lock: '▣',
  dash: '–',
  calendar: '▦',
  menu: '≡',
} as const;

export function Glyph({
  name,
  color,
  size = 16,
}: {
  readonly name: keyof typeof glyphs;
  readonly color: string;
  readonly size?: number;
}) {
  return (
    <Text
      accessible={false}
      importantForAccessibility="no-hide-descendants"
      accessibilityElementsHidden
      allowFontScaling
      style={{ color, fontSize: size, lineHeight: Math.round(size * 1.25), fontWeight: '600' }}
    >
      {glyphs[name]}
    </Text>
  );
}

/** Text hidden from sight but read by screen readers (context such as "Situação:"). */
export function ScreenReaderOnly({ children }: { readonly children: ReactNode }) {
  return (
    <Text
      style={{ position: 'absolute', width: 1, height: 1, opacity: 0 }}
      allowFontScaling={false}
    >
      {children}
    </Text>
  );
}

export function announce(message: string) {
  AccessibilityInfo.announceForAccessibility(message);
}

/**
 * Moves screen-reader focus to a mounted element (and input focus for a field or an imperative
 * handle). Elements that are gone are skipped silently: the caller decides any fallback.
 */
export function moveFocusTo(target: { focus?: () => void } | null | undefined) {
  if (!target) return;
  const host = typeof (target as { measure?: unknown }).measure === 'function';
  const field = typeof (target as { isFocused?: unknown }).isFocused === 'function';
  // Input focus only for text fields and imperative field handles. A heading or button given
  // native focus on Android is kept in view by its ScrollView on every layout pass, which pins
  // the scroll position; screen-reader focus goes through the accessibility event instead.
  if (field || !host) target.focus?.();
  // Only a running screen reader needs its focus moved; on Android the focus event also moves
  // native focus, which would pin the surrounding ScrollView for everyone else.
  if (host)
    void AccessibilityInfo.isScreenReaderEnabled()
      .then((enabled) => {
        if (enabled) AccessibilityInfo.sendAccessibilityEvent(target as never, 'focus');
      })
      .catch(() => undefined);
}
