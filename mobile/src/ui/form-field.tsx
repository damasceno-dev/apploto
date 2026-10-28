import {
  Children,
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Pressable, View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { spacing } from '@/theme/style';
import { AppText, Glyph } from './text';

interface FieldContextValue {
  readonly label: string;
  readonly helper?: string;
  readonly error?: string;
  readonly required: boolean;
  readonly disabled: boolean;
  readonly disabledReason?: string;
  readonly readOnly: boolean;
  readonly registerFocus: (focus: (() => void) | null) => void;
}

const FieldContext = createContext<FieldContextValue | null>(null);

export interface OwnFieldState {
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly invalid?: boolean;
  readonly readOnly?: boolean;
  readonly accessibilityLabel?: string;
  readonly accessibilityHint?: string;
}

/**
 * A control's effective state inside or outside a FormField. The field only **adds**: either the
 * control or its field disables, requires or makes it read-only; an explicit `invalid` from the
 * control wins over the field's error. Outside a field the control's own props apply unchanged.
 */
export function useFieldControl(own: OwnFieldState) {
  const field = useContext(FieldContext);
  const disabled = Boolean(own.disabled) || Boolean(field?.disabled);
  const readOnly = Boolean(own.readOnly) || Boolean(field?.readOnly);
  const required = Boolean(own.required) || Boolean(field?.required);
  const invalid = own.invalid ?? Boolean(field?.error);
  const hint = [
    own.accessibilityHint,
    field?.helper,
    field?.error ? `Erro: ${field.error}` : undefined,
    disabled ? field?.disabledReason : undefined,
  ]
    .filter(Boolean)
    .join('. ');
  return {
    disabled,
    readOnly,
    required,
    invalid,
    accessibilityLabel: own.accessibilityLabel ?? field?.label,
    accessibilityHint: hint === '' ? undefined : hint,
    registerFocus: field?.registerFocus ?? (() => undefined),
    inField: field !== null,
  };
}

export interface FormFieldProps {
  readonly label: string;
  readonly helper?: string;
  /** Inline message only when its association with this field is unambiguous (copy §6). */
  readonly error?: string;
  readonly optional?: boolean;
  readonly required?: boolean;
  readonly disabled?: boolean;
  readonly disabledReason?: string;
  readonly readOnly?: boolean;
  /** Why the value is final (**Período bloqueado**, **Gerada automaticamente**). */
  readonly readOnlyReason?: string;
  /** **Alterado** beside the label when the screen tracks dirty fields. */
  readonly unsaved?: boolean;
  readonly children: ReactNode;
  readonly testID?: string;
}

/** Persistent label, helper, inline error and read-only/disabled reasons around one control. */
export function FormField({
  label,
  helper,
  error,
  optional = false,
  required = false,
  disabled = false,
  disabledReason,
  readOnly = false,
  readOnlyReason,
  unsaved = false,
  children,
  testID,
}: FormFieldProps) {
  const { colors } = useTheme();
  const focusRef = useRef<(() => void) | null>(null);
  const registerFocus = useCallback((focus: (() => void) | null) => {
    focusRef.current = focus;
  }, []);
  const value = useMemo<FieldContextValue>(
    () => ({ label, helper, error, required, disabled, disabledReason, readOnly, registerFocus }),
    [label, helper, error, required, disabled, disabledReason, readOnly, registerFocus],
  );
  return (
    <FieldContext.Provider value={value}>
      <View testID={testID} style={{ gap: spacing.stack.xs }}>
        <Pressable
          accessible={false}
          onPress={() => focusRef.current?.()}
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            alignItems: 'baseline',
            gap: spacing.inline.sm,
          }}
        >
          <AppText
            variant="label"
            tone={disabled ? 'disabled' : 'primary'}
            importantForAccessibility="no"
            accessibilityElementsHidden
          >
            {label}
            {optional ? (
              <AppText variant="helper" tone="muted">
                {' '}
                (opcional)
              </AppText>
            ) : null}
          </AppText>
          {unsaved ? (
            <AppText variant="helper" color={colors.sem.feedback.warning.fg}>
              Alterado
            </AppText>
          ) : null}
        </Pressable>
        {children}
        {error ? (
          <View style={{ flexDirection: 'row', gap: spacing.inline.xs, alignItems: 'flex-start' }}>
            <Glyph name="error" color={colors.sem.feedback.danger.fg} />
            <AppText variant="helper" color={colors.sem.feedback.danger.fg} style={{ flex: 1 }}>
              {error}
            </AppText>
          </View>
        ) : null}
        {disabled && disabledReason ? (
          <AppText variant="helper" tone="muted">
            {disabledReason}
          </AppText>
        ) : helper && !error ? (
          <AppText variant="helper" tone="muted">
            {helper}
          </AppText>
        ) : null}
        {readOnly && readOnlyReason ? (
          <AppText variant="helper" tone="muted">
            {readOnlyReason}
          </AppText>
        ) : null}
      </View>
    </FieldContext.Provider>
  );
}

/** A labelled group of related choices (legend instead of label). */
export function Fieldset({
  legend,
  helper,
  error,
  disabled = false,
  disabledReason,
  required = false,
  children,
}: {
  readonly legend: string;
  readonly helper?: string;
  readonly error?: string;
  readonly disabled?: boolean;
  readonly disabledReason?: string;
  readonly required?: boolean;
  readonly children: ReactNode;
}) {
  return (
    <FormField
      label={legend}
      helper={helper}
      error={error}
      disabled={disabled}
      disabledReason={disabledReason}
      required={required}
    >
      {children}
    </FormField>
  );
}

/**
 * Grid-column variant: columns at least `form.column-min` wide (scaled with the OS text size) with
 * `form.column-gap`; a single column when two do not fit, including at enlarged text.
 */
export function FormGrid({ children }: { readonly children: ReactNode }) {
  const { fontScale } = useTheme();
  const [width, setWidth] = useState(0);
  const gap = spacing.form['column-gap'];
  const min = spacing.form['column-min'] * Math.max(1, fontScale);
  const columns = Math.max(1, Math.floor((width + gap) / (min + gap)));
  const itemWidth = columns === 1 ? '100%' : (width - gap * (columns - 1)) / columns;
  return (
    <View
      onLayout={(event) => setWidth(event.nativeEvent.layout.width)}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        columnGap: gap,
        rowGap: spacing.form['row-gap'],
      }}
    >
      {Children.map(children, (child) => (
        <View style={{ width: itemWidth }}>{child}</View>
      ))}
    </View>
  );
}
