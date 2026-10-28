import { forwardRef, useMemo, useState } from 'react';
import { Pressable, View, type PressableProps, type StyleProp, type ViewStyle } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';

/** Hardware-keyboard / switch-access focus state for a Pressable or TextInput. */
export function useFocusState() {
  const [focused, setFocused] = useState(false);
  const handlers = useMemo(
    () => ({ onFocus: () => setFocused(true), onBlur: () => setFocused(false) }),
    [],
  );
  return [focused, handlers] as const;
}

const outset = spacing.size['focus-ring-width'] + spacing.size['focus-ring-offset'];

/** `sem.focus.ring` outline drawn outside the control (`sem.focus.ring-on-nav` in navigation). */
export function FocusRing({
  visible,
  onNavigation = false,
}: {
  readonly visible: boolean;
  readonly onNavigation?: boolean;
}) {
  const { colors } = useTheme();
  if (!visible) return null;
  return (
    <View
      testID="focus-ring"
      pointerEvents="none"
      style={{
        position: 'absolute',
        top: -outset,
        bottom: -outset,
        left: -outset,
        right: -outset,
        borderWidth: spacing.size['focus-ring-width'],
        borderColor: onNavigation ? colors.sem.focus['ring-on-nav'] : colors.sem.focus.ring,
        borderRadius: radius.focus + spacing.size['focus-ring-offset'],
      }}
    />
  );
}

/**
 * Pressable whose style depends on the pressed state. NativeWind's JSX interop drops function
 * styles on device, so the pressed state is tracked explicitly and a plain style is passed.
 */
export const PressableBox = forwardRef<
  View,
  Omit<PressableProps, 'style'> & { readonly style: (pressed: boolean) => StyleProp<ViewStyle> }
>(function PressableBox({ style, onPressIn, onPressOut, ...props }, ref) {
  const [pressed, setPressed] = useState(false);
  return (
    <Pressable
      ref={ref}
      {...props}
      onPressIn={(event) => {
        setPressed(true);
        onPressIn?.(event);
      }}
      onPressOut={(event) => {
        setPressed(false);
        onPressOut?.(event);
      }}
      style={style(pressed)}
    />
  );
});
