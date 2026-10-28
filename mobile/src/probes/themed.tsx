import { forwardRef } from 'react';
import { TextInput, type TextInputProps } from 'react-native';
import { useTheme } from '../theme/provider';

/** DEV probe input that stays legible in both themes (technical harness only). */
export const ProbeTextInput = forwardRef<TextInput, TextInputProps>(function ProbeTextInput(
  { style, ...props },
  ref,
) {
  const { colors } = useTheme();
  return (
    <TextInput
      ref={ref}
      placeholderTextColor={colors.sem.text.placeholder}
      {...props}
      style={[
        { color: colors.sem.text.primary, borderColor: colors.sem.border.control, borderWidth: 1 },
        style,
      ]}
    />
  );
});
