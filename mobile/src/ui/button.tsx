import { forwardRef, useRef, type ReactNode } from 'react';
import { ActivityIndicator, View, type ViewStyle } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';
import type { SemanticColors } from '@/theme/theme';
import { FocusRing, useFocusState, PressableBox } from './focus';
import { AppText } from './text';

export type ButtonVariant = 'primary' | 'cta' | 'secondary' | 'link' | 'danger' | 'danger-solid';

type ActionColors = SemanticColors['action'];
const actionKey: Record<ButtonVariant, keyof ActionColors> = {
  primary: 'primary',
  cta: 'cta',
  secondary: 'secondary',
  link: 'ghost',
  danger: 'danger',
  'danger-solid': 'danger-solid',
};

export interface ButtonProps {
  readonly label: string;
  readonly onPress?: () => void;
  readonly variant?: ButtonVariant;
  /** Navigation buttons announce as links (record navigation, "Voltar para …"). */
  readonly role?: 'button' | 'link';
  readonly disabled?: boolean;
  /** Adjacent visible reason; also the native accessibility hint ("indisponível" + reason). */
  readonly disabledReason?: string;
  readonly loading?: boolean;
  /** The in-progress verb (**Salvando…**); the label returns when the request settles. */
  readonly loadingLabel?: string;
  /** Overrides the spoken name when the visible label repeats (row context). */
  readonly accessibilityLabel?: string;
  readonly leading?: ReactNode;
  readonly fullWidth?: boolean;
  readonly onNavigation?: boolean;
  readonly testID?: string;
}

/**
 * Button and text link (button.md). Disabled stays focusable and readable with its reason; while
 * loading, activation is blocked so a double tap cannot submit twice. A button never shows success.
 */
export const Button = forwardRef<View, ButtonProps>(function Button(
  {
    label,
    onPress,
    variant = 'primary',
    role = 'button',
    disabled = false,
    disabledReason,
    loading = false,
    loadingLabel,
    accessibilityLabel,
    leading,
    fullWidth = false,
    onNavigation = false,
    testID,
  },
  ref,
) {
  const { colors } = useTheme();
  const [focused, focusHandlers] = useFocusState();
  // The idle width, measured into a ref and applied only while loading: feeding every layout back
  // as a minimum can oscillate inside wrapping rows and keep the screen relayouting.
  const idleWidth = useRef<number | undefined>(undefined);
  const unavailable = disabled || loading;
  const tokens = colors.sem.action[actionKey[variant]] as Record<string, string>;
  const off = colors.sem.action.disabled;
  const isLink = variant === 'link';
  const container = (pressed: boolean): ViewStyle => ({
    minHeight: isLink ? spacing.size['touch-target-min'] : spacing.size['control-height-touch'],
    minWidth: loading ? idleWidth.current : undefined,
    paddingHorizontal: isLink ? 0 : spacing.inset['button-x'],
    paddingVertical: spacing.inline.sm,
    borderRadius: radius.control,
    borderWidth: tokens.border || disabled ? spacing.size['border-width'] : 0,
    borderColor: disabled ? off.border : tokens.border,
    backgroundColor: disabled ? off.bg : pressed && !loading ? tokens['bg-active'] : tokens.bg,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing.inline.sm,
    alignSelf: fullWidth ? 'stretch' : 'flex-start',
  });
  const text = disabled ? off.fg : tokens.fg;
  return (
    <View style={{ alignSelf: fullWidth ? 'stretch' : 'flex-start', gap: spacing.stack.xs }}>
      <PressableBox
        ref={ref}
        testID={testID}
        accessibilityRole={role}
        accessibilityLabel={loading ? (loadingLabel ?? label) : (accessibilityLabel ?? label)}
        accessibilityHint={disabled ? disabledReason : undefined}
        accessibilityState={{ disabled, busy: loading }}
        // Guarded, not just omitted: nothing above the control can receive the press either.
        onPress={() => {
          if (!unavailable) onPress?.();
        }}
        disabled={unavailable}
        {...focusHandlers}
        onLayout={(event) => {
          // Width stays stable when the label switches to the in-progress verb.
          if (!loading) idleWidth.current = event.nativeEvent.layout.width;
        }}
        style={(pressed) => container(pressed)}
      >
        {loading ? <ActivityIndicator size="small" color={text} /> : leading}
        <AppText
          variant="button"
          color={text}
          align={isLink ? 'left' : 'center'}
          style={isLink && !disabled ? { textDecorationLine: 'underline' } : null}
        >
          {loading ? (loadingLabel ?? label) : label}
        </AppText>
        <FocusRing visible={focused} onNavigation={onNavigation} />
      </PressableBox>
      {disabled && disabledReason ? (
        <AppText
          variant="helper"
          tone="muted"
          importantForAccessibility="no"
          accessibilityElementsHidden
        >
          {disabledReason}
        </AppText>
      ) : null}
    </View>
  );
});
