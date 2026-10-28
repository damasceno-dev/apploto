import { formatCount, type IdentifierKind } from '@loto/core';
import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { Pressable, TextInput, View, type TextInputProps } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing, textStyle } from '@/theme/style';
import { FocusRing } from './focus';
import { useFieldControl } from './form-field';
import { editIdentifier, type Selection } from './input-logic';
import { announce, AppText, Glyph } from './text';

const lineHeight = textStyle('control').lineHeight ?? 24;

export type InputKind = 'text' | 'email' | 'password' | 'textarea' | 'search';

export interface FieldControlProps {
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly invalid?: boolean;
  readonly readOnly?: boolean;
  readonly accessibilityLabel?: string;
  readonly testID?: string;
}

export interface InputHandle {
  focus(): void;
}

/** Control chrome shared by every text-like field (input.md anatomy and states). */
export function useControlChrome(state: {
  readonly focused: boolean;
  readonly invalid: boolean;
  readonly disabled: boolean;
}) {
  const { colors } = useTheme();
  return {
    container: {
      minHeight: spacing.size['control-height-touch'],
      borderWidth: spacing.size['border-width'],
      borderRadius: radius.control,
      borderColor: state.disabled
        ? colors.sem.action.disabled.border
        : state.invalid
          ? colors.sem.border['control-invalid']
          : colors.sem.border.control,
      backgroundColor: state.disabled ? colors.sem.action.disabled.bg : colors.sem.surface.default,
      flexDirection: 'row' as const,
      alignItems: 'center' as const,
    },
    text: {
      ...textStyle('control'),
      color: state.disabled ? colors.sem.action.disabled.fg : colors.sem.text.primary,
      flex: 1,
      paddingHorizontal: spacing.inset['control-x'],
      paddingVertical: spacing.inline.sm,
    },
    placeholder: colors.sem.text.placeholder,
    selection: colors.sem.focus.ring,
  };
}

/** Read-only presentation: selectable text on `sem.surface.muted`, no control chrome. */
export function ReadOnlyValue({
  value,
  label,
  align,
  role = 'control',
  testID,
}: {
  readonly value: string;
  readonly label?: string;
  readonly align?: 'left' | 'right';
  readonly role?: 'control' | 'money';
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  return (
    <View
      testID={testID}
      accessible
      accessibilityLabel={label ? `${label}: ${value}` : value}
      style={{
        backgroundColor: colors.sem.surface.muted,
        borderRadius: radius.control,
        paddingHorizontal: spacing.inset['control-x'],
        paddingVertical: spacing.inline.md,
        minHeight: spacing.size['control-height-touch'],
        justifyContent: 'center',
      }}
    >
      <AppText variant={role} selectable align={align}>
        {value}
      </AppText>
    </View>
  );
}

const passwordProps = (role: 'current' | 'new'): Partial<TextInputProps> => ({
  secureTextEntry: true,
  autoCapitalize: 'none',
  autoCorrect: false,
  textContentType: role === 'new' ? 'newPassword' : 'password',
  autoComplete: role === 'new' ? 'new-password' : 'current-password',
});

export interface TextFieldProps extends FieldControlProps {
  readonly kind?: InputKind;
  readonly value: string;
  readonly onChangeText: (value: string) => void;
  readonly placeholder?: string;
  readonly maxLength?: number;
  /** `current-password` versus `new-password` hints. */
  readonly passwordRole?: 'current' | 'new';
  /** Search: polite result count spoken after typing pauses (**3 clientes encontrados**). */
  readonly resultCountLabel?: string;
  readonly autoComplete?: TextInputProps['autoComplete'];
  readonly textContentType?: TextInputProps['textContentType'];
  readonly onSubmitEditing?: () => void;
  readonly returnKeyType?: TextInputProps['returnKeyType'];
}

/**
 * Text, e-mail, password, textarea and search (input.md). Values stay exactly as typed; the field
 * never trims. Read-only renders plain selectable text; disabled keeps the reason in the field.
 */
export const TextField = forwardRef<InputHandle, TextFieldProps>(function TextField(
  {
    kind = 'text',
    value,
    onChangeText,
    placeholder,
    maxLength,
    passwordRole = 'current',
    resultCountLabel,
    autoComplete,
    textContentType,
    onSubmitEditing,
    returnKeyType,
    testID,
    ...own
  },
  ref,
) {
  const control = useFieldControl(own);
  const input = useRef<TextInput>(null);
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const { colors } = useTheme();
  const chrome = useControlChrome({
    focused,
    invalid: control.invalid,
    disabled: control.disabled,
  });
  useImperativeHandle(ref, () => ({ focus: () => input.current?.focus() }), []);
  const { registerFocus } = control;
  useEffect(() => {
    registerFocus(() => input.current?.focus());
    return () => registerFocus(null);
  }, [registerFocus]);
  useEffect(() => {
    if (kind !== 'search' || !resultCountLabel || value === '') return;
    const timer = setTimeout(() => announce(resultCountLabel), 600);
    return () => clearTimeout(timer);
  }, [kind, resultCountLabel, value]);

  if (control.readOnly)
    return <ReadOnlyValue value={value} label={control.accessibilityLabel} testID={testID} />;

  const multiline = kind === 'textarea';
  const nearLimit = maxLength !== undefined && value.length >= Math.floor(maxLength * 0.8);
  const counter =
    maxLength !== undefined ? `${formatCount(value.length)}/${formatCount(maxLength)}` : null;
  const kindProps: Partial<TextInputProps> =
    kind === 'email'
      ? {
          keyboardType: 'email-address',
          autoCapitalize: 'none',
          autoCorrect: false,
          textContentType: 'emailAddress',
          autoComplete: 'email',
        }
      : kind === 'password'
        ? { ...passwordProps(passwordRole), secureTextEntry: !revealed }
        : kind === 'search'
          ? { returnKeyType: 'search', autoCorrect: false }
          : {};
  return (
    <View style={{ gap: spacing.stack.xs }}>
      <View style={chrome.container}>
        {kind === 'search' ? (
          <View style={{ paddingLeft: spacing.inset['control-x'] }}>
            <Glyph name="search" color={colors.sem.text.muted} />
          </View>
        ) : null}
        <TextInput
          ref={input}
          testID={testID}
          value={value}
          onChangeText={onChangeText}
          editable={!control.disabled}
          placeholder={placeholder}
          placeholderTextColor={chrome.placeholder}
          selectionColor={chrome.selection}
          maxLength={maxLength}
          multiline={multiline}
          scrollEnabled={multiline}
          textAlignVertical={multiline ? 'top' : 'center'}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          onSubmitEditing={onSubmitEditing}
          returnKeyType={returnKeyType}
          accessibilityLabel={control.accessibilityLabel}
          accessibilityHint={
            [control.accessibilityHint, nearLimit && counter ? `${counter} caracteres` : undefined]
              .filter(Boolean)
              .join('. ') || undefined
          }
          accessibilityState={{ disabled: control.disabled }}
          aria-required={control.required}
          allowFontScaling
          {...kindProps}
          {...(autoComplete ? { autoComplete } : {})}
          {...(textContentType ? { textContentType } : {})}
          style={[
            chrome.text,
            multiline ? { minHeight: lineHeight * 4, maxHeight: lineHeight * 10 } : null,
          ]}
        />
        {kind === 'password' ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={revealed ? 'Ocultar senha' : 'Mostrar senha'}
            accessibilityState={{ selected: revealed, disabled: control.disabled }}
            disabled={control.disabled}
            onPress={() => setRevealed((shown) => !shown)}
            style={{
              minHeight: spacing.size['touch-target-min'],
              minWidth: spacing.size['touch-target-min'],
              justifyContent: 'center',
              paddingHorizontal: spacing.inline.md,
            }}
          >
            <AppText variant="button-small" color={colors.sem.action.ghost.fg}>
              {revealed ? 'Ocultar' : 'Mostrar'}
            </AppText>
          </Pressable>
        ) : null}
        {kind === 'search' && value !== '' && !control.disabled ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Limpar busca"
            onPress={() => {
              onChangeText('');
              input.current?.focus();
            }}
            style={{
              minHeight: spacing.size['touch-target-min'],
              minWidth: spacing.size['touch-target-min'],
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Glyph name="close" color={colors.sem.text.muted} />
          </Pressable>
        ) : null}
        <FocusRing visible={focused} />
      </View>
      {multiline && counter && nearLimit ? (
        <AppText variant="helper" tone="muted" align="right" importantForAccessibility="no">
          {counter}
        </AppText>
      ) : null}
    </View>
  );
});

export interface IdentifierInputProps extends FieldControlProps {
  readonly kind: IdentifierKind;
  readonly value: string;
  readonly onChangeText: (value: string) => void;
  readonly placeholder?: string;
}

/**
 * CPF, CNPJ, phone and CEP (formatting §6): punctuation appears as digits arrive through core's
 * `maskIdentifierInput`; the caret stays after the same digit. Text the mask cannot hold (letters,
 * overlong, alphanumeric CNPJ) stays verbatim — never truncated or padded.
 */
export const IdentifierInput = forwardRef<InputHandle, IdentifierInputProps>(
  function IdentifierInput({ kind, value, onChangeText, placeholder, testID, ...own }, ref) {
    const control = useFieldControl(own);
    const input = useRef<TextInput>(null);
    const selection = useRef<Selection>({ start: value.length, end: value.length });
    const [controlled, setControlled] = useState<Selection | undefined>(undefined);
    const [focused, setFocused] = useState(false);
    const chrome = useControlChrome({
      focused,
      invalid: control.invalid,
      disabled: control.disabled,
    });
    useImperativeHandle(ref, () => ({ focus: () => input.current?.focus() }), []);
    const { registerFocus } = control;
    useEffect(() => {
      registerFocus(() => input.current?.focus());
      return () => registerFocus(null);
    }, [registerFocus]);
    if (control.readOnly)
      return <ReadOnlyValue value={value} label={control.accessibilityLabel} testID={testID} />;
    return (
      <View style={chrome.container}>
        <TextInput
          ref={input}
          testID={testID}
          value={value}
          selection={controlled}
          editable={!control.disabled}
          keyboardType={kind === 'phone' ? 'phone-pad' : kind === 'cnpj' ? 'default' : 'number-pad'}
          autoComplete={kind === 'phone' ? 'tel' : kind === 'cep' ? 'postal-code' : 'off'}
          textContentType={
            kind === 'phone' ? 'telephoneNumber' : kind === 'cep' ? 'postalCode' : 'none'
          }
          autoCorrect={false}
          placeholder={placeholder}
          placeholderTextColor={chrome.placeholder}
          selectionColor={chrome.selection}
          accessibilityLabel={control.accessibilityLabel}
          accessibilityHint={control.accessibilityHint}
          accessibilityState={{ disabled: control.disabled }}
          aria-required={control.required}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          onSelectionChange={(event) => {
            selection.current = event.nativeEvent.selection;
            setControlled(undefined);
          }}
          onChangeText={(raw) => {
            const next = editIdentifier(kind, value, raw, selection.current);
            selection.current = { start: next.caret, end: next.caret };
            if (next.text !== raw) setControlled({ start: next.caret, end: next.caret });
            onChangeText(next.text);
          }}
          allowFontScaling
          style={chrome.text}
        />
        <FocusRing visible={focused} />
      </View>
    );
  },
);
